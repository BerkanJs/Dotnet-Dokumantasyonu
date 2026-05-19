namespace KitabeviOnion.Application.Interfaces;

public interface IEmailService
// ↑ Application katmanında tanımlanıyor — Infrastructure implement edecek
//   bunu yazmasaydık → Handler doğrudan SmtpClient çağırırdı, test için gerçek SMTP gerekirdi
{
    Task GonderAsync(string alici, string konu, string govde, CancellationToken ct = default);
}
