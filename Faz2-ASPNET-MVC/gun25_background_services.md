# Gün 25 — Background Services ve Job Scheduling

---

## 1. Neden Background Service Gerekir?

HTTP isteği geldi, işlem yapıldı, yanıt döndürüldü — bu senkron akış. Ama bazı işlemler bu akışa sığmaz:

- Kullanıcı sipariş verdi, onay e-postası gönderilecek — ama e-posta gönderimi 2 saniye sürüyor, kullanıcıyı bekletme
- Her gece saat 02:00'de stok raporu üretilecek
- Mesaj kuyruğundan (RabbitMQ, Azure Service Bus) sürekli mesaj okunacak
- Her 5 dakikada bir döviz kuru çekilecek

Bunlar **background işlemler** — HTTP request-response döngüsünün dışında çalışır. .NET'te bunu yapmanın birden fazla yolu var ve seçim önemli.

---

## 2. IHostedService ve BackgroundService — Native .NET

En temel yaklaşım: .NET'in kendi altyapısı, harici paket yok.

**IHostedService** — iki metod implement etmek zorundasın:

```
StartAsync → host başlarken çağrılır
StopAsync  → host kapanırken çağrılır (graceful shutdown)
```

**BackgroundService** — `IHostedService`'in soyut implementasyonu. Sadece `ExecuteAsync` metodunu implement edersin, geri kalanını `BackgroundService` halleder.

```
BackgroundService
  └─ IHostedService'i implement eder
  └─ ExecuteAsync → senin iş mantığın buraya gider
  └─ StartAsync → ExecuteAsync'i arka planda başlatır
  └─ StopAsync  → CancellationToken'ı iptal ederek ExecuteAsync'in durmasını sinyaller
```

Spring'deki `@Scheduled` veya `ApplicationRunner` gibi düşünebilirsin — ama çok daha granüler kontrol.

---

## 3. Hosted Service Lifetime — Host ile Başlar, Biter

Hosted service, uygulamayla aynı süreçte yaşar:

```
dotnet run
  ↓
Host başlar → StartAsync → ExecuteAsync çalışmaya başlar
  ↓
HTTP istekleri karşılanır (paralel)
  ↓
Ctrl+C / SIGTERM
  ↓
Host kapanma sinyali → StopAsync → CancellationToken iptal → ExecuteAsync durur
  ↓
Uygulama kapanır
```

**Graceful shutdown** kritik: kapanma sinyali geldiğinde yarım kalan işi tamamlamak veya güvenli noktada durmak için `CancellationToken` kullanılır.

```csharp
// "stoppingToken" → host kapanmak istediğinde iptal edilir.
// ExecuteAsync bu token'ı izler — iptal sinyali gelince döngüden çıkar.
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await YapilacakIs();
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
    }
    // stoppingToken iptal edilince döngüden çıkıldı → güvenli kapanma
}
```

---

## 4. PeriodicTimer — Drift'e Dayanıklı Periyodik İş

`Task.Delay(5000)` ile döngü kurarsın ama bir sorun var: işin süresi değişirse periyot kayar.

```
İş 200ms sürüyor → Delay(5000) → toplam 5200ms — sorun yok
İş 6000ms sürüyor → Delay(5000) → toplam 11000ms — periyot 11 saniyeye çıktı!
```

**.NET 6+ `PeriodicTimer`** bunu çözer: iş ne kadar sürerse sürsün, bir sonraki tick doğru zamanda gelir (drift yok):

```csharp
// "PeriodicTimer" → her 5 dakikada bir "tick" üretir.
// İşin süresi tick aralığından küçükse sorun yok.
// İşin süresi tick aralığından büyükse bir sonraki tick beklenir (atlanmaz, gecikir).
using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

// "WaitForNextTickAsync" → bir sonraki tick'e kadar bekle, iptal sinyali gelirse false döner.
while (await timer.WaitForNextTickAsync(stoppingToken))
{
    await DovizKuruGuncelle();
}
```

---

## 5. Channel\<T\> — Producer/Consumer Pattern

Bir istek geldiğinde arka planda iş yapmak istiyorsun ama işi ayrı bir thread'e atmak istiyorsun. `Channel<T>` bunu sağlar:

