# Gün 85 — Thread Pool ve Task Scheduling

---

## Thread Nedir, Neden Pahalıdır?

Bir thread, işletim sisteminin programına verdiği bir çalışma birimidir. Her thread yaklaşık 1 MB stack bellek kaplar, oluşturulması yüzlerce mikrosaniye sürer, bağlam değiştirmesi (context switch) CPU'ya ek maliyet yükler.

10 thread → sorun yok.  
500 thread → bellek baskısı, context switch gürültüsü, performans düşüşü.

Peki her istek için `new Thread()` açarsan ne olur?

```csharp
// Kötü — her işlemde sıfırdan thread aç
public void IsIcin()
{
    var thread = new Thread(() => AgirlisliIsYap());
    thread.Start();
    // 1000 eşzamanlı istek → 1000 thread → ~1 GB stack bellek
    // thread oluşturma süresi işin kendisinden uzun sürebilir
}
```

---

## ThreadPool — Thread Bankası

ThreadPool, önceden oluşturulmuş thread'lerin bekletildiği bir havuzdur. İş gelince boştaki bir thread alınır, iş bitince thread havuza geri döner — yeni thread açılmaz.

Gerçek dünya benzetmesi: serbest çalışan yerine kadrolu çalışan. Her iş için yeni biri işe almak yerine mevcut ekipten biri görevi alır, bitince bir sonraki göreve geçer.

```csharp
// ThreadPool'u direkt kullanmak — genellikle Task.Run tercih edilir
ThreadPool.QueueUserWorkItem(_ => AgirlisliIsYap());

// Modern yol — Task.Run arka planda ThreadPool kullanır
await Task.Run(() => AgirlisliIsYap());
// Thread açılmaz, havuzdan alınır → iş bitince geri döner
```

---

## ThreadPool Starvation — Havuz Tükenmesi

ThreadPool'da sınırlı sayıda thread vardır. Eğer thread'ler iş yapmadan bloklanırsa — sync bekleme, `.Result`, `.Wait()` — havuz tükenir ve yeni işler sıraya girer. Bu "starvation"dır.

```csharp
// Tehlikeli — async metodu sync beklemek
public string VeriGetir()
{
    return _httpClient.GetStringAsync(url).Result;
    // .Result → thread'i bloklar, async tamamlanana kadar bekler
    // bu thread artık başka iş yapamaz — havuzdan çıktı ama çalışmıyor
    // 100 istek → 100 thread bloklandı → havuz bitti → yeni istekler bekler
}

// Doğru — async/await ile thread serbest bırakılır
public async Task<string> VeriGetirAsync()
{
    return await _httpClient.GetStringAsync(url);
    // await → thread havuza geri döner, HTTP yanıtı gelince başka thread devam eder
}
```

**ThreadPool starvation belirtisi:** `dotnet-counters`'da `ThreadPool Queue Length` sürekli artıyor, response süresi giderek uzuyor.

---

## Task.Run vs Task.Factory.StartNew

İkisi de iş ThreadPool'a gönderir ama ince farkları vardır.

```csharp
// Task.Run — günlük kullanım için, güvenli varsayılanlar
await Task.Run(() => CpuYogunIs());
// CPU bound işi UI thread'den veya ASP.NET thread'inden ayırmak için

// Task.Factory.StartNew — ince kontrol gerektiğinde
await Task.Factory.StartNew(
    () => CpuYogunIs(),
    CancellationToken.None,
    TaskCreationOptions.LongRunning,    // uzun sürecek iş — ayrı thread açılır, havuzu meşgul etmez
    TaskScheduler.Default);
```

`TaskCreationOptions.LongRunning` ne zaman?  
Thread'in dakikalarca çalışacağını biliyorsan (dosya işleme, döngü, polling) — ThreadPool thread'lerini meşgul etmemek için ayrı thread aç.

