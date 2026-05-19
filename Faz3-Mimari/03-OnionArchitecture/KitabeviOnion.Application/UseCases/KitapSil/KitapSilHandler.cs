using KitabeviOnion.Domain.Exceptions;
using KitabeviOnion.Domain.Interfaces;

namespace KitabeviOnion.Application.UseCases.KitapSil;

public class KitapSilHandler
{
    private readonly IKitapRepository _kitapRepo;
    //               ↑ Domain interface — EF Core görmüyor
    //                 bunu yazmasaydık → DbContext inject etmek zorunda kalırdık

    public KitapSilHandler(IKitapRepository kitapRepo)
    {
        _kitapRepo = kitapRepo;
    }

    public async Task Handle(KitapSilCommand cmd, CancellationToken ct = default)
    {
        var kitap = await _kitapRepo.BulByIdAsync(cmd.KitapId, ct);

        if (kitap is null)
            throw new DomainException($"Kitap bulunamadı: {cmd.KitapId}");
        //  ↑ önce var mı kontrol et — yoksa Infrastructure'a gitme
        //    bunu yazmasaydık → SilAsync içinde null ref hatası alırdık

        await _kitapRepo.SilAsync(cmd.KitapId, ct);
        //               ↑ interface metodu — hangi DB olduğunu bilmiyor
    }
}
