// Gün 6 — Async/Await: Kod Demoları
// Not: Gerçek veritabanı yerine Task.Delay ile gecikme simüle ediyoruz.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

// ============================================================
// BÖLÜM 1: Sync vs Async — thread farkı
// ============================================================
Console.WriteLine("=== Sync vs Async ===");

var sw = Stopwatch.StartNew();

// Sync: her çağrı bir öncekinin bitmesini bekler
Console.WriteLine("Sync çalışıyor...");
var s1 = KitapGetirSync(1);
var s2 = KitapGetirSync(2);
sw.Stop();
Console.WriteLine($"Sync toplam: {sw.ElapsedMilliseconds}ms");

sw.Restart();

// Async: await ile thread bloklanmıyor
Console.WriteLine("Async çalışıyor...");
var a1 = await KitapGetirAsync(1);
var a2 = await KitapGetirAsync(2);
sw.Stop();
Console.WriteLine($"Async sıralı toplam: {sw.ElapsedMilliseconds}ms");
// Sıralı await da benzer sürer — fark thread'in bloklanmaması

Console.WriteLine();

// ============================================================
// BÖLÜM 2: Task.WhenAll — paralel çalıştırma
// ============================================================
Console.WriteLine("=== Task.WhenAll: Paralel ===");

sw.Restart();

// İki görevi aynı anda başlat
var kitaplarGorev = KitaplarGetirAsync();       // 300ms
var kategorilerGorev = KategorilerGetirAsync(); // 200ms

await Task.WhenAll(kitaplarGorev, kategorilerGorev);

sw.Stop();
Console.WriteLine($"Paralel toplam: {sw.ElapsedMilliseconds}ms");
// ~300ms — uzun olanın süresi kadar (sıralı olsaydı 500ms)

var kitaplar = kitaplarGorev.Result;
var kategoriler = kategorilerGorev.Result;
Console.WriteLine($"Kitaplar: {kitaplar.Count}, Kategoriler: {kategoriler.Count}");

Console.WriteLine();

// ============================================================
// BÖLÜM 3: CancellationToken — iptal mekanizması
// ============================================================
Console.WriteLine("=== CancellationToken ===");

var cts = new CancellationTokenSource();

// 150ms sonra iptal sinyali gönder
cts.CancelAfter(TimeSpan.FromMilliseconds(150));

try
{
    // Bu işlem 300ms sürüyor ama 150ms'de iptal edilecek
    await UzunSurenIslemAsync(cts.Token);
    Console.WriteLine("İşlem tamamlandı");
}
catch (OperationCanceledException)
{
    Console.WriteLine("İşlem iptal edildi (kullanıcı bağlantıyı kesti)");
}

Console.WriteLine();

// ============================================================
// BÖLÜM 4: async void tehlikesi — exception yutulur
// ============================================================
Console.WriteLine("=== async void Tehlikesi ===");

try
{
    // async void'i await edemiyoruz — exception yakalayamıyoruz
    AsyncVoidMetot();
    await Task.Delay(200);  // metodun bitmesini "umuyoruz"
    Console.WriteLine("async void çalıştı ama exception'ı yakalayamadık");
}
catch (Exception)
{
    // Bu catch HİÇBİR ZAMAN çalışmaz — async void exception'ı yuttu
    Console.WriteLine("Bu satır hiç çalışmaz");
}

// Doğru yol: async Task döndür
try
{
    await AsyncTaskMetot();
}
catch (Exception ex)
{
    Console.WriteLine($"async Task exception yakalandı: {ex.Message}");
}

Console.WriteLine();

// ============================================================
// BÖLÜM 5: Deadlock simülasyonu — .Result tehlikesi
// ============================================================
Console.WriteLine("=== .Result — Güvenli Kullanım Farkı ===");

// Task.WhenAll sonrası .Result GÜVENLI — await tamamlandı
var gorev1 = KitapGetirAsync(1);
var gorev2 = KitapGetirAsync(2);
await Task.WhenAll(gorev1, gorev2);

