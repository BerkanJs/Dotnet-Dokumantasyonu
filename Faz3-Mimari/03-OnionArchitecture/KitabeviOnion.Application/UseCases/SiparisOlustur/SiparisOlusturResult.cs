namespace KitabeviOnion.Application.UseCases.SiparisOlustur;

public record SiparisOlusturResult(int SiparisId, decimal ToplamTutar);
// ↑ Hangi sipariş oluştu, ne kadar tuttu — Controller bu bilgiyi API response'a çevirir
