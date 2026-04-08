namespace KitabeviMVC.Models.Entities;

// Gün 29: Yazar entity — Kitap ile one-to-many ilişki.
// Bir yazarın birden fazla kitabı olabilir.
// DbContext.OnModelCreating()'de ilişki Fluent API ile tanımlandı.
public class Yazar
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string Soyad { get; set; } = string.Empty;
    public string? Biyografi { get; set; }

    // Navigation property — EF Core bu koleksiyon üzerinden JOIN üretir.
    // "virtual" → Lazy Loading için (bu projede kullanmıyoruz, ama standart).
    public ICollection<Kitap> Kitaplar { get; set; } = [];
}
