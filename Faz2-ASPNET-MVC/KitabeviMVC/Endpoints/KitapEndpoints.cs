using KitabeviMVC.Models.Dto;
using KitabeviMVC.Models.ViewModels;
using KitabeviMVC.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace KitabeviMVC.Endpoints;

// Gün 23: Minimal API — IEndpointRouteBuilder extension pattern.
//
// Tüm endpoint'ler Program.cs'e yazılsaydı hızla okunaksız olurdu.
// Extension metod → her kaynak kendi dosyasında yaşar,
// Program.cs sadece "app.MapKitapEndpoints()" görür.
//
// TypedResults kullanıyoruz (Results değil) — dönüş tipleri compile-time'da belli,
// Swagger otomatik dokümante eder.
public static class KitapEndpoints
{
    // "this IEndpointRouteBuilder app" → extension metod.
    // Çağrısı: app.MapKitapEndpoints()
    public static IEndpointRouteBuilder MapKitapEndpoints(this IEndpointRouteBuilder app)
    {
        // "MapGroup" → ortak prefix + ortak ayarları bir kez yaz.
        // Bu gruptaki her endpoint "/api/minimal/kitaplar" ile başlar.
        // Controller route'undan ayrı prefix: çakışma olmasın.
        var group = app.MapGroup("/api/minimal/kitaplar")
            .WithTags("Kitaplar (Minimal API)")   // Scalar'da bu başlık altında görünür
            .WithOpenApi()                         // Gün 24: TypedResults → OpenAPI şemasına çevir
            .AddEndpointFilter<ValidationEndpointFilter>(); // grubun tüm endpoint'lerine validation

        group.MapGet("/", Liste)
             .WithSummary("Tüm kitapları listele");

        group.MapGet("/{id:int}", Detay)
             .WithSummary("Tek kitap getir");

        group.MapGet("/kategori", KategoriyeGore)
             .WithSummary("Kategoriye göre filtrele");

        group.MapPost("/", Olustur)
             .WithSummary("Yeni kitap ekle");

        group.MapPut("/{id:int}", Guncelle)
             .WithSummary("Kitap güncelle");

        group.MapDelete("/{id:int}", Sil)
             .WithSummary("Kitap sil");

        return app;
    }

    // ─────────────────────────────────────────────────────────────────
    // GET /api/minimal/kitaplar
    //
    // Dönüş tipi: Ok<IReadOnlyList<KitapResponse>>
    // TypedResults → Swagger bu metodun her zaman 200 döndürdüğünü bilir.
    // ─────────────────────────────────────────────────────────────────
    private static Ok<IEnumerable<KitapResponse>> Liste(IKitapServisi servis)
    {
        var kitaplar = servis.HepsiniGetir()
            .Select(k => new KitapResponse(k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi));

        return TypedResults.Ok(kitaplar);
    }

    // ─────────────────────────────────────────────────────────────────
    // GET /api/minimal/kitaplar/42
    //
    // "Results<Ok<KitapResponse>, NotFound>" → ya 200 ya 404.
    // Swagger iki response da dokümante eder.
    // ─────────────────────────────────────────────────────────────────
    private static Results<Ok<KitapResponse>, NotFound> Detay(int id, IKitapServisi servis)
    {
        var kitap = servis.BulById(id);

        if (kitap is null)
            return TypedResults.NotFound();

        var response = new KitapResponse(kitap.Id, kitap.Baslik, kitap.Yazar, kitap.Fiyat, kitap.Kategori, kitap.StokAdedi);
        return TypedResults.Ok(response);
    }

    // ─────────────────────────────────────────────────────────────────
    // GET /api/minimal/kitaplar/kategori?kategori=Roman
    //
    // "[FromQuery]" → query string'den oku.
    // Minimal API'de attribute olmadan da çalışır ama açık yazmak okunabilirliği artırır.
    // ─────────────────────────────────────────────────────────────────
    private static Results<Ok<IEnumerable<KitapResponse>>, BadRequest<string>> KategoriyeGore(
        [AsParameters] KategoriSorgusu sorgu,
        IKitapServisi servis)
    {
        if (string.IsNullOrWhiteSpace(sorgu.Kategori))
            return TypedResults.BadRequest("Kategori parametresi zorunludur.");

        var kitaplar = servis.KategoriyeGoreGetir(sorgu.Kategori)
            .Select(k => new KitapResponse(k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi));

        return TypedResults.Ok(kitaplar);
    }

    // ─────────────────────────────────────────────────────────────────
    // POST /api/minimal/kitaplar
    //
    // ValidationEndpointFilter → KitapOlusturRequest'teki DataAnnotation'ları doğrular.
    // Hata varsa filter 400 döndürür, bu metoda hiç girilmez.
    // ─────────────────────────────────────────────────────────────────
    private static Results<Created<KitapResponse>, Conflict<object>> Olustur(
        KitapOlusturRequest request,
        IKitapServisi servis)
    {
        if (servis.BaslikVarMi(request.Baslik))
            return TypedResults.Conflict((object)new { hata = $"'{request.Baslik}' başlığı zaten mevcut." });

        var formModel = new KitapFormViewModel
        {
            Baslik    = request.Baslik,
            Yazar     = request.Yazar,
            Fiyat     = request.Fiyat,
            Kategori  = request.Kategori,
            StokAdedi = request.StokAdedi
        };

        var yeniId = servis.Ekle(formModel);

        var response = new KitapResponse(yeniId, request.Baslik, request.Yazar, request.Fiyat, request.Kategori, request.StokAdedi);

        // "TypedResults.Created" → 201 + Location header (/api/minimal/kitaplar/43)
        return TypedResults.Created($"/api/minimal/kitaplar/{yeniId}", response);
    }

    // ─────────────────────────────────────────────────────────────────
    // PUT /api/minimal/kitaplar/42
    // ─────────────────────────────────────────────────────────────────
    private static Results<Ok<KitapResponse>, NotFound, Conflict<object>> Guncelle(
        int id,
        KitapGuncelleRequest request,
        IKitapServisi servis)
    {
        if (servis.BulById(id) is null)
            return TypedResults.NotFound();

        if (servis.BaslikVarMi(request.Baslik, haricId: id))
            return TypedResults.Conflict((object)new { hata = $"'{request.Baslik}' başlığı başka bir kitapta kullanılıyor." });

        var formModel = new KitapFormViewModel
        {
            Id        = id,
            Baslik    = request.Baslik,
            Yazar     = request.Yazar,
            Fiyat     = request.Fiyat,
            Kategori  = request.Kategori,
            StokAdedi = request.StokAdedi
        };

        servis.Guncelle(formModel);

        var response = new KitapResponse(id, request.Baslik, request.Yazar, request.Fiyat, request.Kategori, request.StokAdedi);
        return TypedResults.Ok(response);
    }

    // ─────────────────────────────────────────────────────────────────
    // DELETE /api/minimal/kitaplar/42
    // ─────────────────────────────────────────────────────────────────
    private static Results<NoContent, NotFound> Sil(int id, IKitapServisi servis)
    {
        if (servis.BulById(id) is null)
            return TypedResults.NotFound();

        servis.Sil(id);
        return TypedResults.NoContent();
    }
}

// ─────────────────────────────────────────────────────────────────────
// Query string parametrelerini sınıfa toplamak için yardımcı record.
// "[AsParameters]" → record'un property'lerini ayrı ayrı query param olarak bağlar:
//   /api/minimal/kitaplar/kategori?Kategori=Roman
// ─────────────────────────────────────────────────────────────────────
public record KategoriSorgusu([FromQuery] string? Kategori);
