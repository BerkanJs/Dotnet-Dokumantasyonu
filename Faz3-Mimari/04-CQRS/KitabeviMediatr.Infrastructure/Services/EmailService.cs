using KitabeviMediatr.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace KitabeviMediatr.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger) => _logger = logger;

    public async Task GonderAsync(string alici, string konu, string govde, CancellationToken ct = default)
    {
        // Gerçek projede: SmtpClient veya SendGrid buraya
        _logger.LogInformation("Email gönderildi → {Alici} | {Konu}", alici, konu);
        await Task.CompletedTask;
    }
}
