using KitabeviOnion.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace KitabeviOnion.Infrastructure.Services;

public class EmailService : IEmailService
// ↑ Application'daki interface'i implement ediyor
//   bunu yazmasaydık → Application IEmailService'i çözemezdi, DI hata verirdi
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public async Task GonderAsync(string alici, string konu, string govde, CancellationToken ct = default)
    {
        // Gerçek projede: SmtpClient veya SendGrid buraya
        _logger.LogInformation("Email gönderildi → {Alici} | {Konu}", alici, konu);
        await Task.CompletedTask;
    }
}
