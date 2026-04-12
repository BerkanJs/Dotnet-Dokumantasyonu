namespace DipDemo;

// Abstraction YÜKSEK SEVİYELİ MODÜLDE (domain/application katmanında) yaşar
// KitapServisi bu interface'i tanımladı — EfKitapRepository değil
// bunu yazmasaydık → KitapServisi direkt EF Core'a bağımlı olurdu
// yarın Dapper'a geçince KitapServisi de değişmek zorunda kalırdı
public interface IKitapRepository
{
    List<string> HepsiniGetir();
    void Ekle(string baslik);
}
