# Gün 11 — Reflection, Attributes ve Source Generators

---

## 1. Reflection Nedir?

Normal kodda bir sınıfın özelliklerini **yazarken** bilirsin — derleyici de bilir.  
Reflection ise sana "bir tipi **çalışma zamanında** incele" imkânı verir.

```csharp
Type tip = typeof(Kitap);
Console.WriteLine(tip.Name);        // "Kitap"
Console.WriteLine(tip.Namespace);   // "Kitabevi.Domain"

// Bu tipin tüm property'lerini listele
foreach (var prop in tip.GetProperties())
    Console.WriteLine(prop.Name);   // Id, Baslik, Fiyat, ...
```

Yani Reflection şunu sorar: **"Bu nesne, bu sınıf, bu metot hakkında ne biliyorsun?"** — ve cevabı runtime'da verir.

---

## 2. Neden Yavaş?

Reflection, CLR'ın tip sistemine her seferinde **string üzerinden** sorgu atar. Bu pahalı:

- `Type.GetMethod("KaydetAsync")` → CLR string karşılaştırması yapar
- Sonuçta gelen `MethodInfo` nesnesi heap'te oluşturulur
- `Invoke()` çağrısı normal metot çağrısından 10–50x yavaş olabilir

```csharp
// Her çağrıda arama — kötü
for (int i = 0; i < 1000; i++)
{
    var method = typeof(KitapServisi).GetMethod("Getir");
    method.Invoke(servis, new object[] { i });
}

// Cache'le — iyi
private static readonly MethodInfo _getirMethod =
    typeof(KitapServisi).GetMethod("Getir")!;

for (int i = 0; i < 1000; i++)
{
    _getirMethod.Invoke(servis, new object[] { i });
}
```

**Kural:** Reflection'ı sadece uygulama başlangıcında kullan, sonucu cache'le. Hot-path'te (her request'te çağrılan kodda) kullanma.

---

## 3. Attributes — Metadata Ekleme Sistemi

Attribute, bir sınıfa, metoda veya property'ye **ek bilgi yapıştırmanın** yolu. Kod çalışırken bu bilgileri Reflection ile okuyabilirsin.

Zaten sık kullandıkların var:

```csharp
[HttpGet]                   // ASP.NET Core: bu metot GET isteği alır
[Required]                  // DataAnnotations: bu alan zorunlu
[JsonIgnore]                // System.Text.Json: serialize etme
[Authorize]                 // bu endpoint login gerektirir
```

Bunların hepsi attribute — ve framework'ler bunları Reflection ile okuyup davranış sergiler.

---

## 4. Custom Attribute Yazmak

`Attribute` sınıfından türetirsen kendi attribute'unu yazabilirsin:

```csharp
// "Bu property audit log'a yazılsın" anlamında
[AttributeUsage(AttributeTargets.Property)]
public class AuditAttribute : Attribute
{
    public string Aciklama { get; }
    public AuditAttribute(string aciklama) => Aciklama = aciklama;
}

// Kullanım
public class Kitap
{
    public int Id { get; set; }

    [Audit("Başlık değiştirildi")]
    public string Baslik { get; set; } = "";

    [Audit("Fiyat güncellendi")]
    public decimal Fiyat { get; set; }
}
```

Şimdi bu attribute'u Reflection ile oku:

```csharp
var tip = typeof(Kitap);
foreach (var prop in tip.GetProperties())
{
    var audit = prop.GetCustomAttribute<AuditAttribute>();
    if (audit != null)
        Console.WriteLine($"{prop.Name}: {audit.Aciklama}");
}
// Baslik: Başlık değiştirildi
// Fiyat:  Fiyat güncellendi
```

Bu tam olarak EF Core'un `[Required]`, `[MaxLength]` gibi attribute'ları işleme şeklidir.

---

## 5. Activator.CreateInstance — Neden Yavaş?

Reflection ile nesne oluşturmanın yolu:

```csharp
// Normal yol — JIT optimize eder, hızlı
var kitap = new Kitap();

// Reflection yolu — her seferinde constructor arama, heap alloc
var kitap = (Kitap)Activator.CreateInstance(typeof(Kitap))!;
```

`Activator.CreateInstance` şunları yapar:
1. Tipin constructor'larını arar
2. Parametre tiplerini eşleştirir
3. Nesneyi oluşturur

Bunların hepsi runtime'da, her seferinde. Dependency Injection container'ları bunu **başlangıçta bir kez** yapar ve sonucu cache'ler — bu yüzden DI yavaş değildir.

---

## 6. Expression Trees — LINQ Provider'ların Temeli

Reflection ile bir metodu çağırabilirsin ama yavaş. **Expression Trees** ile aynı işi JIT-friendly şekilde yapabilirsin.

Expression Tree, kodun kendisini değil **kodun yapısını** temsil eder — adeta kod'un AST'ı.

```csharp
// Bu LINQ sorgusu bir Expression Tree:
Expression<Func<Kitap, bool>> expr = k => k.Fiyat < 50;

// EF Core bunu SQL'e çevirir:
// WHERE Fiyat < 50
```

EF Core sorguları hiçbir zaman bellekte çalışmaz — Expression Tree'yi alır, SQL'e çevirir ve veritabanına gönderir. Bunu normal delegate ile yapamazsın:

```csharp
// Func<Kitap, bool> — bellekte çalışır, SQL üretemez
Func<Kitap, bool> fonksiyon = k => k.Fiyat < 50;

// Expression<Func<Kitap, bool>> — EF Core SQL üretir
Expression<Func<Kitap, bool>> ifade = k => k.Fiyat < 50;

// IQueryable.Where → Expression bekler (veritabanı sorgusu)
dbContext.Kitaplar.Where(ifade).ToList();
```

