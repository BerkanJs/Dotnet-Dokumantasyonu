# Gün 104 — Domain Event Dispatch: Doğru Mimari

---

## Domain Event Nedir? Neden Var?

İş dünyasında bir şey olduğunda — sipariş oluşturuldu, ödeme alındı, kullanıcı kaydoldu — bu "şey"in birden fazla sonucu vardır:

**Sipariş oluşturulduğunda:**
1. Kullanıcıya onay maili gitmeli
2. Stoktan ürün düşmeli
3. Sevkiyat servisine bildirim gitmeli
4. Analytics sistemine kayıt atılmalı
5. CRM güncellenmeli

Bunların hepsini sipariş servisinin kendisi yapsa — sipariş servisi her şeyi bilmek zorunda. E-posta nasıl gönderilir, stok nasıl düşer, sevkiyat formatı nedir... Sipariş servisi 1000 satırdan 5000 satıra çıkar, test edilemez, bakımı imkansızlaşır.

**Domain event yaklaşımı:** Sipariş servisi der ki "ben sipariş oluşturdum" ve `SiparisOlusturulduEvent` yayınlar. Sonra kim ne yaparsa kendi bilir. E-posta servisi event'i dinler, mail atar. Stok servisi event'i dinler, stok düşürür. Sipariş servisi bu reaksiyonların varlığından bile haberdar olmayabilir.

**Analoji:** Düğün davetiyesi. Sen sadece "evleniyorum" duyurusu yapıyorsun. Kim hediye alır, kim çiçek gönderir, kim katılır — sana bağlı değil. Davetiyeyi yolladın, gerisini ilgilenenler halleder. Eğer her hediye için "sen ne göndereceksin?" diye herkesi tek tek arasaydın — düğünü organize edemezdin.

---

## Bu Dersin Gerçek Sorusu

Domain event'in ne olduğu basit. **Asıl zor soru:** Bu event'i ne zaman ve nasıl dispatch (yayınlayacaksın)?

Düşün — kullanıcı "Sipariş Ver" butonuna bastı. Servisinde şu sıralama var:

1. Sipariş nesnesini oluştur
2. Veritabanına yaz (SaveChanges)
3. SiparisOlusturulduEvent yayınla

İşte bu sıranın doğru olması hayati. Yanlış yere koyarsan — hayalet siparişler, kayıp mailler, tutarsız stok... Hata sessizce büyür, fark ettiğinde geç olur.

Üç temel yaklaşım var. Tek tek ne demek olduğuna, hangi felaket senaryosuna yol açtığına bakacağız.

---

## Yaklaşım 1: SaveChanges'ten ÖNCE Dispatch

Mantık: "Önce event'i yayınlayalım, handler'lar tepkilerini versin, sonra kaydedelim."

```
Sipariş oluştur → Event yayınla → Handler'lar çalışır → SaveChanges
```

İlk bakışta makul görünüyor. Ama bir sorun var: **SaveChanges başarısız olabilir.** Ya DB bağlantısı koparsa? Ya unique constraint ihlali olursa? Ya disk dolu olursa?

### Hayalet Sipariş Felaketi

Şu senaryoyu düşün:

