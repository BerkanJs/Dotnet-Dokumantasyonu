# Gün 5 — String ve Koleksiyonlar: Performans Tuzakları

---

## 1. String Nedir? Neden Değiştirilemez?

`string` C#'ta bir `class` — yani reference type. Ama diğer class'lardan farklı davranır: **bir kez oluşturunca değiştiremezsin.**

```csharp
string isim = "Berkan";
isim = isim + " Yılmaz";  // bu isim'i değiştirmiyor
                           // "Berkan Yılmaz" adında yeni bir string oluşturuyor
                           // "Berkan" hâlâ heap'te, GC silinceye kadar
```

Her `+` işlemi yeni bir string nesnesi üretir. Orijinal string dokunulmaz.

**Neden böyle tasarlandı?** Güvenlik ve öngörülebilirlik. Bir string'i birden fazla yerde kullanıyorsun — birinin değiştirmesi diğerini etkilemesin.

---

## 2. String Birleştirme Tuzağı — StringBuilder

Döngü içinde string birleştirirsen ciddi performans sorunu çıkar:

```csharp
// KÖTÜ — her iterasyonda yeni string, önceki atılır
string sonuc = "";
for (int i = 0; i < 10000; i++)
{
    sonuc += i.ToString();  // 10000 yeni string nesnesi oluştu
}
```

10.000 iterasyonda 10.000 string nesnesi heap'e gider. GC bunları toplamak zorunda kalır.

**Çözüm: StringBuilder**

```csharp
// İYİ — tek nesne üzerinde çalışır
var sb = new StringBuilder();
for (int i = 0; i < 10000; i++)
{
    sb.Append(i);  // aynı nesneye ekler
}
string sonuc = sb.ToString();  // en sonda bir kez string'e çevirir
```

`StringBuilder` içinde büyüyen bir char dizisi tutar, döngü bittikten sonra tek seferlik string üretir.

**Web geliştirmede ne zaman karşılaşırsın?**

HTML veya SQL string'i elle oluşturuyorsan (template engine yoksa), CSV export yapıyorsan. Ama modern web'de genellikle `string.Join`, interpolation veya `StringBuilder` yeterlidir. Çok nadir elle yazarsın.

---

## 3. String Karşılaştırma — Küçük Ama Önemli

```csharp
string a = "kitap";
string b = "Kitap";

Console.WriteLine(a == b);  // false — büyük/küçük harf duyarlı
Console.WriteLine(string.Equals(a, b, StringComparison.OrdinalIgnoreCase));  // true
```

API'de kullanıcıdan gelen veriyi karşılaştırırken büyük/küçük harf farkını düşün. `OrdinalIgnoreCase` bu iş için standart.

---

## 4. Koleksiyonlar — Array, List, IEnumerable

### Array

Sabit boyutlu, tip güvenli, hızlı erişim:

```csharp
int[] sayilar = new int[5];  // 5 elemanlı, sonradan büyütemezsin
sayilar[0] = 10;
```

Web geliştirmede doğrudan `int[]` pek kullanmazsın. Ama `byte[]` ile dosya/binary işlemlerinde görürsün.

### List\<T\>

Dinamik boyutlu, en sık kullandığın koleksiyon:

```csharp
var kitaplar = new List<KitapDto>();
kitaplar.Add(new KitapDto("Clean Code", 45m));
kitaplar.Add(new KitapDto("DDD", 60m));
```

API'den liste dönerken, veritabanından veri çekerken hep `List<T>`.

---

## 5. IEnumerable\<T\> — "Üzerinden Geçilebilir" Sözleşmesi

`IEnumerable<T>` bir interface. "Bu koleksiyonun elemanlarını tek tek verebilirim" demek.

`List<T>`, `Array`, `string[]` hepsi `IEnumerable<T>` implement eder. Yani bunların hepsine `foreach` yapabilirsin.

**Önemli özelliği: Deferred Execution (Ertelenmiş Çalışma)**

`IEnumerable<T>` üzerinde LINQ kullanırken sorgu hemen çalışmaz. "Çalış" dediğinde çalışır.

```csharp
var kitaplar = new List<string> { "Clean Code", "DDD", "SICP" };

// Bu satırda hiçbir şey çalışmıyor — sadece tarif yazıldı
var uzunlar = kitaplar.Where(k => k.Length > 5);

// Burada çalışıyor — foreach tetikledi
foreach (var k in uzunlar)
    Console.WriteLine(k);
```

`uzunlar` bir sorgu tarifi. `foreach`'e kadar tek bir eleman filtrelenmedi. Buna **lazy evaluation** denir.

**Neden önemli?** Sorguyu bir yerde tanımlayıp başka yerde çalıştırabilirsin. Ve IQueryable ile bu çok daha kritik bir hal alıyor.
 
---

## 6. IQueryable\<T\> — EF Core'un Kalbi

Bu fark web geliştirmede **en kritik** koleksiyon bilgisidir. EF Core kullanırken mutlaka anlaşılması lazım.

Önce sorunu görelim:

```csharp
// KÖTÜ — veritabanından TÜM kitapları çekiyor, sonra C#'ta filtreliyor
List<Kitap> tumKitaplar = await db.Kitaplar.ToListAsync();  // SELECT * FROM Kitaplar
var ucuzlar = tumKitaplar.Where(k => k.Fiyat < 50);        // C#'ta filtreleniyor
```

