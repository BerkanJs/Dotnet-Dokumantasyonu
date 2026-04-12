namespace StructuralDemo.Adapter;

// Bizim domain interface'imiz — Türkçe, domain diline uygun
// Uygulama kodu sadece bunu biliyor, dış API'yi bilmiyor
public interface IFiyatSaglayici
{
    decimal FiyatGetir(string isbn);
    bool StokVarMi(string isbn);
}

// Adapter: DisTedarikciApi → IFiyatSaglayici
// İki uyumsuz interface arasında çevirmen görevi yapar
// bunu yazmasaydık → uygulama kodu DisTedarikciApi'ye direkt bağımlı olurdu
// tedarikçi değişince tüm çağıran kodlar değişmek zorunda kalırdı
public class TedarikciAdapter : IFiyatSaglayici
{
    private readonly DisTedarikciApi _api;

    public TedarikciAdapter(DisTedarikciApi api) => _api = api;

    public decimal FiyatGetir(string isbn)
        => _api.GetPrice(isbn);         // İngilizce → Türkçe domain metoduna çevir

    public bool StokVarMi(string isbn)
        => _api.CheckAvailability(isbn); // İngilizce → Türkçe domain metoduna çevir
}
