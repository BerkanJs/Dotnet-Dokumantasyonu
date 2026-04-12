using KitabeviMVC.Data;
using MediatR;

namespace KitabeviMVC.Features.Kitaplar;

// Gün 35: KitapSilCommand'ı işleyen handler.
// FindAsync ile kayıt aranır; bulunamazsa false döner.
// Bulunan kayıt Remove ile işaretlenir, SaveChanges ile DELETE SQL üretilir.
public class KitapSilCommandHandler : IRequestHandler<KitapSilCommand, bool>
{
    private readonly KitabeviDbContext _context;

    public KitapSilCommandHandler(KitabeviDbContext context)
        => _context = context;

    public async Task<bool> Handle(KitapSilCommand request, CancellationToken ct)
    {
        var kitap = await _context.Kitaplar.FindAsync(
            new object[] { request.Id }, ct);
        // FindAsync: Change Tracker'a bakar, yoksa DB'ye gider.
        // GetByIdAsync sonra Remove: iki adım — ama varlık doğrulaması yapılmış olur.

        if (kitap is null)
            return false;
        // Kayıt bulunamadıysa: false → controller 404 döndürür.
        // Exception fırlatmak: her caller try/catch yazmak zorunda kalır — verbose.

        _context.Kitaplar.Remove(kitap);
        // Remove: Change Tracker'da "Deleted" state'ine geçirir.
        // SaveChanges'e kadar gerçek DELETE SQL üretilmez.

        await _context.SaveChangesAsync(ct);
        // SaveChangesAsync: DELETE FROM Kitaplar WHERE Id = @id SQL üretir.
        // ct: istek iptal sinyali gelirse transaction durdurulur.

        return true;
        // Başarılı silme: controller 204 NoContent döndürebilir.
    }
}
