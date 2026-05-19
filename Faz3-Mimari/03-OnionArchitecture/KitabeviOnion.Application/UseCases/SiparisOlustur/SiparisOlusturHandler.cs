using KitabeviOnion.Application.Interfaces;
using KitabeviOnion.Domain.Entities;
using KitabeviOnion.Domain.Exceptions;
using KitabeviOnion.Domain.Interfaces;

namespace KitabeviOnion.Application.UseCases.SiparisOlustur;

public class SiparisOlusturHandler
{
    private readonly IKitapRepository _kitapRepo;
    private readonly ISiparisRepository _siparisRepo;
    private readonly IEmailService _emailService;
    // ↑ üç bağımlılığın hepsi interface — Infrastructure'ın ne kullandığını bilmiyor
    //   bunu yazmasaydık → test için gerçek DB ve gerçek SMTP gerekirdi

    public SiparisOlusturHandler(
        IKitapRepository kitapRepo,
        ISiparisRepository siparisRepo,
        IEmailService emailService)
    {
        _kitapRepo = kitapRepo;
        _siparisRepo = siparisRepo;
        _emailService = emailService;
    }

    public async Task<SiparisOlusturResult> Handle(
        SiparisOlusturCommand cmd,
        CancellationToken ct = default)
    {
        // 1. Kitabı getir
        var kitap = await _kitapRepo.BulByIdAsync(cmd.KitapId, ct);

        if (kitap is null)
            throw new DomainException($"Kitap bulunamadı: {cmd.KitapId}");
        //  ↑ uygulama akışı kuralı — var mı yok mu kontrolü

        // 2. Domain kuralı: stok yeterli mi?
        kitap.StokAzalt(cmd.Adet);
        // ↑ "adet > stok ise hata fırlat" kuralı Kitap.StokAzalt içinde
        //   Handler bu if'i yazmak zorunda değil — kurala uymak zorunda olan Domain

        // 3. Sipariş oluştur
        var siparis = new Siparis(cmd.KullaniciId);
        //                        ↑ Domain entity constructor — durum Beklemede başlar

        siparis.KalemEkle(kitap.Id, kitap.Baslik, kitap.Fiyat, cmd.Adet);
        //      ↑ "onaylanmış siparişe kalem eklenemez" kuralı Siparis içinde
        //        Handler bu kontrolü yazmak zorunda değil

        siparis.Onayla();
        // ↑ "boş sipariş onaylanamaz" kuralı Siparis içinde
        //   aynı zamanda domain event toplandı: "SiparisOnaylandi:..."

        // 4. Kaydet
        await _siparisRepo.EkleAsync(siparis, ct);
        await _siparisRepo.KaydetAsync(ct);
        // ↑ transaction: iki repo aynı SaveChanges'te — ya ikisi de ya hiçbiri

        // 5. Bildirim — domain event'e tepki
        await _emailService.GonderAsync(
            alici: cmd.KullaniciId,
            konu: "Siparişiniz Alındı",
            govde: $"Sipariş #{siparis.Id} oluşturuldu. Tutar: {siparis.ToplamTutar():C}",
            ct: ct);
        // ↑ email burada — ama Siparis sınıfı EmailService'i bilmiyor (SRP korundu)
        //   bunu yazmasaydık → email Siparis.Onayla() içinde olurdu, test için gerçek SMTP gerekirdi

        return new SiparisOlusturResult(siparis.Id, siparis.ToplamTutar());
    }
}
