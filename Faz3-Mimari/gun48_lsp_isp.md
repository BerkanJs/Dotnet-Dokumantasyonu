# Gün 48 — LSP + ISP

---

## BÖLÜM 1: Liskov Substitution Principle (LSP)

### 1. Günlük Hayat Analojisi

Bir kiralık araç şirketini düşün. "Araç" sözleşmesi şunu söylüyor: "yakıt doldur, çalıştır, dön." Müşteri elektrikli araç kiralayınca aynı sözleşmeden yola çıkıyor. Ama benzin istasyonuna gittiğinde araç çalışmıyor — şarj lazım. Sözleşme aynı, davranış farklı. Müşteri aldatıldı.

**LSP ihlali tam bu:** Alt tip üst tipin yerine geçtiğinde çağıran kod beklenmedik davranışla karşılaşıyor.

---

### 2. Tanım

> "Subtype must be substitutable for its base type without altering program correctness."
> — Barbara Liskov

```
Üst tip: Kitap → StokDus() çağrılabilir, stok azalır
Alt tip: DijitalKitap → StokDus() çağrılıyor ama stok kavramı yok

→ Çağıran "Kitap" aldığında güvende hissediyor
→ "DijitalKitap" gelince davranış değişiyor
→ LSP ihlali
```

---

### 3. Faz2'de Böyle Yaptık

Faz2'de `IKitapServisi` implement eden iki class vardı:

```csharp
// EfKitapServisi   → tüm metodları gerçekten uygular ✓
// CachedKitapServisi → tüm metodları gerçekten uygular ✓
```

`CachedKitapServisi`, `IKitapServisi`'nin yerine geçebiliyor — controller hiçbir şeyi fark etmiyor. Bu **LSP uyumlu.**

Şimdi bir LSP ihlalini Kitabevi domain'inde görelim: fiziksel kitap ve dijital kitap kalıtım üzerinden modellenseydi ne olurdu?

---

### 4. LSP İhlali

```csharp
// Lsp/DijitalKitap.cs
public class DijitalKitap : Kitap
{
    public override int StokAdedi
    {
        get => int.MaxValue;   // "sonsuz stok" numarası yapıyor
        set { /* sessizce yut */ }
    }

    public override void StokDus()
    {
        // Hiçbir şey yapmıyor — base class'ın garantisini bozuyor
    }
}
```

Çağıran kod şunu bekliyor:

```csharp
void SatinAl(Kitap kitap)
{
    kitap.StokDus();        // stok azalacak diye bekliyor
    // DijitalKitap gelirse → hiçbir şey olmaz, çağıran aldatıldı
}
```

**İhlal sinyalleri:**
- Override içinde boş gövde
- Override içinde `throw new NotSupportedException()`
- Override içinde base'in garantisini kıran davranış

---

### 5. Büyük Projede Böyle Yapmalısın

Kalıtım yerine **ortak interface** — her tip kendi davranışını tanımlar:

```csharp
// Lsp/LSPDuzeltme.cs
public interface ISatilabilir
{
    string Baslik { get; }
    decimal Fiyat { get; }
    bool StokVarMi();
    // bunu yazmasaydık → çağıran her seferinde tip kontrolü yapmak zorunda kalırdı
    // if (urun is FizikselKitap f && f.StokAdedi > 0) → OCP + LSP ihlali
}

public class FizikselKitap : ISatilabilir
{
    public int StokAdedi { get; set; }
    public bool StokVarMi() => StokAdedi > 0;
    // gerçek stok kontrolü — davranış netleşti
}

public class EKitap : ISatilabilir
{
    public bool StokVarMi() => true;
    // dijital ürün her zaman mevcut — çağıran bunu bilmek zorunda değil
    // ISatilabilir üzerinden gelince doğru davranışı alır → LSP sağlandı
}
```

Çağıran kod:

```csharp
// Fiziksel mi dijital mi bilmiyor — her ikisi de ISatilabilir
foreach (var urun in urunler)
{
    if (urun.StokVarMi())
        Console.WriteLine($"{urun.Baslik} satılabilir");
}
```

---

### 6. 500 vs 50k Kullanıcı — LSP

| | 500 kullanıcı/ay | 50k kullanıcı/ay |
|---|---|---|
| **LSP ihlali ne zaman patlar?** | Küçük projede gözden kaçar | Çok sayıda alt tip, ekip büyüyünce tespit edilmesi zorlaşır |
| **Overengineering sinyali** | Her class için interface açmak | — |
| **Pratik kural** | Alt tip override'da base'in garantisini kırıyorsa → kalıtımı kes | Composition > Inheritance tercih et |

---

---

## BÖLÜM 2: Interface Segregation Principle (ISP)

### 1. Günlük Hayat Analojisi

Bir kasiyere "hem ürün tara, hem muhasebe defteri tut, hem depo sayımı yap, hem de güvenlik kameralarını izle" dersen — kasiyerin işi imkansızlaşır. Her görev için ayrı bir rol tanımı olmalı.