var sonuc1 = gorev1.Result;  // güvenli — zaten tamamlandı
var sonuc2 = gorev2.Result;  // güvenli — zaten tamamlandı
Console.WriteLine($"WhenAll sonrası .Result güvenli: {sonuc1.Baslik}, {sonuc2.Baslik}");

// YANLIŞ kullanım (yorum satırında — deadlock olur):
// var kitap = KitapGetirAsync(1).Result;  // TEHLİKELİ — deadlock riski

Console.WriteLine();

// ============================================================
// BÖLÜM 6: ValueTask — cache senaryosu
// ============================================================
Console.WriteLine("=== ValueTask: Cache Senaryosu ===");

var servis = new KitapServis();

// İlk çağrı — veritabanından alır (Task allocation var)
sw.Restart();
var k1 = await servis.KitapGetirAsync(1);
sw.Stop();
Console.WriteLine($"İlk çağrı (DB): {sw.ElapsedMilliseconds}ms — {k1.Baslik}");

// İkinci çağrı — cache'den alır (ValueTask, allocation yok)
sw.Restart();
var k2 = await servis.KitapGetirAsync(1);
sw.Stop();
Console.WriteLine($"İkinci çağrı (cache): {sw.ElapsedMilliseconds}ms — {k2.Baslik}");

// ============================================================
// Yardımcı local fonksiyonlar
// ============================================================

// Sync versiyon — thread bloklar
static KitapDto KitapGetirSync(int id)
{
    Thread.Sleep(100);  // veritabanı gecikmesi simülasyonu
    return new KitapDto(id, $"Kitap-{id}", 45m);
}

// Async versiyon — thread serbest kalır
static async Task<KitapDto> KitapGetirAsync(int id)
{
    await Task.Delay(100);  // veritabanı gecikmesi simülasyonu
    return new KitapDto(id, $"Kitap-{id}", 45m);
}

static async Task<List<KitapDto>> KitaplarGetirAsync()
{
    await Task.Delay(300);
    return new List<KitapDto> { new(1, "Clean Code", 45m), new(2, "DDD", 85m) };
}

static async Task<List<KategoriDto>> KategorilerGetirAsync()
{
    await Task.Delay(200);
    return new List<KategoriDto> { new(1, "Yazılım"), new(2, "Mimari") };
}

// CancellationToken destekli işlem
static async Task UzunSurenIslemAsync(CancellationToken cancellationToken)
{
    Console.WriteLine("  Uzun işlem başladı (300ms)...");
    await Task.Delay(300, cancellationToken);  // iptal sinyali gelirse exception fırlatır
    Console.WriteLine("  Uzun işlem bitti");
}

// async void — tehlikeli
static async void AsyncVoidMetot()
{
    await Task.Delay(50);
    throw new Exception("async void exception — kimse yakalayamaz!");
}

// async Task — doğru
static async Task AsyncTaskMetot()
{
    await Task.Delay(50);
    throw new Exception("async Task exception — yakalanabilir");
}

// ============================================================
// Tip tanımları — local fonksiyonlardan SONRA gelmeli
// ============================================================

record KitapDto(int Id, string Baslik, decimal Fiyat);
record KategoriDto(int Id, string Ad);

// ValueTask örneği
class KitapServis
{
    private readonly Dictionary<int, KitapDto> _cache = new();

    public ValueTask<KitapDto> KitapGetirAsync(int id)
    {
        // Cache'de varsa allocation yapmadan dön
        if (_cache.TryGetValue(id, out var cached))
            return ValueTask.FromResult(cached);

        // Yoksa async yoldan getir
        return new ValueTask<KitapDto>(VeritabanindenGetirAsync(id));
    }

    private async Task<KitapDto> VeritabanindenGetirAsync(int id)
    {
        await Task.Delay(100);  // DB gecikmesi
        var kitap = new KitapDto(id, $"Kitap-{id}", 45m);
        _cache[id] = kitap;
        return kitap;
    }
}
