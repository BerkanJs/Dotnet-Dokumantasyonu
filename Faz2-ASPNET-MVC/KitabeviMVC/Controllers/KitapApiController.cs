using Asp.Versioning;
using KitabeviMVC.Models.Dto;
using KitabeviMVC.Models.ViewModels;
using KitabeviMVC.Services;
using Microsoft.AspNetCore.Mvc;

namespace KitabeviMVC.Controllers;

// Gün 22: REST API — resource odaklı tasarım + HTTP method semantikleri + versioning.
//
// [ApiController] → MVC Controller'dan farklı:
//   - ModelState geçersizse otomatik 400 döner (if (!ModelState.IsValid) yazmana gerek yok)
//   - [FromBody] inference: complex parametreler otomatik body'den okunur
//   - ProblemDetails formatında hata yanıtları
//
// "ControllerBase" → View desteği yok. API controller'lar View döndürmez.
//   Controller (MVC) → View + API
//   ControllerBase   → sadece API
//
// [ApiVersion] → bu controller hangi versiyonları destekliyor?
// [Route] → URL'de "v{version:apiVersion}" → v1, v2 gibi expand edilir.
[ApiController]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/kitaplar")]
public class KitapApiController : ControllerBase
{
    private readonly IKitapServisi _kitapServisi;
    private readonly ILogger<KitapApiController> _logger;

