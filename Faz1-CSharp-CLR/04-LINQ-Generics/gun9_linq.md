# Gün 9 — LINQ: Derinlemesine ve Performans Tuzakları

---

## 1. LINQ Nedir, Gerçekte Ne Yapıyor?

LINQ (Language Integrated Query), koleksiyonlar üzerinde sorgu yazmak için C#'ın sunduğu bir yöntem. İki yazım biçimi var:

```csharp
var kitaplar = new List<Kitap> { ... };

// Query syntax — SQL'e benzer
var sorgu1 = from k in kitaplar
             where k.Fiyat < 50
             orderby k.Baslik
             select k;

// Method syntax — daha yaygın kullanılan
var sorgu2 = kitaplar
    .Where(k => k.Fiyat < 50)
    .OrderBy(k => k.Baslik);
```

İkisi de **aynı IL kodunu üretir**. Method syntax daha yaygın — pratikte hep onu kullanırsın.

LINQ aslında **extension method** kullanır. `Where`, `Select`, `OrderBy` gibi metodlar `IEnumerable<T>` interface'ine sonradan eklenmiş metodlardır. Extension method ne demek — aşağıda göreceğiz.

---

## 2. Extension Method — LINQ'nun Sırrı

Extension method, var olan bir tipe yeni metot eklemek demek. Kaynak koduna dokunmana gerek yok.

```csharp
// string tipine yeni metot ekliyoruz
static class StringExtensions
{
    public static string BuyukHarfle(this string s)
        => s.ToUpper();
}

// Kullanım — sanki string'in kendi metoduymuş gibi
string isim = "berkan";
Console.WriteLine(isim.BuyukHarfle());  // BERKAN
```

`this string s` — "bu metot string'e ait gibi çağrılabilir" anlamına geliyor.

LINQ'daki tüm metodlar (`Where`, `Select` vb.) tam olarak böyle tanımlanmış. `IEnumerable<T>`'ye yazılmış extension metodlar. Bu yüzden her listede, dizide, sorguda çalışıyor.

---

## 3. Sık Kullanılan LINQ Metodları

Kitabevi örneği üzerinden:

```csharp
var kitaplar = db.Kitaplar.ToList(); // demo için bellekte çalışıyoruz
```

**Filtreleme:**
```csharp
// Where — koşula uyanları al
var ucuzlar = kitaplar.Where(k => k.Fiyat < 50);

// First — ilkini al, yoksa exception
var ilk = kitaplar.First(k => k.Fiyat < 50);

// FirstOrDefault — ilkini al, yoksa null
var ilkVeyaNull = kitaplar.FirstOrDefault(k => k.Fiyat < 50);

// Single — tam olarak bir tane olmalı, yoksa veya fazlaysa exception
var tekKitap = kitaplar.Single(k => k.Id == 1);
```

**Dönüştürme:**
```csharp
// Select — her elemanı başka bir forma çevir
var isimler = kitaplar.Select(k => k.Baslik);

// Select ile DTO dönüşümü — web'de çok kullanılır
var dtoListesi = kitaplar.Select(k => new KitapDto(k.Baslik, k.Fiyat));
```

**Sıralama:**
```csharp
var ucuzdanPahaliya = kitaplar.OrderBy(k => k.Fiyat);
var pahalidanUcuza  = kitaplar.OrderByDescending(k => k.Fiyat);

// Çoklu sıralama
var siralı = kitaplar
    .OrderBy(k => k.Yazar)
    .ThenBy(k => k.Baslik);
```

**Gruplama:**
```csharp
// Yazara göre grupla
var yazaraGore = kitaplar.GroupBy(k => k.Yazar);

foreach (var grup in yazaraGore)
{
    Console.WriteLine($"Yazar: {grup.Key}");
    foreach (var k in grup)
        Console.WriteLine($"  - {k.Baslik}");
}
```

**Kontrol:**
```csharp
bool hepsiUcuz = kitaplar.All(k => k.Fiyat < 100);
bool biriUcuz  = kitaplar.Any(k => k.Fiyat < 50);
int  sayi      = kitaplar.Count(k => k.Fiyat < 50);
```

---

## 4. Deferred Execution — Gün 5'ten Daha Derin

Gün 5'te gördük: LINQ sorgusu `foreach` veya `ToList()` çağrılana kadar çalışmaz.

Bunu biraz daha ilerletelim:

```csharp
var kaynak = new List<int> { 1, 2, 3, 4, 5 };

var sorgu = kaynak.Where(x => x > 2);  // henüz çalışmadı

kaynak.Add(6);  // listeye yeni eleman ekledik

// Şimdi çalıştırıyoruz — 6'yı da görüyor
foreach (var x in sorgu)
    Console.Write(x + " ");  // 3 4 5 6
```

Sorgu tanımlandığında listeye baktı ama işlemedi. `foreach` çalıştığında listeye tekrar baktı — o an 6 da vardı.

**Ne zaman sorun?** Aynı sorguyu iki kez iterate edersen — **multiple enumeration**:

```csharp
IEnumerable<Kitap> sorgu = kitaplar.Where(k => k.Fiyat < 50);

int adet = sorgu.Count();    // 1. pass — tüm koleksiyonu taradı
var ilk  = sorgu.First();    // 2. pass — tekrar taradı

// Çözüm: bir kez materialize et
var liste = sorgu.ToList();
int adet = liste.Count;      // anlık, pass yok
var ilk  = liste.First();    // anlık
```

