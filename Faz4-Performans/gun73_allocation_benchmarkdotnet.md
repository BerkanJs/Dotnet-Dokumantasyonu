# Gün 73 — Allocation Analizi: BenchmarkDotNet ve dotMemory

Bugünün amacı: performans sorunlarını tahmin etmek yerine ölçmek — ve allocation'ın neden önemli olduğunu, araçların nasıl kullanıldığını anlamak.

---

## Gerçek Hayatta Bu Nerede Karşına Çıkar?

API response süresi zamanla uzuyor. Loglar temiz, hata yok. Ama "neden yavaş?" sorusunun cevabı yok.

İki yaygın hata:
1. "Burada `string +` kullanıyoruz, yavaş olmalı" → Tahmin. Ölçmedin.
2. BenchmarkDotNet çalıştırıyorsun ama allocation'a bakmıyorsun → Sadece süreyi görüyorsun, asıl sorunu kaçırıyorsun.

**Kural: Ölçmeden optimize etme. Optimize etmeden profil çıkarma.**

---

## Allocation Neden Kötü? GC Baskısı

.NET'te heap'e her nesne yazıldığında Garbage Collector (GC) er ya da geç temizlik yapmak zorundadır.

```
Allocation → Nesne heap'e gider
           → GC takip eder
           → GC çalışınca → tüm thread'ler durur (STW — Stop The World)
           → Uygulama o süre boyunca yanıt vermez
```

Küçük ve sık allocation'lar GC'yi sık çalıştırır:

```csharp
// Her çağrıda yeni string oluşturuyor — 10k istek/sn = 10k allocation/sn
public string MesajOlustur(string isim)
{
    return "Merhaba, " + isim + "! Hoş geldin.";
    // + operatörü her seferinde yeni string nesnesi oluşturur
    // bunu string.Format yazsaydık → aynı sorun, hatta daha fazla allocation
}
```

500 kullanıcıda fark edilmez. 50k istekte GC pause'ları latency spike'larına dönüşür.

---

## BenchmarkDotNet — Kurulum ve İlk Benchmark

```
dotnet add package BenchmarkDotNet
```

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

// [MemoryDiagnoser] → allocation bilgisini de göster
// bunu yazmasaydık → sadece süre görünür, kaç byte allocation olduğunu bilemezsin
[MemoryDiagnoser]

// [SimpleJob] → hangi .NET runtime'da çalışacak
[SimpleJob]
public class StringBirlestirmeBenchmark
{
    private const string Isim = "Berkan";

    [Benchmark(Baseline = true)]  // karşılaştırma için baz alınacak metot
    public string ArtiBirlestime()
    {
        return "Merhaba, " + Isim + "! Hoş geldin.";
        // her çağrıda 2 geçici string + 1 sonuç string → 3 allocation
    }

    [Benchmark]
    public string StringFormat()
    {
        return string.Format("Merhaba, {0}! Hoş geldin.", Isim);
        // format string parse edilir → yine allocation var
    }

    [Benchmark]
    public string Interpolation()
    {
        return $"Merhaba, {Isim}! Hoş geldin.";
        // modern C# → compiler optimize eder ama yine heap allocation
    }

    [Benchmark]
    public string StringBuilder()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("Merhaba, ");
        sb.Append(Isim);
        sb.Append("! Hoş geldin.");
        return sb.ToString();
        // kısa string için StringBuilder → overhead fazla, daha yavaş olabilir
        // bunu uzun string döngüsünde kullansaydık → çok daha iyi
    }
}

// Program.cs
BenchmarkRunner.Run<StringBirlestirmeBenchmark>();
```

**Çalıştırma — mutlaka Release modda:**

```bash
dotnet run -c Release
# bunu Debug modda çalıştırsaydın → JIT optimizasyonları devre dışı, sonuçlar yanıltıcı
```

**Örnek çıktı:**

```
| Method        | Mean     | Allocated |
|-------------- |---------:|----------:|
| ArtiBirlesme  | 45.2 ns  | 96 B      |  ← baseline
| StringFormat  | 89.1 ns  | 128 B     |  ← daha yavaş ve fazla allocation
| Interpolation | 43.8 ns  | 96 B      |  ← artı ile neredeyse aynı
| StringBuilder | 112.3 ns | 168 B     |  ← kısa string için kötü seçim
```

Bu sonuçtan şunu anlarsın: kısa string birleştirmede `+` ile interpolation farkı yok. `StringBuilder` kısa string için yanlış seçim.

---

## Gerçekçi Benchmark — LINQ vs For Döngüsü

```csharp
[MemoryDiagnoser]
public class LinqVsForBenchmark
{
    private readonly List<int> _sayilar = Enumerable.Range(1, 1000).ToList();

