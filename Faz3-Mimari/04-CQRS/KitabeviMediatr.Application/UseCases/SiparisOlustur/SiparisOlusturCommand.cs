using MediatR;

namespace KitabeviMediatr.Application.UseCases.SiparisOlustur;

public record SiparisOlusturCommand(
    string KullaniciId,
    int KitapId,
    int Adet
) : IRequest<SiparisOlusturResult>;
//  ↑ Command → IRequest: MediatR bu nesneyi alır, doğru handler'ı bulur, sonucu döner
//    bunu yazmasaydık → _mediator.Send(cmd) derlenmezdi
//
// Gün59 ile fark: ": IRequest<SiparisOlusturResult>" eklendi
// Artık Controller bu command'i doğrudan [FromBody] ile alabilir,
// ayrı SiparisOlusturRequest DTO'ya gerek kalmadı
