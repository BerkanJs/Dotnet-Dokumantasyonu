namespace LspIspDemo.Isp;

// ❌ FAT INTERFACE — ISP İhlali
// CachedKitapServisi sadece okuma metodlarını kullanıyor
// ama TopluSil + StokSifirla'yı da implement ETMEK ZORUNDA
// → NotImplementedException veya boş gövde → çağıran aldatılıyor
public interface IKitabeviServisiFat
{
    // Okuma
    List<string> HepsiniGetir();
    string? BulById(int id);

    // Yazma
    void Ekle(string baslik);
    void Sil(int id);

    // Toplu operasyonlar — sadece EF Core ile çalışır
    // CachedKitapServisi bunu implement edemez ama interface zorluyor
    int TopluSil(string kategori);
    int StokSifirla(string kategori);
}
