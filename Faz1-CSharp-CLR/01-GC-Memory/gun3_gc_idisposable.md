# Gün 3 — Garbage Collector: Derinlemesine

---

## 1. GC Neden Var?

Gün 2'de heap'te nesneler oluşturulduğunu gördük. Peki bu nesneler ne zaman silinir?

C veya C++'ta bunu sen yaparsın: `malloc` ile al, `free` ile bırak. Unutursan — **memory leak**. Bellek dolana kadar program çalışır, sonra çöker.

CLR bunu senin yerine yapar. **Garbage Collector (GC)**, heap'te artık kullanılmayan nesneleri tespit edip siler. Ama bunu nasıl yapar, ne zaman yapar — bunları anlamak önemli.

---

## 2. GC "Kullanılmayan" Nesneyi Nasıl Bulur?

GC şunu sorar: "Bu nesneye hâlâ bir yerden ulaşılabilir mi?"

Ulaşılabilir = bir değişken, bir referans, bir statik alan bu nesneyi tutuyorsa yaşıyor demektir.

```csharp
var kullanici = new Kullanici();  // nesne oluştu, kullanici değişkeni onu tutuyor
kullanici = null;                  // artık tutan yok → GC silebilir
```

GC tüm "kök" noktalardan başlayıp ulaşılabilen nesneleri işaretler. Ulaşılamayan her şey silinir. Buna **Mark and Sweep** denir.

---

## 3. Generational GC — Nesiller

Her nesne oluşturulur, bir süre yaşar, sonra ölür. Ama hepsinin ömrü aynı değil:

- Bir HTTP request'te oluşan `string` → birkaç milisaniye
- Veritabanı bağlantısı → birkaç saniye/dakika
- Uygulama konfigürasyonu → uygulama ömrü boyunca

.NET GC bu gerçeği kullanır. Nesneleri **nesillere** böler:

```
Gen 0  →  yeni doğan nesneler  (küçük, hızlı taranır)
Gen 1  →  Gen 0'ı hayatta kalanlar  (tampon bölge)
Gen 2  →  uzun yaşayan nesneler  (büyük, nadir taranır)
```

**Object Lifetime Hipotezi:** Çoğu nesne çok kısa yaşar. Gen 0 küçük tutulur ve sık sık toplanır. Kısa yaşayan nesnelerin büyük çoğunluğu Gen 0'da ölür — Gen 1 veya Gen 2'ye hiç ulaşmaz.

**Pratik sonuç:** Gen 0 collection çok hızlı (microsecond). Gen 2 collection yavaş ve pahalı.

---

## 4. Nesiller Arası Geçiş

Bir nesne Gen 0 collection'dan sağ çıkarsa Gen 1'e terfi eder. Gen 1'den sağ çıkarsa Gen 2'ye geçer.

```
[Nesne oluştu]
    → Gen 0'a girer
    → GC Gen 0'ı topladı, nesne hâlâ kullanılıyor
    → Gen 1'e terfi
    → GC Gen 1'i topladı, nesne hâlâ kullanılıyor
    → Gen 2'ye terfi
    → Uygulama ömrü boyunca burada yaşar
```

Web API'de her request'te oluşan nesneler idealde Gen 0'da ölmeli. Bunu başarırsan GC baskısı minimumdur.

---

## 5. Large Object Heap (LOH)

Gün 2'de kısaca değindik. Şimdi biraz daha derine:

**85 KB'dan büyük nesneler** doğrudan LOH'a gider — nesil sistemi dışında.

Neden sorunlu?

1. LOH **compaction** yapılmaz (varsayılan). Normal heap'te GC nesneleri sıkıştırır. LOH'ta yapmaz.
2. Zamanla LOH'ta boş delikleri oluşur — bu **fragmentation**.
3. Fragmentation sonucu: 100 MB boş alan var ama hepsi küçük parçalarda → 50 MB'lık nesne sığmaz.

```csharp
// Bunlar LOH'a gider:
byte[] tampon = new byte[100_000];        // ~100 KB
var buyukListe = new List<int>(100_000);  // iç dizi ~400 KB
```

**Çözüm:** Büyük buffer'ları `ArrayPool<T>` ile kirala, kullan, geri ver. Sürekli yeni `byte[]` oluşturma.

---

## 6. Pinned Object Heap (POH) — .NET 5+

Unmanaged kod (P/Invoke, native kütüphaneler) ile çalışırken bazen belleği sabit tutman gerekir. GC normal şartlarda nesneleri taşır — ama native kod bir nesnenin adresini tutuyorsa nesne taşınırsa adres bozulur.