    [Benchmark(Baseline = true)]
    public int LinqSum()
    {
        return _sayilar.Where(x => x % 2 == 0).Sum();
        // Where → yeni IEnumerable oluşturur → allocation
        // Sum → enumerate eder
    }

    [Benchmark]
    public int ForSum()
    {
        int toplam = 0;
        for (int i = 0; i < _sayilar.Count; i++)
        {
            if (_sayilar[i] % 2 == 0)
                toplam += _sayilar[i];
                // allocation yok — sadece register/stack operasyonu
        }
        return toplam;
    }
}
```

**Sonuç örneği:**

```
| Method  | Mean     | Allocated |
|-------- |---------:|----------:|
| LinqSum | 3.21 μs  | 32 B      |
| ForSum  | 1.14 μs  | 0 B       |  ← sıfır allocation
```

For döngüsü 3x hızlı ve sıfır allocation. Ama LINQ okunabilirliği artırıyor.

**Gerçek hayatta karar:** Hot path'te (çok sık çalışan yer) for kullan. Tek seferlik veya az çalışan yerde LINQ okunabilirlik için tercih edilebilir.

---

## MemoryDiagnoser Çıktısını Okumak

```
| Method | Mean    | Error   | StdDev  | Gen0   | Gen1 | Allocated |
|------- |--------:|--------:|--------:|-------:|-----:|----------:|
| A      | 1.23 μs | 0.01 μs | 0.01 μs | 0.1234 |    - | 512 B     |
| B      | 0.45 μs | 0.00 μs | 0.00 μs |      - |    - | 0 B       |
```

- **Gen0 / Gen1:** GC koleksiyonu tetiklendi mi? Gen0 sık temizlenen kısa ömürlü nesneler için.  
  `0.1234` → her 1000 işlemde 123 Gen0 collection demek.  
  Bunu yazmasaydık → allocation'ın GC baskısına etkisini göremezdin.

- **Allocated:** Toplam heap allocation — `0 B` hedef.

- **Mean:** Ortalama süre.

- **Error / StdDev:** Ölçüm kararlı mı? Yüksek StdDev → sonuç güvenilmez.

---

## dotMemory ile Memory Profiling

BenchmarkDotNet micro-benchmark'lar için idealdir.  
Gerçek uygulama davranışını görmek için **dotMemory** (JetBrains) kullanılır.

**Ne zaman dotMemory?**
- Hangi nesne türü en çok yer kaplıyor?
- Memory leak var mı? (nesne hiç serbest bırakılmıyor)
- Hangi metot en fazla allocation yapıyor?

**Gerçek hayatta kullanım:**
1. dotMemory'yi uygulamaya bağla (Attach to process).
2. Yük altında snapshot al.
3. "Retained Size" en yüksek nesne türlerine bak.
4. Call stack'te allocation kaynağını bul.
5. Benchmark yaz → optimize et → tekrar ölç.

---

## dotnet-trace — CLI ile Profiling

JetBrains lisansı yoksa ücretsiz alternatif:

```bash
# Kurulum
dotnet tool install --global dotnet-trace

# Çalışan uygulamayı izle (PID ile)
dotnet-trace collect --process-id 1234 --providers Microsoft-DotNETCore-SampleProfiler

# Sonucu SpeedScope ile görselleştir
dotnet-trace convert trace.nettrace --format Speedscope
```

```bash
# dotnet-counters — anlık metrikler
dotnet tool install --global dotnet-counters

# PID bulmak için:
dotnet-counters ps

# İzlemeye başla:
dotnet-counters monitor --process-id 1234 --counters System.Runtime
# --counters yazmasaydık → tüm counter'lar gelir, ekran dolar, okunmaz
```

**Örnek çıktı — yorumlarla:**

```
[System.Runtime]
    GC Heap Size (MB)                  48      ← toplam heap kullanımı
    Gen 0 GC Count (count / 1 sec)      5      ← yüksekse kısa ömürlü çok nesne var
    Gen 1 GC Count (count / 1 sec)      1
    Gen 2 GC Count (count / 1 sec)      0      ← > 0 ise ciddi sorun, uzun süre pause
    Allocation Rate (B / 1 sec)   2,345,120    ← saniyede 2.3 MB — yüksek, araştır
    Exception Count (count / 1 sec)     0
    ThreadPool Thread Count            12
