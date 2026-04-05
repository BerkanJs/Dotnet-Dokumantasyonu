# Gün 2 — Stack, Heap ve Bellek Modeli

---

## 1. Bellek Neden Önemli?

Program çalışırken tüm değişkenler, nesneler, fonksiyon çağrıları bir yerde tutulur. Bu yer bilgisayarın RAM'i. Ama RAM'in tamamı aynı şekilde kullanılmaz — CLR iki farklı bölge kullanır: **Stack** ve **Heap**.

Bu ikisinin farkını anlamak, ileride GC, performans ve bellek sızıntılarını anlamanın temelidir.

---

## 2. Stack Nedir?

Stack, **tabak yığını** gibi düşün. Bir tabak koyarsın, üstüne bir tane daha, üstüne bir tane daha. Almak istediğinde en üstteki tabağı alırsın — altındakini alamazsın.

Program çalışırken her fonksiyon çağrısı stack'e bir "çerçeve" (frame) koyar. Fonksiyon bitince o çerçeve stack'ten kalkar.

```
Toplama(5, 3) çağrılır
  → Stack'e frame eklenir: a=5, b=3
  → Hesap yapılır
  → Fonksiyon biter, frame stack'ten kalkar
```

**Stack'in özellikleri:**
- Çok hızlı — sadece bir sayacı artırıp azaltmak
- Otomatik temizlenir — fonksiyon bitince her şey gider
- Boyutu sınırlı — genellikle 1-8 MB (stack overflow hatası buradan gelir)
- Sıralı yap — son giren ilk çıkar (LIFO)

---

## 3. Heap Nedir?

Heap, **büyük bir depo** gibi. İstediğin zaman içine koy, istediğin zaman al. Sıra yok, düzen yok.

Heap daha büyük ve esnektir ama yönetimi karmaşık. Kim ne zaman koydu, ne zaman kaldıracak? İşte CLR'nin GC (Garbage Collector) dediğimiz parçası bunu halleder. GC'yi Gün 3'te detaylı göreceğiz.

**Heap'in özellikleri:**
- Büyük alan — RAM kadar büyüyebilir
- Yönetimi GC'ye bırakılır
- Stack'e göre daha yavaş erişim
- Nesneler burada "yaşar"

---

## 4. Value Types — Stack'te Yaşayan Türler

**Value type** nedir? Değeri doğrudan içinde taşıyan tür. Kopyalandığında değerin kendisi kopyalanır.

C#'ta value type'lar:
- `int`, `long`, `double`, `bool`, `char`
- `DateTime`, `Guid`
- `struct` ile tanımladığın tipler

```csharp
int a = 5;
int b = a;  // a'nın değeri kopyalandı
b = 10;     // a hâlâ 5, b 10 oldu
```

`a` ve `b` tamamen bağımsız. Birini değiştirmek diğerini etkilemez.

**`struct` nedir?**

`struct`, kendi tanımladığın value type. Örneğin:

```csharp
struct Nokta
{
    public int X;
    public int Y;
}

Nokta n1 = new Nokta { X = 1, Y = 2 };
Nokta n2 = n1;  // n1'in tüm değerleri kopyalandı
n2.X = 99;      // n1.X hâlâ 1
```
Eğer Nokta bir class ise:
class referans tipidir.
n2 = n1 dediğinizde aslında iki değişken de aynı nesneyi gösteriyor.
Eğer Nokta bir class olsaydı, n2.X = 99 yaptığınızda n1.X de 99 olurdu, çünkü ikisi de aynı nesneye işaret ediyor.
Eğer Nokta bir struct ise:
struct değer tipidir.
n2 = n1 dediğinizde n1’in değerleri kopyalanır, ayrı bir nesne oluşur.
---

## 5. Reference Types — Heap'te Yaşayan Türler

**Reference type** nedir? Değeri doğrudan taşımaz, değerin nerede olduğunu (adresini) taşır. Kopyalandığında adres kopyalanır, değer değil.

C#'ta reference type'lar:
- `class` ile tanımladığın tipler
- `string`
- `array` (int[] bile olsa)

```csharp
class Nokta
{
    public int X;
    public int Y;
}

Nokta n1 = new Nokta { X = 1, Y = 2 };
Nokta n2 = n1;  // n1'in adresi kopyalandı — ikisi aynı nesneye bakıyor
n2.X = 99;      // n1.X de 99 oldu!
```

`n1` ve `n2` heap'teki aynı nesneye işaret ediyor. Birini değiştirmek diğerini de etkiler.

---

## 6. "Value Types Her Zaman Stack'tedir" — Yanılgı

Bu çok yaygın bir hata. **Value type olması, stack'te olacağı anlamına gelmez.**

Bir `int` değişkeni bir `class` içindeyse, o `int` heap'te yaşar:

```csharp
class Kullanici
{
    public int Yas;  // Yas bir value type ama Kullanici heap'te
}
```

`Kullanici` nesnesi heap'e konur. `Yas` onun içindedir, dolayısıyla `Yas` da heap'tedir.

**Gerçek kural:** Değişkenin nerede yaşadığı, o değişkeni içeren yapıya bağlıdır.

- Fonksiyon içinde yerel `int` → stack
- Bir `class` içindeki `int` alanı → heap (class ile birlikte)
- Bir `struct` içindeki `int` alanı → struct neredeyse orada

---

## 7. Boxing ve Unboxing — Performans Tuzağı

