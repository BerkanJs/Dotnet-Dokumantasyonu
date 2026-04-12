namespace KitabeviMVC.Features.Kitaplar;

// Gün 35: Query sonuç DTO — view'a sadece gereken alanlar gider.
// Kitap entity'sini doğrudan döndürmek: navigation property döngüleri, gereksiz alanlar.
// record: değer eşitliği, immutable — response nesneleri değiştirilmemeli.
public record KitapListeDto(
    int     Id,
    string  Baslik,
    string  Yazar,
    decimal Fiyat,
    string  Kategori,
    int     StokAdedi
);
