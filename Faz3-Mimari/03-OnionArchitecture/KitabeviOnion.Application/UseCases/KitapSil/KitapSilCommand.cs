namespace KitabeviOnion.Application.UseCases.KitapSil;

public record KitapSilCommand(int KitapId);
// ↑ CQRS Command — yazma isteği, side effect var (DB'den silinecek)
//   record: immutable — command nesneleri uçuşta değişmemeli
//   bunu yazmasaydık → Handler'a int id geçerdik, ne geldiği belirsiz olurdu