Bazen bir value type'ı reference type gibi kullanman gerekir. Bu durumda CLR değeri heap'e koyup bir referans döner — buna **boxing** denir.

```csharp
int sayi = 42;
object obj = sayi;  // boxing — int heap'e kopyalandı
int geri = (int)obj; // unboxing — heap'ten tekrar çıkarıldı
```

**Neden tuzak?**

Boxing sırasında:
1. Heap'te yeni bir nesne oluşturulur
2. Değer oraya kopyalanır
3. GC bu nesneyi takip etmek zorundadır

Bir kez olunca sorun değil. Ama döngü içinde binlerce kez olursa ciddi performans kaybı.

```csharp
// Kötü — her iterasyonda boxing
var liste = new ArrayList();
for (int i = 0; i < 10000; i++)
    liste.Add(i);  // int → object boxing

// İyi — boxing yok
var liste = new List<int>();
for (int i = 0; i < 10000; i++)
    liste.Add(i);
```

`ArrayList` `object` tutar — her `int` box edilir. `List<int>` generic, boxing olmaz.

**Java ile fark:** Java'da `int` bir `Integer` nesnesine otomatik dönüşür (auto-boxing). Kaçış yolu sınırlı. C#'ta `struct` kullanarak boxing'ten bilinçli kaçabilirsin.
Java’da primitive tipler (int, double, boolean) nesne değildir, stack’te tutulur ve hızlıdır.
Ama Java tüm koleksiyonlar ve generics için sadece nesnelerle çalışır, primitive tipleri değil.
---

## 8. `Span<T>` — Stack'e Dost Modern Yaklaşım

Diyelim ki büyük bir dizinin bir parçasıyla çalışmak istiyorsun. Klasik yol: o parçayı kopyalayıp yeni bir dizi oluşturmak → heap'te allocation.

`Span<T>` buna alternatif. Kopyalama yapmadan var olan belleğin bir bölümüne "pencere" açar.

```csharp
int[] dizi = { 1, 2, 3, 4, 5 };

// Klasik yol: yeni dizi kopyası — heap allocation
int[] parca = dizi[1..3];  // { 2, 3 }

// Span yolu: kopyalama yok, aynı belleğe bakıyor
Span<int> span = dizi.AsSpan(1, 2);  // { 2, 3 } — ama kopya değil, pencere
```

`Span<T>` stack üzerinde yaşar ve heap allocation yapmaz. Yüksek performans gerektiren kod (parser, binary protokol, sıkıştırma) için kullanılır.

Şimdilik "böyle bir şey var, heap allocation'dan kaçınmak için kullanılır" bilmek yeterli. Faz 4'te derinlemesine göreceğiz.

---

## 9. LOH — Large Object Heap

Heap'te 85 KB'dan büyük nesneler özel bir bölgeye gider: **Large Object Heap (LOH)**.

Neden özel? Büyük nesneleri normal heap'te taşımak çok pahalı. GC onları yerinde bırakır ama bu zamanla heap'te "delikler" açar — buna **fragmentation** denir.

Pratikte ne demek?

```csharp
// Bu nesne LOH'a gider — byte[100000] = ~100 KB
byte[] buyukVeri = new byte[100_000];
```

Büyük array'leri döngü içinde sürekli oluşturmak LOH baskısı yaratır. Bunu Gün 3'te GC ile birlikte daha iyi anlayacağız.

---

## 10. Web Geliştirmede Nerede Görünür?

- **Her HTTP request** → controller nesnesi heap'te oluşturulur, request biter, GC temizler
- **Boxing tuzağı** → eski `ArrayList`, `Hashtable` kullanan legacy kod görürsen boxing var demektir
- **`Span<T>`** → JSON parsing, binary protokol, yüksek TPS API'lerde allocation azaltmak için
- **LOH baskısı** → büyük dosya yükleme, büyük array işlemleri, streaming yapılmazsa LOH'a gider
- **Stack overflow** → sonsuz recursive çağrı, stack doldu → uygulama çöker

---

## 11. Kontrol Soruları

1. Stack ve Heap arasındaki temel fark nedir? Hangisi neden daha hızlı?

Stack daha ufak ilk giren son cıkar temasıyla tasarlanmıs Heap daha buyukler için stackde value typelar yasar 
heapde referans objeleri var stack daha hızlı 

2. Şu kodu düşün:
   ```csharp
   class Araba { public int Hiz; }
   Araba a = new Araba();
   Araba b = a;
   b.Hiz = 200;
   ```
   `a.Hiz` kaçtır? Neden?

   bu bir class adresi kopyalıyor a.Hiz 200 dür 

3. Boxing nedir? `List<int>` ile `ArrayList` arasındaki performans farkının sebebi nedir?

eğer bir arrayin var içine object türünde ve içine integer bir degisken pushluyorsun bu boxing ile olur List int'de generic olarak verildiği için sürekli boxing ve unboxing
yapılmaz daha tasarruflu 

4. Bir value type her zaman stack'te mi yaşar? Örnekle açıkla.

hayır bu bir yanıldı üst scope mesela bir class ise heap'de dir onun içindeki value'de heape alınır

5. `Span<T>` neden allocation yapmaz? Ne zaman kullanmak mantıklı?

bir arrayin ufak bir parçasına pencere açmak için kullanılır öbür türlü kopyalamak gerekir maaliyetli bu da bellek için 
