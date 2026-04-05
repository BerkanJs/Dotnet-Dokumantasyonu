# Gün 4 — struct, class, record: Ne Zaman Ne Kullanılır?

---

## 1. Gün 2'den Hatırlayalım

Gün 2'de iki tip gördük:

- `struct` → value type. Kopyaladığında içindeki değerler kopyalanır. İkisi bağımsız olur.
- `class` → reference type. Kopyaladığında aynı nesneye iki farklı isimle bakarsın. Birini değiştirirsen diğeri de değişir.

Bugün buna `record` ekleyeceğiz. Ama önce neden `record`'a ihtiyaç duyulduğunu anlayalım.

---

## 2. Sorun: class ile Veri Taşımak Zor

Bir API yazdın. Frontend kitap bilgisi istiyor, sen de şu class'ı döndürüyorsun:

```csharp
class KitapResponse
{
    public string Baslik { get; set; }
    public decimal Fiyat { get; set; }
}
```

Şimdi iki tane `KitapResponse` oluşturdun, ikisi de aynı bilgiyi taşıyor:

```csharp
var a = new KitapResponse { Baslik = "Clean Code", Fiyat = 45m };
var b = new KitapResponse { Baslik = "Clean Code", Fiyat = 45m };

Console.WriteLine(a == b);  // false
```

**Neden `false`?** Çünkü `class` referans tipi. `a` ve `b` heap'te iki farklı nesne. İçerikleri aynı olsa da CLR "bunlar farklı nesneler" der. Seni ilgilendiren içerik aynı mı diye sormak istiyordun, ama `class` bunu yapmıyor.

Bunu düzeltmek için `Equals` override etmen gerekir — her sınıf için tekrar tekrar. Sıkıcı ve hata yapmaya açık.

---

## 3. record Nedir?

`record`, veri taşımak için özel olarak tasarlanmış bir tür. Senin yerine şunları otomatik halleder:

- İçerik karşılaştırması (`==` çalışır, içeriğe bakar)
- `ToString()` (ekrana yazdırınca alanları gösterir)
- Kopyalama (`with` ifadesi — aşağıda açıklıyorum)

**Tanımlama:**

```csharp
record KitapResponse(string Baslik, decimal Fiyat);
```

Bu tek satır. Aşağıdaki `class`'la aynı işi yapar, üstüne otomatik karşılaştırma ve kopyalama da ekler:

```csharp
class KitapResponse
{
    public string Baslik { get; }
    public decimal Fiyat { get; }

    public KitapResponse(string baslik, decimal fiyat)
    {
        Baslik = baslik;
        Fiyat = fiyat;
    }

    // Equals, GetHashCode, ToString... bunları da kendin yazman gerekirdi
}
```

**İçerik karşılaştırması:**

```csharp
var a = new KitapResponse("Clean Code", 45m);
var b = new KitapResponse("Clean Code", 45m);

Console.WriteLine(a == b);  // true — içerik aynı, record bunu anlıyor
```

---

## 4. record Değiştirilemez (Immutable)

`record` ile oluşturduğun nesneyi sonradan değiştiremezsin:

```csharp
var kitap = new KitapResponse("Clean Code", 45m);
kitap.Fiyat = 36m;  // HATA — derlenmez
```

Neden değiştirilemez? Çünkü `record` veri taşıma amacıyla tasarlandı. Bir API response veya istek modeli oluşturuldu mu, değişmemeli. Bu sayede "bir yerde değiştirildi mi acaba?" diye düşünmene gerek kalmaz.

---

## 5. with İfadesi — "Şunu Değiştirerek Yeni Bir Tane Ver"

Peki değer değiştiremiyorsan, güncelleme nasıl yapacaksın?

`with` ifadesi şunu yapar: **orijinali olduğu gibi bırakır, sadece belirttiğin alanları değiştirerek yeni bir kopya oluşturur.**

```csharp
var kitap = new KitapResponse("Clean Code", 45m);

// Fiyatı değiştirilmiş yeni bir kopya istiyorum
var indirimli = kitap with { Fiyat = 36m };

Console.WriteLine(kitap.Fiyat);     // 45 — orijinal dokunulmadı
Console.WriteLine(indirimli.Fiyat); // 36 — yeni kopya
```

Bunu şöyle düşün: Bir form doldurdun. `with` "bu formun kopyasını çıkar, sadece şu alanı değiştir" demek. Orjinal form hâlâ yerinde duruyor.

