# Gün 76 — String Optimizasyonları

String işlemleri uygulamalarda en sık allocation kaynağıdır. Doğru araç seçimi GC baskısını ciddi ölçüde düşürür.

---

## string.Concat vs StringBuilder vs string.Create

**Sorun:** String'ler C#'ta değiştirilemez (immutable). Her `+` işlemi bellekte yeni bir string nesnesi oluşturur.

```
"Merhaba" + ", " + "Berkan"
  → önce "Merhaba, " oluşur   (1. allocation)
  → sonra "Merhaba, Berkan"   (2. allocation)
  → ilk geçici string çöpe gider
```

Döngüde 1000 kez yapılırsa → 1000 geçici string → GC baskısı.

```csharp
// Az parça (2-4) → interpolation yeterli, compiler optimize eder
string sonuc = $"{ad} {soyad}";

// Döngüde birleştirme → StringBuilder zorunlu
// StringBuilder içinde büyüyen bir char buffer'dır — heap'e tek seferde yazar
var sb = new StringBuilder();
foreach (var satir in emirler)
{
    sb.AppendLine(satir);           // buffer'a ekle — yeni string oluşturmaz
}
string metin = sb.ToString();       // tek allocation — sadece burada

// Boyutu önceden bilinen, çok sık çağrılan hot path → string.Create
string tam = string.Create(ad.Length + 1 + soyad.Length, (ad, soyad), (span, s) =>
{
    s.ad.AsSpan().CopyTo(span);             // direkt belleğe yaz
    span[s.ad.Length] = ' ';
    s.soyad.AsSpan().CopyTo(span[(s.ad.Length + 1)..]);
});
// string.Create → sonuç string için tek allocation, içi Span ile doldurulur
// bunu interpolation ile yazsaydık → compiler zaten buna yakın kod üretir ama
// string.Create kontrolü tamamen sende — serializer/parser yazarken tercih edilir
```

---

## Interpolated String Handler (C# 10+)

**Ne işe yarar?** `$"..."` ifadesinin gereksiz yere string oluşturmasını engeller.

Klasik sorun: loglama seviyesi DEBUG kapalıyken bile string oluşturuluyordu.

```csharp
// Eski C# — Debug kapalı olsa bile "Sipariş 42 işlendi..." string'i bellekte oluşur
logger.LogDebug("Sipariş " + siparisId + " işlendi");   // allocation boşa gitti

// C# 10+ interpolated string handler ile:
logger.LogDebug($"Sipariş {siparisId} işlendi, toplam: {tutar:C}");
// Logger önce "Debug açık mı?" diye bakar
// Kapalıysa → string hiç oluşturulmaz, allocation sıfır
// Açıksa → string oluşturulur ve loglanır
```

Bu davranış `Microsoft.Extensions.Logging` içine gömülü — sen sadece `$"..."` kullanıyorsun, gerisi otomatik.

---

## SearchValues\<T\> — Hızlı Karakter Arama

**Ne işe yarar?** Bir string içinde birden fazla karakterden herhangi birini aramak için kullanılır.

Örnek: CSV, JSON veya log satırlarını ayraç karakterine göre bölmek.

```
"Berkan;35;İstanbul|Türkiye"
          ↑         ↑
     ';' veya '|' nerede? → IndexOfAny ile bul
```

```csharp
// Eski yol — her çağrıda new char[] oluşturulur
int idx = metin.IndexOfAny(new[] { ',', ';', '|' });
// new[] → her çağrıda heap allocation

// SearchValues — arama tablosu bir kez hazırlanır, hep kullanılır
private static readonly SearchValues<char> _ayraclar =
    SearchValues.Create(',', ';', '|');
// static readonly → uygulama başlarken bir kez kurulur
// içinde SIMD hızlandırmalı arama tablosu var — IndexOfAny'den çok daha hızlı

int idx2 = metin.AsSpan().IndexOfAny(_ayraclar);    // allocation yok, hızlı
```

**Kitabevi senaryosu:** Toplu kitap import dosyası parse ederken her satırda ayraç arama — SearchValues burada belirgin fark yaratır.

---

## Regex — IsMatch vs Compiled vs GeneratedRegex

**Regex nedir?** Bir string'in belirli bir kalıba uyup uymadığını kontrol eden örüntü dilidir.

