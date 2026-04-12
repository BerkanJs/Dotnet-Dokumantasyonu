using StructuralDemo.Decorator;
using StructuralDemo.Adapter;
using StructuralDemo.Facade;

// ── Decorator ─────────────────────────────────────────────────
Console.WriteLine("=== Decorator ===\n");

IKitapServisi servis = new CachedKitapServisi(new EfKitapServisi());

servis.HepsiniGetir();  // DB'ye gider
servis.HepsiniGetir();  // cache'den döner — DB çağrısı yok

// ── Adapter ───────────────────────────────────────────────────
Console.WriteLine("\n=== Adapter ===\n");

IFiyatSaglayici fiyatSaglayici = new TedarikciAdapter(new DisTedarikciApi());
// Uygulama IFiyatSaglayici biliyor — DisTedarikciApi bilmiyor
// Tedarikçi değişince sadece yeni Adapter yazılır

Console.WriteLine($"Fiyat: {fiyatSaglayici.FiyatGetir("978-0-13-110362-7"):C0}");
Console.WriteLine($"Stok: {fiyatSaglayici.StokVarMi("978-0-13-110362-7")}");

// ── Facade ────────────────────────────────────────────────────
Console.WriteLine("\n=== Facade ===\n");

var facade = new SiparisFacade();
var takipNo = facade.SiparisVer(
    isbn:  "978-0-13-110362-7",
    fiyat: 150,
    adres: "İstanbul, Kadıköy",
    email: "berkan@example.com");

Console.WriteLine($"\nTakip no: {takipNo}");
