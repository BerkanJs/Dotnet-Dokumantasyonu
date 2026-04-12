using KitabeviMVC.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMVC.Features.Kitaplar;

// Gün 35: KitapGuncelleCommand'ı işleyen handler.
// FindAsync ile kayıt aranır; bulunamazsa false döner.
// Bulunan kayıt güncellenir, SaveChanges ile persiste edilir.
public class KitapGuncelleCommandHandler : IRequestHandler<KitapGuncelleCommand, bool>
{
    private readonly KitabeviDbContext _context;

    public KitapGuncelleCommandHandler(KitabeviDbContext context)
        => _context = context;
    // Primary constructor yerine klasik atama: okunabilirlik için.

    public async Task<bool> Handle(KitapGuncelleCommand request, CancellationToken ct)
    {
        var kitap = await _context.Kitaplar.FindAsync(
            new object[] { request.Id }, ct);
        // FindAsync(keyValues, ct): primary key ile arama — önce Change Tracker'a bakar,
        // yoksa DB'ye gider. FirstOrDefaultAsync'ten daha hızlı ID araması için.
        // bunu FirstOrDefaultAsync(k => k.Id == request.Id) ile yazmak da doğru ama daha yavaş.

        if (kitap is null)
            return false;
        // Kayıt bulunamazsa false: controller bunu 404'e çevirir.
        // Exception fırlatmak yerine false: CQRS handler'ı presentation kararından bağımsız tutar.

        kitap.Baslik    = request.Baslik;
        kitap.Yazar     = request.Yazar;
        kitap.Fiyat     = request.Fiyat;
        kitap.Kategori  = request.Kategori;
        kitap.StokAdedi = request.StokAdedi;
        // Property atama: Change Tracker "Modified" state'i fark eder.
        // SaveChanges'de sadece değişen kolonlar UPDATE SQL'ine girer.
        // bunu new Kitap { Id = ... } ile _context.Update() yaparak da güncelleyebiliriz,
        // ama FindAsync + property değişikliği daha güvenli: kayıt varlığı önceden doğrulandı.

        await _context.SaveChangesAsync(ct);
        // SaveChangesAsync: UPDATE SQL üretir.
        // ct: istek iptal edilirse transaction durdurulur.

        return true;
        // Başarılı güncelleme: controller redirect veya 200 OK döndürebilir.
    }
}
