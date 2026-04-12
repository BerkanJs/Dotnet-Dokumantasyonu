using KitabeviMVC.Data;
using KitabeviMVC.Models.Entities;
using MediatR;

namespace KitabeviMVC.Features.Kitaplar;

// Gün 35: KitapEkleCommand'ı işleyen handler.
// Validation geçtikten sonra entity oluşturur, kaydeder, yeni Id'yi döndürür.
public class KitapEkleCommandHandler : IRequestHandler<KitapEkleCommand, int>
{
    private readonly KitabeviDbContext _context;

    public KitapEkleCommandHandler(KitabeviDbContext context)
        => _context = context;

    public async Task<int> Handle(KitapEkleCommand request, CancellationToken ct)
    {
        var kitap = new Kitap
        {
            Baslik       = request.Baslik,
            Yazar        = request.Yazar,
            Fiyat        = request.Fiyat,
            Kategori     = request.Kategori,
            StokAdedi    = request.StokAdedi,
            EklemeTarihi = DateTime.UtcNow
        };

        _context.Kitaplar.Add(kitap);
        await _context.SaveChangesAsync(ct);
        // SaveChangesAsync(ct): iptal sinyali gelirse transaction durdurulur
        // SaveChanges() yazsaydık: thread bloke edilir, async'in faydası azalır

        return kitap.Id;
        // EF Core SaveChanges sonrası Id'yi DB'den doldurur — biz okuyoruz
        // SaveChanges öncesi kitap.Id okusaydık: 0 dönerdi (DB henüz atamamış)
    }
}
