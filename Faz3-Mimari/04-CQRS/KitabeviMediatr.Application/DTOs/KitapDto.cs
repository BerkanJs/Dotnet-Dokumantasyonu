namespace KitabeviMediatr.Application.DTOs;

public record KitapDto(
    int Id,
    string Baslik,
    string Isbn,
    decimal Fiyat,
    string ParaBirimi,
    int StokAdedi
);
