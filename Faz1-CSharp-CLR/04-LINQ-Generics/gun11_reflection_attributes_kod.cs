// Gün 11 — Reflection, Attributes ve Source Generators
// Her bölüm tek bir kavramı gösterir.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

// ─────────────────────────────────────────────
// Domain
// ─────────────────────────────────────────────

public class Kitap
{
    public int    Id     { get; set; }
    public string Baslik { get; set; } = "";
    public decimal Fiyat { get; set; }
    public string  Yazar { get; set; } = "";
}

// ─────────────────────────────────────────────
// BÖLÜM 1 — Reflection: Tip Bilgisini Runtime'da Okumak
// ─────────────────────────────────────────────

public static class Bolum1_Reflection
{
    public static void Calistir()
    {
        Console.WriteLine("=== BÖLÜM 1: Reflection ===\n");

        Type tip = typeof(Kitap);

        // Tipin adı ve namespace'i
        Console.WriteLine($"Tip adı   : {tip.Name}");
        Console.WriteLine($"Namespace : {tip.Namespace ?? "(yok)"}");
        Console.WriteLine($"Assembly  : {tip.Assembly.GetName().Name}");

        Console.WriteLine("\nProperty'ler:");
        foreach (var prop in tip.GetProperties())
        {
            Console.WriteLine($"  {prop.PropertyType.Name,-10} {prop.Name}");
        }

        // Runtime'da nesne oluşturma (Activator)
        var kitap = (Kitap)Activator.CreateInstance(typeof(Kitap))!;
        kitap.Baslik = "Reflection ile oluşturuldu";

        // Runtime'da property'e değer atama
        PropertyInfo? baslikProp = tip.GetProperty("Baslik");
        baslikProp?.SetValue(kitap, "Reflection ile değer set edildi");

        Console.WriteLine($"\nKitap başlığı: {kitap.Baslik}");
    }
}

// ─────────────────────────────────────────────
// BÖLÜM 2 — Reflection Cache: Yavaşlığı Gidermek
// ─────────────────────────────────────────────

public static class Bolum2_ReflectionCache
{
    // Başlangıçta bir kez al, static'te tut
    private static readonly PropertyInfo[] _kitapProperties =
        typeof(Kitap).GetProperties();

    private static readonly MethodInfo _toStringMethod =
        typeof(Kitap).GetMethod(nameof(object.ToString))!;

    public static void Calistir()
    {
        Console.WriteLine("\n=== BÖLÜM 2: Reflection Cache ===\n");

        var kitap = new Kitap { Id = 1, Baslik = "Clean Code", Fiyat = 75m };

        // Cache'lenmiş property listesiyle döngü
        foreach (var prop in _kitapProperties)
        {
            var deger = prop.GetValue(kitap);
            Console.WriteLine($"  {prop.Name}: {deger}");
        }

        Console.WriteLine("\nCache'lenmiş MethodInfo kullanıldı (tek seferlik arama).");
    }
}

// ─────────────────────────────────────────────
// BÖLÜM 3 — Custom Attribute Tanımlama ve Okuma
// ─────────────────────────────────────────────

// Sadece property'lere uygulanabilir
[AttributeUsage(AttributeTargets.Property)]
public class AuditAttribute : Attribute
{
    public string Aciklama { get; }
    public AuditAttribute(string aciklama) => Aciklama = aciklama;
}

// Sadece sınıflara uygulanabilir
[AttributeUsage(AttributeTargets.Class)]
public class ServisAttribute : Attribute
{
    public string Tanim { get; }
    public ServisAttribute(string tanim) => Tanim = tanim;
}

// Attribute'ları yapıştır
[Servis("Kitap yönetim entity'si")]
public class KitapWithAudit
{
    public int Id { get; set; }

    [Audit("Başlık değiştirildi")]
    public string Baslik { get; set; } = "";

    [Audit("Fiyat güncellendi")]
    public decimal Fiyat { get; set; }

    // Attribute yok — log'a yazılmaz
    public string Yazar { get; set; } = "";
}

public static class Bolum3_CustomAttribute
{
    public static void Calistir()
    {
        Console.WriteLine("\n=== BÖLÜM 3: Custom Attribute ===\n");

        Type tip = typeof(KitapWithAudit);

        // Sınıf üzerindeki attribute
        var servisAttr = tip.GetCustomAttribute<ServisAttribute>();
        if (servisAttr != null)
            Console.WriteLine($"Servis tanımı: {servisAttr.Tanim}");

        // Property'lerdeki attribute'ları oku
        Console.WriteLine("\nAudit property'leri:");
        foreach (var prop in tip.GetProperties())
        {
            var audit = prop.GetCustomAttribute<AuditAttribute>();
            if (audit != null)
                Console.WriteLine($"  [{prop.Name}] → {audit.Aciklama}");
        }
    }
}

// ─────────────────────────────────────────────
// BÖLÜM 4 — Expression Trees: Func vs Expression<Func>
// ─────────────────────────────────────────────