Faz 2'de EF Core yazarken bunu tekrar göreceksin — `IQueryable` üzerindeki her LINQ çağrısı Expression Tree biriktirir, `ToList()` geldiğinde SQL'e döner.

---

## 7. Source Generators — Compile-Time Code Generation

Source Generator, C# 9 ile gelen bir özellik: **derleme sırasında** otomatik kod üretir.

Neden önemli? Reflection'ın yaptığı birçok şeyi **sıfır runtime maliyetiyle** yapabilirsin.

```csharp
// Bu attribute'u görünce Source Generator otomatik ToString() üretir
[AutoToString]
public partial class Kitap
{
    public int Id { get; set; }
    public string Baslik { get; set; } = "";
}

// Source Generator şunu üretir (derleme sırasında):
// public override string ToString() => $"Kitap {{ Id={Id}, Baslik={Baslik} }}";
```

.NET'in kendi kütüphanelerinde de kullanılır:

```csharp
// System.Text.Json — Reflection yerine Source Generator
[JsonSerializable(typeof(Kitap))]
public partial class KitapJsonContext : JsonSerializerContext { }

// Runtime'da Reflection yok — derleme sırasında serialize kodu üretildi
var json = JsonSerializer.Serialize(kitap, KitapJsonContext.Default.Kitap);
```

---

## 8. Hangisini Kullanmalısın?

| Yaklaşım | Maliyet | Ne zaman |
|---|---|---|
| **Reflection** | Runtime, yavaş | Başlangıçta, cache'leyerek |
| **Expression Trees** | Runtime, JIT-friendly | EF Core gibi query builder'larda |
| **Source Generator** | Compile-time, sıfır | Tekrarlayan boilerplate kodu için |

```
Reflection → sadece başlangıçta, cache'le
Expression Trees → runtime'da ama JIT-friendly
Source Generator → compile-time, zero runtime cost
```

---

## 9. Web Geliştirmede Nerede Görünür?

**ASP.NET Core — Model Binding:**

```csharp
// Framework, Reflection ile bu property'lere değer atar
public class KitapOlusturRequest
{
    [Required]
    [MaxLength(200)]
    public string Baslik { get; set; } = "";

    [Range(0, 9999)]
    public decimal Fiyat { get; set; }
}
```

ASP.NET Core, `[Required]` ve `[Range]` attribute'larını Reflection ile okur, validation uygular.

**EF Core — Entity Konfigürasyonu:**

```csharp
public class Kitap
{
    [Key]
    public int Id { get; set; }

    [MaxLength(200)]       // EF Core: VARCHAR(200) üretir
    [Required]             // EF Core: NOT NULL üretir
    public string Baslik { get; set; } = "";
}
```

EF Core bu attribute'ları Reflection ile okur, migration oluştururken SQL şemasına yansıtır.

**Dependency Injection:**

```csharp
// DI container başlangıçta Reflection ile constructor'ı analiz eder
// "KitapServisi'nin KonstruktorI IKitapRepository istiyor"
// Sonucu cache'ler — her request'te yeniden okumaz
builder.Services.AddScoped<KitapServisi>();
```

---

## 10. Kontrol Soruları

1. Reflection neden yavaştır? Nasıl optimize edilir?

2. Şu iki satır arasındaki fark nedir?
   ```csharp
   Func<Kitap, bool> f1 = k => k.Fiyat < 50;
   Expression<Func<Kitap, bool>> f2 = k => k.Fiyat < 50;
   ```
Func<Kitap, bool> f1: Bu bir Delegatedir. Derlenmiş, executable (çalıştırılabilir) bir koddur. Bellekteki bir listenin içinde "şu koşula uyanı bul" demek için doğrudan CPU tarafından işletilir.

Expression<Func<Kitap, bool>> f2: Bu bir Veri Yapısıdır (Data Structure). Kodun kendisi değil, kodun ağaç yapısındaki şemasıdır. İçinde "Fiyat özelliği 50'den küçük mü?" bilgisini bir nesne hiyerarşisi olarak tutar.

3. EF Core neden `IQueryable.Where()` içindeki lambda'yı SQL'e çevirebilirken, bellekteki `List.Where()` çeviremez?

List.Where() metodu Func bekler. Ona verdiğiniz lambda çoktan derlenmiş bir koddur; EF Core bu kodun içine bakıp "Burada hangi kolon kullanılmış?" diye anlayamaz.

IQueryable.Where() metodu Expression bekler. EF Core (veya ilgili Provider), bu ifade ağacını (Expression Tree) tarayarak düğüm düğüm okur: "Bak burada bir 'Fiyat' mülkü var, burada bir 'Küçüktür' operatörü var..." der ve bu parçaları SQL metnine (SELECT ... WHERE Fiyat < 50) tercüme eder.

4. `[Required]` attribute'unu hem ASP.NET Core hem de EF Core kullanıyor — ikisi nasıl aynı attribute'dan farklı davranış çıkarır?
ASP.NET Core (Validation Layer): Bir Request geldiğinde, Model Binder bu attribute'a bakar. Eğer alan boşsa ModelState.IsValid değerini false yapar.

EF Core (Migration Layer): Veritabanı şemasını oluştururken bu attribute'a bakar ve SQL'deki kolonun yanına NOT NULL kısıtlamasını ekler.
5. Source Generator ile Reflection arasındaki temel fark ne?
Source Generator, Reflection'ın yaptığı "kod inceleme" işini derleme aşamasında yapıp, o işi yapacak kodu önceden yazdığı için çok daha moderndir.
