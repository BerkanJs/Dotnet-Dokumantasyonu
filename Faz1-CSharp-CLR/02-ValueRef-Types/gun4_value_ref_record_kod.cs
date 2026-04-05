// Gün 4 — struct, class, record: Kod Demoları

using System;

// ============================================================
// BÖLÜM 1: class vs record — eşitlik farkı
// ============================================================
Console.WriteLine("=== class vs record: Eşitlik Karşılaştırması ===");

var classA = new KitapClass { Baslik = "Clean Code", Fiyat = 45m };
var classB = new KitapClass { Baslik = "Clean Code", Fiyat = 45m };
Console.WriteLine($"class ==  : {classA == classB}");   // false — farklı nesneler
Console.WriteLine($"class Equals: {classA.Equals(classB)}"); // false — referans karşılaştırma

var recordA = new KitapRecord("Clean Code", 45m);
var recordB = new KitapRecord("Clean Code", 45m);
Console.WriteLine($"record == : {recordA == recordB}");  // true — içerik aynı
Console.WriteLine($"record Equals: {recordA.Equals(recordB)}"); // true

Console.WriteLine();

// ============================================================
// BÖLÜM 2: record — with ifadesi ve immutability
// ============================================================
Console.WriteLine("=== record: with ifadesi ===");

var kitap = new KitapRecord("Clean Code", 45m);
Console.WriteLine($"Orijinal: {kitap}");

// with: yeni kopya oluşturur, sadece belirtilen alan değişir
var indirimli = kitap with { Fiyat = 36m };
Console.WriteLine($"İndirimli: {indirimli}");
Console.WriteLine($"Orijinal değişmedi: {kitap}");  // hâlâ 45m

Console.WriteLine();

// ============================================================
// BÖLÜM 3: API pattern — record ile request/response
// ============================================================
Console.WriteLine("=== Web API Pattern: record Request/Response ===");

// İstek geldi
var request = new CreateKitapRequest("Pragmatic Programmer", "Hunt & Thomas", 55m);
Console.WriteLine($"Request: {request}");

// İşlendi, response oluşturuldu
var response = new KitapResponse(1, request.Baslik, request.Fiyat);
Console.WriteLine($"Response: {response}");

// İndirimli versiyon — orijinal bozulmadan
var indirimliResponse = response with { Fiyat = response.Fiyat * 0.9m };
Console.WriteLine($"İndirimli Response: {indirimliResponse}");

Console.WriteLine();

// ============================================================
// BÖLÜM 4: Value Object pattern — record ile
// ============================================================
Console.WriteLine("=== Value Object: Email ===");

var email1 = new Email("test@example.com");
var email2 = new Email("test@example.com");
var email3 = new Email("baska@example.com");

Console.WriteLine($"email1 == email2: {email1 == email2}");  // true — aynı değer
Console.WriteLine($"email1 == email3: {email1 == email3}");  // false — farklı değer

try
{
    var gecersiz = new Email("gecersiz-email");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Geçersiz email hatası: {ex.Message}");
}

Console.WriteLine();

// ============================================================
// BÖLÜM 5: struct — ne zaman anlamlı
// ============================================================
Console.WriteLine("=== struct: Koordinat Örneği ===");

var nokta1 = new Koordinat(3.0, 4.0);
var nokta2 = nokta1;  // değer kopyalandı
nokta2 = new Koordinat(10.0, 20.0);

Console.WriteLine($"nokta1: {nokta1}");  // 3, 4 — değişmedi
Console.WriteLine($"nokta2: {nokta2}");  // 10, 20

Console.WriteLine();

// ============================================================
// BÖLÜM 6: record struct — ikisinin birleşimi
// ============================================================
Console.WriteLine("=== record struct ===");

var rs1 = new KoordinatRecord(1.0, 2.0);
var rs2 = new KoordinatRecord(1.0, 2.0);
Console.WriteLine($"record struct ==: {rs1 == rs2}");  // true — değer karşılaştırma
Console.WriteLine($"rs1: {rs1}");  // otomatik ToString

// ============================================================
// Tip tanımları
// ============================================================

// Mutable class — referans karşılaştırma
class KitapClass
{
    public string Baslik { get; set; }
    public decimal Fiyat { get; set; }
}

// Immutable record — değer karşılaştırma, otomatik ToString
record KitapRecord(string Baslik, decimal Fiyat);

// API modelleri
record CreateKitapRequest(string Baslik, string Yazar, decimal Fiyat);
record KitapResponse(int Id, string Baslik, decimal Fiyat);

// Value Object — validasyon constructor'da
record Email
{
    public string Deger { get; }

    public Email(string deger)
    {
        if (string.IsNullOrWhiteSpace(deger) || !deger.Contains('@'))
            throw new ArgumentException("Geçersiz email adresi");
        Deger = deger;
    }

    public override string ToString() => Deger;
}

// Struct — küçük, immutable, value semantics
struct Koordinat
{
    public double X { get; }
    public double Y { get; }

    public Koordinat(double x, double y) { X = x; Y = y; }
    public override string ToString() => $"({X}, {Y})";
}

// record struct — C# 10+
record struct KoordinatRecord(double X, double Y);