public static class Bolum4_ExpressionTrees
{
    public static void Calistir()
    {
        Console.WriteLine("\n=== BÖLÜM 4: Expression Trees ===\n");

        var kitaplar = new List<Kitap>
        {
            new() { Id = 1, Baslik = "Clean Code",      Fiyat = 45m },
            new() { Id = 2, Baslik = "Domain-Driven",   Fiyat = 90m },
            new() { Id = 3, Baslik = "Pragmatic Prog.",  Fiyat = 60m },
        };

        // Func<Kitap, bool> — delegate, bellekte çalışır
        Func<Kitap, bool> delegateFiltre = k => k.Fiyat < 60;
        var sonuc1 = kitaplar.Where(delegateFiltre).ToList();
        Console.WriteLine($"Delegate (List.Where) → {sonuc1.Count} kitap");

        // Expression<Func<Kitap, bool>> — ifade ağacı
        // EF Core gibi LINQ provider'lar bunu SQL'e çevirir
        Expression<Func<Kitap, bool>> expressionFiltre = k => k.Fiyat < 60;

        // Bellekte de kullanmak istersen Compile() et
        var derlenmisFonk = expressionFiltre.Compile();
        var sonuc2 = kitaplar.Where(derlenmisFonk).ToList();
        Console.WriteLine($"Expression (derlenmiş) → {sonuc2.Count} kitap");

        // Expression Tree'nin yapısını incele
        Console.WriteLine($"\nExpression tipi : {expressionFiltre.NodeType}");
        Console.WriteLine($"Body tipi       : {expressionFiltre.Body.NodeType}");
        // → "LessThan" — EF Core bunu görüp WHERE Fiyat < 60 yazar
    }
}

// ─────────────────────────────────────────────
// BÖLÜM 5 — Generic Filtre Builder (Attribute + Expression)
// ─────────────────────────────────────────────

// "Bu property zorunludur" diyen custom attribute
[AttributeUsage(AttributeTargets.Property)]
public class ZorunluAlanAttribute : Attribute { }

public class KitapOlusturRequest
{
    [ZorunluAlan]
    public string Baslik { get; set; } = "";

    [ZorunluAlan]
    public string Yazar { get; set; } = "";

    public decimal Fiyat { get; set; }  // zorunlu değil
}

// Reflection ile basit validator
public static class Bolum5_AttributeValidator
{
    // Cache — sadece başlangıçta hesapla
    private static readonly Dictionary<Type, PropertyInfo[]> _zorunluPropCache = new();

    private static PropertyInfo[] ZorunluPropGetir(Type tip)
    {
        if (!_zorunluPropCache.TryGetValue(tip, out var props))
        {
            props = tip.GetProperties()
                       .Where(p => p.GetCustomAttribute<ZorunluAlanAttribute>() != null)
                       .ToArray();
            _zorunluPropCache[tip] = props;
        }
        return props;
    }

    public static List<string> Dogrula<T>(T nesne)
    {
        var hatalar = new List<string>();
        foreach (var prop in ZorunluPropGetir(typeof(T)))
        {
            var deger = prop.GetValue(nesne)?.ToString();
            if (string.IsNullOrWhiteSpace(deger))
                hatalar.Add($"{prop.Name} alanı zorunludur.");
        }
        return hatalar;
    }

    public static void Calistir()
    {
        Console.WriteLine("\n=== BÖLÜM 5: Attribute Tabanlı Validator ===\n");

        var eksikRequest = new KitapOlusturRequest { Baslik = "", Yazar = "Berkan", Fiyat = 50m };
        var hatalar = Dogrula(eksikRequest);

        if (hatalar.Any())
        {
            Console.WriteLine("Doğrulama hataları:");
            foreach (var h in hatalar)
                Console.WriteLine($"  ✗ {h}");
        }
        else
        {
            Console.WriteLine("Geçerli istek.");
        }
    }
}

// ─────────────────────────────────────────────
// BÖLÜM 6 — Source Generator Simülasyonu
// ─────────────────────────────────────────────
// Gerçek Source Generator bir Roslyn analyzer'dır — burada elle gösteriyoruz.
// Normalde bu kod derleme sırasında otomatik üretilirdi.

// partial sınıf — Source Generator ikinci parçayı üretir
public partial class KitapEntity
{
    public int    Id     { get; set; }
    public string Baslik { get; set; } = "";
    public decimal Fiyat { get; set; }
}

// Source Generator'ın üreteceği kod (normalde ayrı dosyada, otomatik):
public partial class KitapEntity
{
    // Reflection olmadan, derleme zamanında üretilmiş ToString
    public override string ToString()
        => $"KitapEntity {{ Id={Id}, Baslik={Baslik}, Fiyat={Fiyat} }}";

    // Reflection olmadan, derleme zamanında üretilmiş eşitlik karşılaştırması
    public bool IdEsitMi(KitapEntity diger) => Id == diger.Id;
}

public static class Bolum6_SourceGeneratorSimulasyon
{
    public static void Calistir()
    {
        Console.WriteLine("\n=== BÖLÜM 6: Source Generator Simülasyonu ===\n");

        var kitap = new KitapEntity { Id = 1, Baslik = "Clean Architecture", Fiyat = 80m };
        Console.WriteLine(kitap.ToString());
        // Reflection yok — sıfır runtime maliyet
        // ToString kodu derleme zamanında üretildi

        Console.WriteLine("\n[Gerçek Source Generator örneği:]");
        Console.WriteLine("  System.Text.Json → [JsonSerializable] attribute ile");
        Console.WriteLine("  derleme zamanında serialize kodu üretir.");
        Console.WriteLine("  AOT (Ahead-of-Time) derleme için zorunludur.");
    }
}

// ─────────────────────────────────────────────
// MAIN
// ─────────────────────────────────────────────

public static class Program
{
    public static void Main()
    {
        Bolum1_Reflection.Calistir();
        Bolum2_ReflectionCache.Calistir();
        Bolum3_CustomAttribute.Calistir();
        Bolum4_ExpressionTrees.Calistir();
        Bolum5_AttributeValidator.Calistir();
        Bolum6_SourceGeneratorSimulasyon.Calistir();
    }
}
