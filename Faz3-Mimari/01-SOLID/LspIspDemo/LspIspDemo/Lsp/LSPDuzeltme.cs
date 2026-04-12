namespace LspIspDemo.Lsp;

// ✅ LSP Düzeltme: Ortak davranışı interface'e çıkar, kalıtım yerine kompozisyon

// Her ürünün ortak davranışı: satılabilir ve fiyatı var
public interface ISatilabilir
{
    string Baslik { get; }
    decimal Fiyat { get; }
    bool StokVarMi();
    // bunu yazmasaydık → çağıran kod StokDus'u çağırmadan önce
    // "bu fiziksel mi dijital mi?" diye sormak zorunda kalırdı
}

public class FizikselKitap : ISatilabilir
{
    public string Baslik { get; set; } = string.Empty;
    public decimal Fiyat { get; set; }
    public int StokAdedi { get; set; }

    public bool StokVarMi() => StokAdedi > 0;
    // bunu yazmasaydık → çağıran kod doğrudan StokAdedi > 0 yazardı → encapsulation bozulur

    public void StokDus()
    {
        if (!StokVarMi()) throw new InvalidOperationException("Stok yok");
        StokAdedi--;
    }
}

public class EKitap : ISatilabilir
{
    public string Baslik { get; set; } = string.Empty;
    public decimal Fiyat { get; set; }
    public string IndirmeLinki { get; set; } = string.Empty;

    public bool StokVarMi() => true;
    // dijital ürün her zaman mevcut — çağıran kod bunu bilmek zorunda değil
    // ISatilabilir üzerinden gelirse doğru davranışı alır → LSP sağlanmış
}
