namespace KitabeviMediatr.Application.UseCases.KitapEkle;

public record KitapEkleResult(
    int KitapId,
    string Baslik,
    string Isbn,
    decimal Fiyat,
    string ParaBirimi,
    int StokAdedi
);
// ↑ Kitap entity'sini olduğu gibi döndürmüyoruz
//   bunu yazmasaydık → Domain entity API'ye sızardı,
//   entity değişince API kontratı da bozulurdu
