using MediatR;

namespace KitabeviMVC.Features.Kitaplar;

// Gün 35: CQRS — okuma tarafı.
// IRequest<T>: bu mesaj gönderilince T tipinde yanıt beklenir.
// record: immutable — query nesneleri değiştirilmemeli
public record KitapListeQuery(string? Kategori = null) : IRequest<IList<KitapListeDto>>;
// string? Kategori: null gelirse tüm kitaplar, değer gelirse filtreli liste
