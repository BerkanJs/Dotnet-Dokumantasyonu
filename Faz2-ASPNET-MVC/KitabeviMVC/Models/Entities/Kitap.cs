using System.ComponentModel.DataAnnotations;

namespace KitabeviMVC.Models.Entities;

public class Kitap
{
    public int Id { get; set; }
    public string Baslik { get; set; } = string.Empty;
    public string Yazar { get; set; } = string.Empty;
    public decimal Fiyat { get; set; }
    public string Kategori { get; set; } = string.Empty;
    public int StokAdedi { get; set; }
    public DateTime EklemeTarihi { get; set; } = DateTime.UtcNow;

    // Gün 29: Foreign Key — YazarId null olabilir (optional relationship).
    // Mevcut seed verisi YazarId içermiyor; nullable yaparak geriye dönük uyumluluk sağlanır.
    public int? YazarId { get; set; }

    // Navigation property — EF Core bunu YazarId üzerinden JOIN ile çözer.
    // "?" → YazarId null olduğunda bu property de null gelir (optional).
    public Yazar? YazarNavigation { get; set; }

    // ─────────────────────────────────────────────────────────────────────
    // Gün 33: Optimistic Concurrency Token.
    //
    // [Timestamp] → SQL Server'da "rowversion" tipinde kolon oluşturur (8 byte).
    // Her UPDATE işleminde DB bu değeri otomatik değiştirir — manuel set gerekmez.
    //
    // EF Core SaveChanges() çağrısında ürettiği SQL'e şunu ekler:
    //   WHERE Id = @id AND RowVersion = @originalRowVersion
    // @originalRowVersion eşleşmezse → DbUpdateConcurrencyException fırlatır.
    //
    // DİKKAT: InMemory provider bu alanı görmezden gelir — concurrency exception
    // sadece SQL Server (veya gerçek bir DB) ile test edilebilir.
    // ─────────────────────────────────────────────────────────────────────
    [Timestamp]
    public byte[]? RowVersion { get; set; }
    // nullable: InMemory provider'da null kalır; SQL Server'da DB doldurur.
    // null! (non-nullable) yazsaydık InMemory'de uygulama başlarken null-warning alırdık.
}
