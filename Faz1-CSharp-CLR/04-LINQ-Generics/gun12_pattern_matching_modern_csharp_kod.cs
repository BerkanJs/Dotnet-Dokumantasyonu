// Gün 12 — Pattern Matching ve Modern C# Özellikleri
// Her bölüm tek bir kavramı gösterir.

using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────
// Domain
// ─────────────────────────────────────────────

public enum SiparisDurumu { Aktif, Hazir, Kargoda, Teslim, Iptal }

public class Musteri
{
    public required string Ad   { get; init; }
    public required string Sehir { get; init; }
}

public class Siparis
{
    public required int           Id           { get; init; }
    public required SiparisDurumu Durum        { get; set; }
    public required decimal       ToplamFiyat  { get; init; }
    public required Musteri       Musteri      { get; init; }
    public string?                IptalNedeni  { get; set; }  // nullable — opsiyonel
}

// ─────────────────────────────────────────────
// BÖLÜM 1 — switch Expression
// ─────────────────────────────────────────────

public static class Bolum1_SwitchExpression
{
    // Eski yol — statement
    static string AciklamaGetirEski(SiparisDurumu durum)
    {
        switch (durum)
        {
            case SiparisDurumu.Aktif:   return "Sipariş işleniyor";
            case SiparisDurumu.Hazir:   return "Kargoya hazır";
            case SiparisDurumu.Kargoda: return "Kargoda";
            case SiparisDurumu.Teslim:  return "Teslim edildi";
            case SiparisDurumu.Iptal:   return "İptal edildi";
            default:                    return "Bilinmiyor";
        }
    }

    // Yeni yol — expression, değer döndürür
    static string AciklamaGetir(SiparisDurumu durum) => durum switch
    {
        SiparisDurumu.Aktif   => "Sipariş işleniyor",
        SiparisDurumu.Hazir   => "Kargoya hazır",
        SiparisDurumu.Kargoda => "Kargoda",
        SiparisDurumu.Teslim  => "Teslim edildi",
        SiparisDurumu.Iptal   => "İptal edildi",
        _                     => "Bilinmiyor"   // default
    };

    public static void Calistir()
    {
        Console.WriteLine("=== BÖLÜM 1: switch Expression ===\n");

        foreach (SiparisDurumu durum in Enum.GetValues<SiparisDurumu>())
            Console.WriteLine($"  {durum,-10} → {AciklamaGetir(durum)}");
    }
}

// ─────────────────────────────────────────────
// BÖLÜM 2 — Type Pattern ve when
// ─────────────────────────────────────────────

public interface IUrun
{
    string Baslik { get; }
    decimal Fiyat { get; }
}

public class Kitap : IUrun
{
    public required string  Baslik   { get; init; }
    public required decimal Fiyat    { get; init; }
    public required string  Kategori { get; init; }
}

public class Dergi : IUrun
{
    public required string  Baslik { get; init; }
    public required decimal Fiyat  { get; init; }
    public required int     Sayi   { get; init; }
}

public static class Bolum2_TypePattern
{
    // Tipe ve değere göre indirim — type pattern + when
    static decimal IndirimHesapla(IUrun urun) => urun switch
    {
        Kitap k when k.Fiyat > 100  => k.Fiyat * 0.15m,  // pahalı kitap: %15
        Kitap k when k.Kategori == "Programlama" => k.Fiyat * 0.10m,  // yazılım: %10
        Kitap k                     => k.Fiyat * 0.05m,  // diğer kitap: %5
        Dergi d                     => d.Fiyat * 0.20m,  // dergi: %20
        _                           => 0m
    };

    // is ile tip kontrolü + cast aynı anda
    static void UrunBilgisiYazdir(IUrun urun)
    {
        if (urun is Kitap kitap)
            Console.WriteLine($"  [Kitap] {kitap.Baslik} — Kategori: {kitap.Kategori}");
        else if (urun is Dergi dergi)
            Console.WriteLine($"  [Dergi] {dergi.Baslik} — Sayı: {dergi.Sayi}");
    }

    public static void Calistir()
    {
        Console.WriteLine("\n=== BÖLÜM 2: Type Pattern ===\n");

        var urunler = new List<IUrun>
        {
            new Kitap { Baslik = "Clean Code",        Fiyat = 75m,  Kategori = "Programlama" },
            new Kitap { Baslik = "Savaş ve Barış",    Fiyat = 120m, Kategori = "Roman"        },
            new Dergi { Baslik = "Linux Journal",     Fiyat = 30m,  Sayi = 42                 },
        };

        foreach (var urun in urunler)
        {
            UrunBilgisiYazdir(urun);
            var indirim = IndirimHesapla(urun);
            Console.WriteLine($"           İndirim: {indirim:C}  → Net: {urun.Fiyat - indirim:C}");
        }
    }
}

