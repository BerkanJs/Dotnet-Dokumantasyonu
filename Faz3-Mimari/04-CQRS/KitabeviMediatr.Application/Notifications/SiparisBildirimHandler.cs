using MediatR;
using Microsoft.Extensions.Logging;

namespace KitabeviMediatr.Application.Notifications;

public class SiparisBildirimHandler : INotificationHandler<SiparisOlusturulduNotification>
//                                     ↑ aynı notification'ı dinleyen ikinci handler
//                                       SiparisEmailHandler'a tek satır dokunmadık
//                                       50k'da bu güç önemli: yeni bildirim kanalı (SMS, push)
//                                       → yeni handler, mevcut kod değişmez (OCP)
{
    private readonly ILogger<SiparisBildirimHandler> _logger;

    public SiparisBildirimHandler(ILogger<SiparisBildirimHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(SiparisOlusturulduNotification notification, CancellationToken ct)
    {
        _logger.LogInformation("Yeni sipariş bildirimi: {Data}", notification.EventData);
        return Task.CompletedTask;
        // ↑ async olmayan handler → Task.CompletedTask döner
        //   bunu yazmasaydık → derleme hatası: Handle() Task dönmeli
    }
}
