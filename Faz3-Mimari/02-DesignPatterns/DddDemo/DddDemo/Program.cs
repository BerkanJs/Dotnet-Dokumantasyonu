using DddDemo;

// ── Value Object ───────────────────────────────────────────────
Console.WriteLine("=== Value Object ===\n");

var fiyat1 = new Fiyat(100, "TRY");
var fiyat2 = new Fiyat(100, "TRY");
var fiyat3 = new Fiyat(150, "TRY");

Console.WriteLine($"fiyat1 == fiyat2: {fiyat1 == fiyat2}");  // true — value semantics
Console.WriteLine($"fiyat1 == fiyat3: {fiyat1 == fiyat3}");  // false

Console.WriteLine($"KDV'li: {fiyat1.KdvEkle()}");            // 118,00 TRY

var isbn = new Isbn("978-0-13-110362-7");
Console.WriteLine($"ISBN: {isbn}");

try { var _ = new Fiyat(-50); }
catch (Exception e) { Console.WriteLine($"[HATA] {e.Message}"); }

// ── Aggregate Root ─────────────────────────────────────────────
Console.WriteLine("\n=== Aggregate Root — Siparis ===\n");

var siparis = new Siparis(1, "berkan@example.com");
siparis.KalemEkle(101, "Dune",        new Fiyat(150), 2);
siparis.KalemEkle(102, "1984",        new Fiyat(120), 1);
siparis.KalemEkle(101, "Dune",        new Fiyat(150), 1); // aynı kitap → adet 3'e çıkar

Console.WriteLine($"Kalem sayısı: {siparis.Kalemler.Count}");
Console.WriteLine($"Toplam: {siparis.ToplamTutar():N2} TRY");

siparis.Onayla();
Console.WriteLine($"Durum: {siparis.Durum}");
Console.WriteLine($"Domain event: {siparis.DomainEvents[0].GetType().Name}");

// Onaylı siparişe kalem eklemeye çalış
try { siparis.KalemEkle(103, "Sefiller", new Fiyat(90), 1); }
catch (Exception e) { Console.WriteLine($"[HATA] {e.Message}"); }

// ── Domain Event dispatch simülasyonu ─────────────────────────
Console.WriteLine("\n=== Domain Event Dispatch ===\n");

foreach (var domainEvent in siparis.DomainEvents)
{
    if (domainEvent is SiparisOlusturulduEvent e)
        Console.WriteLine($"[EMAIL] '{e.MusteriEmail}' → Siparişiniz alındı. Tutar: {e.Tutar:N2} TRY");
}
// gerçek projede: SaveChanges sonrası MediatR.Publish() ile dispatch edilir
