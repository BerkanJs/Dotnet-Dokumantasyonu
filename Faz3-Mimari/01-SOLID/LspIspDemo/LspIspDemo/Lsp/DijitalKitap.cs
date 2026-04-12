namespace LspIspDemo.Lsp;

// ❌ LSP İHLALİ: DijitalKitap, Kitap'ın yerine GEÇEMİYOR
// Çağıran kod "Kitap" beklediğinde DijitalKitap verirsek davranış değişiyor:
//   kitap.StokDus() → Kitap için çalışır, DijitalKitap için exception fırlatır
// Bu Liskov'u ihlal eder: alt tip, üst tipin yerine geçemez
public class DijitalKitap : Kitap
{
    public string IndirmeLinki { get; set; } = string.Empty;

    // Dijital kitabın stoğu olmaz — ama base class StokAdedi bekliyor
    public override int StokAdedi
    {
        get => int.MaxValue;                    // "sonsuz stok" gibi davranıyor
        set { /* yoksay */ }                    // set gelince sessizce yutuyoruz
        // bunu yazmasaydık → her satın almada "Stok yok" exception'ı fırlardı
    }

    public override void StokDus()
    {
        // Dijital kitap satılınca stok düşmez — base'in aksine
        // ÇAĞIRAN KOD BU FARKI BİLMEK ZORUNDA → LSP ihlali
    }
}
