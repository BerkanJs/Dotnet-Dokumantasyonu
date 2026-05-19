using KitabeviMediatr.Domain.Entities;
using KitabeviMediatr.Domain.Exceptions;
using KitabeviMediatr.Domain.Interfaces;
using KitabeviMediatr.Domain.ValueObjects;
using MediatR;

namespace KitabeviMediatr.Application.UseCases.KitapEkle;

public class KitapEkleHandler : IRequestHandler<KitapEkleCommand, KitapEkleResult>
//                               ↑ MediatR: KitapEkleCommand → bu handler
{
    private readonly IKitapRepository _kitapRepo;
    //               ↑ Domain interface — Infrastructure bilgisi yok

    public KitapEkleHandler(IKitapRepository kitapRepo)
    {
        _kitapRepo = kitapRepo;
    }

    public async Task<KitapEkleResult> Handle(
        KitapEkleCommand request,
        CancellationToken cancellationToken)
    {
        // 1. ISBN Value Object oluştur — format kontrolü burada
        var isbn = new Isbn(request.Isbn);
        //          ↑ "978abc" → DomainException fırlar, handler'a ulaşmaz
        //            bunu yazmasaydık → string olarak saklanırdı, sonradan temizlenmesi zor

        // 2. Duplikat ISBN kontrolü
        var isbnVar = await _kitapRepo.IsbnMevcutMu(isbn, cancellationToken);
        if (isbnVar)
            throw new DomainException($"Bu ISBN zaten kayıtlı: {isbn.Deger}");
        //  ↑ DB'ye sormadan domain kuralını uygulama: aynı ISBN iki kez olamaz
        //    bunu yazmasaydık → aynı ISBN'li iki kitap DB'ye girebilirdi

        // 3. Fiyat Value Object oluştur
        var fiyat = new Fiyat(request.Fiyat, request.ParaBirimi);
        //           ↑ 0 veya negatif → DomainException fırlar
        //             bunu yazmasaydık → decimal kullanırdık, para birimi kaybolurdu

        // 4. Domain entity oluştur — tüm kurallar Domain constructor'da
        var kitap = new Kitap(request.Baslik, isbn, fiyat, request.IlkStok);
        //                                                   ↑ negatif stok → DomainException

        // 5. Kaydet
        await _kitapRepo.EkleAsync(kitap, cancellationToken);
        await _kitapRepo.KaydetAsync(cancellationToken);
        //                ↑ SaveChanges — entity ID burada atanır (DB identity)

        // 6. Result — entity → DTO dönüşümü Application'da
        return new KitapEkleResult(
            KitapId: kitap.Id,
            //               ↑ SaveChanges sonrası ID DB'den geldi
            Baslik: kitap.Baslik,
            Isbn: kitap.Isbn.Deger,
            //              ↑ Value Object → primitive: API kontratı düz tip bekliyor
            Fiyat: kitap.Fiyat.Deger,
            ParaBirimi: kitap.Fiyat.ParaBirimi,
            StokAdedi: kitap.StokAdedi);
    }
}
