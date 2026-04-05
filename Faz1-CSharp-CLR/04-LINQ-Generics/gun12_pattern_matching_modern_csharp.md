# Gün 12 — Pattern Matching ve Modern C# Özellikleri

---

## 1. Pattern Matching Nedir?

Pattern matching, bir değerin **şekline bakarak** ne yapacağını belirleme yöntemi.  
Klasik `if/else` ve `switch`'ten farklı: tip kontrolü, null kontrolü ve değer eşleştirmeyi **tek satırda** yapabilirsin.

Java'dan tanıdıksın — ama C#'ın pattern matching'i çok daha güçlü.

---

## 2. switch Expression (C# 8+)

Eski `switch` statement yerine sonuç **döndüren** switch:

```csharp
// Eski yol — statement, return yok
string AciklamaGetir(string durum)
{
    switch (durum)
    {
        case "Aktif":   return "Sipariş işleniyor";
        case "Iptal":   return "Sipariş iptal edildi";
        default:        return "Bilinmiyor";
    }
}

// Yeni yol — expression, değer döndürür
string AciklamaGetir(string durum) => durum switch
{
    "Aktif"  => "Sipariş işleniyor",
    "Iptal"  => "Sipariş iptal edildi",
    _        => "Bilinmiyor"   // default
};
```

`_` → her şeyle eşleşen "wildcard". Tüm case'ler karşılanmazsa derleyici **uyarı verir**.

---

## 3. Type Pattern — Tip Kontrolü

```csharp
// Eski yol
if (nesne is Kitap)
{
    var kitap = (Kitap)nesne;  // cast
    Console.WriteLine(kitap.Baslik);
}

// Yeni yol — is ile aynı anda cast
if (nesne is Kitap kitap)
{
    Console.WriteLine(kitap.Baslik);  // kitap burada kullanılabilir
}
```

switch ile birlikte:

```csharp
// IUrun arayüzünü implemente eden farklı tipler
decimal IndirimHesapla(IUrun urun) => urun switch
{
    Kitap k when k.Fiyat > 100 => k.Fiyat * 0.10m,  // pahalı kitap: %10
    Kitap k                    => k.Fiyat * 0.05m,  // diğer kitap: %5
    Dergi d                    => d.Fiyat * 0.15m,  // dergi: %15
    _                          => 0m
};
```

`when` → ek koşul ekler. Tipi eşleştir **ve** koşulu sağla.

---

## 4. Property Pattern — İç Değerlere Bakma

Nesnenin property'lerine bakarak eşleştirme:

```csharp
// Kitabın durumuna göre mesaj
string DurumMesaji(Siparis siparis) => siparis switch
{
    { Durum: "Aktif",  ToplamFiyat: > 500 } => "VIP sipariş — öncelikli işle",
    { Durum: "Aktif"                       } => "Normal sipariş",
    { Durum: "Iptal",  IptalNedeni: null   } => "Nedensiz iptal",
    { Durum: "Iptal"                       } => "İptal edildi",
    _                                        => "Bilinmeyen durum"
};
```

İç içe property pattern:

```csharp
// Müşterinin şehrine göre kargo ücreti
decimal KargoUcretiHesapla(Siparis siparis) => siparis switch
{
    { Musteri: { Sehir: "İstanbul" } } => 0m,    // İstanbul: ücretsiz
    { Musteri: { Sehir: "Ankara"   } } => 15m,
    _                                  => 25m
};
```

---

## 5. Nullable Reference Types (C# 8+)

C# 8 öncesinde her referans tipi null olabilirdi — derleyici uyarmıyordu.  
C# 8 ile `string` ve `string?` **farklı** anlamlara gelir:

```csharp
string  baslik = null;   // ⚠️ Derleyici uyarı verir — null olamaz
string? baslik = null;   // ✓ Açıkça nullable — null olabilir
```

Bu neden önemli? Domain model'ini daha net ifade eder:

```csharp
public class Kitap
{
    public int    Id     { get; set; }
    public string Baslik { get; set; } = "";  // null olamaz, kesin var
    public string? Aciklama { get; set; }     // null olabilir, opsiyonel
}
```

Kodu okuyan biri `Aciklama` için null kontrolü yapması gerektiğini hemen anlar.

**Null-forgiving operator `!`:**

```csharp
// "Ben garanti ediyorum, null değil" — derleyiciyi susturur
string baslik = GetirBaslik()!;
```

Kullan ama dikkatli: null gelirse `NullReferenceException` alırsın, derleyici artık korumaz.

---

## 6. Record Types (C# 9+)

Record, **değer semantiği** olan bir referans tipi.  
"İki nesne aynı veriye sahipse eşittir" demek.

