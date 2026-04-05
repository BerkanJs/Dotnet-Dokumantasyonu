// Gün 9 — LINQ: Kod Demoları

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

var kitaplar = new List<Kitap>
{
    new(1, "Clean Code",          "Martin",   45m,  "Yazılım"),
    new(2, "DDD",                 "Evans",    85m,  "Mimari"),
    new(3, "SICP",                "Abelson",  30m,  "Teori"),
    new(4, "Refactoring",         "Fowler",   55m,  "Yazılım"),
    new(5, "POEAA",               "Fowler",   90m,  "Mimari"),
    new(6, "The Pragmatic Prog",  "Hunt",     50m,  "Yazılım"),
};

// ============================================================
// BÖLÜM 1: Temel LINQ metodları
// ============================================================
Console.WriteLine("=== Temel LINQ Metodları ===");

// Where + OrderBy + Select
var ucuzlar = kitaplar
    .Where(k => k.Fiyat < 60)
    .OrderBy(k => k.Baslik)
    .Select(k => new KitapDto(k.Baslik, k.Fiyat));

Console.WriteLine("60'tan ucuz kitaplar (alfabetik):");
foreach (var k in ucuzlar)
    Console.WriteLine($"  {k}");

Console.WriteLine();

// GroupBy
Console.WriteLine("Kategoriye göre gruplar:");
var gruplar = kitaplar.GroupBy(k => k.Kategori);
foreach (var grup in gruplar)
{
    Console.WriteLine($"  [{grup.Key}]");
    foreach (var k in grup)
        Console.WriteLine($"    - {k.Baslik} ({k.Fiyat:C})");
}

Console.WriteLine();

// ============================================================
// BÖLÜM 2: Any() vs Count() — performans farkı
// ============================================================
Console.WriteLine("=== Any() vs Count() ===");

var buyukListe = Enumerable.Range(1, 1_000_000).ToList();

var sw = Stopwatch.StartNew();
bool countSonuc = buyukListe.Count(x => x > 500_000) > 0;  // tümünü sayar
sw.Stop();
Console.WriteLine($"Count() > 0 : {sw.ElapsedMilliseconds}ms — sonuç: {countSonuc}");

sw.Restart();
bool anySonuc = buyukListe.Any(x => x > 500_000);  // ilk eşleşmede durur
sw.Stop();
Console.WriteLine($"Any()       : {sw.ElapsedMilliseconds}ms — sonuç: {anySonuc}");

Console.WriteLine();

// ============================================================
// BÖLÜM 3: First() vs FirstOrDefault()
// ============================================================
Console.WriteLine("=== First() vs FirstOrDefault() ===");

// FirstOrDefault — bulamazsa null döner
var bulunan = kitaplar.FirstOrDefault(k => k.Id == 3);
var bulunamayan = kitaplar.FirstOrDefault(k => k.Id == 99);

Console.WriteLine($"Id=3 : {bulunan?.Baslik ?? "null"}");
Console.WriteLine($"Id=99: {bulunamayan?.Baslik ?? "null"}");

// First() — bulamazsa exception fırlatır
try
{
    var exc = kitaplar.First(k => k.Id == 99);  // exception!
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"First() exception: {ex.Message}");
}

Console.WriteLine();

// ============================================================
// BÖLÜM 4: Multiple enumeration tuzağı
// ============================================================
Console.WriteLine("=== Multiple Enumeration ===");

int passCount = 0;

IEnumerable<Kitap> sorgu = kitaplar.Where(k =>
{
    passCount++;
    return k.Fiyat < 60;
});

Console.WriteLine("Sorgu tanımlandı — henüz çalışmadı");
Console.WriteLine($"Pass count: {passCount}");

var adet = sorgu.Count();   // 1. pass
Console.WriteLine($"Count() sonrası pass count: {passCount}");

var ilk = sorgu.First();    // 2. pass
Console.WriteLine($"First() sonrası pass count: {passCount}");

// Çözüm: bir kez ToList()
passCount = 0;
var liste = kitaplar.Where(k =>
{
    passCount++;
    return k.Fiyat < 60;
}).ToList();

Console.WriteLine($"\nToList() sonrası pass count: {passCount}");
var adet2 = liste.Count;  // bellekten — pass yok
var ilk2 = liste.First(); // bellekten — pass yok
Console.WriteLine($"Sonrasında pass count: {passCount}");

Console.WriteLine();

// ============================================================
// BÖLÜM 5: yield return — lazy iterator
// ============================================================
Console.WriteLine("=== yield return ===");

Console.WriteLine("Büyük kitaplar (yield ile lazy):");
foreach (var k in PahalıKitaplar(kitaplar, 60m))
    Console.WriteLine($"  {k.Baslik} — {k.Fiyat:C}");

Console.WriteLine();

// ============================================================
// BÖLÜM 6: Extension method — kendi LINQ metodunu yaz
// ============================================================
Console.WriteLine("=== Extension Method ===");

// string'e yeni metot ekledik — sanki string'in kendi metoduymuş gibi
string baslik = "clean code";
Console.WriteLine(baslik.BasHarfBuyut());  // Clean Code

// List<Kitap>'a extension metot
var enUcuz = kitaplar.EnUcuzu();
Console.WriteLine($"En ucuz: {enUcuz?.Baslik} — {enUcuz?.Fiyat:C}");

Console.WriteLine();

// ============================================================
// BÖLÜM 7: Deferred execution — sonradan eklenen eleman
// ============================================================
Console.WriteLine("=== Deferred Execution: Sonradan Ekleme ===");

var sayilar = new List<int> { 1, 2, 3 };
var ikidenbuyuk = sayilar.Where(x => x > 2);  // tarif — çalışmadı

sayilar.Add(10);  // sorgu tanımlandıktan sonra eklendi
sayilar.Add(20);

Console.Write("Sonuç (10 ve 20 görünür): ");
foreach (var x in ikidenbuyuk)  // şimdi çalışıyor — 10 ve 20'yi de görüyor
    Console.Write(x + " ");
Console.WriteLine();

// ============================================================
// Local fonksiyonlar
// ============================================================

static IEnumerable<Kitap> PahalıKitaplar(List<Kitap> liste, decimal esik)
{
    foreach (var k in liste)
    {
        if (k.Fiyat > esik)
            yield return k;  // bu elemanı ver, devam et — tüm liste bellekte değil
    }
}

// ============================================================
// Extension metodlar
// ============================================================
static class StringExtensions
{
    // string'e eklendi — this string s
    public static string BasHarfBuyut(this string s)
        => string.Join(" ", s.Split(' ').Select(w =>
            w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w));
}

static class KitapExtensions
{
    // List<Kitap>'a eklendi
    public static Kitap? EnUcuzu(this IEnumerable<Kitap> kitaplar)
        => kitaplar.MinBy(k => k.Fiyat);
}

// ============================================================
// Tip tanımları
// ============================================================
record Kitap(int Id, string Baslik, string Yazar, decimal Fiyat, string Kategori);
record KitapDto(string Baslik, decimal Fiyat);