// ─────────────────────────────────────────────
// BÖLÜM 3 — Property Pattern
// ─────────────────────────────────────────────

public static class Bolum3_PropertyPattern
{
    // Sipariş durumu + fiyata göre mesaj — property pattern
    static string OncelikMesaji(Siparis siparis) => siparis switch
    {
        { Durum: SiparisDurumu.Aktif, ToplamFiyat: > 500 } => "VIP — öncelikli işle",
        { Durum: SiparisDurumu.Aktif                      } => "Normal sipariş",
        { Durum: SiparisDurumu.Iptal, IptalNedeni: null   } => "Nedensiz iptal — araştır",
        { Durum: SiparisDurumu.Iptal                      } => "İptal edildi",
        { Durum: SiparisDurumu.Teslim                     } => "Tamamlandı",
        _                                                    => "İşlemde"
    };

    // İç içe property pattern — müşteri şehrine göre kargo
    static decimal KargoUcretiHesapla(Siparis siparis) => siparis switch
    {
        { Musteri: { Sehir: "İstanbul" } } => 0m,
        { Musteri: { Sehir: "Ankara"   } } => 15m,
        { Musteri: { Sehir: "İzmir"    } } => 15m,
        _                                  => 30m
    };

    public static void Calistir()
    {
        Console.WriteLine("\n=== BÖLÜM 3: Property Pattern ===\n");

        var siparisler = new List<Siparis>
        {
            new()
            {
                Id = 1, Durum = SiparisDurumu.Aktif, ToplamFiyat = 750m,
                Musteri = new() { Ad = "Ali", Sehir = "İstanbul" }
            },
            new()
            {
                Id = 2, Durum = SiparisDurumu.Aktif, ToplamFiyat = 80m,
                Musteri = new() { Ad = "Ayşe", Sehir = "Bursa" }
            },
            new()
            {
                Id = 3, Durum = SiparisDurumu.Iptal, IptalNedeni = null,
                ToplamFiyat = 200m, Musteri = new() { Ad = "Mehmet", Sehir = "Ankara" }
            },
        };

        foreach (var s in siparisler)
        {
            var mesaj = OncelikMesaji(s);
            var kargo = KargoUcretiHesapla(s);
            Console.WriteLine($"  Sipariş #{s.Id} ({s.Musteri.Sehir}): {mesaj} | Kargo: {kargo:C}");
        }
    }
}

// ─────────────────────────────────────────────
// BÖLÜM 4 — Nullable Reference Types
// ─────────────────────────────────────────────

public class KitapDetay
{
    public required string  Baslik    { get; init; }   // null olamaz
    public required string  Yazar     { get; init; }   // null olamaz
    public string?          Aciklama  { get; init; }   // null olabilir — opsiyonel
    public string?          KapakUrl  { get; init; }   // null olabilir — opsiyonel
}

public static class Bolum4_NullableReferenceTypes
{
    static void KitapYazdir(KitapDetay kitap)
    {
        Console.WriteLine($"  {kitap.Baslik} — {kitap.Yazar}");

        // Nullable olduğu için null kontrol etmek gerekiyor
        if (kitap.Aciklama != null)
            Console.WriteLine($"  Açıklama: {kitap.Aciklama}");

        // Null-coalescing — null gelirse varsayılan kullan
        var kapak = kitap.KapakUrl ?? "varsayilan-kapak.jpg";
        Console.WriteLine($"  Kapak: {kapak}");
    }

    public static void Calistir()
    {
        Console.WriteLine("\n=== BÖLÜM 4: Nullable Reference Types ===\n");

        var tamKitap = new KitapDetay
        {
            Baslik   = "Clean Code",
            Yazar    = "Robert C. Martin",
            Aciklama = "Okunabilir kod yazmanın yolları",
            KapakUrl = "clean-code.jpg"
        };

        var eksikKitap = new KitapDetay
        {
            Baslik = "Pragmatic Programmer",
            Yazar  = "Andrew Hunt",
            // Aciklama ve KapakUrl yok — null
        };

        KitapYazdir(tamKitap);
        Console.WriteLine();
        KitapYazdir(eksikKitap);
    }
}

// ─────────────────────────────────────────────
// BÖLÜM 5 — Record Types ve with Expression
// ─────────────────────────────────────────────

