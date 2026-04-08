using KitabeviMVC.Authorization;
using KitabeviMVC.Filters;
using KitabeviMVC.Models.ViewModels;
using KitabeviMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitabeviMVC.Controllers;

// ─────────────────────────────────────────────────────────────────────
// Gün 29: Tüm action'lar async'e çevrildi.
//
// IKitapServisi artık async metodlar sunuyor (EF Core uyumu).
// Controller action'ları async Task<IActionResult> döndürür;
// ASP.NET Core pipeline bunu otomatik olarak yönetir.
// ─────────────────────────────────────────────────────────────────────
[Route("kitaplar")]
[TypeFilter(typeof(ValidationFilter))]
public class KitapController : Controller
{
    private readonly IKitapServisi _kitapServisi;
    private readonly ILogger<KitapController> _logger;
    private readonly IAuthorizationService _authService;

    public KitapController(
        IKitapServisi kitapServisi,
        ILogger<KitapController> logger,
        IAuthorizationService authService)
    {
        _kitapServisi = kitapServisi;
        _logger = logger;
        _authService = authService;
    }

    // ─────────────────────────────────────────────────────────────
    // GET /kitaplar
    // ─────────────────────────────────────────────────────────────
    [HttpGet("")]
    public async Task<IActionResult> Liste()
    {
        ViewData["Title"] = "Kitaplar";
        var model = await _kitapServisi.HepsiniGetirAsync();
        return View(model);
    }

    // ─────────────────────────────────────────────────────────────
    // GET /kitaplar/detay/42
    // ─────────────────────────────────────────────────────────────
    [HttpGet("detay/{id:int}")]
    public async Task<IActionResult> Detay(int id)
    {
        var model = await _kitapServisi.BulByIdAsync(id);

        if (model is null)
            return NotFound();

        ViewData["Title"] = model.Baslik;
        return View(model);
    }

    // ─────────────────────────────────────────────────────────────
    // GET /kitaplar/ekle → boş formu göster
    // ─────────────────────────────────────────────────────────────
    [Authorize(Policy = "KitapEkleme")]
    [HttpGet("ekle")]
    public IActionResult Ekle()
    {
        ViewData["Title"] = "Yeni Kitap Ekle";
        return View(new KitapFormViewModel());
    }

    // ─────────────────────────────────────────────────────────────
    // POST /kitaplar/ekle → formu kaydet
    // ─────────────────────────────────────────────────────────────
    [Authorize(Policy = "KitapEkleme")]
    [HttpPost("ekle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ekle(KitapFormViewModel model)
    {
        if (await _kitapServisi.BaslikVarMiAsync(model.Baslik))
        {
            ModelState.AddModelError(nameof(model.Baslik), "Bu başlıkta bir kitap zaten mevcut.");
            ViewData["Title"] = "Yeni Kitap Ekle";
            return View(model);
        }

        var yeniId = await _kitapServisi.EkleAsync(model);
        _logger.LogInformation("Kitap eklendi: {Baslik} (ID={Id})", model.Baslik, yeniId);

        TempData["BasariMesaji"] = $"'{model.Baslik}' başarıyla eklendi.";
        return RedirectToAction(nameof(Detay), new { id = yeniId });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /kitaplar/duzenle/42 → dolu formu göster
    // ─────────────────────────────────────────────────────────────
    [Authorize]
    [HttpGet("duzenle/{id:int}")]
    public async Task<IActionResult> Duzenle(int id)
    {
        var model = await _kitapServisi.BulByIdAsync(id);

        if (model is null)
            return NotFound();

        var yetki = await _authService.AuthorizeAsync(User, model, "KitapDuzenleme");
        if (!yetki.Succeeded)
            return Forbid();

        ViewData["Title"] = $"Düzenle: {model.Baslik}";
        return View(model);
    }

    // ─────────────────────────────────────────────────────────────
    // POST /kitaplar/duzenle/42 → değişiklikleri kaydet
    // ─────────────────────────────────────────────────────────────
    [Authorize]
    [HttpPost("duzenle/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duzenle(int id, KitapFormViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        if (await _kitapServisi.BaslikVarMiAsync(model.Baslik, haricId: id))
        {
            ModelState.AddModelError(nameof(model.Baslik), "Bu başlıkta başka bir kitap zaten mevcut.");
            ViewData["Title"] = $"Düzenle: {model.Baslik}";
            return View(model);
        }

        var yetki = await _authService.AuthorizeAsync(User, model, "KitapDuzenleme");
        if (!yetki.Succeeded)
            return Forbid();

        var basarili = await _kitapServisi.GuncelleAsync(model);
        if (!basarili)
            return NotFound();

        _logger.LogInformation("Kitap güncellendi: {Baslik} (ID={Id})", model.Baslik, id);
        TempData["BasariMesaji"] = $"'{model.Baslik}' başarıyla güncellendi.";

        return RedirectToAction(nameof(Detay), new { id });
    }

    // ─────────────────────────────────────────────────────────────
    // POST /kitaplar/sil/42
    // ─────────────────────────────────────────────────────────────
    [Authorize(Policy = "KitapSilme")]
    [HttpPost("sil/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(int id)
    {
        var kitap = await _kitapServisi.BulByIdAsync(id);
        if (kitap is null)
            return NotFound();

        await _kitapServisi.SilAsync(id);
        _logger.LogInformation("Kitap silindi: ID={Id}", id);

        TempData["BasariMesaji"] = $"'{kitap.Baslik}' silindi.";
        return RedirectToAction(nameof(Liste));
    }
}