```csharp
// Normal class — referans eşitliği
public class KitapClass
{
    public string Baslik { get; set; } = "";
}

var a = new KitapClass { Baslik = "Clean Code" };
var b = new KitapClass { Baslik = "Clean Code" };
Console.WriteLine(a == b);  // false — farklı nesneler

// Record — değer eşitliği
public record KitapRecord(string Baslik, decimal Fiyat);

var x = new KitapRecord("Clean Code", 75m);
var y = new KitapRecord("Clean Code", 75m);
Console.WriteLine(x == y);  // true — aynı veri
```

Record otomatik olarak şunları üretir:
- `Equals()` ve `==` — değerleri karşılaştırır
- `GetHashCode()` — değere göre hash
- `ToString()` — okunabilir çıktı: `KitapRecord { Baslik = Clean Code, Fiyat = 75 }`

**`with` expression — kopyala ve değiştir:**

```csharp
var kitap   = new KitapRecord("Clean Code", 75m);
var indirimli = kitap with { Fiyat = 60m };  // yeni nesne, Baslik aynı

Console.WriteLine(kitap.Fiyat);      // 75 — değişmedi
Console.WriteLine(indirimli.Fiyat);  // 60
```

Record immutable'dır — `with` orijinali değiştirmez, yeni nesne döner.

**Web geliştirmede nerede görünür?**

```csharp
// DTO ve request/response için ideal — sadece veri taşır
public record KitapOlusturRequest(
    string Baslik,
    string Yazar,
    decimal Fiyat
);

public record KitapResponse(
    int    Id,
    string Baslik,
    string Yazar,
    decimal Fiyat
);
```

Faz 2'de MVC yazarken bu pattern'i sürekli kullanacaksın. Record ile DTO yazmak hem kısa hem güvenli.

---

## 7. Init-Only Properties (C# 9+)

`init` setter, nesne **oluşturulurken** atanabilir ama sonradan değiştirilemez:

```csharp
public class Kitap
{
    public int    Id     { get; init; }  // sadece new {} içinde set edilebilir
    public string Baslik { get; init; } = "";
}

var kitap = new Kitap { Id = 1, Baslik = "Clean Code" };
kitap.Id = 2;  // ❌ Derleme hatası — init sadece constructor'da
```

`set` yerine `init` kullanmak: "bu nesne oluşturulduktan sonra değişmez" garantisi verir.

---

## 8. Required Members (C# 11+)

`required` keyword — nesne oluştururken bu alanı doldurmak **zorunlu**:

```csharp
public class KitapOlusturRequest
{
    public required string Baslik { get; init; }
    public required string Yazar  { get; init; }
    public decimal Fiyat { get; init; }  // zorunlu değil
}

var request = new KitapOlusturRequest
{
    Baslik = "Clean Code",
    // Yazar eksik → Derleme hatası
};
```

`[Required]` attribute'undan farkı: bu **compile-time** kontroldür, runtime validation değil.

---

## 9. Raw String Literals (C# 11+)

Çok satırlı string'ler için kaçış karakterleri (`\"`, `\n`) yazmaktan kurtulursun:

```csharp
// Eski yol — kaçış karakterleri dolu
string json = "{\"Baslik\": \"Clean Code\", \"Fiyat\": 75}";

// Raw string literal — """ ile başlar/biter
string json = """
    {
        "Baslik": "Clean Code",
        "Fiyat": 75
    }
    """;
```

Test yazarken veya JSON/SQL içeren string'lerde çok kullanışlı.

---

## 10. Mimari Önemi

| Özellik | Neden önemli |
|---|---|
| **Nullable Reference Types** | Domain model'de "null olabilir mi?" sorusu koda yansır |
| **Record Types** | Value Object pattern için ideal (DDD'de sık kullanılır) |
| **Pattern Matching** | Tip bazlı dal ayrımını functional tarzda yapar |
| **Required Members** | DTO'larda compile-time zorunlu alan garantisi |

Faz 2'de MVC controller'ları yazarken:
- Request/Response → `record`
- Zorunlu alanlar → `required` veya `[Required]`
- Servis katmanı dönüş tipleri → pattern matching ile işle

---

## 11. Kontrol Soruları

1. `switch` statement ile `switch` expression arasındaki temel fark nedir?
eskisi bir şey yapar deger döndürmez bu deger de döndürüyor 
2. Şu iki satır neden farklı anlama gelir?
   ```csharp
   string baslik = null;
   string? baslik = null;
   ```
null olabilir 

3. `record` ile `class` arasındaki eşitlik farkı nedir? Hangi durumda `record` tercih edersin?

immutable record ve veri tasırken veri getirirken daha kolay record örnek dto
4. `with` expression ne döndürür — orijinal nesneyi mi değiştirir, yoksa yeni nesne mi oluşturur?
yeni nesne olusturur 
5. `required` keyword ile `[Required]` attribute'u arasındaki fark nedir?
birisi 
rquired derleme zamanında hata fırlatır diğeri runtime sırasında