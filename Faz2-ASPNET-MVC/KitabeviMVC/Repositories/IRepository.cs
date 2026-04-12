namespace KitabeviMVC.Repositories;

// Gün 34: Generic Repository arayüzü — tüm entity'lerin ortak CRUD operasyonları.
// Controller bu arayüzü görür; EF Core mı Dapper mı bilmez.
public interface IRepository<T> where T : class
{
    Task<T?>         GetByIdAsync(int id);
    Task<IList<T>>   GetAllAsync();
    Task             AddAsync(T entity);
    void             Update(T entity);    // EF Core'da update senkron — tracking zaten var
    void             Delete(T entity);
    Task             SaveAsync();
}
