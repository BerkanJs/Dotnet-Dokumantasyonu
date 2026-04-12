using KitabeviMVC.Authorization;
using KitabeviMVC.Filters;
using KitabeviMVC.Models.ViewModels;
using KitabeviMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMVC.Controllers;

// ─────────────────────────────────────────────────────────────────────
// Gün 29: Tüm action'lar async'e çevrildi.
// Gün 30: IKitapSorguServisi inject edildi — Include + Projection kullanan
//         DetayYazarlaGetirAsync ve AyniKategoridekilerAsync metodları.
// Gün 31: N+1 korumalı YazarlariyleHepsiniGetirAsync ve compiled query
//         kullanan HizliDetayGetirAsync action'ları eklendi.
// Gün 33: IKitapBatchServisi inject edildi — ExecuteUpdate/Delete action'ları,
//         Duzenle POST'a DbUpdateConcurrencyException yakalama eklendi.
// ─────────────────────────────────────────────────────────────────────
[Route("kitaplar")]
[TypeFilter(typeof(ValidationFilter))]
public class KitapController : Controller
{
    private readonly IKitapServisi _kitapServisi;
    private readonly IKitapSorguServisi _sorguServisi;
    private readonly IKitapBatchServisi _batchServisi;
    private readonly ILogger<KitapController> _logger;
    private readonly IAuthorizationService _authService;

