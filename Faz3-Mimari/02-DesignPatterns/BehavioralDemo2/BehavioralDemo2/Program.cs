using BehavioralDemo2.Mediator;
using BehavioralDemo2.State;
using BehavioralDemo2.Iterator;

// ── Mediator ──────────────────────────────────────────────────
Console.WriteLine("=== Mediator ===\n");

var depo     = new KitapDepo();
var mediator = new KitapMediator(depo);

// Controller sadece mediator'ı biliyor — handler'ları bilmiyor
mediator.Gonder(new KitapEkleKomut("1984", 120));
mediator.Gonder(new KitapEkleKomut("Dune", 150));

var kitaplar = mediator.Gonder(new KitapListesorgu());
Console.WriteLine("Liste: " + string.Join(", ", kitaplar));

// ── State ─────────────────────────────────────────────────────
Console.WriteLine("\n=== State ===\n");

var siparis = new Siparis();
Console.WriteLine(siparis);

siparis.Gonder();       // henüz ödeme alınmadı — hata
siparis.OdemeAl();      // durum geçişi: Bekleme → Hazırlanıyor
Console.WriteLine(siparis);

siparis.OdemeAl();      // zaten alındı — hata
siparis.Gonder();       // durum geçişi: Hazırlanıyor → Yolda
Console.WriteLine(siparis);

siparis.Teslim();       // durum geçişi: Yolda → Teslim Edildi
Console.WriteLine(siparis);

// ── Iterator ──────────────────────────────────────────────────
Console.WriteLine("\n=== Iterator ===\n");

var koleksiyon = new KitapKoleksiyonu();

Console.WriteLine("yield return (lazy):");
foreach (var kitap in koleksiyon.PahaliBas(100))
    Console.WriteLine($"  {kitap}");

Console.WriteLine("\nIAsyncEnumerable (streaming):");
await foreach (var kitap in koleksiyon.StreamKitaplar())
    Console.Write($"{kitap}  ");
Console.WriteLine();
