using BehavioralDemo1.Strategy;
using BehavioralDemo1.Observer;
using BehavioralDemo1.Command;

// ── Strategy ──────────────────────────────────────────────────
Console.WriteLine("=== Strategy ===\n");

var listeServisi = new KitapListeServisi();

Console.WriteLine(string.Join(", ", listeServisi.Listele(new AdaSirala())));
Console.WriteLine(string.Join(", ", listeServisi.Listele(new TersAdaSirala())));
Console.WriteLine(string.Join(", ", listeServisi.Listele(new RastgeleSirala())));
// KitapListeServisi değişmedi — sadece strateji değişti

// ── Observer ──────────────────────────────────────────────────
Console.WriteLine("\n=== Observer ===\n");

var depo = new KitapDepo();

new EmailBildirimServisi().Abone(depo);  // abone ol
new StokUyariServisi().Abone(depo);      // abone ol

depo.Ekle("Dune", 120);                 // event tetiklenir → 2 subscriber çalışır
depo.Ekle("Foundation", 250);           // StokUyari da devreye girer

// ── Command ───────────────────────────────────────────────────
Console.WriteLine("\n=== Command ===\n");

var siparisDepo  = new SiparisDepo();
var islemci      = new SiparisIslemcisi();

islemci.Calistir(new KitapSiparisKomutu("1984", siparisDepo));
islemci.Calistir(new KitapSiparisKomutu("Sefiller", siparisDepo));
islemci.SonGeriAl();   // son siparişi iptal et
islemci.SonGeriAl();   // bir öncekini de iptal et
islemci.SonGeriAl();   // geri alınacak şey kalmadı
