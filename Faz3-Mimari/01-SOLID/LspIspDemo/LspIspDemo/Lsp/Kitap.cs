namespace LspIspDemo.Lsp;

// Temel kitap — fiziksel ürün
public class Kitap
{
    public string Baslik { get; set; } = string.Empty;
    public decimal Fiyat { get; set; }
    public virtual int StokAdedi { get; set; }   // virtual: alt sınıf override edebilir
    // bunu virtual yapmasaydık → DijitalKitap override edemezdi

    public virtual void StokDus()
    {
        if (StokAdedi <= 0)
            throw new InvalidOperationException("Stok yok");
        // bunu yazmasaydık → stok ekside gidebilirdi
        StokAdedi--;
    }
}
