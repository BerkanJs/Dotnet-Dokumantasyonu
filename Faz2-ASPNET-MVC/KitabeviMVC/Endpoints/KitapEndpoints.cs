using KitabeviMVC.Models.Dto;
using KitabeviMVC.Models.ViewModels;
using KitabeviMVC.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace KitabeviMVC.Endpoints;

// Gün 23: Minimal API — IEndpointRouteBuilder extension pattern.
// Gün 29: Servis çağrıları async'e çevrildi.
public static class KitapEndpoints
{
    public static IEndpointRouteBuilder MapKitapEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/minimal/kitaplar")
            .WithTags("Kitaplar (Minimal API)")
            .WithOpenApi()
            .AddEndpointFilter<ValidationEndpointFilter>();

        group.MapGet("/", Liste).WithSummary("Tüm kitapları listele");
        group.MapGet("/{id:int}", Detay).WithSummary("Tek kitap getir");
        group.MapGet("/kategori", KategoriyeGore).WithSummary("Kategoriye göre filtrele");
        group.MapPost("/", Olustur).WithSummary("Yeni kitap ekle");
        group.MapPut("/{id:int}", Guncelle).WithSummary("Kitap güncelle");
        group.MapDelete("/{id:int}", Sil).WithSummary("Kitap sil");

        return app;
    }

    private static async Task<Ok<IEnumerable<KitapResponse>>> Liste(IKitapServisi servis)
    {
        var kitaplar = await servis.HepsiniGetirAsync();
        var response = kitaplar.Select(k =>
            new KitapResponse(k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi));
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<KitapResponse>, NotFound>> Detay(int id, IKitapServisi servis)
    {
        var kitap = await servis.BulByIdAsync(id);
        if (kitap is null)
            return TypedResults.NotFound();

        var response = new KitapResponse(
            kitap.Id, kitap.Baslik, kitap.Yazar, kitap.Fiyat, kitap.Kategori, kitap.StokAdedi);
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<IEnumerable<KitapResponse>>, BadRequest<string>>> KategoriyeGore(
        [AsParameters] KategoriSorgusu sorgu,
        IKitapServisi servis)
    {
        if (string.IsNullOrWhiteSpace(sorgu.Kategori))
            return TypedResults.BadRequest("Kategori parametresi zorunludur.");

        var kitaplar = await servis.KategoriyeGoreGetirAsync(sorgu.Kategori);
        var response = kitaplar.Select(k =>
            new KitapResponse(k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi));
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Created<KitapResponse>, Conflict<object>>> Olustur(
        KitapOlusturRequest request,
        IKitapServisi servis)
    {
        if (await servis.BaslikVarMiAsync(request.Baslik))
            return TypedResults.Conflict((object)new { hata = $"'{request.Baslik}' başlığı zaten mevcut." });

        var formModel = new KitapFormViewModel
        {
            Baslik    = request.Baslik,
            Yazar     = request.Yazar,
            Fiyat     = request.Fiyat,
            Kategori  = request.Kategori,
            StokAdedi = request.StokAdedi
        };

        var yeniId = await servis.EkleAsync(formModel);
        var response = new KitapResponse(
            yeniId, request.Baslik, request.Yazar, request.Fiyat, request.Kategori, request.StokAdedi);
        return TypedResults.Created($"/api/minimal/kitaplar/{yeniId}", response);
    }

    private static async Task<Results<Ok<KitapResponse>, NotFound, Conflict<object>>> Guncelle(
        int id,
        KitapGuncelleRequest request,
        IKitapServisi servis)
    {
        if (await servis.BulByIdAsync(id) is null)
            return TypedResults.NotFound();

        if (await servis.BaslikVarMiAsync(request.Baslik, haricId: id))
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

        await servis.GuncelleAsync(formModel);
        var response = new KitapResponse(
            id, request.Baslik, request.Yazar, request.Fiyat, request.Kategori, request.StokAdedi);
        return TypedResults.Ok(response);
    }

    private static async Task<Results<NoContent, NotFound>> Sil(int id, IKitapServisi servis)
    {
        if (await servis.BulByIdAsync(id) is null)
            return TypedResults.NotFound();

        await servis.SilAsync(id);
        return TypedResults.NoContent();
    }
}

public record KategoriSorgusu([FromQuery] string? Kategori);