**ISP:** İstemciler kullanmadıkları metodları olan interface'lere bağımlı olmamalı.

---

### 2. Tanım

> "Clients should not be forced to depend on interfaces they do not use."
> — Robert C. Martin

---

### 3. Faz2'de Böyle Yaptık — Bu Sefer Direkt Doğrusu

Faz2'de `IKitapServisi` tek başınaydı:

```csharp
public interface IKitapServisi
{
    Task<IReadOnlyList<KitapListeViewModel>> HepsiniGetirAsync();
    Task<KitapFormViewModel?> BulByIdAsync(int id);
    Task<int> EkleAsync(KitapFormViewModel model);
    Task<bool> SilAsync(int id);
    // ...
}
```

`CachedKitapServisi` bunu implement ediyor — sorun yok, tüm metodlar mantıklı.

Ama Gün 30'da `IKitapSorguServisi`, Gün 33'te `IKitapBatchServisi` eklendi. Neden `IKitapServisi`'ne eklemedik?

Faz2 `IKitapBatchServisi.cs` yorumundan:

```
// Buraya toplu operasyon eklemek tüm sınıflara stub ekletir
// → Interface Segregation Principle ihlali
// Sadece EfKitapServisi implement eder — EF Core 7+'a özgü
```

**Bu karar tam ISP.** `CachedKitapServisi` batch operasyonları implement etmek zorunda kalmadı.

---

### 4. Fat Interface İhlali

Eğer her şeyi `IKitapServisi`'ne koysaydık:

```csharp
// ❌ Fat interface — ISP ihlali
public interface IKitabeviServisiFat
{
    List<string> HepsiniGetir();
    string? BulById(int id);
    void Ekle(string baslik);
    void Sil(int id);
    int TopluSil(string kategori);      // sadece EF Core yapabilir
    int StokSifirla(string kategori);   // sadece EF Core yapabilir
}

// CachedKitapServisi zorla implement etmek zorunda:
public class CachedKitapServisi : IKitabeviServisiFat
{
    public int TopluSil(string kategori)
        => throw new NotSupportedException(); // ❌ LSP de ihlal oluyor
    public int StokSifirla(string kategori)
        => throw new NotSupportedException(); // ❌
}
```

---

### 5. Büyük Projede Böyle Yapmalısın

```csharp
// Isp/ISPDuzeltme.cs

public interface IKitapOkuma
{
    List<string> HepsiniGetir();
    string? BulById(int id);
    // CachedKitapServisi sadece bunu implement eder — fazladan metod yok
}

public interface IKitapYazma
{
    void Ekle(string baslik);
    void Sil(int id);
    // yazma ihtiyacı olan controller sadece bunu inject eder
}

public interface IKitapBatch
{
    int TopluSil(string kategori);
    int StokSifirla(string kategori);
    // sadece EfKitapServisi implement eder, cache servisi hiç görmez
}

// CachedKitapServisi — sadece ihtiyacı olan interface'leri alır
public class CachedKitapServisi : IKitapOkuma
{
    private readonly IKitapOkuma _gercekServis;
    // bunu yazmasaydık → batch metodlarını da implement etmek zorunda kalırdı
    // NotSupportedException → LSP ihlali
}

// EfKitapServisi — gerçekten her şeyi yapabiliyor, hepsini implement eder
public class EfKitapServisi : IKitapOkuma, IKitapYazma, IKitapBatch { }
```

Controller'da:

```csharp
// Sadece okuma yapan controller → IKitapOkuma inject eder
// Batch işlemi yapan background job → IKitapBatch inject eder
// İkisini karıştırmaz, ikisi birbirini bilmez
```

---

### 6. LSP + ISP Bağlantısı

```
Fat interface → class zorla implement eder
             → kullanamadığı metod için NotSupportedException
             → çağıran aldatılır
             → LSP ihlali

Yani: ISP ihlali çoğunlukla LSP ihlaline de yol açar.
```

---

### 7. 500 vs 50k Kullanıcı — ISP

| | 500 kullanıcı/ay | 50k kullanıcı/ay |
|---|---|---|
| **Fat interface bırak?** | 1-2 implementasyon varsa → çalışır | ❌ Implementasyon sayısı artınca her stub bir risk |
| **Interface böl?** | Implementasyonlar farklılaşınca uygula | ✅ Test edilebilirlik + mock kolaylığı için şart |
| **Overengineering sinyali** | Her metod için ayrı interface açmak | — |

**Pratik kural:** Bir class'ın implement ettiği interface'de `throw new NotSupportedException()` veya boş gövde görüyorsan — interface'i böl.

---

## Sorular

1. `CachedKitapServisi`, `IKitapServisi`'nin yerine geçebiliyor mu? LSP açısından değerlendir.
2. Faz2'de `IKitapSorguServisi` neden `IKitapServisi`'nden ayrıldı? Sadece ISP mi, başka sebep var mı?
3. Bir interface'i ne kadar küçük bölebilirsin? Sınır nerede?
