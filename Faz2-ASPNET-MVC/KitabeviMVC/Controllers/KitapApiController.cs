using Asp.Versioning;
using KitabeviMVC.Models.Dto;
using KitabeviMVC.Models.ViewModels;
using KitabeviMVC.Services;
using Microsoft.AspNetCore.Mvc;

namespace KitabeviMVC.Controllers;

// Gün 22: REST API — resource odaklı tasarım + HTTP method semantikleri + versioning.
// Gün 29: Tüm servis çağrıları async'e çevrildi (IKitapServisi artık async).
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

    /// <summary>Tüm kitapları listeler.</summary>
    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IEnumerable<KitapResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<KitapResponse>>> Liste()
    {
        var kitaplar = await _kitapServisi.HepsiniGetirAsync();
        var response = kitaplar.Select(k => new KitapResponse(
            k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi));
        return Ok(response);
    }

    /// <summary>Tüm kitapları indirimli fiyatlarıyla listeler (V2).</summary>
    [HttpGet]
    [MapToApiVersion("2.0")]
    [ProducesResponseType<IEnumerable<KitapResponseV2>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<KitapResponseV2>>> ListeV2()
    {
        var kitaplar = await _kitapServisi.HepsiniGetirAsync();
        var response = kitaplar.Select(k => new KitapResponseV2(
            k.Id, k.Baslik, k.Yazar, k.Fiyat,
            IndirimliFiyat: Math.Round(k.Fiyat * 0.90m, 2),
            k.Kategori, k.StokAdedi));
        return Ok(response);
    }

    /// <summary>Belirtilen ID'ye sahip kitabı getirir.</summary>
    /// <param name="id">Kitabın veritabanı ID'si.</param>
    /// <response code="200">Kitap bulundu.</response>
    /// <response code="404">Kitap bulunamadı.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<KitapResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KitapResponse>> Detay(int id)
    {
        var kitap = await _kitapServisi.BulByIdAsync(id);
        if (kitap is null)
            return NotFound();

        var response = new KitapResponse(
            kitap.Id, kitap.Baslik, kitap.Yazar,
            kitap.Fiyat, kitap.Kategori, kitap.StokAdedi);
        return Ok(response);
    }

    /// <summary>Kategoriye göre kitap listeler.</summary>
    [HttpGet("kategori")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IEnumerable<KitapResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<KitapResponse>>> KategoriyeGore([FromQuery] string kategori)
    {
        if (string.IsNullOrWhiteSpace(kategori))
            return BadRequest(new { hata = "Kategori parametresi zorunludur." });

        var kitaplar = await _kitapServisi.KategoriyeGoreGetirAsync(kategori);
        var response = kitaplar.Select(k => new KitapResponse(
            k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi));
        return Ok(response);
    }

    /// <summary>Yeni kitap oluşturur.</summary>
    /// <response code="201">Kitap oluşturuldu.</response>
    /// <response code="400">Validation hatası.</response>
    /// <response code="409">Aynı başlıkta kitap zaten mevcut.</response>
    [HttpPost]
    [ProducesResponseType<KitapResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KitapResponse>> Olustur([FromBody] KitapOlusturRequest request)
    {
        if (await _kitapServisi.BaslikVarMiAsync(request.Baslik))
            return Conflict(new { hata = $"'{request.Baslik}' başlıklı kitap zaten mevcut." });

        var formModel = new KitapFormViewModel
        {
            Baslik    = request.Baslik,
            Yazar     = request.Yazar,
            Fiyat     = request.Fiyat,
            Kategori  = request.Kategori,
            StokAdedi = request.StokAdedi
        };

        var yeniId = await _kitapServisi.EkleAsync(formModel);
        _logger.LogInformation("[API] Kitap oluşturuldu: {Baslik} ID={Id}", request.Baslik, yeniId);

        var response = new KitapResponse(
            yeniId, request.Baslik, request.Yazar,
            request.Fiyat, request.Kategori, request.StokAdedi);
        return CreatedAtAction(nameof(Detay), new { id = yeniId }, response);
    }

    /// <summary>Kitabı tamamen günceller.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<KitapResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KitapResponse>> Guncelle(int id, [FromBody] KitapGuncelleRequest request)
    {
        var mevcutKitap = await _kitapServisi.BulByIdAsync(id);
        if (mevcutKitap is null)
            return NotFound();

        if (await _kitapServisi.BaslikVarMiAsync(request.Baslik, haricId: id))
            return Conflict(new { hata = $"'{request.Baslik}' başlığı başka bir kitapta kullanılıyor." });

        var formModel = new KitapFormViewModel
        {
            Id        = id,
            Baslik    = request.Baslik,
            Yazar     = request.Yazar,
            Fiyat     = request.Fiyat,
            Kategori  = request.Kategori,
            StokAdedi = request.StokAdedi
        };

        await _kitapServisi.GuncelleAsync(formModel);
        _logger.LogInformation("[API] Kitap güncellendi: ID={Id}", id);

        var response = new KitapResponse(
            id, request.Baslik, request.Yazar,
            request.Fiyat, request.Kategori, request.StokAdedi);
        return Ok(response);
    }

    /// <summary>Kitabı siler.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Sil(int id)
    {
        var kitap = await _kitapServisi.BulByIdAsync(id);
        if (kitap is null)
            return NotFound();

        await _kitapServisi.SilAsync(id);
        _logger.LogInformation("[API] Kitap silindi: ID={Id}", id);

        return NoContent();
    }
}
