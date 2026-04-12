using LspIspDemo.Lsp;
using LspIspDemo.Isp;

// ── LSP Demo ──────────────────────────────────────────────────
Console.WriteLine("=== LSP: ISatilabilir üzerinden polimorfizm ===\n");

List<ISatilabilir> urunler =
[
    new FizikselKitap { Baslik = "Suç ve Ceza", Fiyat = 120, StokAdedi = 3 },
    new EKitap        { Baslik = "Dune",        Fiyat = 80,  IndirmeLinki = "https://..." },
];

foreach (var urun in urunler)
{
    // Çağıran kod fiziksel mi dijital mi bilmiyor — LSP sağlanmış
    var durum = urun.StokVarMi() ? "stok var" : "stok yok";
    Console.WriteLine($"{urun.Baslik} ({urun.Fiyat:C0}) — {durum}");
}

// ── ISP Demo ──────────────────────────────────────────────────
Console.WriteLine("\n=== ISP: Ayrılmış interface'ler ===\n");

var efServis     = new EfKitapServisi();
var cacheServis  = new CachedKitapServisi(efServis);

// Controller sadece okuma interface'ini biliyor
IKitapOkuma okuma = cacheServis;
Console.WriteLine("Kitaplar: " + string.Join(", ", okuma.HepsiniGetir()));

// Batch işlem sadece EF servisine gidiyor — cache servisi bilmez
IKitapBatch batch = efServis;
batch.StokSifirla("Roman");
