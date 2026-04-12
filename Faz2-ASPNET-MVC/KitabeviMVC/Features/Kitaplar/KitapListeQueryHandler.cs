using KitabeviMVC.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMVC.Features.Kitaplar;

// Gün 35: KitapListeQuery'yi işleyen handler.
// MediatR, Send(KitapListeQuery) çağrısında DI üzerinden bu sınıfı bulup çalıştırır.
public class KitapListeQueryHandler : IRequestHandler<KitapListeQuery, IList<KitapListeDto>>
{
    private readonly KitabeviDbContext _context;

    public KitapListeQueryHandler(KitabeviDbContext context)
    {
        _context = context;
        // Query tarafında doğrudan DbContext kullanmak kabul edilebilir:
        // Query handler'lar küçük ve tek sorumlu — Repository katmanı ekstra soyutlama olurdu
    }

    public async Task<IList<KitapListeDto>> Handle(KitapListeQuery request, CancellationToken ct)
    {
        var sorgu = _context.Kitaplar.AsNoTracking();
        // AsNoTracking: okuma — Change Tracker'a kaydetmek gereksiz bellek harcar
        // yazmasaydık: 1000 kayıt → 1000 EntityEntry bellekte → GC baskısı

        if (!string.IsNullOrEmpty(request.Kategori))
            sorgu = sorgu.Where(k => k.Kategori == request.Kategori);
            // koşullu filtre: null gelirse WHERE eklenmez, tüm kitaplar gelir

        return await sorgu
            .OrderBy(k => k.Baslik)
            .Select(k => new KitapListeDto(k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
            // Select: sadece ihtiyaç duyulan kolonlar SQL'e girer (SELECT * yapmaz)
            // bunu yazmasaydık: tüm kolonlar + navigation property serialization riski
            .ToListAsync(ct);
            // ct (CancellationToken): istek iptal edilirse sorgu da durur
    }
}
