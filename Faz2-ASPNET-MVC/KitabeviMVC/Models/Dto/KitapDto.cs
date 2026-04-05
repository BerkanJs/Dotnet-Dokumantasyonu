using System.ComponentModel.DataAnnotations;

namespace KitabeviMVC.Models.Dto;

// Gün 22: API için ayrı DTO'lar.
//
// MVC'de KitapFormViewModel kullanıyorduk — form, DataAnnotation, View bağlantısı var.
// API için ayrı DTO'lar: istemciye ne döner, istemciden ne gelir — net ve bağımsız.
// MVC ViewModel değişirse API etkilenmez; API değişirse MVC etkilenmez.

// ── Response DTO — istemciye dönen veri ─────────────────────────────
// "record" → C# 9 ile gelen immutable veri tipi.
// Constructor parametreleri otomatik property olur.
// Eşitlik karşılaştırması değer bazlı çalışır (value equality).
// API response için ideal: oluşturulur, okunur, değiştirilmez.
public record KitapResponse(
    int    Id,
    string Baslik,
    string Yazar,
    decimal Fiyat,
    string Kategori,
    int    StokAdedi
);

// ── V2 Response — yeni alan eklendi, V1 istemciler etkilenmez ───────
// Versioning'in somut örneği: V2'de "IndirimliFiyat" eklendi.
// V1 istemciler hâlâ /api/v1/kitaplar çağırır → KitapResponse alır.
// V2 istemciler /api/v2/kitaplar çağırır → KitapResponseV2 alır.
public record KitapResponseV2(
    int    Id,
    string Baslik,
    string Yazar,
    decimal Fiyat,
    decimal IndirimliFiyat, // yeni alan — V1'de yoktu
    string Kategori,
    int    StokAdedi
);

// ── Create Request — POST /api/v1/kitaplar body'si ──────────────────
// DataAnnotation'lar burada da geçerli — [ApiController] otomatik 400 döner.
public class KitapOlusturRequest
{
    [Required(ErrorMessage = "Başlık zorunludur")]
    [StringLength(200)]
    public string Baslik { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yazar zorunludur")]
    public string Yazar { get; set; } = string.Empty;

    [Range(0, 10000, ErrorMessage = "Fiyat 0-10000 arasında olmalıdır")]
    public decimal Fiyat { get; set; }

    public string Kategori { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int StokAdedi { get; set; }
}

// ── Update Request — PUT /api/v1/kitaplar/{id} body'si ──────────────
// PUT tüm nesneyi günceller — tüm alanlar zorunlu.
// PATCH olsaydı alanlar opsiyonel olurdu (nullable).
public class KitapGuncelleRequest
{
    [Required]
    [StringLength(200)]
    public string Baslik { get; set; } = string.Empty;

    [Required]
    public string Yazar { get; set; } = string.Empty;

    [Range(0, 10000)]
    public decimal Fiyat { get; set; }

    public string Kategori { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int StokAdedi { get; set; }
}
