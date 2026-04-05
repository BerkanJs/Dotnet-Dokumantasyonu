using KitabeviMVC.Authorization;
using KitabeviMVC.Filters;
using KitabeviMVC.Models.ViewModels;
using KitabeviMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitabeviMVC.Controllers;

// ─────────────────────────────────────────────────────────────────────
// Gün 19: [TypeFilter] — Controller seviyesi filter.
//
// [TypeFilter(typeof(ValidationFilter))] → bu controller'daki tüm
// action'larda ValidationFilter çalışır.
//
// TypeFilter: filter'ı DI container üzerinden oluşturur.
// "typeof(ValidationFilter)" → filter'ın tip bilgisi, "new" demiyoruz.
//
// Farkı global filter'dan: sadece bu controller etkilenir.
// HomeController'daki action'larda ValidationFilter çalışmaz.
// ─────────────────────────────────────────────────────────────────────
[Route("kitaplar")]
[TypeFilter(typeof(ValidationFilter))]
public class KitapController : Controller
{
    private readonly IKitapServisi _kitapServisi;
    private readonly ILogger<KitapController> _logger;

    // Gün 20: IAuthorizationService → resource-based authorization için.
    // [Authorize] attribute ile yapılamayan "bu kaydın sahibi mi?" kontrolünü yapar.
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
    //
    // return View(model) → Views/Kitap/Liste.cshtml dosyasını
    // model ile birlikte render eder.
    //
    // ViewData["Title"] → _Layout.cshtml'deki <title> etiketine gider.
    // ─────────────────────────────────────────────────────────────
    [HttpGet("")]
    public IActionResult Liste()
    {
        ViewData["Title"] = "Kitaplar";
        var model = _kitapServisi.HepsiniGetir();
        return View(model);
    }

    // ─────────────────────────────────────────────────────────────
    // GET /kitaplar/detay/42
    //
    // Kayıt bulunamazsa NotFound() → 404 döner.
    // Burada return type ActionResult<T> yerine IActionResult —
    // çünkü View döndürüyoruz, tip bilgisi Swagger'a gerek yok.
    // ─────────────────────────────────────────────────────────────
    [HttpGet("detay/{id:int}")]
    public IActionResult Detay(int id)
    {
        var model = _kitapServisi.BulById(id);

        if (model is null)
            return NotFound();

        ViewData["Title"] = model.Baslik;
        return View(model);
    }