10.000 kitap varsa hepsi RAM'e gelir, sonra C#'ta filtre yapılır. Veritabanı boşuna çalışmış olur.

```csharp
// İYİ — filtre veritabanında yapılıyor
IQueryable<Kitap> sorgu = db.Kitaplar.Where(k => k.Fiyat < 50);
List<Kitap> ucuzlar = await sorgu.ToListAsync();
// SELECT * FROM Kitaplar WHERE Fiyat < 50
```

**Fark ne?**

- `IEnumerable<T>` → filtre C#'ta yapılır. Veri önce RAM'e gelir.
- `IQueryable<T>` → filtre SQL'e çevrilir. Veri veritabanında filtrelenir, sadece sonuç RAM'e gelir.

EF Core `IQueryable` üzerinde `.Where()`, `.OrderBy()`, `.Select()` çağırırsan bunları SQL'e çevirir. `.ToList()` veya `.ToListAsync()` dediğinde SQL çalışır ve sonuç gelir.

```csharp
// Bu zincir bir SQL üretir — tek sorguda:
var kitaplar = await db.Kitaplar
    .Where(k => k.Fiyat < 100)
    .OrderBy(k => k.Baslik)
    .Select(k => new KitapDto(k.Baslik, k.Fiyat))
    .ToListAsync();

// Üretilen SQL:
// SELECT Baslik, Fiyat FROM Kitaplar
// WHERE Fiyat < 100
// ORDER BY Baslik
```

**Kural:**

```
ToList() çağırmadan önce filtrele
→ SQL veritabanında çalışır, sadece ihtiyacın olan veri gelir

ToList() çağırdıktan sonra filtrele
→ tüm veri RAM'e gelir, C#'ta filtre yapılır — gereksiz yük
```

---

## 7. IEnumerable vs IQueryable — Özet

| | `IEnumerable<T>` | `IQueryable<T>` |
|---|---|---|
| Nerede çalışır? | C# (bellekte) | Veritabanında (SQL) |
| Ne zaman gelir veri? | `foreach` veya LINQ terminasyonunda | `.ToList()` çağrısında |
| EF Core'da | `.ToList()` sonrası | `.ToList()` öncesi |
| Kullanım | In-memory koleksiyonlar | ORM sorguları |

---

## 8. Event Handler Memory Leak

Bu biraz farklı bir konu ama önemli. Bir nesne başka bir nesnenin event'ine abone olursa, abone olan nesne GC tarafından silinemiyor.

```csharp
class SiparisServisi
{
    public event Action SiparisVerildi;
}

class BildirimServisi
{
    public BildirimServisi(SiparisServisi siparis)
    {
        siparis.SiparisVerildi += Bildir;  // abone oldu
    }

    void Bildir() { /* ... */ }
}
```

`BildirimServisi` nesnesi kullanılmasa bile, `SiparisServisi` onu tutuyor — `SiparisVerildi` event'i referans veriyor. GC silemez. Memory leak.

**Çözüm:** İşin bitince `-=` ile abonelikten çık, ya da `IDisposable` implement edip `Dispose()` içinde çık.

Web geliştirmede static event'ler veya uzun yaşayan servislerdeki event'ler bu soruna yol açar. DI ile çalışırken lifetime'lara dikkat etmek bu yüzden önemli — Gün sonunda göreceğiz.

---

## 9. Web Geliştirmede Özet

- **String birleştirme döngüsü** → `StringBuilder` kullan
- **String karşılaştırma** → `OrdinalIgnoreCase` ile yap
- **Listeler** → `List<T>` çoğunluk için yeterli
- **EF Core sorguları** → `ToList()` öncesi filtrele, `IQueryable` üzerinde çalış
- **Event subscription** → kullanmayacaksan abonelikten çık

---

## 10. Kontrol Soruları

1. Döngü içinde `string +=` yapmak neden performans sorunu yaratır?
her bir sırada string olusturup heap'e atar 10000 iterasyonda 10000 string olusturur 

2. `IEnumerable<T>` ile `IQueryable<T>` arasındaki fark nedir? EF Core'da neden önemli?
biri ramde calısıyor oteki dbde Inumerable veriyi tarhi edip foreach ile bize döner filtrelemeyi ramde yapar Queryable da sql sorgusu gibi calısır 

3. Şu iki kodu karşılaştır — hangisi daha verimli, neden?
   ```csharp
   // A:
   var liste = db.Kitaplar.ToList().Where(k => k.Fiyat < 50);

   // B:
   var liste = db.Kitaplar.Where(k => k.Fiyat < 50).ToList();
   ```

ikincisi daha iyi birinci tüm kitapları toList ile getirip sonra filtreliyor 


4. Deferred execution ne demek? `IEnumerable` üzerindeki LINQ ne zaman çalışır?
var result = list.Where(x => x > 10);
burada hiçbir şey calısmaz  result.ToList();
result.Count();
result.First();
 bu gibi seyler lazım calısması için 

 
5. Event handler neden memory leak'e yol açar?

abonelik sistemi var bir class var diyelim bu class bir nesne tanımlasın 2. class da bu nesneyi kendi içinde cagırıp kullansın 2. class hiç kullanılmasa bile 1. class silinmedici sürece GC silmez 2. classı bu da memory leak'e yol açar 