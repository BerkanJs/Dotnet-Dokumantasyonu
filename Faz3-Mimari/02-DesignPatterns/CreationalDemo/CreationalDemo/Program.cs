using CreationalDemo.Factory;
using CreationalDemo.Builder;

var kitaplar = new List<string> { "Suç ve Ceza", "Sefiller", "Dune", "1984" };

// ── Factory Method ─────────────────────────────────────────────
Console.WriteLine("=== Factory Method ===\n");

// Çağıran kod hangi sınıfın oluşturulduğunu bilmiyor
var exporter = ExporterFactory.Olustur("pdf");
exporter.Disa_Aktar(kitaplar);

exporter = ExporterFactory.Olustur("excel");
exporter.Disa_Aktar(kitaplar);

// ── Builder ────────────────────────────────────────────────────
Console.WriteLine("\n=== Builder (Fluent) ===\n");

var sorgu = KitapSorgu.Olustur()
    .Kategori("Roman")
    .FiyatAraligi(50, 200)
    .Limit(10)
    .SadeceStoktakiler()
    .Bitir();

Console.WriteLine(sorgu);

// Farklı kombinasyon — aynı builder, farklı parametreler
var sorgu2 = KitapSorgu.Olustur()
    .Limit(5)
    .Bitir();

Console.WriteLine(sorgu2);
