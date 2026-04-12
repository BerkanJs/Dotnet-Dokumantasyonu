namespace KitabeviMVC.Services;

// ─────────────────────────────────────────────────────────────────────
// Gün 33: Toplu (Batch) Operasyonlar — EF Core 7+ ExecuteUpdate/Delete.
//
// Neden ayrı interface?
//   → IKitapServisi: tüm implementasyonlar (CachedKitapServisi, KitapServisi,
//     EfKitapServisi) bu interface'i uygular. Buraya toplu operasyon eklemek
//     tüm sınıflara stub ekletir → Interface Segregation Principle ihlali.
//   → IKitapSorguServisi: sorgu metodları için — yazma operasyonları buraya girmez.
//   → IKitapBatchServisi: sadece EfKitapServisi implement eder — EF Core 7+'a özgü.
//
// Ne zaman kullanılır?
//   → Binlerce satırı aynı koşulla güncelle/sil: ExecuteUpdateAsync / ExecuteDeleteAsync
//   → Change Tracker'a gerek yok, tek SQL üretir, bellek yükü sıfır.
// ─────────────────────────────────────────────────────────────────────
public interface IKitapBatchServisi
{
    // Belirli kategorideki tüm kitapların stok adedini sıfırla.
    // Dönüş: etkilenen satır sayısı.
    // Kullanım: raf çıkışı, sezon sonu temizliği vb.
    Task<int> KategoriStokSifirlaAsync(string kategori);

    // Belirli kategorideki kitaplara yüzde bazlı fiyat artışı uygula.
    // artisOrani: 0.10 → %10 artış. Örnek: 45 → 49.50
    // Dönüş: etkilenen satır sayısı.
    Task<int> TopluFiyatArttirAsync(string kategori, decimal artisOrani);

    // Stok adedi 0 ve belirtilen yıldan eski kitapları kalıcı olarak sil.
    // yilEsigi: kaç yıldan önce eklenen kitaplar silinmeli? Varsayılan 2 yıl.
    // Dönüş: silinen satır sayısı.
    Task<int> EskiStoksuzlariSilAsync(int yilEsigi = 2);
}