**Neden yararlı?**

Diyelim ki aynı kitabın orijinal ve indirimli fiyatlı versiyonunu göstermek istiyorsun. `with` ile orijinal nesneyi bozmadan yeni bir versiyon üretirsin.

---

## 6. class mı, record mı? — Hangisini Ne Zaman Kullanırsın?

**`class` kullan** — davranışı olan, değişebilen nesneler için:

```csharp
class KitapService   // iş mantığı burada, fonksiyonlar var
class KitapRepository // veritabanı işlemleri
class KitapController // HTTP isteklerini karşılar
class Kitap           // EF Core entity — veritabanına yazılır, güncellenir
```

**`record` kullan** — sadece veri taşıyan, değişmemesi gereken nesneler için:

```csharp
record KitapResponse(int Id, string Baslik, decimal Fiyat);  // API'den dönen cevap
record CreateKitapRequest(string Baslik, string Yazar, decimal Fiyat);  // API'ye gelen istek
record KitapDto(string Baslik, decimal Fiyat);  // katmanlar arası veri taşıma
```

Kısa kural:

```
Bir şey yapıyor musun?      → class
Sadece veri taşıyor musun?  → record
```

---

## 7. EF Core Entity Neden class Olmalı?

EF Core, veritabanından okuduğu nesneyi sonradan güncellemeni bekler:

```csharp
var kitap = await db.Kitaplar.FindAsync(1);
kitap.Fiyat = 50m;  // değiştiriyoruz
await db.SaveChangesAsync();  // güncelleme veritabanına yazıldı
```

`record` bunu yapamaz — değiştirilemez. Bu yüzden EF Core entity'leri `class` olmalı.

---

## 8. struct — Kısaca

Web geliştirmede kendi `struct`'ını nadiren yazarsın. Ama .NET'in içinde çok var: `DateTime`, `Guid`, `int`, `bool` hepsi struct.

Ne zaman yazarsın? Çok küçük, çok sık oluşturulan, sadece veri tutan şeyler için. Örnek: 2D koordinat noktası, renk değeri (RGBA).

Web geliştirmede yeterli kural: **bir şeyin `struct` olduğunu görürsen**, kopyalandığında bağımsız kopya oluştuğunu bil.

---

## 9. Düşük Seviye: readonly struct, ref struct

Bu ikisi performans optimizasyonu için kullanılır. Web API yazarken ihtiyaç duymassın, sadece isimlerini bil:

- **`readonly struct`:** Struct'ın alanlarının değiştirilememesini garanti eder.
- **`ref struct`:** Sadece stack'te yaşar. `Span<T>` bu şekilde tanımlı — Gün 2'de gördüğümüz.

---

## 10. Özet Tablo

| | `class` | `record` | `struct` |
|---|---|---|---|
| Tip | Reference | Reference (ama değer karşılaştırır) | Value |
| Değiştirilebilir mi? | Evet | Hayır (varsayılan) | Evet |
| `==` ne karşılaştırır? | Adres | İçerik | İçerik |
| Web'de kullanım | Controller, Service, Entity | DTO, Request, Response | Nadiren |

---

## 11. Kontrol Soruları

1. `record` tanımlarken `class`'tan ne fark ederek yazarsın? Syntax'ta ne değişiyor?

record genelde api cagrılarında response requestte kullanırız değiştirilemez degiskenleri tanımlamak için sadece veri taşıyan nesnelerde kullanılır with ile aynı nesneden degisken olusturulup farklı tanımlanabilir

2. Şu kodu düşün — çıktı ne olur, neden?
   ```csharp
   var a = new KitapResponse("Clean Code", 45m);
   var b = new KitapResponse("Clean Code", 45m);
   Console.WriteLine(a == b);

   ```

recorda true class ile false cıkar cunku a ile b nin adresi classda farklı recordda aynı olur 

3. `with` ifadesi orijinal nesneyi değiştirir mi? Ne döndürür?
hayır orijinalden bir kopya alıp degistirirz 

4. EF Core entity'sini `record` yapsan ne olur?
EF core entity değişime acık olmalı veri tabanına yazılması gerekiyor ona ne iletirsek bu durumda record tutulmaması lazım 

5. API'ye gelen bir istek modelini (örn. `CreateKitapRequest`) neden `record` yaparsın?
çünkü sadece getir yapıyor veri tasıyor veri degismiyor bu yüzden record yapmak clean code 