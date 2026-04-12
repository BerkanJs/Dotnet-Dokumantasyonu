using KitabeviMVC.Data;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMVC.Repositories;

// Gün 34: Generic EF Core repository implementasyonu.
// T : class kısıtı → EF Core DbSet<T>'nin kabul ettiği her entity burada kullanılabilir.
public class EfRepository<T> : IRepository<T> where T : class
{
    protected readonly KitabeviDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public EfRepository(KitabeviDbContext context)
    {
        _context = context;              // DI'dan Scoped olarak gelir — new yazmıyoruz
        _dbSet   = context.Set<T>();     // T'ye karşılık gelen DbSet<T>
                                         // bunu yazmadan her metodda context.Kitaplar gibi
                                         // elle belirtmek zorunda kalırdık
    }

    public async Task<T?> GetByIdAsync(int id)
        => await _dbSet.FindAsync(id);
        // FindAsync: önce Change Tracker'a bakar, bulursa DB'ye gitmez
        // FirstOrDefaultAsync(x => x.Id == id) yazsaydık her seferinde DB'ye giderdi

    public async Task<IList<T>> GetAllAsync()
        => await _dbSet.AsNoTracking().ToListAsync();
        // AsNoTracking: sadece okuyoruz — Change Tracker'a kaydetmek gereksiz bellek harcar

    public async Task AddAsync(T entity)
        => await _dbSet.AddAsync(entity);
        // AddAsync: entity'yi "Added" durumuna alır — SaveAsync çağrılana kadar DB'ye gitmez

    public void Update(T entity)
        => _context.Entry(entity).State = EntityState.Modified;
        // Modified: "tüm property'ler değişti say" → SaveAsync'te UPDATE üretir
        // bunu yazmasaydık entity tracked değilse EF Core değişikliği görmezdi

    public void Delete(T entity)
        => _dbSet.Remove(entity);
        // Remove: "Deleted" durumuna alır — SaveAsync'te DELETE üretir

    public async Task SaveAsync()
        => await _context.SaveChangesAsync();
        // Tüm Add/Update/Delete değişikliklerini tek transaction'da yazar
}
