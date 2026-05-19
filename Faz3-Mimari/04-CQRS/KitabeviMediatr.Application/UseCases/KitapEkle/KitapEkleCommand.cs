using MediatR;

namespace KitabeviMediatr.Application.UseCases.KitapEkle;

public record KitapEkleCommand(
    string Baslik,
    string Isbn,
    decimal Fiyat,
    string ParaBirimi,
    int IlkStok
) : IRequest<KitapEkleResult>;
//  ↑ IRequest<KitapEkleResult>: MediatR "bu command gelince KitapEkleResult dön" biliyor
//    bunu yazmasaydık → _mediator.Send(cmd) hangi tip döndüreceğini bilemezdi
