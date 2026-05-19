using KitabeviMediatr.Application.DTOs;
using MediatR;

namespace KitabeviMediatr.Application.UseCases.KitapListele;

public record KitapListeleQuery(
//             ↑ Gün67: Specification'a geçilecek filtre parametreleri query'ye taşındı
//               Gün60'da: public record KitapListeleQuery() — filtre yoktu
    decimal MinFiyat = 0,
    //       ↑ varsayılan 0: belirtilmezse alt sınır yok
    //         bunu yazmasaydık → query her zaman zorunlu min/max almak zorunda kalırdı
    decimal MaxFiyat = decimal.MaxValue,
    //       ↑ varsayılan MaxValue: belirtilmezse üst sınır yok
    //         bunu yazmasaydık → "sadece aktif" sorgusu için MaxFiyat zorla girilmek zorunda kalırdı
    bool SadeceStoktakiler = false
    //   ↑ varsayılan false: aksi belirtilmezse stok filtresi yok
    //     bunu yazmasaydık → her seferinde stokta olup olmadığını handler tahmin ederdi
) : IRequest<IReadOnlyList<KitapDto>>;
//   ↑ MediatR convention: "bu query'nin cevabı IReadOnlyList<KitapDto>"
