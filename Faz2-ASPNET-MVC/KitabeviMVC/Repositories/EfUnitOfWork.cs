using KitabeviMVC.Data;

namespace KitabeviMVC.Repositories;

// Gün 34: Unit of Work implementasyonu.
// Tek KitabeviDbContext tüm repository'lere paylaşılır → aynı transaction garantisi.
public class EfUnitOfWork : IUnitOfWork
{
    private readonly KitabeviDbContext _context;
    private IKitapRepository? _kitaplar;

    public EfUnitOfWork(KitabeviDbContext context)
    {
        _context = context;
        // context DI'dan Scoped olarak gelir
        // Scoped: request başında bir instance, request sonunda dispose edilir
        // Singleton yapılsaydı: tüm request'ler aynı context → thread-safety sorunu
    }

    public IKitapRepository Kitaplar
        => _kitaplar ??= new EfKitapRepository(_context);
        // ??= (null-coalescing assignment): null ise oluştur ve ata, değilse mevcut nesneyi döndür
        // Lazy init: repository sadece kullanılırsa oluşturulur
        // Her erişimde new EfKitapRepository() yazsaydık: yeni nesne ama aynı context (gereksiz alloc)

    public async Task<int> SaveAsync()
        => await _context.SaveChangesAsync();
        // Tüm repository'lerdeki Add/Update/Delete'leri tek çağrıyla DB'ye yazar
        // Her repository kendi SaveAsync'ini çağırsaydık: ayrı transaction → tutarsız veri riski

    public void Dispose()
        => _context.Dispose();
        // DI zaten Scoped context'i request sonunda dispose eder
        // Bu ekstra güvence: IUnitOfWork using bloğunda kullanılırsa context erken temizlenir
}