    public KitapApiController(IKitapServisi kitapServisi, ILogger<KitapApiController> logger)
    {
        _kitapServisi = kitapServisi;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    // GET /api/v1/kitaplar
    // GET /api/v2/kitaplar   ← V2: IndirimliFiyat alanı eklendi
    //
    // İdempotent + Safe: Aynı isteği 100 kez göndermek aynı sonucu verir,
    // sunucu durumu değişmez.
    //
    // 200 OK → başarılı, body'de liste var.
    // ─────────────────────────────────────────────────────────────────
    /// <summary>Tüm kitapları listeler.</summary>
    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IEnumerable<KitapResponse>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<KitapResponse>> Liste()
    {
        var kitaplar = _kitapServisi.HepsiniGetir();

        // ViewModel → Response DTO dönüşümü.
        // Servis katmanı API DTO'larını bilmemeli — dönüşüm controller'da.
        var response = kitaplar.Select(k => new KitapResponse(
            k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi));

        return Ok(response); // 200 OK + JSON body
    }

    /// <summary>Tüm kitapları indirimli fiyatlarıyla listeler (V2).</summary>
    [HttpGet]
    [MapToApiVersion("2.0")]
    [ProducesResponseType<IEnumerable<KitapResponseV2>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<KitapResponseV2>> ListeV2()
    {
        var kitaplar = _kitapServisi.HepsiniGetir();

        // V2: IndirimliFiyat hesaplanıyor — %10 indirim
        var response = kitaplar.Select(k => new KitapResponseV2(
            k.Id,
            k.Baslik,
            k.Yazar,
            k.Fiyat,
            IndirimliFiyat: Math.Round(k.Fiyat * 0.90m, 2), // "m" → decimal literal
            k.Kategori,
            k.StokAdedi));

        return Ok(response);
    }

    // ─────────────────────────────────────────────────────────────────
    // GET /api/v1/kitaplar/42
    //
    // 200 OK  → kayıt bulundu, body'de var.
    // 404 Not Found → kayıt yok — body'de ProblemDetails.
    // ─────────────────────────────────────────────────────────────────
    /// <summary>Belirtilen ID'ye sahip kitabı getirir.</summary>
    /// <param name="id">Kitabın veritabanı ID'si.</param>
    /// <response code="200">Kitap bulundu.</response>
    /// <response code="404">Kitap bulunamadı.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<KitapResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<KitapResponse> Detay(int id)
    {
        var kitap = _kitapServisi.BulById(id);

        if (kitap is null)
            return NotFound(); // 404 — [ApiController] bunu ProblemDetails'e çevirir

        var response = new KitapResponse(
            kitap.Id, kitap.Baslik, kitap.Yazar,
            kitap.Fiyat, kitap.Kategori, kitap.StokAdedi);

        return Ok(response); // 200
    }

    // ─────────────────────────────────────────────────────────────────
    // GET /api/v1/kitaplar?kategori=Roman
    //
    // Query string ile filtreleme — ayrı endpoint değil, aynı Liste endpoint'i.
    // REST: filtre query param olur, URL değişmez.
    // ─────────────────────────────────────────────────────────────────
    /// <summary>Kategoriye göre kitap listeler.</summary>
    /// <param name="kategori">Filtre uygulanacak kategori adı.</param>
    [HttpGet("kategori")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IEnumerable<KitapResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyList<KitapResponse>> KategoriyeGore([FromQuery] string kategori)
    {
        if (string.IsNullOrWhiteSpace(kategori))
            return BadRequest(new { hata = "Kategori parametresi zorunludur." }); // 400

        var kitaplar = _kitapServisi.KategoriyeGoreGetir(kategori);

        var response = kitaplar.Select(k => new KitapResponse(
            k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi));

        return Ok(response); // 200 — boş liste de 200, null değil
    }

    // ─────────────────────────────────────────────────────────────────
    // POST /api/v1/kitaplar
    //
    // Yeni kayıt oluştur.
    // İdempotent DEĞİL: aynı isteği 2 kez gönderirsen 2 kayıt olur.
    //
    // 201 Created → başarılı, yeni kayıt oluştu.
    //   Location header: yeni kaynağın URL'i → /api/v1/kitaplar/43
    // 400 Bad Request → validation hatası ([ApiController] otomatik döner).
    // 409 Conflict → aynı başlık zaten var.
    // ─────────────────────────────────────────────────────────────────
    /// <summary>Yeni kitap oluşturur.</summary>
    /// <response code="201">Kitap oluşturuldu. Location header'da URL var.</response>
    /// <response code="400">Validation hatası.</response>
    /// <response code="409">Aynı başlıkta kitap zaten mevcut.</response>
    [HttpPost]
    [ProducesResponseType<KitapResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<KitapResponse> Olustur([FromBody] KitapOlusturRequest request)
    {
        // [ApiController] sayesinde ModelState kontrolüne gerek yok —
        // validation başarısız olursa buraya hiç gelmez, otomatik 400 döner.

        // İş kuralı: başlık benzersiz olmalı — bu DataAnnotation ile yapılamaz
        if (_kitapServisi.BaslikVarMi(request.Baslik))
        {
            // 409 Conflict → "bu kaynak zaten var" anlamı
            return Conflict(new { hata = $"'{request.Baslik}' başlıklı kitap zaten mevcut." });
        }

        // Servis ViewModel bekliyor — DTO'dan ViewModel'e dönüşüm
        var formModel = new KitapFormViewModel
        {
            Baslik    = request.Baslik,
            Yazar     = request.Yazar,
            Fiyat     = request.Fiyat,
            Kategori  = request.Kategori,
            StokAdedi = request.StokAdedi
        };

        var yeniId = _kitapServisi.Ekle(formModel);
        _logger.LogInformation("[API] Kitap oluşturuldu: {Baslik} ID={Id}", request.Baslik, yeniId);

        var response = new KitapResponse(
            yeniId, request.Baslik, request.Yazar,
            request.Fiyat, request.Kategori, request.StokAdedi);

        // 201 Created + Location header.
        // "CreatedAtAction" → action adı + route değerleri → URL üretir.
        // Örn: Location: /api/v1/kitaplar/43
        return CreatedAtAction(nameof(Detay), new { id = yeniId }, response);
    }

    // ─────────────────────────────────────────────────────────────────
    // PUT /api/v1/kitaplar/42
    //
    // Kaynağı tamamen güncelle — tüm alanlar gönderilmeli.
    // İdempotent: aynı isteği 10 kez göndermek aynı sonucu verir.
    //
    // 200 OK        → güncellendi, body'de güncel kayıt.
    // 400 Bad Request → validation hatası.
    // 404 Not Found   → kayıt yok.
    // 409 Conflict    → başlık başka kayıtta var.
    // ─────────────────────────────────────────────────────────────────
    /// <summary>Kitabı tamamen günceller (tüm alanlar gönderilmeli).</summary>
    /// <response code="200">Güncellendi.</response>
    /// <response code="400">Validation hatası.</response>
    /// <response code="404">Kitap bulunamadı.</response>
    /// <response code="409">Başlık çakışması.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<KitapResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<KitapResponse> Guncelle(int id, [FromBody] KitapGuncelleRequest request)
    {
        var mevcutKitap = _kitapServisi.BulById(id);
        if (mevcutKitap is null)
            return NotFound(); // 404

        if (_kitapServisi.BaslikVarMi(request.Baslik, haricId: id))
            return Conflict(new { hata = $"'{request.Baslik}' başlığı başka bir kitapta kullanılıyor." }); // 409

        var formModel = new KitapFormViewModel
        {
            Id        = id,
            Baslik    = request.Baslik,
            Yazar     = request.Yazar,
            Fiyat     = request.Fiyat,
            Kategori  = request.Kategori,
            StokAdedi = request.StokAdedi
        };

        _kitapServisi.Guncelle(formModel);
        _logger.LogInformation("[API] Kitap güncellendi: ID={Id}", id);

        var response = new KitapResponse(
            id, request.Baslik, request.Yazar,
            request.Fiyat, request.Kategori, request.StokAdedi);

        return Ok(response); // 200 + güncel kayıt
    }

    // ─────────────────────────────────────────────────────────────────
    // DELETE /api/v1/kitaplar/42
    //
    // İdempotent: kayıt yoksa da sonuç aynı — "kayıt yok".
    //
    // 204 No Content → silindi, body yok.
    // 404 Not Found  → zaten yoktu.
    // ─────────────────────────────────────────────────────────────────
    /// <summary>Kitabı siler.</summary>
    /// <response code="204">Silindi. Body yok.</response>
    /// <response code="404">Kitap bulunamadı.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Sil(int id)
    {
        var kitap = _kitapServisi.BulById(id);
        if (kitap is null)
            return NotFound(); // 404

        _kitapServisi.Sil(id);
        _logger.LogInformation("[API] Kitap silindi: ID={Id}", id);

        // 204 No Content — silinen kaydı body'de döndürme.
        // "NoContent()" → HTTP 204, body yok.
        return NoContent();
    }
}
