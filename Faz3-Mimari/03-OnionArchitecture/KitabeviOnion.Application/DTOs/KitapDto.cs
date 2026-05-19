namespace KitabeviOnion.Application.DTOs;

public record KitapDto(
//            ↑ record: immutable DTO, değer eşitliği otomatik
    int Id,
    string Baslik,
    string Isbn,
    decimal Fiyat,
    string ParaBirimi,
    int StokAdedi
);
// Domain entity'yi olduğu gibi döndürmiyoruz — API sözleşmesi ayrı
// bunu yazmasaydık → internal entity alanları (private set) API'ye sızardı,
// domain modeli değişince API kontratı da bozulurdu