```csharp
// Polling döngüsü — LongRunning uygun
Task.Factory.StartNew(async () =>
{
    while (!ct.IsCancellationRequested)
    {
        await KuyrukKontrolEtAsync();
        await Task.Delay(1000, ct);
    }
}, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);
```

---

## Synchronization Context — Hangi Thread'e Dön?

`await` tamamlandığında kod hangi thread'de devam eder? Bunu `SynchronizationContext` belirler.

**WPF / Windows Forms:**  
UI thread'inde başlayan await, tamamlanınca yine UI thread'ine döner. Çünkü UI kontrollerine yalnızca UI thread'i erişebilir.

```csharp
// WPF button click
private async void Button_Click(object sender, RoutedEventArgs e)
{
    var veri = await _repo.GetAsync();  // arka planda çalışır
    TextBox.Text = veri;                // UI thread'inde devam eder — güvenli
    // ConfigureAwait(false) yazsaydın → farklı thread → TextBox.Text = cross-thread exception
}
```

**ASP.NET Classic (.NET Framework):**  
Her istek bir SynchronizationContext'e sahipti — await sonrası aynı isteğin context'ine dönülürdü. Bu `.Result` kullanımında deadlock'a yol açıyordu.

```
Thread A: await başladı, context'i tuttu
Thread B: tamamlandı, context'e girmek istiyor ama A tutuyor
Thread A: .Result ile B'yi bekliyor
→ Deadlock — ikisi birbirini bekliyor
```

**ASP.NET Core:**  
SynchronizationContext yoktur. Await tamamlanınca ThreadPool'dan herhangi bir thread devam eder. Bu hem daha hızlı hem de deadlock riskini ortadan kaldırır.

```csharp
// ASP.NET Core'da bu güvenli
var veri = await _repo.GetAsync();  // farklı thread devam edebilir
return Ok(veri);                    // sorun yok — UI thread zorunluluğu yok
```

---

## Custom TaskScheduler — Ne Zaman Gerekir?

Çok nadir bir ihtiyaç. İş parçalarının belirli thread'lerde veya sırayla çalışmasını istiyorsan TaskScheduler yaz.

Yaygın kullanım: tek thread'li bir TaskScheduler ile tüm işlerin sıraya girmesini zorlamak.

```csharp
// ConcurrentExclusiveSchedulerPair — okuma paralel, yazma sıralı
var pair = new ConcurrentExclusiveSchedulerPair();

// Yazma işleri — exclusive, sırayla
Task.Factory.StartNew(() => Yaz(), CancellationToken.None,
    TaskCreationOptions.None, pair.ExclusiveScheduler);

// Okuma işleri — concurrent, paralel
Task.Factory.StartNew(() => Oku(), CancellationToken.None,
    TaskCreationOptions.None, pair.ConcurrentScheduler);
```

Gerçek projede bu kadar ince kontrole nadiren ihtiyaç duyulur — `SemaphoreSlim` veya `Channel` genellikle yeterlidir.

---

## Özet

| Konu | Özet |
|---|---|
| `new Thread()` | Pahalı, nadir kullan — LongRunning hariç |
| ThreadPool | Otomatik yönetilen havuz — `Task.Run` ile kullan |
| Starvation | `.Result` / `.Wait()` → thread bloklanır → havuz tükenir |
| `Task.Run` | CPU bound işi arka plana at |
| `Task.Factory.StartNew` | LongRunning veya özel scheduler gerektiğinde |
| SynchronizationContext | ASP.NET Core'da yok — deadlock riski ortadan kalktı |

---

## Kontrol Soruları

1. `new Thread()` yerine ThreadPool kullanmanın iki avantajı nedir?
2. `.Result` kullanımı ASP.NET Classic'te neden deadlock'a yol açıyordu?
3. `TaskCreationOptions.LongRunning` ne zaman kullanılır?
4. ASP.NET Core'da SynchronizationContext neden yoktur ve bu ne avantajı sağlar?