Bunu engellemek için nesne **pin** edilir. Eskiden pinned nesneler normal heap'te kalır ve GC'yi zorlaştırırdı.

**.NET 5 ile POH (Pinned Object Heap) geldi.** Pinned nesneler ayrı bir heap'e gider, GC onları zaten taşımaz. Normal heap'i rahatsız etmez.

Günlük web geliştirmede bunu kullanmazsın. Ama "neden .NET 5'te POH eklendi?" sorusunun cevabı bu.

---

## 7. GC Modları

**Workstation GC:** Tek bir thread üzerinde GC. Masaüstü uygulamalar için varsayılan. Düşük gecikme hedefler.

**Server GC:** Her CPU çekirdeği için ayrı GC thread'i ve heap. Sunucu uygulamaları için. Daha yüksek throughput, biraz daha fazla bellek kullanır.

ASP.NET Core varsayılan olarak Server GC kullanır. Bunu `runtimeconfig.json` ile değiştirebilirsin ama genellikle varsayılan doğrudur.

**Background GC:** Gen 2 collection arka planda çalışır, uygulamayı bloklamaz. .NET'in varsayılan modu. Eski "Concurrent GC"nin gelişmiş hali.

---

## 8. `GC.Collect()` Neden Kötü?

Kodu yazarken "şimdi GC çalışsın" diye `GC.Collect()` çağırmak cazip görünebilir. Ama bu neredeyse her zaman yanlıştır.

**Neden?**

1. GC kendi zamanlama algoritmalarına göre çalışır — sen ondan daha iyi bilemezsin
2. `GC.Collect()` çağrısı **tüm nesneleri** bir nesil ilerletir. Gen 0'da ölmesi gereken kısa ömürlü nesneler Gen 1'e taşınır, sonra Gen 2'ye gider. Böylece kısa ömürlü nesneler uzun yaşayan kategorisine girer — tam tersi etki.
3. Full GC (Gen 0+1+2) çağrısı uygulamayı durdurabilir.

**İstisna:** Benchmark yazmadan önce temiz baseline için, ya da belirli test senaryolarında. Production'da kullanılmaz.

---

## 9. IDisposable — Deterministik Temizlik

GC belleği temizler ama **bellek dışı kaynakları** temizlemez:

- Veritabanı bağlantısı
- Dosya handle'ı
- Network soketi
- HTTP bağlantısı

Bu kaynaklar sınırlı. 100 tane açık veritabanı bağlantısı bırakırsan veritabanı yeni bağlantı kabul etmez. GC'nin bunları "eninde sonunda" temizlemesini bekleyemezsin.

**`IDisposable`** bu sorunu çözer. "Ben temizlenebilirim, hazır olduğunda `Dispose()` çağır" sözleşmesi.

```csharp
public interface IDisposable
{
    void Dispose();
}
```

**`using` bloğu** `Dispose()`'u otomatik çağırır — blok bitince veya exception fırlatılsa bile:

```csharp
// Uzun yol
var baglanti = new SqlConnection(connStr);
try
{
    baglanti.Open();
    // ... işlemler
}
finally
{
    baglanti.Dispose();  // her halükarda çağrılır
}

// Kısa yol — using aynı şeyi yapar
using var baglanti = new SqlConnection(connStr);
baglanti.Open();
// blok bitince otomatik Dispose() çağrılır
```

---

## 10. Finalizer — Neden Tehlikelidir?

Finalizer (`~ClassName`), bir nesne GC tarafından silinmeden önce çağrılan bir metot. Java'daki `finalize()` ile aynı konsept.

```csharp
class KaynaklıSınıf
{
    ~KaynaklıSınıf()  // finalizer
    {
        // temizlik
    }
}
```

**Neden tehlikeli?**

1. **Zamanlama belirsiz.** Ne zaman çağrılacağını bilemezsin. Nesne ölmüş olabilir ama finalizer henüz çalışmamış.

2. **GC sürecini uzatır.** Finalizer'ı olan nesne hemen silinmez. GC onu "finalizer kuyruğu"na alır, özel bir thread finalizer'ı çalıştırır, sonraki GC döngüsünde silinir. Nesne bir nesil fazla yaşar — Gen 0'da ölmesi gerekirken Gen 1'e kadar gider.

3. **Exception yutulur.** Finalizer içinde fırlayan exception sessizce yutulur.