```
HTTP Request → Channel'a yaz (hızlı, non-blocking)
                    ↓
Background Service → Channel'dan oku → işi yap (yavaş, arka planda)
```

**Back-pressure**: Channel doluysa yazma işlemi bekler — sistem kendini korur.

```csharp
// "Channel<T>" → thread-safe kuyruk.
// "BoundedChannel" → maksimum kapasite sınırı var (back-pressure için).
// "UnboundedChannel" → sınırsız kapasite (dikkatli kullan — bellek tüketebilir).
Channel<SiparisOlusturulduOlayi> _kanal = Channel.CreateBounded<SiparisOlusturulduOlayi>(
    new BoundedChannelOptions(capacity: 100)
    {
        // Kanal doluysa: yeni öğe beklesinmi yoksa hata mı versin?
        FullMode = BoundedChannelFullMode.Wait
    });

// Controller'da (producer — yazar):
await _kanal.Writer.WriteAsync(new SiparisOlusturulduOlayi(siparisId));

// BackgroundService'te (consumer — okur):
await foreach (var olay in _kanal.Reader.ReadAllAsync(stoppingToken))
{
    await EmailGonder(olay.SiparisId);
}
```

---

## 6. Hangfire — Persistence, Retry, Dashboard

`BackgroundService` sade ve hafiftir ama eksikleri var:
- Uygulama çöktüğünde kuyruktaki işler kaybolur
- İşler başarısız olursa otomatik retry yok
- Hangi işlerin çalıştığını görecek dashboard yok

**Hangfire** bu eksikleri kapatır. İşleri SQL Server veya Redis'te saklar — uygulama yeniden başlasa bile işler kaybolmaz.

**4 iş tipi:**

```
Fire-and-forget  → "bir kez çalıştır, şimdi veya birazdan"
Delayed          → "X dakika sonra çalıştır"
Recurring        → "her gece 02:00'de çalıştır" (cron)
Continuation     → "A bitti, ardından B çalıştır"
```

```csharp
// Fire-and-forget: sipariş onay e-postası
// Kullanıcıyı bekletmeden background'a at
BackgroundJob.Enqueue<IEmailServisi>(s => s.OnayEmailiGonder(siparisId));

// Delayed: 10 dakika sonra stok uyarısı gönder
BackgroundJob.Schedule<IEmailServisi>(
    s => s.StokUyarisiGonder(urunId),
    TimeSpan.FromMinutes(10));

// Recurring: her gece 02:00'de günlük rapor
RecurringJob.AddOrUpdate<IRaporServisi>(
    "gunluk-rapor",                  // job kimliği — aynı isimle tekrar çağrılırsa günceller
    s => s.GunlukRaporOlustur(),
    "0 2 * * *");                    // cron: her gece 02:00

// Continuation: ödeme al → ardından kargo oluştur
var odemeJobId = BackgroundJob.Enqueue<IOdemeServisi>(s => s.OdemeAl(siparisId));
BackgroundJob.ContinueJobWith<IKargoServisi>(odemeJobId, s => s.KargoOlustur(siparisId));
```

**Otomatik retry:**

```csharp
// Başarısız olursa 3 kez daha dene — üstel geri çekilme ile (1dk, 5dk, 10dk aralarla)
[AutomaticRetry(Attempts = 3)]
public async Task OnayEmailiGonder(int siparisId)
{
    // E-posta servisi geçici olarak kullanılamazsa exception fırlatır
    // Hangfire yakalar, retry kuyruğuna alır
    await _emailServisi.GonderAsync(siparisId);
}
```

**Dashboard** — `/hangfire` adresinde: hangi işler çalışıyor, kaçı başarısız, geçmiş kayıtları.

---

## 7. Quartz.NET — Karmaşık Zamanlama, Clustering

Hangfire job yönetimi için güçlüdür. Ama karmaşık zamanlama senaryolarında ve birden fazla uygulama instance'ının çalıştığı ortamlarda (load balancer) **Quartz.NET** daha uygun:

**DisallowConcurrentExecution** — aynı job birden fazla instance'da aynı anda çalışmasın:

```csharp
// Çok kritik: raporlama job'ı 3 sunucuda aynı anda çalışmamalı
// [DisallowConcurrentExecution] → Quartz bunu veritabanı seviyesinde garanti eder
[DisallowConcurrentExecution]
public class AylikRaporJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await _raporServisi.AylikRaporOlustur();
    }
}
```