---

## 5. yield return — Lazy Iterator

`yield return` kendi LINQ-benzeri metodlarını yazmanı sağlar. Tüm koleksiyonu bellekte tutmadan eleman eleman üretir.

```csharp
IEnumerable<int> SadeceCiftler(IEnumerable<int> sayilar)
{
    foreach (var s in sayilar)
    {
        if (s % 2 == 0)
            yield return s;  // sadece bu elemanı ver, devam et
    }
}

// Kullanım
foreach (var c in SadeceCiftler(new[] { 1, 2, 3, 4, 5 }))
    Console.Write(c + " ");  // 2 4
```

`yield return` ile metot her çağrıldığında kaldığı yerden devam eder. Tüm listeyi bellekte tutmadan büyük veri setlerini işleyebilirsin.

Web'de gerçek kullanımı: büyük dosyaları satır satır okumak, sayfalandırılmış veri akışı.

---

## 6. Performans Tuzakları — Bunları Ezberle

### Any() vs Count()

```csharp
// KÖTÜ — tüm koleksiyonu sayıyor
if (kitaplar.Count() > 0) { }

// İYİ — ilk elemanı bulunca durur
if (kitaplar.Any()) { }
```

`Count()` sona kadar gider. `Any()` bir eleman bulunca durur. EF Core'da da fark yaratır: `COUNT(*)` vs `EXISTS`.

### FirstOrDefault() vs First()

```csharp
// KÖTÜ — eleman yoksa InvalidOperationException fırlatır
var kitap = kitaplar.First(k => k.Id == 99);

// İYİ — eleman yoksa null döner
var kitap = kitaplar.FirstOrDefault(k => k.Id == 99);
if (kitap is null) return NotFound();
```

Web API'de bulunamayan kaynaklar için `First()` kullanma — exception'ı sen yakalamazsın, 500 döner.

### Select İçinde ToList()

```csharp
// KÖTÜ — gereksiz ara liste oluşturuyor
var sonuc = kitaplar
    .Select(k => k.Baslik)
    .ToList()              // ara liste — gereksiz
    .Where(b => b.Length > 5)
    .ToList();

// İYİ — zinciri tamamla, sonra bir kez ToList()
var sonuc = kitaplar
    .Select(k => k.Baslik)
    .Where(b => b.Length > 5)
    .ToList();
```

### Multiple Enumeration

```csharp
// KÖTÜ — sorgu iki kez çalışıyor
IEnumerable<Kitap> sorgu = GetKitaplar();
if (sorgu.Any())               // 1. pass
    var ilk = sorgu.First();   // 2. pass

// İYİ — bir kez materialize et
var liste = GetKitaplar().ToList();
if (liste.Any())
    var ilk = liste.First();
```

---

## 7. LINQ Zinciri — Kaç Pass?

```csharp
kitaplar
    .Where(k => k.Fiyat < 50)    // pass değil, tarif
    .OrderBy(k => k.Baslik)      // pass değil, tarif
    .Select(k => k.Baslik)       // pass değil, tarif
    .ToList();                   // burada 1 pass — hepsi birlikte
```

Zincir tek bir geçişte çalışır. Her eleman sırayla `Where` → `OrderBy` → `Select`'ten geçer. Üç ayrı pass değil.

Ama `OrderBy` sıralama için tüm koleksiyonu görmek zorunda — bu `ToList()`'e benzer bir etki yaratır. `Where` önce gelirse filtrelenmiş küçük set sıralanır — daha verimli.

---

## 8. Web Geliştirmede Özet

- **Her DTO dönüşümü** → `.Select(k => new KitapDto(...))`
- **Null güvenliği** → `FirstOrDefault` + null check
- **Varlık kontrolü** → `Any()`, asla `Count() > 0` değil
- **EF Core'da** → zinciri `ToList()` öncesi tamamla, sonra materialize et
- **Aynı sorguyu iki kez kullanacaksan** → önce `.ToList()` ile materialize et

---

## 9. Kontrol Soruları

1. `First()` ile `FirstOrDefault()` arasındaki fark nedir? Web API'de neden `FirstOrDefault` tercih edilir?
First bulamazsa exeption fırlatır yakalayamazsak 500 döner sıkıntı firstordefault null fırlatır 
2. `Any()` neden `Count() > 0`'dan daha verimli?
any bir elemanı bulunca durur count devam eder
3. Şu kodda kaç kez veritabanına gidilir? Nasıl düzeltirsin?
   ```csharp
   IEnumerable<Kitap> sorgu = db.Kitaplar.Where(k => k.Fiyat < 50);
   var adet = sorgu.Count();
   var liste = sorgu.ToList();
   ```
ilk veri cekilir ve .Tolist yapılır onun üstüne sorgu atılır her sorguda tekrar veri çekmemis oluruz bu kodda 2 kez gidiliyor
4. Extension method nedir? LINQ metodları neden her koleksiyonda çalışır?
cunku sonradan eklenmislerdir mesela string kütüphanesine de method ekleyebilirisin extention olarak bunlar sanki oraya ait gibi calısır linq methodlar da böyle 
5. `yield return` ne işe yarar? `return new List<T>()` yerine ne zaman tercih edilir?
yield return oldugunda eğer istenilen sorgu gerçeklestiğinde index numarası saklanır ve sorgu o indexten devam eder tekrar 0'ıncı indexten sorgu atılmaz
büyük veri setlerinde yield return daha iyi 