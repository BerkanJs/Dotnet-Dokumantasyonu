# Gün 82 — Thread Safety: Lock, Monitor, Interlocked

---

## Thread Safety Neden Önemli?

Bir ASP.NET uygulaması aynı anda onlarca isteği paralel işler. Her istek ayrı bir thread'de çalışır. Bu thread'ler bellekteki aynı nesnelere — cache, sayaç, liste — aynı anda erişebilir.

Tek kişi kullanıyorsa sorun yok. Ama iki kişi aynı anda aynı şeyi değiştirmeye çalışırsa ne olur?

Gerçek dünya benzetmesi: iki kişi aynı Word belgesini ağ üzerinden açtı, ikisi de aynı satırı aynı anda sildi. Hangisinin silmesi geçerli? Sistem ikisini de kabul ederse veri bozulur.

---

## Race Condition — Yarış Durumu

Race condition, iki thread'in sonucu birbirinin adımlarına bağlı hale geldiğinde ortaya çıkar.

```
Thread A: _deger oku → 9
Thread B: _deger oku → 9       (A daha yazmadan B da okudu)
Thread A: 9 + 1 = 10, yaz
Thread B: 9 + 1 = 10, yaz     (B de 9 üzerine yazdı — bir artırma kayboldu)
```

Beklenen sonuç 11, gerçek sonuç 10. Ve bu hata her seferinde olmuyor — thread'lerin tam o anda çakışmasına bağlı. Bu yüzden race condition'lar yakalanması en zor hatalardandır.

```csharp
public class Sayac
{
    private int _deger = 0;

    public void Artir()
    {
        _deger++;
        // Tek satır gibi görünür ama CPU'da 3 ayrı adım:
        // 1. _deger'i oku
        // 2. 1 ekle
        // 3. geri yaz
        // İki thread 1. adımda çakışırsa → bir artırma kaybolur
    }
}
```

---

## lock — Kapıyı Kilitle, İş Bitince Aç

`lock` mekanizması şunu söyler: "Bu bloğa aynı anda yalnızca bir thread girebilir. Diğerleri sırada bekler."

Gerçek dünya benzetmesi: banka şubesindeki tek kasiyerli gişe. Müşteriler sıraya girer, biri işini bitirince diğeri içeri alınır. Aynı anda iki müşteri gişede olamaz.

```csharp
public class GuvenliSayac
{
    private int _deger = 0;
    private readonly object _kilit = new();
    // _kilit nesnesi "gişe" görevi görür — kimin içeride olduğunu takip eder
    // bunu public yapmamalısın — dışarıdan aynı nesneye lock alınırsa deadlock riski

    public void Artir()
    {
        lock (_kilit)
        {
            _deger++;   // bu bloğa aynı anda tek thread girer
        }               // bloktan çıkınca kilit serbest — sıradaki thread alır
    }

    public int Oku()
    {
        lock (_kilit)
            return _deger;
        // okuma da lock'lamalısın — lock olmadan okuyucular yarım yazılmış değeri görebilir
    }
}
```

`lock` aslında `Monitor.Enter` ve `Monitor.Exit` için kısa yazım biçimidir:

```csharp
Monitor.Enter(_kilit);
try
{
    _deger++;
}
finally
{
    Monitor.Exit(_kilit);   // exception fırlasa bile kilit mutlaka serbest bırakılır
                             // bunu yazmasaydık → exception'da kilit sonsuza kalır, deadlock
}
```

**lock'un maliyeti:** Thread'ler sırada beklediğinde boşa harcanan CPU süresi olur. Az thread, kısa işlem → sorun yok. Çok thread, yoğun erişim → lock darboğaz olabilir.

---

## Interlocked — Kilitsiz Atomik İşlem

Sadece bir sayıyı artırmak veya değiştirmek için `lock` kullanmak biraz fazla. `Interlocked` bu iş için CPU'nun yerleşik atomic komutlarını kullanır — lock mekanizması hiç devreye girmez.

Gerçek dünya benzetmesi: kasiyerli gişe yerine otomatik bilet makinesi. Sıra bekleme yok, her işlem kendi içinde tamamlanmış.

```csharp
public class AtomikSayac
{
    private int _deger = 0;

    public void Artir()
    {
        Interlocked.Increment(ref _deger);
        // CPU seviyesinde tek bir talimat — araya başka thread giremez
        // lock'tan çok daha hızlı çünkü OS scheduler devreye girmez
    }

    public void Azalt()
    {
        Interlocked.Decrement(ref _deger);
    }

    public int Oku()
    {
        return Interlocked.CompareExchange(ref _deger, 0, 0);
        // değeri değiştirmiyor — ama okumayı memory barrier ile güvenli hale getiriyor
        // volatile kullanmak da işe yarar ama Interlocked daha açık niyeti gösterir
    }

    public void Sifirla()
    {
        Interlocked.Exchange(ref _deger, 0);
        // atomic olarak 0 atar — eski değeri döndürür (gerekirse kullanırsın)
    }
}
```

