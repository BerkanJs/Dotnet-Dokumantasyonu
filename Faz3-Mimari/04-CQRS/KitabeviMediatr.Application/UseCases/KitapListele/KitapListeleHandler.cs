using KitabeviMediatr.Application.DTOs;
using KitabeviMediatr.Domain.Entities;
using KitabeviMediatr.Domain.Interfaces;
using KitabeviMediatr.Domain.Specifications;
using MediatR;

namespace KitabeviMediatr.Application.UseCases.KitapListele;

public class KitapListeleHandler : IRequestHandler<KitapListeleQuery, IReadOnlyList<KitapDto>>
{
    private readonly IKitapRepository _kitapRepo;

    public KitapListeleHandler(IKitapRepository kitapRepo)
    {
        _kitapRepo = kitapRepo;
    }

    public async Task<IReadOnlyList<KitapDto>> Handle(
        KitapListeleQuery request,
        CancellationToken cancellationToken)
    {
        // Temel specification: her zaman sadece aktif kitaplar
        ISpecification<Kitap> spec = new AktifKitaplarSpecification();
        //                           ↑ başlangıç: aktif filtre zorunlu — silinmiş kitap dönmez
        //                             bunu yazmasaydık → TumunuGetirAsync çağırırdık, Aktif = false olanlar da gelirdi

        // Fiyat filtresi isteniyorsa AND ile birleştir
        if (request.MinFiyat > 0 || request.MaxFiyat < decimal.MaxValue)
        {
            var fiyatSpec = new FiyatAraligiSpecification(request.MinFiyat, request.MaxFiyat);
            spec = new AndSpecification<Kitap>(spec, fiyatSpec);
            //     ↑ mevcut spec + fiyat filtresi — ikisi birlikte uygulanıyor
            //       bunu yazmasaydık → fiyat filtresi TüMünü getirip sonra LINQ ile filtrelemek zorunda kalırdık
        }

        // Stok filtresi isteniyorsa AND ile ekle
        if (request.SadeceStoktakiler)
        {
            var stokSpec = new StoktaOlanKitaplarSpecification();
            spec = new AndSpecification<Kitap>(spec, stokSpec);
            //     ↑ şimdiye kadar oluşan spec + stok filtresi
            //       3 filtre zincirleme: aktif AND fiyat AND stokta
            //       bunu yazmasaydık → if (k.StokAdedi > 0) handler içinde yazardık,
            //       "stokta" tanımı değişince handler güncellenmek zorunda kalırdı
        }

        var kitaplar = await _kitapRepo.ListAsync(spec, cancellationToken);
        //                               ↑ repository, spec'in Criteria'sını SQL'e çevirir
        //                                 handler, hangi filtrenin geldiğini bilmiyor — spec biliyor

        return kitaplar
            .Select(k => new KitapDto(
                k.Id,
                k.Baslik,
                k.Isbn.Deger,
                k.Fiyat.Deger,
                k.Fiyat.ParaBirimi,
                k.StokAdedi))
            .ToList()
            .AsReadOnly();
    }
}
