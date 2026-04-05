// Gün 5 — String ve Koleksiyonlar: Kod Demoları

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

// ============================================================
// BÖLÜM 1: String birleştirme — + vs StringBuilder
// ============================================================
Console.WriteLine("=== String + vs StringBuilder ===");

const int TEKRAR = 10_000;

// Kötü yol: her iterasyonda yeni string nesnesi
var sw = Stopwatch.StartNew();
string concat = "";
for (int i = 0; i < TEKRAR; i++)
    concat += i;  // heap'e yeni string, eskisi atılacak
sw.Stop();
Console.WriteLine($"string +      : {sw.ElapsedMilliseconds} ms, uzunluk: {concat.Length}");

// İyi yol: tek nesne, en sonda bir kez string
sw.Restart();
var sb = new StringBuilder();
for (int i = 0; i < TEKRAR; i++)
    sb.Append(i);
string sbSonuc = sb.ToString();
sw.Stop();
Console.WriteLine($"StringBuilder : {sw.ElapsedMilliseconds} ms, uzunluk: {sbSonuc.Length}");

Console.WriteLine();

// ============================================================
// BÖLÜM 2: String karşılaştırma
// ============================================================
Console.WriteLine("=== String Karşılaştırma ===");

string a = "kitap";
string b = "KITAP";

Console.WriteLine($"a == b                  : {a == b}");           // false
Console.WriteLine($"OrdinalIgnoreCase       : {string.Equals(a, b, StringComparison.OrdinalIgnoreCase)}");  // true

// API'de kullanıcı "İstanbul" yerine "istanbul" gönderirse
string sehir1 = "İstanbul";
string sehir2 = "istanbul";
Console.WriteLine($"Şehir eşit mi?          : {string.Equals(sehir1, sehir2, StringComparison.OrdinalIgnoreCase)}");

Console.WriteLine();

// ============================================================
// BÖLÜM 3: IEnumerable — deferred execution
// ============================================================
Console.WriteLine("=== IEnumerable: Deferred Execution ===");

var kitaplar = new List<string> { "Clean Code", "DDD", "SICP", "Refactoring" };

// Where çağrısı hiçbir şeyi çalıştırmıyor — sadece tarif
var sorgu = kitaplar.Where(k =>
{
    Console.WriteLine($"  [filtre çalışıyor] {k}");
    return k.Length > 5;
});

Console.WriteLine("Where tanımlandı ama henüz çalışmadı.");
Console.WriteLine("foreach başlıyor:");

// foreach tetikledi — şimdi çalışıyor
foreach (var k in sorgu)
    Console.WriteLine($"  -> {k}");

Console.WriteLine();

// ============================================================
// BÖLÜM 4: IQueryable simülasyonu — EF Core davranışı
// ============================================================
Console.WriteLine("=== IQueryable vs IEnumerable (EF Core Simülasyonu) ===");

// Gerçek projede bu db.Kitaplar olurdu
// Burada in-memory ile aynı mantığı gösteriyoruz

var tumKitaplar = new List<Kitap>
{
    new Kitap("Clean Code", 45m),
    new Kitap("DDD", 85m),
    new Kitap("SICP", 30m),
    new Kitap("Refactoring", 55m),
    new Kitap("POEAA", 90m),
};

// KÖTÜ: Önce ToList() — tümü belleğe gelir, sonra C#'ta filtre
Console.WriteLine("KÖTÜ yol (önce ToList):");
var kotuyol = tumKitaplar
    .ToList()                        // <— burada 5 kitap belleğe geldi
    .Where(k => k.Fiyat < 60)       // C#'ta filtreleniyor
    .ToList();
Console.WriteLine($"  Sonuç: {kotuyol.Count} kitap");

// İYİ: Önce filtre — sadece uygun kayıtlar gelir
Console.WriteLine("İYİ yol (önce Where, sonra ToList):");
var iyiyol = tumKitaplar
    .Where(k => k.Fiyat < 60)       // filtre tarifi (IQueryable'da SQL olurdu)
    .ToList();                        // <— burada sadece uygun kayıtlar geldi
Console.WriteLine($"  Sonuç: {iyiyol.Count} kitap");

Console.WriteLine();

// Zincir sorgu — EF Core'da bunu SQL'e çevirir
Console.WriteLine("Zincir sorgu (EF Core'da SQL olurdu):");
var zincir = tumKitaplar
    .Where(k => k.Fiyat < 80)
    .OrderBy(k => k.Baslik)
    .Select(k => new KitapDto(k.Baslik, k.Fiyat))
    .ToList();

foreach (var k in zincir)
    Console.WriteLine($"  {k}");

Console.WriteLine();

// ============================================================
// BÖLÜM 5: Event handler memory leak demosu
// ============================================================
Console.WriteLine("=== Event Handler Memory Leak ===");

var siparisServisi = new SiparisServisi();

// Blok içinde bildirim servisi oluşturuldu ve event'e abone oldu
{
    var bildirim = new BildirimServisi(siparisServisi);
    // blok bitti — bildirim değişkeni scope dışında
    // ama siparisServisi hâlâ onu event üzerinden tutuyor
}

// GC çalışsa bile bildirim servisi silinmez — event referans veriyor
GC.Collect();
GC.WaitForPendingFinalizers();

siparisServisi.SiparisVer();  // bildirim servisi hâlâ çağrılıyor — sızıntı
Console.WriteLine("(BildirimServisi hâlâ ayakta — silinmedi)");

Console.WriteLine();

// Doğru kullanım: IDisposable ile abonelikten çık
Console.WriteLine("Doğru yol: IDisposable ile abonelik yönetimi:");
using (var bildirimGüvenli = new BildirimServisiGuvenli(siparisServisi))
{
    siparisServisi.SiparisVer();
}
// Dispose çağrıldı → -= yapıldı → GC silebilir
siparisServisi.SiparisVer();  // artık BildirimServisiGuvenli çağrılmıyor

// ============================================================
// Tip tanımları
// ============================================================

record Kitap(string Baslik, decimal Fiyat);
record KitapDto(string Baslik, decimal Fiyat);

class SiparisServisi
{
    public event Action? SiparisVerildi;

    public void SiparisVer()
    {
        Console.WriteLine("  Sipariş verildi!");
        SiparisVerildi?.Invoke();
    }
}

// Memory leak örneği — abonelikten çıkmıyor
class BildirimServisi
{
    public BildirimServisi(SiparisServisi servis)
    {
        servis.SiparisVerildi += Bildir;
        Console.WriteLine("  BildirimServisi abone oldu");
    }

    void Bildir() => Console.WriteLine("  [BildirimServisi] Bildirim gönderildi");
}

// Doğru örnek — IDisposable ile abonelikten çıkıyor
class BildirimServisiGuvenli : IDisposable
{
    private readonly SiparisServisi _servis;

    public BildirimServisiGuvenli(SiparisServisi servis)
    {
        _servis = servis;
        _servis.SiparisVerildi += Bildir;
        Console.WriteLine("  BildirimServisiGuvenli abone oldu");
    }

    void Bildir() => Console.WriteLine("  [BildirimServisiGuvenli] Bildirim gönderildi");

    public void Dispose()
    {
        _servis.SiparisVerildi -= Bildir;  // abonelikten çıktı
        Console.WriteLine("  BildirimServisiGuvenli abonelikten çıktı (Dispose)");
    }
}
