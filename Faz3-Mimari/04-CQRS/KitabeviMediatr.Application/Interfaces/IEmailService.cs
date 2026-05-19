namespace KitabeviMediatr.Application.Interfaces;

public interface IEmailService
{
    Task GonderAsync(string alici, string konu, string govde, CancellationToken ct = default);
}