// Positional record — constructor parametreli
public record KitapDto(string Baslik, string Yazar, decimal Fiyat);

// Property record — daha ayrıntılı
public record SiparisResponse
{
    public required int           Id          { get; init; }
    public required string        MusteriAdi  { get; init; }
    public required decimal       Toplam      { get; init; }
    public required SiparisDurumu Durum       { get; init; }
}

public static class Bolum5_Records
{
    public static void Calistir()
    {
        Console.WriteLine("\n=== BÖLÜM 5: Record Types ===\n");

        // Değer eşitliği
        var kitap1 = new KitapDto("Clean Code", "Martin", 75m);
        var kitap2 = new KitapDto("Clean Code", "Martin", 75m);
        Console.WriteLine($"kitap1 == kitap2 : {kitap1 == kitap2}");   // true
        Console.WriteLine($"kitap1.ToString(): {kitap1}");

        // with expression — kopyala ve değiştir
        var indirimli = kitap1 with { Fiyat = 60m };
        Console.WriteLine($"\nOrijinal fiyat  : {kitap1.Fiyat}");      // 75 — değişmedi
        Console.WriteLine($"İndirimli fiyat : {indirimli.Fiyat}");    // 60

        // Response DTO olarak kullanım
        var response = new SiparisResponse
        {
            Id         = 1,
            MusteriAdi = "Ali Veli",
            Toplam     = 150m,
            Durum      = SiparisDurumu.Aktif
        };

        // Durum güncellenince yeni nesne — orijinal değişmez
        var guncellenmis = response with { Durum = SiparisDurumu.Kargoda };
        Console.WriteLine($"\nOrijinal durum    : {response.Durum}");
        Console.WriteLine($"Güncellenmiş durum: {guncellenmis.Durum}");
    }
}

// ─────────────────────────────────────────────
// BÖLÜM 6 — Init-Only ve Required Members
// ─────────────────────────────────────────────

public class KitapOlusturRequest
{
    // required — new {} içinde doldurmak zorunlu
    public required string  Baslik { get; init; }
    public required string  Yazar  { get; init; }

    // Zorunlu değil, varsayılanı var
    public decimal Fiyat    { get; init; } = 0m;
    public string? Aciklama { get; init; }
}

public static class Bolum6_InitRequired
{
    public static void Calistir()
    {
        Console.WriteLine("\n=== BÖLÜM 6: Init-Only ve Required ===\n");

        var request = new KitapOlusturRequest
        {
            Baslik = "Domain-Driven Design",
            Yazar  = "Eric Evans",
            Fiyat  = 120m
            // Aciklama opsiyonel — boş bırakılabilir
        };

        Console.WriteLine($"Başlık : {request.Baslik}");
        Console.WriteLine($"Yazar  : {request.Yazar}");
        Console.WriteLine($"Fiyat  : {request.Fiyat}");
        Console.WriteLine($"Açıkl. : {request.Aciklama ?? "(yok)"}");

        // request.Baslik = "Değiştir";  ← Derleme hatası — init sadece new içinde
    }
}

// ─────────────────────────────────────────────
// BÖLÜM 7 — Raw String Literals (C# 11+)
// ─────────────────────────────────────────────

public static class Bolum7_RawStringLiterals
{
    public static void Calistir()
    {
        Console.WriteLine("\n=== BÖLÜM 7: Raw String Literals ===\n");

        // Eski yol — kaçış karakterleri
        string eskiJson = "{\"Baslik\": \"Clean Code\", \"Fiyat\": 75}";

        // Raw string literal — """ ile
        string yeniJson = """
            {
                "Baslik": "Clean Code",
                "Fiyat": 75
            }
            """;

        // SQL için de kullanışlı
        string sql = """
            SELECT Id, Baslik, Fiyat
            FROM Kitaplar
            WHERE Fiyat > 50
            ORDER BY Baslik
            """;

        Console.WriteLine("JSON:");
        Console.WriteLine(yeniJson);
        Console.WriteLine("\nSQL:");
        Console.WriteLine(sql);
    }
}

// ─────────────────────────────────────────────
// MAIN
// ─────────────────────────────────────────────

public static class Program
{
    public static void Main()
    {
        Bolum1_SwitchExpression.Calistir();
        Bolum2_TypePattern.Calistir();
        Bolum3_PropertyPattern.Calistir();
        Bolum4_NullableReferenceTypes.Calistir();
        Bolum5_Records.Calistir();
        Bolum6_InitRequired.Calistir();
        Bolum7_RawStringLiterals.Calistir();
    }
}
