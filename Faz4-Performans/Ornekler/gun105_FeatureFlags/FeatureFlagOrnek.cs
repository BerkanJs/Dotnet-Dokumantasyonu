// GÜN 105 — Feature Flags
// Deploy ≠ Release: kodu deploy et, flag kapalı → kullanıcıya görünmez
// Flag aç → release — rollback = flag kapat

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.Mvc;

namespace Ornekler.gun105;

// --- 1. Flag isimleri — magic string'den kaçın ---
public static class FeatureFlags
{
    public const string YeniOdemeAkisi = "YeniOdemeAkisi";
    public const string KaranlıkMod = "KaranlıkMod";
    public const string BetaArama = "BetaArama";
}

// --- 2. Program.cs kurulum ---
public static class FeatureFlagSetup
{
    public static void Kaydet(WebApplicationBuilder builder)
    {
        // ne yapar: appsettings.json'daki "FeatureManagement" bloğunu okur
        // bunu yazmasaydık: her flag kontrolü için custom config okuma yazmak zorunda kalırdık
        builder.Services.AddFeatureManagement()
            // ne yapar: belirli kullanıcı/grup için flag aç (A/B test)
            .AddFeatureFilter<Microsoft.FeatureManagement.FeatureFilters.PercentageFilter>()
            .AddFeatureFilter<Microsoft.FeatureManagement.FeatureFilters.TimeWindowFilter>();
    }
}

// appsettings.json örneği:
// "FeatureManagement": {
//   "YeniOdemeAkisi": true,
//   "KaranlıkMod": false,
//   "BetaArama": {
//     "EnabledFor": [
//       {
//         "Name": "Percentage",
//         "Parameters": { "Value": 20 }  ← kullanıcıların %20'si için aç
//       }
//     ]
//   }
// }

// --- 3. Servis içinde kullanım ---
public class OdemeServisi
{
    private readonly IFeatureManager _featureManager;

    public OdemeServisi(IFeatureManager featureManager)
        => _featureManager = featureManager;

    public async Task<string> OdemeYapAsync(decimal tutar)
    {
        // ne yapar: flag açıksa yeni akış, kapalıysa eski akış
        // bunu yazmasaydık: yeni kodu deploy etmek = otomatik release — geri almanın yolu yok
        if (await _featureManager.IsEnabledAsync(FeatureFlags.YeniOdemeAkisi))
        {
            return await YeniOdemeAkisiAsync(tutar);
        }

        return await EskiOdemeAkisiAsync(tutar);
    }

    private Task<string> YeniOdemeAkisiAsync(decimal tutar) =>
        Task.FromResult($"Yeni akış: {tutar:C}");

    private Task<string> EskiOdemeAkisiAsync(decimal tutar) =>
        Task.FromResult($"Eski akış: {tutar:C}");
}

// --- 4. Controller'da attribute ile kullanım ---
// [FeatureGate(FeatureFlags.BetaArama)]
// public class BetaAramaController : ControllerBase
// {
//     // Flag kapalıysa 404 döner — controller erişilemez
// }

// --- 5. Minimal API'da kullanım ---
public static class FeatureEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/beta/arama", async (IFeatureManager fm, string q) =>
        {
            // ne yapar: endpoint içinde flag kontrol et
            if (!await fm.IsEnabledAsync(FeatureFlags.BetaArama))
                return Results.NotFound();

            return Results.Ok($"Beta arama sonuçları: {q}");
        });
    }
}

// --- 6. Flag temizleme stratejisi ---
// Flag debt: kullanılmayan flag'ler birikirse kod okunaksız olur
// Kural: flag release'den sonra max 2 sprint bekler → eski kod silinir, flag kaldırılır
// Release flag: kalıcı — permission flag, ops flag olabilir
// Experiment flag: A/B test bitince kaldır
// Geçici flag: yeni özellik stable olunca kaldır