**Ne zaman Interlocked, ne zaman lock?**  
Tek bir değişken üzerinde basit bir işlem yapıyorsan → `Interlocked`.  
Birden fazla değişkeni birlikte tutarlı değiştirmen gerekiyorsa → `lock`. Çünkü Interlocked tek bir değişken için çalışır, iki değişkeni aynı anda atomik değiştiremez.

---

## ReaderWriterLockSlim — Okuyanlara Öncelik

Bir cache düşün: saniyede 1000 okuma, saniyede 1 yazma var. Normal `lock` kullanırsan 1000 okuyucu da sırayla bekler — oysa okumalar birbirini engellemez.

`ReaderWriterLockSlim` şunu söyler:
- Okuma yapan thread'ler aynı anda içeri girebilir — birbirlerini bekletmez.
- Yazma yapan thread girince tüm okuyucular bekler, yazma bitince devam ederler.

```csharp
public class KitapCache
{
    private readonly Dictionary<int, Kitap> _cache = new();
    private readonly ReaderWriterLockSlim _rwLock = new();

    public Kitap? Oku(int id)
    {
        _rwLock.EnterReadLock();
        // birden fazla thread aynı anda buraya girebilir
        // normal lock'ta her biri sıra beklerdi — burada hepsi paralel okur
        try
        {
            _cache.TryGetValue(id, out var kitap);
            return kitap;
        }
        finally { _rwLock.ExitReadLock(); }
    }

    public void Ekle(int id, Kitap kitap)
    {
        _rwLock.EnterWriteLock();
        // yazma başlarken tüm okuyucular beklemeye girer
        // yazma bitince okuyucular devam eder
        try   { _cache[id] = kitap; }
        finally { _rwLock.ExitWriteLock(); }
    }
}
```

**Ne zaman kullanırsın?** Yoğun okunan ama seyrek güncellenen bir yapı varsa — bellek cache'i, konfigürasyon tablosu gibi. Okuma/yazma oranı dengeliyse basit `lock` daha az karmaşık.

---

## volatile — Önbelleği Atla

CPU, sık erişilen değişkenleri performans için kendi önbelleğine (register/L1 cache) alabilir. Bir thread değeri değiştirdiğinde diğer thread hâlâ eski önbellek değerini görüyor olabilir.

`volatile` bu önbelleklemeyi devre dışı bırakır — her okuma/yazma direkt RAM'e gider.

```csharp
public class ServisYonetici
{
    private volatile bool _calisyor = true;
    // volatile olmasaydı → derleyici veya CPU bu değeri önbelleğe alır
    // Durdur() false yapar ama Calistir() hâlâ önbellekteki true'yu görür → sonsuz döngü

    public void Calistir()
    {
        while (_calisyor)   // her döngüde RAM'den okur — güncel değeri görür
            IsYap();
    }

    public void Durdur()
    {
        _calisyor = false;  // bu yazma tüm thread'lere anında görünür
    }
}
```

**Önemli kısıt:** `volatile` yalnızca tek bir okuma veya yazmanın görünürlüğünü garanti eder. `_deger++` gibi "oku, değiştir, yaz" zinciri için yetmez — orada `Interlocked` veya `lock` gerekir.

---

## Özet — Hangi Araç Ne Zaman?

| Durum | Araç | Neden |
|---|---|---|
| Birden fazla değişkeni birlikte değiştir | `lock` | Atomik blok gerekir |
| Tek sayaç artır/azalt | `Interlocked` | Lock overhead'i yok |
| Okuma çok, yazma az | `ReaderWriterLockSlim` | Okuyucular paralel çalışır |
| Durum bayrağı (bool) | `volatile` | Görünürlük yeterli, işlem yok |

---

## 500 vs 50K Kullanıcı

| | 500 | 50K |
|---|---|---|
| `lock` | Yeterli, overhead fark edilmez | Hot path'te darboğaz olabilir |
| `Interlocked` | İyi alışkanlık, lock yerine geç | Mutlaka tercih et |
| `ReaderWriterLockSlim` | Nadiren gerekir | Yoğun okunan cache için değerli |
| `volatile` | Basit bayrak için yeterli | Aynı |

---

## Kontrol Soruları

1. `_deger++` neden race condition'a yol açar? CPU adımlarıyla açıkla.
2. `Interlocked.Increment` `lock`'tan neden daha hızlıdır?
3. `ReaderWriterLockSlim`'de okuma sırasında yazma isteği gelirse ne olur?
4. `volatile` tek başına `_deger++`'ı thread-safe yapar mı? Neden?
