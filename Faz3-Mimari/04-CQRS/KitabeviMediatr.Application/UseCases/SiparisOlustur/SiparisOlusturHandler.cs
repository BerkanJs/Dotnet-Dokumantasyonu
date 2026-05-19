using KitabeviMediatr.Application.Interfaces;
using KitabeviMediatr.Application.Notifications;
using KitabeviMediatr.Domain.Entities;
using KitabeviMediatr.Domain.Exceptions;
using KitabeviMediatr.Domain.Interfaces;
using MediatR;

namespace KitabeviMediatr.Application.UseCases.SiparisOlustur;

public class SiparisOlusturHandler : IRequestHandler<SiparisOlusturCommand, SiparisOlusturResult>
//                                    ↑ Gün59'dan fark: IRequestHandler<,> eklendi
{
    private readonly IKitapRepository _kitapRepo;
    private readonly ISiparisRepository _siparisRepo;
    private readonly IEmailService _emailService;
    private readonly IMediator _mediator;
    //               ↑ YENİ — handler başka event publish edebilmek için mediator tutabilir
    //                 Gün59'da yoktu: email doğrudan burada gönderiliyordu
    //                 Gün60'da: Publish() ile SiparisEmailHandler'a delege ediliyor

    public SiparisOlusturHandler(
        IKitapRepository kitapRepo,
        ISiparisRepository siparisRepo,
        IEmailService emailService,
        IMediator mediator)
    {
        _kitapRepo = kitapRepo;
        _siparisRepo = siparisRepo;
        _emailService = emailService;
        _mediator = mediator;
    }

    public async Task<SiparisOlusturResult> Handle(
        SiparisOlusturCommand request,
        CancellationToken cancellationToken)
    {
        var kitap = await _kitapRepo.BulByIdAsync(request.KitapId, cancellationToken);

        if (kitap is null)
            throw new DomainException($"Kitap bulunamadı: {request.KitapId}");

        kitap.StokAzalt(request.Adet);

        var siparis = new Siparis(request.KullaniciId);
        siparis.KalemEkle(kitap.Id, kitap.Baslik, kitap.Fiyat, request.Adet);
        siparis.Onayla();

        await _siparisRepo.EkleAsync(siparis, cancellationToken);
        await _siparisRepo.KaydetAsync(cancellationToken);

        // Domain event'leri publish et
        foreach (var domainEvent in siparis.DomainEvents)
            await _mediator.Publish(new SiparisOlusturulduNotification(domainEvent), cancellationToken);
        //                          ↑ her event → ilgilenen tüm handler'lara dağıtılır
        //                            Gün59'da: _emailService.GonderAsync() burada çağrılıyordu
        //                            Gün60'da: Publish() → SiparisEmailHandler + SiparisBildirimHandler
        //                            yeni alıcı eklemek Handler'a dokunmak zorunda değiliz
        //                            bunu yazmasaydık → email gönderme burada inline kalırdı

        return new SiparisOlusturResult(siparis.Id, siparis.ToplamTutar());
    }
}