**Ne zaman finalizer kullanılır?**

Sadece ve sadece **unmanaged kaynak** tutuyorsan. Managed kaynaklar (başka nesneler) için finalizer'a gerek yok — GC onları zaten temizler. Örneğin `SafeHandle` türevleri.

Günlük uygulama kodunda finalizer yazma. `IDisposable` yeterli.

---

## 11. Dispose Pattern — Doğru Uygulama

Hem managed hem unmanaged kaynak tutan bir sınıf için tam pattern:

```csharp
public class BaglantiYoneticisi : IDisposable
{
    private bool _disposed = false;

    // Managed kaynak
    private SqlConnection _baglanti;

    // Unmanaged kaynak (örnek — gerçekte SafeHandle kullanılır)
    private IntPtr _nativeHandle;

    public BaglantiYoneticisi()
    {
        _baglanti = new SqlConnection("...");
        // _nativeHandle = NativeMethods.Open(...);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);  // Dispose çağrıldı, finalizer'a gerek yok
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Managed kaynakları burada temizle
            _baglanti?.Dispose();
        }

        // Unmanaged kaynakları her iki durumda da temizle
        // NativeMethods.Close(_nativeHandle);

        _disposed = true;
    }

    ~BaglantiYoneticisi()  // finalizer — sadece unmanaged kaynak varsa
    {
        Dispose(disposing: false);
    }
}
```

**`GC.SuppressFinalize(this)` neden var?**

`Dispose()` çağrıldığında temizlik yapıldı. Finalizer'ın tekrar çalışmasına gerek yok. Bu satır GC'ye "bu nesnenin finalizer'ını atlayabilirsin" der → nesne daha erken ve ucuza toplanır.

---

## 12. Java Karşılaştırması

**Java G1GC vs .NET GC:**

Java'nın G1GC de generational, ama bölge tabanlı (region-based). .NET GC daha basit nesil modeli kullanır ama Server GC ile paralel çalışabilir.

**`finalize()` → deprecated:**

Java 9'da deprecated edildi, Java 18'de kaldırıldı. Neden? Aynı sebep — zamanlama belirsizliği, GC'yi ağırlaştırıyor. C#'ta da finalizer'dan kaçınılır, same reasoning.

**IDisposable vs AutoCloseable:**

Java'da `AutoCloseable` + try-with-resources. C#'ta `IDisposable` + `using`. Konsept aynı, syntax farklı.

---

## 13. Web Geliştirmede Nerede Görünür?

- **Her request sonu:** Controller, DbContext, HttpClient — bunlar `IDisposable`. ASP.NET Core DI container `Scoped` lifetime'da bunları otomatik `Dispose` eder.
- **`using` unutulursa:** DbContext dispose edilmezse bağlantı havuzu tükenir → yeni request bağlantı bulamaz → timeout.
- **Gen 2 collection:** Request başına çok nesne yaratıp uzun yaşatırsan Gen 2 collection devreye girer → latency spike (genellikle yüzlerce ms).
- **LOH baskısı:** Büyük JSON response veya dosya buffer'ı heap'e atılırsa LOH'a gider.

---

## 14. Kontrol Soruları

1. GC "kullanılmayan nesne" tespitini nasıl yapar? "Kullanılmayan" tam olarak ne demek?

ilk olarak bunların cagrıldıgı referanslara bakar cagrılmıyorlarsa ihtiyaç yoksa bunları siler 

2. Gen 0 neden küçük tutulur? Kısa ömürlü bir nesne Gen 2'ye kadar çıkarsa ne olur?

Bu bir leak değil, ama yanlış lifetime yönetimi → performans problemi

3. `IDisposable` neden var? GC her şeyi temizlemez mi?

hayır temizlemez Db baglantısı gibi referansları silmez bunların disposible olarak isaretlenmesi lazım

4. Finalizer yazıldığında nesnenin ömrü nasıl uzar? Neden bu kötüdür?

GC neyi ne zaman silmesi gerektiğini biliyor zaten sen bunu cagırırsan ömrü uzayabilir nesnelerin

5. `GC.SuppressFinalize(this)` ne yapar ve neden `Dispose()` içinde çağrılır?
Finalizer iptal edilir
Nesne normal nesneler gibi tek GC’de temizlenir
Performans korunur

6. Bir ASP.NET Core endpoint'inde `new SqlConnection()` açıp `Dispose()` çağırmazsan ne olur?

Connection leak olur (memory değil, resource leak)
