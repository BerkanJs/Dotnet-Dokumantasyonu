using System.Diagnostics;
using KitabeviMediatr.Domain.Entities;
using KitabeviMediatr.Domain.Interfaces;
using KitabeviMediatr.Domain.Specifications;
using KitabeviMediatr.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace KitabeviMediatr.Infrastructure.Persistence.Repositories;

public class LoggingKitapRepository : IKitapRepository
//                                     ↑ yine aynı interface — zincire eklenebilir
{
    private readonly IKitapRepository _inner;
    private readonly ILogger<LoggingKitapRepository> _logger;

    public LoggingKitapRepository(IKitapRepository inner, ILogger<LoggingKitapRepository> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Kitap>> TumunuGetirAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var sonuc = await _inner.TumunuGetirAsync(ct);
        sw.Stop();

        _logger.LogInformation("TumunuGetir: {Adet} kitap, {Ms}ms", sonuc.Count, sw.ElapsedMilliseconds);
        //      ↑ performans log — hangi sorgu kaç ms sürdü
        //        cache hit'te ~0ms, DB'de ~24ms — fark buradan görülür
        //        bunu yazmasaydık → handler'a logging kodu eklemek zorunda kalırdık

        return sonuc;
    }

    public async Task<Kitap?> BulByIdAsync(int id, CancellationToken ct = default)
    {
        var sonuc = await _inner.BulByIdAsync(id, ct);
        _logger.LogInformation("BulById({Id}): {Sonuc}", id, sonuc is null ? "bulunamadı" : "bulundu");
        return sonuc;
    }

    public Task EkleAsync(Kitap kitap, CancellationToken ct = default)
        => _inner.EkleAsync(kitap, ct);
    //   ↑ logging gerekmiyorsa doğrudan ilet — her metodu loglamak zorunda değiliz

    public Task KaydetAsync(CancellationToken ct = default)
        => _inner.KaydetAsync(ct);

    public Task<bool> IsbnMevcutMu(Isbn isbn, CancellationToken ct = default)
        => _inner.IsbnMevcutMu(isbn, ct);

    public async Task<IReadOnlyList<Kitap>> ListAsync(ISpecification<Kitap> spec, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var sonuc = await _inner.ListAsync(spec, ct);
        sw.Stop();

        _logger.LogInformation("ListAsync({Spec}): {Adet} kitap, {Ms}ms",
            spec.GetType().Name,
            //  ↑ hangi specification geldi — "AndSpecification`1", "AktifKitaplarSpecification"
            //    bunu yazmasaydık → hangi filtrenin yavaş olduğunu log'dan anlayamazdık
            sonuc.Count, sw.ElapsedMilliseconds);

        return sonuc;
    }
}