1. Sipariş nesnesini oluşturdun (henüz DB'de değil, sadece bellekte)
2. Event'i yayınladın
3. E-posta handler çalıştı → kullanıcıya "siparişiniz alındı, teşekkürler" maili gitti
4. Stok handler çalıştı → stoktan 1 düştü
5. SaveChanges çağrıldı → **HATA: DB connection timeout**
6. Sipariş aslında DB'ye yazılamadı

Şimdi durum şu:
- Kullanıcı "sipariş aldım" maili aldı, ama sistemde sipariş yok
- Stokta 1 ürün düştü, ama sipariş yok — stok yanlış
- Kullanıcı paneline gitti, sipariş listesinde hiçbir şey yok → "sipariş kayboldu" şikayeti
- Destek ekibi araştırdı, sistemde gerçekten yok → açıklama yok

Buna **hayalet sipariş** denir. Müşteri "sipariş verdim" diyor, sen "yok" diyorsun. İkiniz de haklısınız. Ama veri tutarsız.

### Bu Yaklaşım Ne Zaman Mantıklı?

Sadece bir durumda: Eğer handler'lar AYNI DbContext'i kullanıyorsa ve henüz SaveChanges çağrılmadıysa — yani değişiklikler bellekte birikiyor, hepsi tek bir transaction'da atomik olarak yazılacak. Bu durumda event handler'ın DB değişiklikleri de aynı sepete giriyor, hata olursa hepsi rollback olur.

Ama bu özel durumda bile risk var: handler dış servise çağrı yaparsa (e-posta, ödeme API) — o dış çağrı geri alınamaz. Mail gönderildi, geri çağıramazsın.

**Pratik tavsiye:** SaveChanges'ten önce dispatch yapma. Çok özel durumlarda, çok dikkatli olarak yapabilirsin ama varsayılan yaklaşım bu olmamalı.

---

## Yaklaşım 2: SaveChanges'ten SONRA Dispatch

Mantık: "Önce kaydet, kayıt başarılıysa event yayınla."

```
Sipariş oluştur → SaveChanges → Event yayınla → Handler'lar çalışır
```

Bu yaklaşım hayalet siparişi önler. Sipariş DB'ye yazıldıktan sonra event yayınlanıyor — yazılma başarısızsa event hiç oluşmuyor.

Ama yeni bir sorun var: **Event yayınlandıktan sonra dispatch sürecinde hata olursa?**

### Kayıp Yan Etki Sorunu

Senaryo:
1. Sipariş oluşturuldu, SaveChanges başarılı → DB'de var ✓
2. Event yayınlandı → MediatR handler'lara dağıtmaya başladı
3. E-posta handler çalıştı → mail servisi 500 dönüyor → exception
4. Geri kalan handler'lar çalışamadı (stok, analytics, CRM)

Şimdi durum:
- Sipariş DB'de var (kullanıcı görebiliyor) ✓
- Ama mail gitmedi
- Stok düşmedi → birinin alamayacağı bir ürün satılabilir görünüyor
- Sevkiyat servisi haberdar değil

Sipariş varlığı tutarlı ama yan etkiler kayıp. Bu **hayalet siparişten daha iyi** çünkü kullanıcı en azından siparişini görebiliyor. Ama tutarsızlık devam ediyor.

### Bu Yaklaşımın Yerleşmesi: SaveChangesInterceptor

Gün 99'da gördüğümüz interceptor mekanizması bu yaklaşımı uygulamanın temiz yolu. Save öncesi event'leri topla, save başarılıysa dispatch et:

```csharp
public class DomainEventInterceptor : SaveChangesInterceptor
{
    private List<INotification> _pendingEvents = new();

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(...)
    {
        // Save ÖNCESİ: entity'lerden event'leri topla
        foreach (var entry in context.ChangeTracker.Entries<IHasDomainEvents>())
        {
            _pendingEvents.AddRange(entry.Entity.DomainEvents);
            entry.Entity.ClearDomainEvents();
        }
        return base.SavingChangesAsync(...);
    }

    public override async ValueTask<int> SavedChangesAsync(...)
    {
        // Save BAŞARILI: şimdi dispatch et
        foreach (var ev in _pendingEvents)
            await _mediator.Publish(ev);
        _pendingEvents.Clear();
        return await base.SavedChangesAsync(...);
    }
}
```

Mantık: SaveChanges başarısız olursa SavedChangesAsync hiç çağrılmaz → event'ler kaybolur ama zaten sipariş de yazılmadı → tutarlı.

### Bu Yaklaşım Hangi Senaryolarda Yeterli?

İki şart sağlanırsa bu yaklaşımla yaşayabilirsin:

**Şart 1: Handler'lar idempotent**
Event tekrar gönderilirse aynı sonucu üretmeli. Mesela "kullanıcıya hoş geldin maili gönder" handler'ı iki kez çalışırsa iki mail gider — kötü ama felaket değil. "Stoktan 1 düş" iki kez çalışırsa stok yanlış — felaket.

**Şart 2: Handler hatası tolere edilebilir**
Yan etki başarısız olursa dünya durmuyor olmalı. Mail gitmediyse kullanıcı sonra şikayet eder, manuel düzeltilir. Ama "müşteri kredi kartından çek" başarısız olduysa para gelmemiş, sipariş bedava verilmiş — bunu telafi edemezsin.

Bu şartlardan biri sağlanmıyorsa → bir sonraki yaklaşıma geç.

---

## Yaklaşım 3: Outbox Pattern — Gerçek Garanti

Outbox pattern karmaşık bir problemi çözmek için var: **iki ayrı sistem nasıl atomik olarak güncellenir?**

DB ile message queue iki ayrı sistemdir. DB'ye sipariş yazmak ve queue'ya event göndermek **iki ayrı işlem**dir. Birini başarıyla yapıp diğerini başaramazsan — tutarsızlık. Geleneksel veritabanı transaction'ları iki farklı sistemi kapsayamaz (distributed transaction çok pahalı ve güvenilmez).

### Outbox'ın Çözümü

Sipariş'i ve event'i **aynı DB'ye** yazıyorsun. Aynı transaction içinde. İkisi de yazılır veya ikisi de yazılmaz — DB bu garantiyi veriyor.

Sonra ayrı bir worker süreci bu "outbox" tablosunu okuyup event'leri queue'ya gönderiyor.

```
1. ADIM (web request içinde):
   Sipariş → DB
   Event → outbox tablosu (aynı DB, aynı transaction)
   COMMIT → ikisi de atomik olarak yazıldı

2. ADIM (background worker):
   Outbox tablosunu poll et
   Henüz işlenmemiş event var mı? Al, queue'ya gönder
   Başarılı olursa "işlendi" işaretle
   Başarısız olursa tekrar dene (sonsuza kadar veya max attempt'e kadar)
```

### Outbox Neden Çalışır?

Anahtar fikir: DB transaction'ı atomik. İki kayıt (sipariş + event) ya birden olur ya birden olmaz. Bu nokta sağlam.

Sonrasında worker'ın yapacağı tek iş: outbox'taki event'leri queue'ya iletmek. Worker çökerse → restart olunca kaldığı yerden devam eder. Queue çökerse → worker bekler, queue gelince gönderir. Network koparsa → retry eder.

**Garantisi:** **At-least-once delivery.** Event en az bir kez gönderilecek. Ağı veya servisi yeterince beklersen mutlaka iletilecek.

### At-Least-Once Sorunu ve İdempotency

"At-least-once" demek "tam bir kez" değil demek. Aynı event birden fazla kez gönderilebilir. Mesela:

- Worker event'i queue'ya gönderdi → consumer aldı → işledi → ACK göndereceği sırada network koptu
- Worker "iletildi mi?" bilmiyor → tekrar gönderiyor
- Consumer aynı event'i ikinci kez alıyor → bir kez daha mı işlesin?

İşte burada Gün 100'deki inbox pattern devreye giriyor. Consumer her event'in MessageId'sini kontrol eder. Daha önce işlemişse atlar.

**Net kural:** Outbox kullanıyorsan, consumer'ın idempotent olması ZORUNLU. Event'in iki kez gelmesi hayatın gerçeği — bunu güvenli şekilde tolere etmen lazım.

### Outbox Tablosu — Basit Yapı

```csharp
public class OutboxMessage
{
    public Guid Id { get; set; }              // event'in benzersiz kimliği
    public string Type { get; set; }          // event tipi (deserialize için)
    public string Payload { get; set; }       // event'in JSON içeriği
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; } // null = henüz işlenmedi
    public int RetryCount { get; set; }       // kaç kez denendi
    public string? Error { get; set; }        // son hata mesajı
}
```

Sipariş servisi event'i normal kayıt gibi DbContext'e ekler:

```csharp
public async Task SiparisOlusturAsync(SiparisDto dto)
{
    var siparis = new Siparis(dto);
    _context.Siparisler.Add(siparis);

    _context.OutboxMessages.Add(new OutboxMessage
    {
        Type = nameof(SiparisOlusturulduEvent),
        Payload = JsonSerializer.Serialize(new SiparisOlusturulduEvent(siparis.Id))
    });

    await _context.SaveChangesAsync();
    // Sipariş + outbox kaydı tek transaction'da, atomik
}
```

Background worker periyodik olarak okuyup işler — kod detayını uzatmıyorum, prensip net olsun yeter.

### Outbox Ne Zaman Zorunlu?

- Event başka bir servise gidiyorsa (microservice ortamı)
- Event kaybı kabul edilemiyorsa (ödeme, sipariş, kritik iş süreçleri)
- "Tam tutarlılık" istiyorsan
- Audit/compliance gereği her event'in kaydı olmalıysa

### Outbox Ne Zaman Aşırı?

- Tek monolith uygulama
- Handler'lar in-process çalışıyor
- Mail/log/analytics gibi tolere edilebilir yan etkiler
- Geliştirici sayısı az, altyapı yatırımı pahalı

Çoğu küçük-orta projede Yaklaşım 2 (SaveChanges sonrası interceptor dispatch) yeterli. Outbox'a microservice'e geçince veya kritik event akışında ihtiyaç duyacaksın.

---

## Eventual Consistency — Anlık Tutarlılık Yanılgısı

Burada bir kavram var ki anlamadan domain event'leri doğru kullanamazsın: **eventual consistency** (sonunda tutarlılık).

Geleneksel transactional düşünce: "Sipariş oluşturuldu → stok düştü, mail gitti, sevkiyat haberdar oldu" — hepsi anında ve atomik. Tek transaction.

Domain event yaklaşımında bu garanti yok. Sipariş anında oluşur ama:
- Stok 50ms sonra düşer (handler asenkron çalışıyor)
- Mail 2 saniye sonra gider (mail servisi yavaş)
- Sevkiyat 5 saniye sonra haberdar olur (queue'dan geçiyor)

Bu ARALIK BOYUNCA sistem tutarsız görünür. Sipariş var ama stok düşmemiş. Bu kabul edilebilir mi?

**Eventual consistency demek:** Sistem ER VEYA GEÇ tutarlı olacak. Anlık değil ama "yeterince yakın zamanda" hepsi senkronize olacak.

Kullanıcı deneyiminde bu önemli:
- Kullanıcı sipariş verdi → "siparişiniz alındı" mesajı gördü
- Ama "siparişlerim" sayfasına gittiğinde sipariş henüz görünmüyor olabilir (read model henüz güncellenmemiş)
- 2-3 saniye sonra refresh edince görünüyor

Bu kabul edilebilir mi? Çoğunlukla evet. Ama bazı durumlarda hayır — örneğin ödeme bittikten sonra "ödemeniz başarısız" mesajı görüyorsa kullanıcı (henüz event işlenmemiş), kötü deneyim.

**Pratik:** Eventual consistency'yi kabul ettiğin senaryolar için domain event kullan. Anlık tutarlılık gereken senaryolarda klasik transaction (aynı SaveChanges içinde her şey) kullan.

---

## Handler'ın Transaction Scope'u

Handler çalıştığında hangi DB transaction'ında çalışıyor — bu önemli bir tasarım kararı.

### Senaryo A: Handler Aynı Transaction'da

Save öncesi dispatch yaparsan ve handler aynı DbContext'i kullanırsa, handler'ın yaptığı değişiklikler ana SaveChanges ile birlikte commit olur.

**Avantajı:** Atomik tutarlılık. Sipariş + stok düşümü ya birlikte olur, ya hiç olmaz. Domain'in iç tutarlılığı korunur.

**Dezavantajı:** Handler hatası tüm transaction'ı rollback eder. "Mail gönderme handler" hata verdi diye sipariş iptal mi olsun? Bu istemediğin bir bağımlılık.

**Ne zaman uygun:** Handler'ın işi domain'in bir parçasıysa. "Sipariş oluşturuldu → stok düş" gerçekten birlikte olması gereken iki şey. Stok düşmeyecekse sipariş olmamalı.

### Senaryo B: Handler Yeni Transaction'da

Save sonrası dispatch yaparsan, handler çalıştığında ana transaction zaten kapanmış. Handler kendi DbContext scope'unda çalışır.

**Avantajı:** Handler izolasyonu. Bir handler'ın hatası diğerlerini etkilemez. Yan etkiler bağımsız çalışır.

**Dezavantajı:** Tutarlılık garantisi zayıf. Ana iş tamam ama yan etkilerden biri başarısız olabilir.

**Ne zaman uygun:** Handler'ın işi domain'in dışında. Mail göndermek, analytics'e atmak, başka servise bildirmek. Bunlar başarısız olsa bile sipariş geçerli.

### Pratik Karar Matrisi

| Handler'ın yaptığı iş | Tercih |
|----------------------|--------|
| Domain'in iç parçası (stok, bakiye, ilişkili durum) | Aynı transaction VEYA Outbox |
| Bilgilendirme (mail, push notification) | Yeni transaction, save sonrası |
| Analytics, logging, raporlama | Yeni transaction, save sonrası |
| Başka servise integration event | Outbox (kesinlikle) |

---

## Handler Çalışma Sırası

Bir event'e birden fazla handler dinliyor. Sırayla mı paralel mi çalışırlar? Hangi sırayla? Birinin hatası diğerlerini engeller mi?

### MediatR Davranışı (Varsayılan)

MediatR `Publish` çağrıldığında handler'ları **sırayla** çalıştırır. Sıra deterministik değil — DI container'ın handler'ları ne sırayla bulduğuna bağlı.

**Önemli:** Bir handler exception fırlatırsa ne olur? MediatR varsayılan olarak diğer handler'ları yine de çalıştırır mı yoksa durur mu?

Eski versiyonlarda exception olunca dururdu. Yeni versiyonlarda davranış konfigüre edilebilir. Genel olarak: **handler'ların birbirinden bağımsız olduğunu varsay.** Birinin hatası diğerini etkilemesin. Her handler kendi try-catch'ini yazsın.

### Sıra Önemli Olursa?

Eğer handler'ların belirli bir sırada çalışması gerekiyorsa — bu zaten kötü tasarım sinyali. Event-driven mimaride handler'lar bağımsız olmalı. Sıralı süreç istiyorsan saga pattern veya orchestrator kullan.

---

## IDomainEventHandler vs INotificationHandler — Hangi Soyutlama?

MediatR'ın `INotificationHandler<T>` interface'i var. Çoğu projede bunu kullanıyorsun:

```csharp
public class SiparisHandler : INotificationHandler<SiparisOlusturulduEvent>
{
    public Task Handle(SiparisOlusturulduEvent notification, CancellationToken ct) { ... }
}
```

Ama bazı projelerde `IDomainEventHandler<T>` diye kendi interface'lerini görürsün:

```csharp
public interface IDomainEventHandler<T> where T : IDomainEvent
{
    Task HandleAsync(T domainEvent, CancellationToken ct);
}
```

İkisi de aynı şeyi yapıyor. Fark **felsefi**:

**INotificationHandler** kullanırsan — domain katmanın MediatR'a bağımlı oluyor. Domain event sınıfların `INotification` interface'ini implement etmek zorunda. MediatR bir framework, domain'ine sızıyor.

**IDomainEventHandler** kullanırsan — domain MediatR'ı bilmiyor. Kendi soyutlamanı yazıyorsun. Sonra infrastructure katmanında bu soyutlamayı MediatR'a (veya başka şeye) bağlıyorsun.

Clean Architecture'ın katı uygulanması: Domain hiçbir framework'ü bilmemeli. Bu açıdan custom `IDomainEventHandler` doğru. Pratik açıdan: MediatR zaten kullanıyorsun, ekstra soyutlama gereksiz karmaşa.

**Pragmatik tavsiye:** Küçük-orta projede MediatR'ın `INotificationHandler`'ı yeterli. Domain'in framework'ten tamamen izole olması büyük bir avantaj sağlamıyor. Büyük, uzun ömürlü, kütüphane bağımlılığından uzak kalmak isteyen projelerde custom soyutlama anlamlı.

---

## Üç Katmanlı Event Hiyerarşisi

İyi mimaride üç farklı seviye event var. Karıştırılmaması önemli.

### 1. Domain Event — En İçeride

**Nerede:** Tek bounded context içinde, in-process.
**Dil:** Domain'in iş dili. Türkçe alan adları, internal tipler olabilir.
**Yaşam süresi:** Çok kısa — yayınlanır, handler'lar çalışır, biter.
**Kontrat:** Yok. İç değişikliklerde özgürce değiştirilebilir.

Örnek: `SiparisOlusturuldu` — sipariş modülü yayınlar, aynı uygulamadaki stok modülü dinler.

### 2. Application Event — Use Case Bazlı

**Nerede:** Application katmanında, hâlâ in-process.
**Amaç:** Use case akışını yönetir. Domain event'lerden farklı, "iş süreci" odaklı.

Örnek: `SiparisAkisiBaslatildi` — kullanıcı checkout'a girdi, ödeme servisine yönlendirildi. Bu domain event değil, application-level akış.

Pratikte domain event ile application event sıkça karışır. Net ayrım için domain event'i "iş kuralları açısından önemli olay", application event'i "teknik/akış olayı" diye düşün.

### 3. Integration Event — Servisler Arası

**Nerede:** Başka servislere gidiyor. Message broker üzerinden (RabbitMQ, Kafka).
**Dil:** Stabil, dokümante edilmiş kontrat. İngilizce alan adları, primitive tipler.
**Yaşam süresi:** Uzun. Versiyonlanır, geriye uyumlu olmak zorunda.
**Kontrat:** Var ve sıkıdır. Değiştirirsen tüketici servisleri kırılır.

Örnek: `OrderCreatedV1` — sipariş servisi yayınlar, sevkiyat ve fatura servisleri dinler. Bu event'in formatı stabildir.

### Dönüşüm — Domain Event → Integration Event

Domain event'i direkt başka servise göndermek **yanlış**. Çünkü:
- Domain event'in formatı içsel, değişebilir. Tüketici servisler bu değişikliklere bağımlı olamaz.
- Domain event içinde aktif entity referansları olabilir (henüz commit olmamış). Integration event'in elinden gelen sadece "kesinleşmiş veri."
- Versiyon yönetimi farklı. Domain event'i değiştirebilirsin ama V1 integration event'i sonsuza kadar desteklemek zorunda kalabilirsin.

Doğru yaklaşım: Domain event yayınla → bir handler bunu yakala → veriyi olgunlaştır, integration event'e dönüştür → outbox'a yaz → worker queue'ya gönder.

İki katman arasında bir "dönüşüm noktası" var. Domain'in iç işleri ile dış dünyaya gönderilen şey ayrılıyor.

---

## Karar Akışı — Pratik Özet

Yeni bir domain event eklerken şu soruları sırayla sor:

**Soru 1: Bu event başka servise mi gidiyor?**
- EVET → Outbox pattern + Integration event dönüşümü. Tartışma yok.
- HAYIR → Devam.

**Soru 2: Handler aynı bounded context'in tutarlılığı için kritik mi?**
- EVET (örn: sipariş + stok atomik olmalı) → Save öncesi dispatch + aynı transaction, VEYA outbox.
- HAYIR → Devam.

**Soru 3: Handler hatası kabul edilebilir mi?**
- EVET (örn: mail gitmediyse sipariş yine de geçerli) → Save sonrası dispatch (interceptor).
- HAYIR → Outbox (garanti istiyorsun).

**Soru 4: Handler idempotent mi?**
- EVET → Sorun yok.
- HAYIR → Idempotent hale getirmek zorundasın. Aksi halde retry'da çift işlem olur.

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de event yapısı yok. Sipariş servisi her şeyi kendi yapıyor — mail, stok, log hepsi tek metotta. Bu yaklaşım 500 kullanıcıda çalışır ama:
- Yeni gereksinim eklenince (mesela SMS bildirimi) sipariş servisi yine değişir
- Test edilemez (sipariş testi mail servisini de çağırıyor)
- 5 farklı servis birbirine sıkıca bağlı

50K kullanıcıda domain event ile bu bağımlılıkları kırarsın. Sipariş servisi sadece "sipariş oluşturuldu" der. Mail, stok, sevkiyat, analytics — her biri kendi handler'ında, bağımsız geliştirilir, bağımsız test edilir.

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| Domain event ile decoupling | İyi alışkanlık, başlangıçta öğren | Zorunlu — bağımlılıkları yönet |
| Interceptor ile save sonrası dispatch | Çoğu senaryoda yeterli | Çoğu senaryoda yeterli |
| Outbox pattern | Gereksiz (basit senaryo) | Microservice veya kritik event'lerde zorunlu |
| Integration event ayrımı | Tek servis, gereksiz | Microservice varsa zorunlu |
| Eventual consistency anlayışı | Kavramsal olarak öğren | Pratik olarak yaşa, UX'i ona göre tasarla |
| Idempotent handler | İyi alışkanlık | Outbox ile zorunlu |

---

## Kontrol Soruları

1. Hayalet sipariş ne demek? SaveChanges'ten önce event dispatch yapmak nasıl bu sorunu yaratıyor?
2. SaveChanges'ten sonra dispatch yapmanın zayıf yanı nedir? Hangi senaryoda yan etki kaybedilir?
3. Outbox pattern'in DB transaction'ından yararlanma mantığı nedir? Niçin iki ayrı sistemi atomik güncelleme problemini çözüyor?
4. "At-least-once delivery" garantisi ne demek? Handler'ların idempotent olması neden zorunlu?
5. Eventual consistency nedir? Tutarsızlık aralığında kullanıcı deneyimini nasıl tasarlarsın?
6. Handler'ı aynı transaction'da vs yeni transaction'da çalıştırmanın trade-off'u nedir?
7. Domain event ile integration event arasındaki farkları say. Aralarındaki dönüşüm nerede yapılır, neden direkt domain event'i başka servise göndermek yanlış?
8. Sıralı çalışması gereken handler'lar olması neden kötü tasarım sinyalidir?