**Cron expression ile karmaşık zamanlama:**

```csharp
// Her hafta Pazartesi ve Çarşamba sabah 08:30'da
ITrigger trigger = TriggerBuilder.Create()
    .WithCronSchedule("0 30 8 ? * MON,WED")
    .Build();

// Her ayın son günü
ITrigger aysonuTrigger = TriggerBuilder.Create()
    .WithCronSchedule("0 0 23 L * ?")
    .Build();
```

**Quartz clustering:** Birden fazla sunucu aynı Quartz veritabanını paylaşır — bir sunucu düşse diğeri devralır.

---

## 8. Worker Service — Standalone Süreç

Bazen background iş HTTP API ile aynı süreçte olmamalıdır:
- Mesaj kuyruğu tüketicisi (RabbitMQ, Azure Service Bus) — sürekli çalışır
- Ağır işlem — API'nin kaynaklarını tüketmemeli
- Docker container olarak ayrı deploy edilmeli

**Worker Service** bağımsız bir .NET uygulamasıdır — HTTP katmanı yok, sadece `BackgroundService`:

```csharp
// Program.cs — Worker Service
var builder = Host.CreateApplicationBuilder(args);

// HTTP yok — sadece worker
builder.Services.AddHostedService<SiparisKuyruguTuketicisi>();

var host = builder.Build();
host.Run();
```

```csharp
// Kuyruktan sürekli mesaj oku
public class SiparisKuyruguTuketicisi : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Mesaj kuyruğuna abone ol
        await foreach (var mesaj in _kuyruk.OkuAsync(stoppingToken))
        {
            await _siparisServisi.IsleAsync(mesaj);
        }
    }
}
```

---

## 9. Seçim Kılavuzu

```
Kullanıcı tetiklemeli iş (e-posta, rapor üret)    → Hangfire
  + retry gerekli                                  → Hangfire
  + ops ekibine dashboard lazım                    → Hangfire

Karmaşık zamanlama (cron, aylık, iş günleri)      → Quartz.NET
  + birden fazla sunucu, aynı anda çalışmasın      → Quartz.NET + clustering

Mesaj kuyruğu tüketicisi                           → Worker Service / BackgroundService
Sürekli döngü, basit periyodik iş                  → BackgroundService + PeriodicTimer
HTTP API'den bağımsız deploy edilmeli               → Worker Service (ayrı proje)
```

---

## 10. Dikkat Edilmesi Gerekenler

**Scoped servis ve Singleton çakışması:** `BackgroundService` Singleton'dır (host boyunca yaşar). Scoped servis (DbContext gibi) doğrudan inject edemezsin — captive dependency problemi. Çözüm: `IServiceScopeFactory` ile her döngüde yeni scope aç:

```csharp
using var scope = _scopeFactory.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<KitabeviDbContext>();
```

**CancellationToken'ı her yere geçir:** `Task.Delay`, `HttpClient`, DB sorguları — hepsi token alabilir. Geçmezsen graceful shutdown çalışmaz, host zorla kapanır.

**Hangfire job'ları stateless olmalı:** Job parametresi olarak büyük nesne geçirme — Hangfire onu serialize edip veritabanına yazar. Sadece ID geç, job içinde nesneyi yükle.

**PeriodicTimer vs Task.Delay:** Sabit aralıklı iş için her zaman `PeriodicTimer` tercih et — drift birikimi önlenir.

---

## 11. Kontrol Soruları

1. `BackgroundService` neden `IHostedService`'den türer? `ExecuteAsync`'i sen implement edince `StartAsync` ve `StopAsync`'e ne olur?

2. `PeriodicTimer`, `Task.Delay` döngüsünden neden daha iyi? "Drift" ne anlama gelir?

3. Hangfire ve `BackgroundService` arasındaki temel fark nedir? Uygulama çöktüğünde ne olur?

4. `BackgroundService`'e `DbContext` doğrudan inject edemezsin. Neden? Çözümü nedir?

5. `[DisallowConcurrentExecution]` ne zaman kritiktir? Olmasa ne olabilir?

6. "Kullanıcı sipariş verdi, stok azaldı, kargo şirketine bildirim gönderilecek" — bu senaryo için hangi aracı seçersin? Neden?
