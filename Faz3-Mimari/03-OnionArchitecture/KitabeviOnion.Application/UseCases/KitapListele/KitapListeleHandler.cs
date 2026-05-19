using KitabeviOnion.Application.DTOs;
using KitabeviOnion.Domain.Interfaces;

namespace KitabeviOnion.Application.UseCases.KitapListele;

public class KitapListeleHandler
{
    private readonly IKitapRepository _kitapRepo;
    //               ↑ Domain interface — EF Core görmüyor
    //                 bunu yazmasaydık → DbContext inject etmek zorunda kalırdık

    public KitapListeleHandler(IKitapRepository kitapRepo)
    {
        _kitapRepo = kitapRepo;
    }

    public async Task<IReadOnlyList<KitapDto>> Handle(
        KitapListeleQuery query,
        CancellationToken ct = default)
    {
        var kitaplar = await _kitapRepo.TumunuGetirAsync(ct);
        //                              ↑ interface metodu — hangi DB olduğunu bilmiyor

        return kitaplar
            .Select(k => new KitapDto(
                k.Id,
                k.Baslik,
                k.Isbn.Deger,        // Value Object → primitive'e çevir
                k.Fiyat.Deger,
                k.Fiyat.ParaBirimi,
                k.StokAdedi))
            .ToList()
            .AsReadOnly();
        // ↑ Domain entity → DTO dönüşümü Application'da — Controller bilmek zorunda değil
        //   bunu yazmasaydık → Controller mapping yapardı, domain değişince Controller da değişirdi
    }
}