```

`Allocation Rate` yüksekse → BenchmarkDotNet ile hangi metodun yaptığını bul.  
`Gen 2 GC Count` > 0 ise → büyük nesneler uzun süre hayatta kalıyor, memory leak şüphesi.

**Örnek çıktı:**

```
[System.Runtime]
    GC Heap Size (MB)                          48
    Gen 0 GC Count                              5    ← son 1 saniyede 5 kez GC çalıştı
    Gen 1 GC Count                              1
    Allocation Rate (B / 1 sec)          2,345,120   ← saniyede 2.3 MB allocation — yüksek
    Exception Count / 1 sec                     0
    ThreadPool Thread Count                    12
```

`Allocation Rate` yüksekse → BenchmarkDotNet ile hangi metodun yaptığını bul.

---

## ETW ve EventPipe — Arka Planda Ne Oluyor?

`dotnet-trace` ve `dotnet-counters` arka planda **EventPipe** kullanır.  
Windows'ta EventPipe, ETW (Event Tracing for Windows) üzerine kuruludur.

Bilmen gereken:
- ETW → kernel-level, çok düşük overhead ile profiling
- EventPipe → cross-platform (Linux/macOS'ta da çalışır)
- dotnet-trace, EventPipe'ı kullanarak `.nettrace` dosyası üretir
- Bu dosyayı Visual Studio veya PerfView ile açabilirsin

Derinlemesine ETW bilmene gerek yok — `dotnet-trace` ve `dotnet-counters` seni oraya götürür.

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de performans ölçümü yoktu:

```csharp
// Faz2 — "yavaşsa log'a bakarız"
public async Task<List<KitapViewModel>> GetKitaplar()
{
    var kitaplar = await _db.Kitaplar.ToListAsync();      // N+1 riski, tüm kolonlar çekiliyor
    return kitaplar.Select(k => new KitapViewModel        // her item için yeni ViewModel — allocation
    {
        Id = k.Id,
        Ad = k.Ad
    }).ToList();                                          // ikinci liste — ikinci allocation
}
```

Faz4'te:
1. Önce ölç — BenchmarkDotNet veya dotnet-counters
2. Allocation kaynağını bul
3. Optimize et
4. Tekrar ölç — iyileşme var mı?

---

## 500 vs 50K Kullanıcı

| Konu | 500 kullanıcı/ay | 50K kullanıcı/ay |
|---|---|---|
| BenchmarkDotNet kurma | Overengineering olabilir | Hot path'ler için zorunlu |
| dotnet-counters | Faydalı — ücretsiz, kolay | Mutlaka kullan |
| Allocation optimizasyonu | Çoğu zaman erken | GC pause = latency spike |
| Memory profiling | Yalnızca leak şüphesi varsa | Düzenli yapılmalı |
| "Tahmin et optimize et" | Küçükte zararı az | Büyükte kaynakları boşa harcar |

**Overengineering sinyali:** Her metoda BenchmarkDotNet yazmak. Yalnızca hot path'ler (çok sık çalışan, ölçülen yavaş yerler) için uygula.

---

## Mini Özet

- Allocation → GC baskısı → latency spike. Büyük ölçekte kritik.
- BenchmarkDotNet: micro-benchmark için, mutlaka `[MemoryDiagnoser]` ile, mutlaka Release modda çalıştır.
- `Gen0 / Allocated` sütunlarına bak — süre tek başına yeterli değil.
- dotnet-counters: çalışan uygulamanın anlık GC ve allocation metriklerini ücretsiz gösterir.
- dotMemory: hangi nesne nerede oluşuyor, memory leak var mı — gerçek uygulama analizi için.
- Kural: önce ölç, sonra optimize et.

---

## Kontrol Soruları

1. BenchmarkDotNet'i Debug modda çalıştırsan sonuçlar neden yanıltıcı olur?
2. `Gen0 GC Count` yüksek çıktığında bu ne anlama gelir?
3. LINQ'nun For döngüsünden yavaş olduğu durumda her zaman For'a mı geçmeliyiz? Neden değil?
4. `dotnet-counters` ile `dotMemory` arasındaki fark nedir, ne zaman hangisini kullanırsın?
