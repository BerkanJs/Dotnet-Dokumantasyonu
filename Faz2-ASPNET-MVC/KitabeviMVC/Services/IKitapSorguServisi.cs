using KitabeviMVC.Models.ViewModels;

namespace KitabeviMVC.Services;

// ─────────────────────────────────────────────────────────────────────
// Gün 30: IQueryable sorgu metodları için ayrı arayüz.
//
// Neden IKitapServisi'ne eklemedik?
//   → IKitapServisi zaten var ve tüm implementasyonları (KitapServisi,
//     CachedKitapServisi) güncellememiz gerekir.
//   → Bu metodlar EF Core'a özgü (Include, AsSplitQuery) — in-memory
//     implementasyonunda anlamsız.
//   → Interface Segregation Principle: istemciler kullanmadıkları
//     metodlara bağımlı olmamalı.
//
// Sadece EfKitapServisi implement eder; controller DI'dan bu arayüzü alır.
// ─────────────────────────────────────────────────────────────────────
public interface IKitapSorguServisi
{
    // Include + Projection: Yazar JOIN'i ile birlikte detay getir
    Task<KitapDetayViewModel?> DetayYazarlaGetirAsync(int id);

    // Dinamik IQueryable zinciri: kategori filtresi + öneri listesi
    Task<IReadOnlyList<KitapListeViewModel>> AyniKategoridekilerAsync(
        string kategori, int haricId, int limit = 5);

    // Fiyat aralığı filtresi — IQueryable dinamik zinciri gösterimi
    Task<IReadOnlyList<KitapListeViewModel>> FiyatAraligiGetirAsync(
        decimal? minFiyat, decimal? maxFiyat, string? kategori = null);

    // Arama — EF.Functions.Like ile SQL LIKE
    Task<IReadOnlyList<KitapListeViewModel>> AraAsync(string aramaMetni);

    // ═════════════════════════════════════════════════════════════════════
    // GÜN 31: N+1 Problemi ve Çözümleri
    // ═════════════════════════════════════════════════════════════════════

    // Compiled query ile ID'ye göre hızlı detay — translation overhead sıfır
    Task<KitapDetayViewModel?> HizliDetayGetirAsync(int id);

    // N+1'den korunan liste — Yazar JOIN ile tek SQL, asla ayrı yazar sorgusu yok
    Task<IReadOnlyList<KitapListeViewModel>> YazarlariyleHepsiniGetirAsync();
}
