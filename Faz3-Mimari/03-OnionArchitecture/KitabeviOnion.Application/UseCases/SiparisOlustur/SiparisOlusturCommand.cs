namespace KitabeviOnion.Application.UseCases.SiparisOlustur;

public record SiparisOlusturCommand(
    string KullaniciId,
    int KitapId,
    int Adet
);
// ↑ CQRS Command — yazma isteği, side effect var (DB değişecek)
//   record: immutable — command nesneleri uçuşta değişmemeli