Örnek: kullanıcı telefon alanına gerçekten 10 rakamlı numara mı girdi?  
Bunu `if` zincirleri yerine bir pattern tanımlarsın, .NET o kalıba göre kontrol eder.

```
^\d{10}$   →  ^ satır başı  \d rakam  {10} tam 10 tane  $ satır sonu
             yani: tam 10 rakamlı bir string mi?

"5321234567"  → eşleşir ✓
"532-123-45"  → eşleşmez ✗  (tire var, 10 rakam değil)
"abc"         → eşleşmez ✗
```

**Sorun:** Her çağrıda Regex bu pattern'ı baştan yorumlar → overhead.

```csharp
// Kötü — her API isteğinde pattern sıfırdan parse edilir
public bool TelefonGecerliMi(string numara)
{
    return Regex.IsMatch(numara, @"^\d{10}$");
    // 10k istek/sn → 10k kez aynı parse işlemi
}

// İyi — bir kez derle, hep kullan
private static readonly Regex _telefonRegex =
    new Regex(@"^\d{10}$", RegexOptions.Compiled);
// Compiled → pattern IL koduna derlenir, eşleşme çok hızlı
// static readonly → uygulama ömründe bir kez oluşturulur

public bool TelefonGecerliMi(string numara)
    => _telefonRegex.IsMatch(numara);

// En iyi (C# 11+ / .NET 7+) — derleme zamanında üretilir
public partial class SiparisValidator
{
    [GeneratedRegex(@"^\d{10}$")]
    private static partial Regex TelefonRegex();
    // GeneratedRegex → compile time'da C# kaynak kodu üretir
    // runtime'da parse yok, startup maliyeti sıfır, AOT uyumlu

    public bool TelefonGecerliMi(string numara)
        => TelefonRegex().IsMatch(numara);
}
```

**Kitabevi senaryosu:** Sipariş formunda telefon, e-posta ve ISBN doğrulama — her biri `GeneratedRegex` ile bir kez tanımlanır, tüm isteklerde sıfır parse maliyetiyle çalışır.

---

## CompositeFormat — Format String Cache

**Ne işe yarar?** `string.Format("Merhaba {0}, bakiyeniz: {1:C}", ...)` ifadesindeki format kalıbı her çağrıda baştan analiz edilir. Bunu önlemek için format kalıbını bir kez parse edip saklarsın.

```
"Merhaba {0}, bakiyeniz: {1:C}"
    → {0} nerede?  → {1} nerede?  → :C ne anlama geliyor?
    → Her çağrıda bu analiz tekrarlanır
```

```csharp
// Her çağrıda format analiz edilir
string s = string.Format("Merhaba {0}, bakiyeniz: {1:C}", ad, bakiye);

// CompositeFormat → bir kez parse et, sonsuza kadar kullan
private static readonly CompositeFormat _karsilamaMesaji =
    CompositeFormat.Parse("Merhaba {0}, bakiyeniz: {1:C}");
// static readonly → analiz bir kez yapıldı, sonuç saklandı

string s2 = string.Format(CultureInfo.CurrentCulture, _karsilamaMesaji, ad, bakiye);
// artık sadece değerleri yerleştiriyor — analiz yok
```

**Kitabevi senaryosu:** Her sipariş onayı e-postasında aynı format kalıbı kullanılıyorsa — 50k siparişte 50k parse işleminden kurtulursun.

---

## Özet — Ne Zaman Ne?

| Durum | Araç |
|---|---|
| 2-4 parça birleştirme | `+` veya interpolation |
| Döngüde birleştirme | `StringBuilder` |
| Boyutu bilinen, hot path | `string.Create` |
| Sabit char setinde arama | `SearchValues<T>` |
| Tekrarlı regex | `static readonly` + `Compiled` veya `GeneratedRegex` |
| Tekrarlı format string | `CompositeFormat` |

---

## Kontrol Soruları

1. Döngüde `+=` ile string birleştirmenin maliyeti neden katlanarak artar?
2. `SearchValues<T>` neden `static readonly` olarak tanımlanmalı?
3. `GeneratedRegex` ile `RegexOptions.Compiled` arasındaki fark nedir?
4. `string.Create` hangi durumda interpolation'dan üstündür?