    // ─────────────────────────────────────────────────────────────
    // GET /kitaplar/ekle → boş formu göster
    //
    // Gün 20: [Authorize(Policy = "KitapEkleme")] → Admin veya Editor rolü gerekli.
    // Giriş yapılmamışsa /hesap/giris?ReturnUrl=/kitaplar/ekle'ye yönlendirilir.
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
    //
    // PRG Pattern (Post-Redirect-Get):
    //   - Başarılı → RedirectToAction (302)
    //   - Hatalı   → return View(model) (ModelState hataları view'a gider)
    //
    // Sayfayı yenileme → POST değil, GET tekrar eder. Çifte kayıt olmaz.
    //
    // [ValidateAntiForgeryToken] → CSRF koruması.
    // View'daki form otomatik gizli token üretir, burada doğrulanır.
    // ─────────────────────────────────────────────────────────────
    [Authorize(Policy = "KitapEkleme")]
    [HttpPost("ekle")]
    [ValidateAntiForgeryToken]
    public IActionResult Ekle(KitapFormViewModel model)
    {
        // ModelState.IsValid kontrolü burada yok — ValidationFilter halletti.
        // Action'a geldiysek model geçerli demektir.

        // Attribute ile yapılamayan iş kuralı kontrolü — DB'ye gidecek
        if (_kitapServisi.BaslikVarMi(model.Baslik))
        {
            // "Baslik" → hangi form alanına ait hata olduğunu belirtir
            ModelState.AddModelError(nameof(model.Baslik), "Bu başlıkta bir kitap zaten mevcut.");
            ViewData["Title"] = "Yeni Kitap Ekle";
            return View(model);
        }

        var yeniId = _kitapServisi.Ekle(model);
        _logger.LogInformation("Kitap eklendi: {Baslik} (ID={Id})", model.Baslik, yeniId);

        // TempData: redirect'ten sonraki ilk request'te okunur, sonra silinir.
        // ViewData burada işe yaramaz — redirect yeni bir request başlatır.
        TempData["BasariMesaji"] = $"'{model.Baslik}' başarıyla eklendi.";

        // PRG: POST bitti → GET'e yönlendir
        return RedirectToAction(nameof(Detay), new { id = yeniId });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /kitaplar/duzenle/42 → dolu formu göster
    //
    // Gün 20: Resource-based authorization.
    // Kitabı kim ekledi? Sadece o veya Admin düzenleyebilir.
    // [Authorize] attribute yetmez — kaydı DB'den çekmeden bilemeyiz.
    // ─────────────────────────────────────────────────────────────
    [Authorize] // en az giriş yapmış olmak gerekli — handler daha ince kontrolü yapar
    [HttpGet("duzenle/{id:int}")]
    public async Task<IActionResult> Duzenle(int id)
    {
        var model = _kitapServisi.BulById(id);

        if (model is null)
            return NotFound();

        // Resource-based yetki kontrolü: bu kitabı düzenleme hakkı var mı?
        // "KitapDuzenleme" policy → KitapDuzenlemeHandler çalışır.
        var yetki = await _authService.AuthorizeAsync(User, model, "KitapDuzenleme");

        if (!yetki.Succeeded)
            return Forbid(); // 403 — giriş yapmış ama bu kitaba yetkisi yok

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
        // Route'daki id ile form body'sindeki model.Id uyuşmalı
        if (id != model.Id)
            return BadRequest();

        // ModelState.IsValid kontrolü burada yok — ValidationFilter halletti.

        if (_kitapServisi.BaslikVarMi(model.Baslik, haricId: id))
        {
            ModelState.AddModelError(nameof(model.Baslik), "Bu başlıkta başka bir kitap zaten mevcut.");
            ViewData["Title"] = $"Düzenle: {model.Baslik}";
            return View(model);
        }

        // POST'ta da yetki kontrolü — GET'ten geçmiş olmak yeterli değil
        var yetki = await _authService.AuthorizeAsync(User, model, "KitapDuzenleme");
        if (!yetki.Succeeded)
            return Forbid();

        var basarili = _kitapServisi.Guncelle(model);

        if (!basarili)
            return NotFound();

        _logger.LogInformation("Kitap güncellendi: {Baslik} (ID={Id})", model.Baslik, id);
        TempData["BasariMesaji"] = $"'{model.Baslik}' başarıyla güncellendi.";

        return RedirectToAction(nameof(Detay), new { id });
    }

    // ─────────────────────────────────────────────────────────────
    // POST /kitaplar/sil/42
    //
    // Silme işlemi GET ile yapılmamalı — bot veya prefetch tarayıcılar
    // GET isteklerini otomatik tetikleyebilir. POST zorunlu.
    //
    // Gün 20: Sadece Admin silebilir — "KitapSilme" policy.
    // ─────────────────────────────────────────────────────────────
    [Authorize(Policy = "KitapSilme")]
    [HttpPost("sil/{id:int}")]
    [ValidateAntiForgeryToken]
    public IActionResult Sil(int id)
    {
        var kitap = _kitapServisi.BulById(id);

        if (kitap is null)
            return NotFound();

        _kitapServisi.Sil(id);
        _logger.LogInformation("Kitap silindi: ID={Id}", id);

        TempData["BasariMesaji"] = $"'{kitap.Baslik}' silindi.";
        return RedirectToAction(nameof(Liste));
    }
}