    public KitapController(
        IKitapServisi kitapServisi,
        IKitapSorguServisi sorguServisi,
        IKitapBatchServisi batchServisi,
        ILogger<KitapController> logger,
        IAuthorizationService authService)
    {
        _kitapServisi = kitapServisi;
        _sorguServisi = sorguServisi;
        _batchServisi = batchServisi;
        // batchServisi: IKitapBatchServisi → EfKitapServisi (ExecuteUpdate/Delete için)
        // bunu inject etmeseyik: ExecuteUpdateAsync özelliğini hiç kullanamaz,
        // Controller'da direkt _context inject etmek zorunda kalırdık (kötü pratik)
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
    //
    // Gün 30: DetayYazarlaGetirAsync — Include + Projection tek sorguda.
    // AyniKategoridekilerAsync — IQueryable zinciri, ikinci sorgu.
    //
    // Toplam: 2 SQL (N+1 değil — öngörülebilir, sabit sayı)
    // ─────────────────────────────────────────────────────────────
    [HttpGet("detay/{id:int}")]
    public async Task<IActionResult> Detay(int id)
    {
        // Sorgu 1: Kitap + Yazar (LEFT JOIN), sadece gerekli kolonlar (Projection)
        var model = await _sorguServisi.DetayYazarlaGetirAsync(id);

        if (model is null)
            return NotFound();

        // Sorgu 2: Aynı kategoriden öneriler (IQueryable dinamik filtre)
        model.KategoriOneriler = await _sorguServisi.AyniKategoridekilerAsync(
            model.Kategori, haricId: id, limit: 4);

        ViewData["Title"] = model.Baslik;
        return View(model);
    }

    // ─────────────────────────────────────────────────────────────
    // GET /kitaplar/ara?q=clean
    //
    // Gün 30: EF.Functions.Like ile SQL LIKE araması.
    // ─────────────────────────────────────────────────────────────
    [HttpGet("ara")]
    public async Task<IActionResult> Ara([FromQuery] string? q)
    {
        ViewData["Title"] = $"Arama: {q}";
        ViewData["AramaMetni"] = q;

        var sonuclar = string.IsNullOrWhiteSpace(q)
            ? []
            : await _sorguServisi.AraAsync(q);

        return View("Liste", sonuclar);
    }

    // ─────────────────────────────────────────────────────────────
    // GET /kitaplar/filtrele?minFiyat=50&maxFiyat=150&kategori=Roman
    //
    // Gün 30: Dinamik IQueryable zinciri — sadece verilen parametreler SQL'e girer.
    // ─────────────────────────────────────────────────────────────
    [HttpGet("filtrele")]
    public async Task<IActionResult> Filtrele(
        [FromQuery] decimal? minFiyat,
        [FromQuery] decimal? maxFiyat,
        [FromQuery] string? kategori)
    {
        ViewData["Title"] = "Filtrele";
        var sonuclar = await _sorguServisi.FiyatAraligiGetirAsync(minFiyat, maxFiyat, kategori);
        return View("Liste", sonuclar);
    }

    // ─────────────────────────────────────────────────────────────
    // GET /kitaplar/yazarli
    //
    // Gün 31: YazarlariyleHepsiniGetirAsync — N+1 olmadan yazar bilgisi.
    //
    // /kitaplar (Liste) ile farkı:
    //   Liste    → Yazar alanı entity'deki string'den gelir (Kitap.Yazar)
    //   Yazarli  → Yazar alanı Yazarlar tablosuyla JOIN'den gelir (tek SQL)
    //
    // Bu action N+1'in nasıl önlendiğini gösterir:
    //   YANLIŞ (N+1): foreach içinde kitap.YazarNavigation.Ad → her iterasyonda SQL
    //   DOĞRU  (bu): Include → tek JOIN → tüm yazarlar tek sorguda
    // ─────────────────────────────────────────────────────────────
    [HttpGet("yazarli")]
    public async Task<IActionResult> YazarliListe()
    {
        ViewData["Title"] = "Kitaplar (Yazar Bilgisiyle)";

        // YazarlariyleHepsiniGetirAsync → tek SQL: kitap + yazar JOIN
        // bunu _kitapServisi.HepsiniGetirAsync() ile yapsaydık
        // YazarNavigation null gelirdi, yazar adı Kitap.Yazar string'inden okunurdu
        var model = await _sorguServisi.YazarlariyleHepsiniGetirAsync();

        return View("Liste", model);
        // "Liste" view'ını yeniden kullandık — KitapListeViewModel döndürdüğümüz için
        // ayrı bir view yazmaya gerek yok
    }

    // ─────────────────────────────────────────────────────────────
    // GET /kitaplar/hizli-detay/42
    //
    // Gün 31: HizliDetayGetirAsync — compiled query ile ID araması.
    //
    // /kitaplar/detay/42 ile farkı:
    //   detay      → DetayYazarlaGetirAsync → Include (LEFT JOIN) kullanır
    //   hizli-detay → HizliDetayGetirAsync  → compiled query, JOIN yok
    //
    // Ne zaman hizli-detay tercih edilir?
    //   → YazarNavigation verisi gerekmediğinde
    //   → Saniyelerce yüzlerce kez çağrılan endpoint'lerde (hot-path)
    //   → Include'un JOIN maliyetini kabul etmek istemediğinde
    // ─────────────────────────────────────────────────────────────
    [HttpGet("hizli-detay/{id:int}")]
    public async Task<IActionResult> HizliDetay(int id)
    {
        // Compiled query: translation overhead yok, doğrudan SQL parametresi bağlanır
        // bunu DetayYazarlaGetirAsync ile yapsaydık her çağrıda Include JOIN overhead'i olurdu
        var model = await _sorguServisi.HizliDetayGetirAsync(id);

        if (model is null)
            return NotFound();
        // NotFound() → 404 HTTP yanıtı; bunu yazmasaydık model null'ken View(null) çağrılırdı
        // → view içinde NullReferenceException atılırdı

        ViewData["Title"] = model.Baslik;
        return View("Detay", model);
        // mevcut Detay view'ını yeniden kullandık — KitapDetayViewModel döndürdüğümüz için
        // ayrı view gerekmez; sadece YazarAdi alanı Yazar'dan gelir (JOIN yok)
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
    // ─────────────────────────────────────────────────────────────────────
    // Gün 33: Duzenle POST — DbUpdateConcurrencyException yakalama eklendi.
    //
    // View'da RowVersion için hidden input ZORUNLU:
    //   <input type="hidden" asp-for="RowVersion" />
    //   (veya @Html.HiddenFor(m => m.RowVersion))
    //
    // RowVersion byte[] olduğu için model binding base64 string olarak alır.
    // ASP.NET Core built-in binder bunu otomatik çözer — ek kod gerekmez.
    // ─────────────────────────────────────────────────────────────────────
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

        try
        {
            var basarili = await _kitapServisi.GuncelleAsync(model);
            // GuncelleAsync: model.RowVersion null değilse WHERE RowVersion = @original ekler
            // SQL Server'da eşleşmezse → DbUpdateConcurrencyException (aşağıda yakalanır)
            // InMemory'de: her zaman başarılı (concurrency kontrolü yok)

            if (!basarili)
                return NotFound();

            _logger.LogInformation("Kitap güncellendi: {Baslik} (ID={Id})", model.Baslik, id);
            TempData["BasariMesaji"] = $"'{model.Baslik}' başarıyla güncellendi.";
            return RedirectToAction(nameof(Detay), new { id });
        }
        catch (DbUpdateConcurrencyException ex)
        // ── Gün 33: Optimistic Concurrency çakışması ──────────────────────
        // Bu arada başka bir kullanıcı veya istek aynı kaydı değiştirdi.
        // ex.Entries: hangi entity'lerin çakıştığı
        // ─────────────────────────────────────────────────────────────────
        {
            _logger.LogWarning(
                "Concurrency çakışması: Kitap Id={Id}, Kullanici={User}",
                id, User.Identity?.Name);

            var entry = ex.Entries.FirstOrDefault();
            // entry: çakışan entity'nin Change Tracker kaydı

            if (entry is not null)
            {
                var dbDegerler = await entry.GetDatabaseValuesAsync();
                // GetDatabaseValuesAsync: DB'deki güncel değerleri çek
                // bunu çağırmasaydık kullanıcıya "ne değişti?" gösteremezdik

                if (dbDegerler is null)
                {
                    // Bu arada başka biri silmiş
                    ModelState.AddModelError("",
                        "Kayıt başka bir kullanıcı tarafından silindi. Düzenleme yapılamaz.");
                }
                else
                {
                    // DB'deki güncel RowVersion'ı model'e aktar
                    // Kullanıcı tekrar "Kaydet"e basarsa yeni RowVersion ile denenir
                    var dbKitap = dbDegerler.ToObject() as KitabeviMVC.Models.Entities.Kitap;
                    model.RowVersion = dbKitap?.RowVersion;
                    // bunu yapmasaydık: kullanıcı tekrar form gönderdiğinde
                    // yine eski RowVersion gönderir → sonsuz çakışma döngüsü

                    ModelState.AddModelError("",
                        "Kayıt bu arada başka bir kullanıcı tarafından değiştirildi. " +
                        "Değişikliklerinizi gözden geçirip tekrar kaydedin.");
                    // kullanıcı formu görür, uyarıyı okur, tekrar gönderir → başarılı
                }
            }

            ViewData["Title"] = $"Düzenle: {model.Baslik}";
            return View(model);
            // aynı view: kullanıcı çakışma mesajını görür, formunu düzeltir
        }
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

    // ═════════════════════════════════════════════════════════════════════
    // GÜN 33: Batch İşlem Action'ları — ExecuteUpdate / ExecuteDelete
    //
    // Bu action'lar yönetici işlemleri olduğu için [Authorize(Policy="KitapSilme")]
    // ile korunuyor (en kısıtlayıcı policy: sadece Admin).
    // ═════════════════════════════════════════════════════════════════════

    // ─────────────────────────────────────────────────────────────────────
    // POST /kitaplar/stok-sifirla
    // Form: <input name="kategori" />
    //
    // ExecuteUpdateAsync ile tek SQL: tüm kategori stoğu sıfırlanır.
    // Change Tracker'a girmez, SaveChanges() çağrılmaz.
    // ─────────────────────────────────────────────────────────────────────
    [Authorize(Policy = "KitapSilme")]
    [HttpPost("stok-sifirla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StokSifirla([FromForm] string kategori)
    {
        if (string.IsNullOrWhiteSpace(kategori))
        {
            TempData["HataMesaji"] = "Kategori boş olamaz.";
            return RedirectToAction(nameof(Liste));
        }

        var etkilenen = await _batchServisi.KategoriStokSifirlaAsync(kategori);
        // tek SQL: UPDATE Kitaplar SET StokAdedi=0 WHERE Kategori=@kategori
        // bunu _kitapServisi ile yapsaydık: ToListAsync → foreach → SaveChanges → N sorgu

        TempData["BasariMesaji"] = etkilenen > 0
            ? $"'{kategori}' kategorisinde {etkilenen} kitabın stoğu sıfırlandı."
            : $"'{kategori}' kategorisinde güncellenecek kitap bulunamadı.";

        return RedirectToAction(nameof(Liste));
    }

    // ─────────────────────────────────────────────────────────────────────
    // POST /kitaplar/fiyat-artis
    // Form: <input name="kategori" /> <input name="artisYuzdesi" />
    //
    // ExecuteUpdateAsync ile hesaplanmış değer:
    //   SET Fiyat = Fiyat * (1 + @artisOrani)
    // ─────────────────────────────────────────────────────────────────────
    [Authorize(Policy = "KitapSilme")]
    [HttpPost("fiyat-artis")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TopluFiyatArtis(
        [FromForm] string kategori,
        [FromForm] decimal artisYuzdesi)
    {
        if (string.IsNullOrWhiteSpace(kategori) || artisYuzdesi <= 0)
        {
            TempData["HataMesaji"] = "Geçerli kategori ve artış yüzdesi giriniz.";
            return RedirectToAction(nameof(Liste));
        }

        var artisOrani = artisYuzdesi / 100m;
        // artisYuzdesi=10 → artisOrani=0.10 → Fiyat * 1.10 → %10 artış

        try
        {
            var etkilenen = await _batchServisi.TopluFiyatArttirAsync(kategori, artisOrani);

            TempData["BasariMesaji"] = etkilenen > 0
                ? $"'{kategori}' kategorisinde {etkilenen} kitaba %{artisYuzdesi} fiyat artışı uygulandı."
                : $"'{kategori}' kategorisinde stoklu kitap bulunamadı.";
        }
        catch (ArgumentOutOfRangeException)
        {
            TempData["HataMesaji"] = "Artış oranı 0 ile 500% arasında olmalıdır.";
        }

        return RedirectToAction(nameof(Liste));
    }

    // ─────────────────────────────────────────────────────────────────────
    // POST /kitaplar/eski-temizle
    // Form: <input name="yilEsigi" /> (varsayılan: 2)
    //
    // ExecuteDeleteAsync: stok=0 ve N yıldan önce eklenen kitapları sil.
    // GERİ ALINAMAZ — production'da onay adımı zorunlu.
    // ─────────────────────────────────────────────────────────────────────
    [Authorize(Policy = "KitapSilme")]
    [HttpPost("eski-temizle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EskiTemizle([FromForm] int yilEsigi = 2)
    {
        if (yilEsigi < 1)
        {
            TempData["HataMesaji"] = "Yıl eşiği en az 1 olmalıdır.";
            return RedirectToAction(nameof(Liste));
        }

        var silinen = await _batchServisi.EskiStoksuzlariSilAsync(yilEsigi);
        // tek SQL DELETE: stok=0 AND eklemeTarihi < @sinir
        // bunu _kitapServisi.SilAsync döngüsüyle yapsaydık:
        // N adet FindAsync + Remove + SaveChanges → N+1 silme pattern'i

        TempData["BasariMesaji"] = silinen > 0
            ? $"{silinen} adet eski stoksuz kitap kalıcı olarak silindi."
            : $"Silinecek eski stoksuz kitap bulunamadı ({yilEsigi} yıl eşiği).";

        return RedirectToAction(nameof(Liste));
    }
}
