using MediatR;

namespace KitabeviMVC.Features.Kitaplar;

// Gün 35: CQRS — silme command.
// Sadece silinecek kaydın Id'si taşınır — başka alan gerekmez.
// IRequest<bool>: kayıt bulunup silindi mi? — true/false döner.
public record KitapSilCommand(int Id) : IRequest<bool>;
// Tek alan: positional record — KitapSilCommand(42) gibi kısa kullanım.
// IRequest<bool>: kayıt bulunamazsa false → controller 404 verir.
// IRequest<Unit> (void): "başarısız" ile "bulunamadı" ayrımı yapılamaz — bilgi kaybı.
