using KitabeviMediatr.Application.Interfaces;
using MediatR;

namespace KitabeviMediatr.Application.Notifications;

public class SiparisEmailHandler : INotificationHandler<SiparisOlusturulduNotification>
//                                  ↑ INotificationHandler — sadece dinliyor, cevap dönmüyor
//                                    IRequestHandler'dan fark: Task (void), TResponse yok
{
    private readonly IEmailService _emailService;

    public SiparisEmailHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task Handle(SiparisOlusturulduNotification notification, CancellationToken ct)
    {
        // EventData parse: "SiparisOnaylandi:42:kullanici@mail.com"
        var parcalar = notification.EventData.Split(':');
        var siparisId = parcalar[1];
        var kullaniciId = parcalar[2];

        await _emailService.GonderAsync(
            alici: kullaniciId,
            konu: "Siparişiniz Alındı",
            govde: $"Sipariş #{siparisId} başarıyla oluşturuldu.",
            ct: ct);
    }
}
