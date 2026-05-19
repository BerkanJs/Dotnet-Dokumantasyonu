// GÜN 86 — Immutability ve Functional Patterns
// Immutable nesne: oluşturulduktan sonra değişmez → thread-safe, tahmin edilebilir

using System.Collections.Immutable;

namespace Ornekler.gun86;

// --- 1. Immutable record ---
public record Adres(string Sokak, string Sehir, string PostaKodu)
{
    // ne yapar: mevcut record'dan sadece bir alanı değiştirerek yeni record döner
    // bunu yazmasaydık: yeni Adres(...) ile tüm alanları tekrar yazmak zorunda kalırdık
    public Adres AdresDegistir(string yeniSokak) =>
        this with { Sokak = yeniSokak };
}

// --- 2. Immutable koleksiyonlar ---
public static class ImmutableKoleksiyon
{
    public static void Goster()
    {
        // ne yapar: değişmez liste — thread-safe, paylaşılabilir
        // bunu yazmasaydık: List<T> paylaşılan state'de race condition olurdu
        ImmutableList<string> liste = ImmutableList.Create("Ali", "Veli", "Ayşe");

        // Ekleme yeni liste döner — orijinal değişmez
        ImmutableList<string> yeniListe = liste.Add("Fatma");

        Console.WriteLine(liste.Count);    // 3 — orijinal değişmedi
        Console.WriteLine(yeniListe.Count); // 4

        // ne yapar: dictionary'yi thread-safe paylaşmak için
        // bunu yazmasaydık: ConcurrentDictionary veya lock kullanmak zorunda kalırdık
        ImmutableDictionary<string, int> sozluk =
            ImmutableDictionary<string, int>.Empty
                .Add("Ali", 25)
                .Add("Veli", 30);
    }
}

// --- 3. Pure function — aynı girdi, her zaman aynı çıktı ---
public static class PureFunctionlar
{
    // PURE: dış durum yok, side effect yok → test edilmesi kolay, paralel güvenli
    public static decimal KdvHesapla(decimal fiyat, decimal kdvOrani = 0.20m)
    {
        // ne yapar: sadece parametrelerden hesaplanır, hiçbir dış state değiştirmez
        // bunu yazmasaydık: global state kullansaydık test yazması zorlaşırdı
        return fiyat * (1 + kdvOrani);
    }

    // IMPURE: dış durum değiştiriyor → test edilmesi zor, parallel unsafe
    private static decimal _toplamKdv = 0;
    public static decimal ImpureKdvHesapla(decimal fiyat)
    {
        var kdv = fiyat * 0.20m;
        _toplamKdv += kdv;  // side effect — global state değişiyor
        return fiyat + kdv;
    }
}

// --- 4. Functional pipeline — map, filter, reduce ---
public record Urun(string Ad, decimal Fiyat, int Stok, string Kategori);

public static class FunctionalPipeline
{
    public static decimal ToplamKitapDegeri(IEnumerable<Urun> urunler)
    {
        // ne yapar: kitapları filtrele → fiyat × stok → topla
        // bunu yazmasaydık: foreach + if + sum elle yazardık
        return urunler
            .Where(u => u.Kategori == "Kitap" && u.Stok > 0)
            .Select(u => u.Fiyat * u.Stok)
            .DefaultIfEmpty(0)
            .Sum();
    }

    // ne yapar: koleksiyonu gruplandırıp kategori bazlı özet çıkarır
    // bunu yazmasaydık: iç içe döngüler ve Dictionary manuel yönetimi gerekirdi
    public static Dictionary<string, decimal> KategoriBazliToplam(IEnumerable<Urun> urunler)
    {
        return urunler
            .GroupBy(u => u.Kategori)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(u => u.Fiyat * u.Stok));
    }
}

// --- 5. Builder pattern ile immutable nesne oluşturma ---
public sealed class SiparisBilgisi
{
    public string KitapId { get; }
    public int Adet { get; }
    public string KullaniciId { get; }
    public DateTime OlusturulmaTarihi { get; }

    private SiparisBilgisi(string kitapId, int adet, string kullaniciId, DateTime tarih)
    {
        KitapId = kitapId;
        Adet = adet;
        KullaniciId = kullaniciId;
        OlusturulmaTarihi = tarih;
    }

    // ne yapar: nesneyi doğrulayarak oluşturur — geçersiz state mümkün değil
    // bunu yazmasaydık: public setter'lar → dışarıdan herhangi biri değiştirebilirdi
    public static SiparisBilgisi Olustur(string kitapId, int adet, string kullaniciId)
    {
        if (string.IsNullOrWhiteSpace(kitapId)) throw new ArgumentException("KitapId boş olamaz");
        if (adet <= 0) throw new ArgumentOutOfRangeException(nameof(adet), "Adet pozitif olmalı");
        if (string.IsNullOrWhiteSpace(kullaniciId)) throw new ArgumentException("KullaniciId boş olamaz");

        return new SiparisBilgisi(kitapId, adet, kullaniciId, DateTime.UtcNow);
    }
}
