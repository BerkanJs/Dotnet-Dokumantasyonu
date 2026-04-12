namespace KitabeviMVC.Repositories;

// Gün 34: Unit of Work — birden fazla repository'yi tek DbContext üzerinden yönetir.
// Tüm repository'lerin değişiklikleri SaveAsync() ile tek transaction'da DB'ye gider.
public interface IUnitOfWork : IDisposable
{
    IKitapRepository Kitaplar { get; }
    // İleride: IYazarRepository Yazarlar { get; }

    Task<int> SaveAsync();
    // int: etkilenen satır sayısı — loglama veya doğrulama için kullanılabilir
}
