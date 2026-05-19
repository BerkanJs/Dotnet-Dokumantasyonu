# Gün 56 — Anti-Patterns

Anti-pattern: çalışıyor gibi görünen ama uzun vadede zarar veren kod yapısı.  
Pattern = "ne yapmalısın". Anti-pattern = "neyi yapıyorsun ama yapmamalısın".

Hepsinin ortak noktası: **küçük projede acısını hissetmiyorsun. Proje büyüyünce seni öldürüyor.**

---

## 1. Anemic Domain Model — "Sadece Çanta"

### Günlük hayat analogu

Bir muhasebeci düşün. Elinde bir defter var — sadece rakamlar yazılı. Hiçbir kural yok: negatif bakiye yazılabilir, tarih sırasız girilebilir, para birimi belirtilmemiş. Tüm kurallar muhasebecinin kafasında. Muhasebeci işten çıkınca her şey gider.

İşte Anemic Domain Model tam bu: **nesne sadece veri taşıyor, kural taşımıyor.**

### Kod

```csharp
// ❌ Anemic — Kitap sadece çanta, kural yok
public class Kitap
{
    public int Id { get; set; }
    public string Baslik { get; set; }
    public decimal Fiyat { get; set; }
    public int Stok { get; set; }   // dışarıdan istediğin değeri yazabilirsin: kitap.Stok = -999
}

// Kural servise gömülmüş
public class SiparisServisi
{
    public void StokDus(Kitap kitap)
    {
        if (kitap.Stok <= 0)             // bu kontrol burada
            throw new Exception("Stok yok");
        kitap.Stok--;
    }
}

public class BatchImportServisi
{
    public void TopluSiparis(Kitap kitap, int adet)
    {
        if (kitap.Stok <= 0)             // aynı kontrol burada da — kopyalandı
            throw new Exception("Stok yok");
        kitap.Stok -= adet;
    }
}

public class KitapApiController : ControllerBase
{
    public IActionResult Siparis(int kitapId)
    {
        if (kitap.Stok <= 0)             // aynı kontrol API'de de — üçüncü kopya
            return BadRequest("Stok yok");
        kitap.Stok--;
    }
}
```

"Stok 0'ın altına inmemeli" kuralı şu an **3 yerde yazılı**. Yarın kural değişirse (mesela minimum stok 2 olsun) — 3 yeri bulmak zorundasın. Birini unutursan bug.

```csharp
// ✅ Rich Domain Model — kural nesnede yaşıyor
public class Kitap
{
    public int Id { get; private set; }
    public string Baslik { get; private set; }
    public decimal Fiyat { get; private set; }
    public int Stok { get; private set; }       // private set: dışarıdan kitap.Stok = -999 yazılamaz
                                                // bunu yazmasaydık → herkes istediği değeri atayabilir

    public void StokDus(int adet)
    {
        if (adet <= 0)
            throw new ArgumentException("Adet sıfırdan büyük olmalı");

        if (adet > Stok)                        // kural tek yerde — kim çağırırsa çağırsın buradan geçiyor
            throw new InvalidOperationException($"Yetersiz stok. Mevcut: {Stok}, İstenen: {adet}");
                                                // bunu yazmasaydık → Stok negatife düşebilirdi

        Stok -= adet;
    }
}
```

Şimdi sipariş senaryosu: kullanıcı 3 kitap sipariş veriyor. Bu akış her kanalda — API, batch, arka plan job — **aynı şekilde** çalışıyor:

```csharp
// SiparisServisi — stok kontrolünü kendisi yazmıyor, Kitap'a soruyor
public class SiparisServisi
{
    private readonly IKitapRepository _kitapRepo;
    private readonly ISiparisRepository _siparisRepo;

    public async Task<Siparis> OlusturAsync(int kitapId, int adet, string musteriAdi)
    {
        var kitap = await _kitapRepo.GetByIdAsync(kitapId);
        // kitap null kontrolü burada — repo'dan geldi, biz sormadık

        kitap.StokDus(adet);
        // ↑ "adet > Stok mu?" kontrolü Kitap içinde
        // yetmiyorsa InvalidOperationException fırlar — servis if yazmadı
        // bunu yazmasaydık → if (adet > kitap.Stok) burada da yazardık, 3. kopya olurdu

        var siparis = new Siparis(kitap, adet, musteriAdi);
        await _siparisRepo.EkleAsync(siparis);
        return siparis;
    }
}

// BatchImportServisi — aynı metodu çağırıyor, kontrol yazmıyor
public class BatchImportServisi
{
    public async Task TopluSiparisAsync(List<SiparisRow> satirlar)
    {
        foreach (var satir in satirlar)
        {
            var kitap = await _kitapRepo.GetByIdAsync(satir.KitapId);
            kitap.StokDus(satir.Adet);     // aynı kural — ayrıca if yazmaya gerek yok
            await _siparisRepo.EkleAsync(new Siparis(kitap, satir.Adet, satir.Musteri));
        }
    }
}

// API Controller — aynı servis, aynı kural
[HttpPost]
public async Task<IActionResult> SiparisVer(SiparisDto dto)
{
    var siparis = await _siparisServisi.OlusturAsync(dto.KitapId, dto.Adet, dto.Musteri);
    return Ok(siparis);
    // stok kontrolü burada da yok — SiparisServisi → Kitap.StokDus() zinciri hallediyor
}
```

**"Minimum stok 2'nin altına düşmesin" kuralı geldi.** Nereyi değiştiriyorsun?

```csharp
// Sadece Kitap.StokDus() — tek yer
public void StokDus(int adet)
{
    if (adet <= 0)
        throw new ArgumentException("Adet sıfırdan büyük olmalı");

    if (Stok - adet < 2)                    // ← sadece burası değişti
        throw new InvalidOperationException($"Stok 2'nin altına düşemez. Mevcut: {Stok}");

    Stok -= adet;
}
// SiparisServisi? Değişmedi.
// BatchImportServisi? Değişmedi.
// Controller? Değişmedi.
```

**Kural değişti mi?** `Kitap.cs`'i aç, bir metodu değiştir, bitti.

---

## 2. God Class — "Her Şeyi Bilen Adam"

### Günlük hayat analogu

Küçük bir kasabada tek bir adam var: hem doktor, hem avukat, hem muhasebeci, hem tamirci. 500 kişilik kasabada işe yarıyor. Şehir 50.000 kişiye büyüyünce bu adam ne olur? Darboğaz. Hasta bekliyor, araba bekliyor, dava bekliyor — hepsi aynı adamı bekliyor. Adamın başı da dönüyor çünkü çok fazla şey biliyor.

### Kod

```csharp
// ❌ God Class — EfKitapServisi her şeyi yapıyor
public class EfKitapServisi : IKitapServisi, IKitapSorguServisi, IKitapBatchServisi
{
    // Kitap CRUD
    public async Task<Kitap> GetByIdAsync(int id) { ... }
    public async Task EkleAsync(Kitap kitap) { ... }
    public async Task GuncelleAsync(Kitap kitap) { ... }
    public async Task SilAsync(int id) { ... }

    // Arama ve filtreleme
    public async Task<List<Kitap>> YazaraGoreGetirAsync(int yazarId) { ... }
    public async Task<List<Kitap>> KategoriyeGoreGetirAsync(int kategoriId) { ... }
    public async Task<PagedResult<Kitap>> SayfaliGetirAsync(int sayfa, int boyut) { ... }
    public async Task<List<Kitap>> AraAsync(string kelime) { ... }

    // Toplu işlemler
    public async Task TopluFiyatGuncelleAsync(int kategoriId, decimal oran) { ... }
    public async Task TopluStokGuncelleAsync(List<StokGuncelleme> guncellemeler) { ... }
    public async Task ArsivleAsync(DateTime kesimTarihi) { ... }

    // Raporlama
    public async Task<KitapRaporu> RaporOlusturAsync(DateRange aralik) { ... }
    public async Task<List<EnCokSatan>> EnCokSatanlarAsync(int topN) { ... }
}
// 400+ satır, tek dosya, her değişiklik buraya dokunuyor
```

**Sinyal:** Dosyayı açtığında "hangi satıra bakacağım?" diye `Ctrl+F` yapıyorsun.  
**Sinyal:** 2 kişi aynı dosyayı aynı anda düzenlemeye çalışıyor → merge conflict.

```csharp
// ✅ Sorumluluk bölündü — her sınıf bir şey yapıyor
public class KitapYazmaServisi      // sadece veri değiştirme işlemleri
{
    public async Task EkleAsync(Kitap kitap) { ... }
    public async Task GuncelleAsync(Kitap kitap) { ... }
    public async Task SilAsync(int id) { ... }
}

public class KitapSorguServisi      // sadece okuma işlemleri
{
    public async Task<Kitap> GetByIdAsync(int id) { ... }
    public async Task<List<Kitap>> AraAsync(string kelime) { ... }
    public async Task<PagedResult<Kitap>> SayfaliGetirAsync(int sayfa, int boyut) { ... }
}

public class KitapBatchServisi      // sadece toplu işlemler
{
    public async Task TopluFiyatGuncelleAsync(int kategoriId, decimal oran) { ... }
    public async Task ArsivleAsync(DateTime kesimTarihi) { ... }
}

public class KitapRaporServisi      // sadece raporlama
{
    public async Task<KitapRaporu> RaporOlusturAsync(DateRange aralik) { ... }
}
// Her dosya ~80-100 satır. Kolayca bulunur, kolayca değiştirilir.
```

---

## 3. Service Locator — "Sürpriz Bağımlılık"

### Günlük hayat analogu

Yeni işe başladın. Müdür diyor ki: "Bir rapor hazırla."  
Sen: "Hangi verilere ihtiyacım var?"  
Müdür: "Bilmiyorum, şirket arşivine gir, kendin bul."

Arşive giriyorsun — binlerce klasör. Hangisini açacaksın? Ne arayacaksın? Bitmedi: raporu test edecek biri var, o da aynı arşive girmek zorunda — neye baktığını bilmiyor.

**Constructor Injection** ise şu: müdür sana işe başlarken masana şunu koyuyor: "Satış tablosu, stok listesi, müşteri veritabanı erişimi — bunlar lazım olacak." Başlamadan ne gerektiğini biliyorsun.

### Kod — aynı sipariş senaryosu

```csharp
// ❌ Service Locator
// Senaryo: sipariş oluştur → stok düş → email at
public class SiparisServisi
{
    private readonly IServiceProvider _provider;
    //                ↑ constructor'a bakan biri ne görüyor?
    //                  "Bu servis IServiceProvider alıyor" — bu hiçbir şey söylemiyor
    //                  Gerçekte ne kullandığını görmek için OlusturAsync'i okuman lazım

    public SiparisServisi(IServiceProvider provider)
        => _provider = provider;

    public async Task OlusturAsync(int kitapId, int adet, string musteri)
    {
        var kitapRepo    = _provider.GetRequiredService<IKitapRepository>();
        //                           ↑ bağımlılık burada ortaya çıkıyor — constructor'da değil
        var siparisRepo  = _provider.GetRequiredService<ISiparisRepository>();
        var emailServis  = _provider.GetRequiredService<IEmailService>();
        //                           ↑ email attığını constructor'dan anlayamazsın

        var kitap = await kitapRepo.GetByIdAsync(kitapId);
        kitap.StokDus(adet);
        await siparisRepo.EkleAsync(new Siparis(kitap, adet, musteri));
        await emailServis.GonderAsync(musteri, "Siparişiniz alındı");
    }
}

// Test yazmak istiyorsun: "SiparisServisi'ni test et, gerçek DB'ye gitmesin"
// Ne mock'layacaksın? Constructor'a baktın: IServiceProvider — bu sana hiçbir şey söylemiyor
// OlusturAsync'i satır satır okumak zorunda kaldın
// Bir metodunu daha eklesem, orada başka bağımlılıklar çıkabilir — haberin olmaz
var servis = new SiparisServisi(???);   // neyi geçeceksin bilmiyorsun
```

```csharp
// ✅ Constructor Injection — aynı senaryo, bağımlılıklar açık
public class SiparisServisi
{
    private readonly IKitapRepository _kitapRepo;
    private readonly ISiparisRepository _siparisRepo;
    private readonly IEmailService _emailServis;
    //               ↑ constructor'a bakan biri anında görüyor:
    //                 "Bu servis repo'ya, siparis repo'ya ve email'e ihtiyaç duyuyor"
    //                 Kod okumana gerek yok

    public SiparisServisi(
        IKitapRepository kitapRepo,
        ISiparisRepository siparisRepo,
        IEmailService emailServis)
    {
        _kitapRepo   = kitapRepo;
        _siparisRepo = siparisRepo;
        _emailServis = emailServis;
        // bunu yazmasaydık → bağımlılıklar metodların içinde _provider.GetRequiredService ile gizlenir
    }

    public async Task OlusturAsync(int kitapId, int adet, string musteri)
    {
        var kitap = await _kitapRepo.GetByIdAsync(kitapId);
        kitap.StokDus(adet);
        await _siparisRepo.EkleAsync(new Siparis(kitap, adet, musteri));
        await _emailServis.GonderAsync(musteri, "Siparişiniz alındı");
    }
}

// Test yazmak istiyorsun: constructor'a baktın, 3 şey lazım, mock'ladın, bitti
var mockKitapRepo   = new Mock<IKitapRepository>();
var mockSiparisRepo = new Mock<ISiparisRepository>();
var mockEmail       = new Mock<IEmailService>();

var servis = new SiparisServisi(mockKitapRepo.Object, mockSiparisRepo.Object, mockEmail.Object);
// neyi geçeceğini biliyorsun — constructor söyledi
```

**Özet fark:** Service Locator'da sınıf ihtiyaçlarını kendisi gidip buluyor — dışarıdan görünmüyor. Constructor Injection'da ihtiyaçlar kapıdan girerken veriliyor — kim baksa görüyor, test eden de görüyor.

---

## 4. Shotgun Surgery — "Tek Değişiklik, 10 Dosya"

### Günlük hayat analogu

Evin her odasında bağımsız bir elektrik tesisatı var. Işıkların rengini değiştirmek istiyorsun — mutfak, salon, yatak odası, banyo... her odaya ayrı ayrı girmek zorundasın. Merkezi bir sistem olsaydı tek yerden değiştirirdin.

### Kod

```csharp
// Senaryo: "Kitap başlığı en az 3, en fazla 200 karakter olsun" kuralı geldi.

// ❌ Shotgun Surgery — kural 5 yerde dağınık
// KitapController.cs
if (model.Baslik.Length < 3 || model.Baslik.Length > 200)
    ModelState.AddModelError("Baslik", "3-200 karakter arası olmalı");

// KitapApiController.cs
if (dto.Baslik.Length < 3 || dto.Baslik.Length > 200)    // kopya
    return BadRequest("Başlık geçersiz");

// EfKitapServisi.cs
if (kitap.Baslik.Length < 3 || kitap.Baslik.Length > 200) // kopya
    throw new Exception("Başlık geçersiz");

// ImportServisi.cs
if (satirData.Baslik?.Length < 3 || satirData.Baslik?.Length > 200) // kopya
    hatalar.Add("Başlık geçersiz");

// KitapFormViewModel.cs
[MinLength(3), MaxLength(200)]  // kopya — attribute olarak
public string Baslik { get; set; }

// Kural değişti: minimum 5 karakter olsun
// → 5 dosya bul, 5 yeri değiştir, birini unutursan tutarsızlık
```

```csharp
// ✅ Tek nokta — KitapBaslikKurali
public static class KitapKurallari
{
    public const int BaslikMinUzunluk = 3;    // değişince tek yer
    public const int BaslikMaxUzunluk = 200;  // bunu yazmasaydık → magic number her yerde

    public static bool BaslikGecerliMi(string baslik)
        => baslik?.Length >= BaslikMinUzunluk && baslik.Length <= BaslikMaxUzunluk;
                                              // bunu yazmasaydık → koşul her yerde kopyalanır
}

// Tüm yerler bunu çağırıyor:
if (!KitapKurallari.BaslikGecerliMi(model.Baslik))
    ModelState.AddModelError("Baslik", "Başlık geçersiz");

// Kural değişti? KitapKurallari.cs'i aç, BaslikMinUzunluk = 5 yap — bitti.
```

**Sinyal:** "Bu kuralı değiştireceğim" dediğinde `Ctrl+Shift+F` ile aramak zorunda kalıyorsun.

---

## 5. Primitive Obsession — "Her Şey String, Her Şey int"

### Günlük hayat analogu

Bir doktor randevu sisteminde hasta yaşını `string` olarak saklıyor: `"yirmi beş"`, `"25 yaş"`, `"25"` — hepsi farklı formatta. Hesap yapamazsın, sıralayamazsın, karşılaştıramazsın. Yaşı `int` olarak saklasaydın bunların hiçbiri sorun olmazdı.

Primitive Obsession: domain kavramını temsil etmek için doğru tipi oluşturmak yerine `int`, `string`, `decimal` kullanmak.

### Kod

```csharp
// ❌ Primitive Obsession
public class Kitap
{
    public decimal Fiyat { get; set; }    // hangi para birimi? TRY mi, USD mi?
    public string Isbn { get; set; }      // format kontrolü yok — "abc" yazılabilir
    public string Email { get; set; }     // geçerli mi? @ var mı? kontrol nerede?
}

// Sonuç: aynı kontroller her yerde tekrar
public class SiparisServisi
{
    public void Olustur(Siparis siparis)
    {
        if (siparis.Kitap.Fiyat <= 0)           // kontrol burada
            throw new Exception("Fiyat geçersiz");
    }
}

public class KampanyaServisi
{
    public void Uygula(Kitap kitap)
    {
        if (kitap.Fiyat <= 0)                   // aynı kontrol burada da
            throw new Exception("Fiyat geçersiz");
    }
}
// ISBN kontrolü? Email kontrolü? Aynı şekilde her yerde kopyalanmış.
```

```csharp
// ✅ Value Object — kural nesnenin içinde, bir kez yazılmış
public record Fiyat
{
    public decimal Deger { get; }
    public string ParaBirimi { get; }           // para birimi artık kavramın parçası

    public Fiyat(decimal deger, string paraBirimi = "TRY")
    {
        if (deger <= 0)                         // kontrol burada — bir kez yazıldı
            throw new ArgumentException("Fiyat sıfırdan büyük olmalı");
        Deger = deger;
        ParaBirimi = paraBirimi;                // bunu yazmasaydık → her yerde if (fiyat <= 0)
    }

    // İki fiyatı topla — para birimi farklıysa patlasın, sessizce yanlış hesaplama yapmasın
    public Fiyat Topla(Fiyat diger)
    {
        if (ParaBirimi != diger.ParaBirimi)
            throw new InvalidOperationException("Farklı para birimleri toplanamaz");
        return new Fiyat(Deger + diger.Deger, ParaBirimi);
    }
}

public record Isbn
{
    public string Deger { get; }

    public Isbn(string deger)
    {
        if (string.IsNullOrWhiteSpace(deger) || deger.Length != 13)
            throw new ArgumentException("ISBN 13 karakter olmalı");  // kontrol bir kez
        if (!deger.All(char.IsDigit))
            throw new ArgumentException("ISBN sadece rakam içermeli");
        Deger = deger;
    }
}

public record Email
{
    public string Deger { get; }

    public Email(string deger)
    {
        if (!deger.Contains('@'))               // kontrol bir kez — her yerde geçerli
            throw new ArgumentException("Geçersiz email");
        Deger = deger.ToLowerInvariant();       // normalize da burada — her yerde küçük harf garantisi
    }
}

// Kullanım
public class Kitap
{
    public Fiyat Fiyat { get; private set; }   // artık decimal değil — para birimi dahil
    public Isbn Isbn { get; private set; }     // artık string değil — format garantili
}

var kitap = new Kitap(new Fiyat(150), new Isbn("9786053604"));
// Kitap oluştuğunda fiyat ve ISBN zaten geçerli — sonra tekrar kontrol etmene gerek yok
```

**Ne zaman Value Object açma?** Aynı kontrol 3+ yerde tekrarlanıyorsa. Tek yerde kalıyorsa primitive yeterli.

---

## 6. Feature Envy — "Komşunun Eşyalarıyla İlgilenen"

### Günlük hayat analogu

Komşunun mutfağını, dolabını, tariflerini sen yönetiyorsun — kendi evin değil. Komşunun bir şeyi değişince sen de etkileniyorsun. Bu iş senin değil.

Kod olarak: bir metodun kendi sınıfının verilerinden çok **başka bir sınıfın** verilerini kullanması.

### Kod

```csharp
// ❌ Feature Envy — SiparisServisi, Kitap'ın iç detaylarına fazla giriyor
public class SiparisServisi
{
    public decimal ToplamHesapla(Kitap kitap, int adet)
    {
        // Bu metodun her satırı Kitap'ın verisini kullanıyor
        // ama Kitap class'ında değil — SiparisServisi'nde
        decimal kdv = kitap.Fiyat * 0.18m;
        decimal indirim = kitap.Stok > 100
            ? kitap.Fiyat * 0.05m
            : 0m;
        decimal birimFiyat = kitap.Fiyat + kdv - indirim;
        return birimFiyat * adet;

        // Kitap'ın stok ve fiyat mantığı değişirse → SiparisServisi de değişmek zorunda
        // Bu bilgi Kitap'a ait, SiparisServisi bu detayı bilmemeli
    }
}
```

```csharp
// ✅ Mantık doğru sınıfa taşındı
public class Kitap
{
    public decimal Fiyat { get; private set; }
    public int Stok { get; private set; }

    public decimal KdvliFiyat()
        => Fiyat * 1.18m;                      // KDV hesabı Kitap'ın işi

    public decimal GecerliSatisFiyati()
        => Stok > 100
            ? Fiyat * 0.95m                    // stok fazlaysa indirim — bu Kitap kuralı
            : Fiyat;                           // bunu yazmasaydık → SiparisServisi stok detayını bilirdi
}

public class SiparisServisi
{
    public decimal ToplamHesapla(Kitap kitap, int adet)
        => kitap.GecerliSatisFiyati() * adet;  // SiparisServisi artık detayı bilmiyor
                                               // Kitap'ın fiyat mantığı değişirse burası değişmez
}
```

**Test:** Stok indirimi kuralı değişti → sadece `Kitap.GecerliSatisFiyati()` değişiyor. `SiparisServisi` dokunulmuyor.

---

## 7. Magic Numbers / Strings — "Bu 42 Neyin Nesi?"

### Günlük hayat analogu

İş yerinde bir süreç belgesi: "Formu 42 gün içinde doldurun, 7 kopya çıkartın, kodu XR-99 girin." 42 nereden geldi? 7 neden? XR-99 neyin kısaltması? Belgeyi yazan adam işten çıktı — kimse bilmiyor.

### Kod

```csharp
// ❌ Magic numbers/strings — rakamlar açıklamasız
public class StokServisi
{
    public void KontrolEt(Kitap kitap)
    {
        if (kitap.Stok < 5)                    // neden 5? nereden geldi bu sayı?
            UyariGonder(kitap);

        if (kitap.Fiyat > 1000)               // neden 1000? iş kuralı mı? vergi eşiği mi?
            OzelIndirimUygula(kitap);
    }
}

public class SenkronizasyonServisi
{
    public void Calistir()
    {
        Thread.Sleep(30000);                   // 30000 ms? 30 saniye mi? 300 saniye mi?
        _cache.Set("kitaplar:all", data);      // "kitaplar:all" string'i 3 yerde mi var? tutarlı mı?
    }
}
```

```csharp
// ✅ Adlandırılmış sabitler — her sayının bir adı var
public static class StokKurallari
{
    public const int KritikStokEsigi = 5;          // adı var → değişince tek yer
    public const decimal OzelIndirimFiyatEsigi = 1000m; // bunu yazmasaydık → magic number her yerde
}

public static class CacheAnahtarlari
{
    public const string TumKitaplar = "kitaplar:all";  // string tek yerde — yazım hatası riski yok
    public const string PopulerKitaplar = "kitaplar:populer";
}

public static class SistemAyarlari
{
    public static readonly TimeSpan SenkronizasyonBeklemesi = TimeSpan.FromSeconds(30);
    // TimeSpan.FromSeconds(30) → 30'un birimi belli: saniye
    // bunu yazmasaydık → Thread.Sleep(30000) ms mi saniye mi? her okuyan hesaplar
}

public class StokServisi
{
    public void KontrolEt(Kitap kitap)
    {
        if (kitap.Stok < StokKurallari.KritikStokEsigi)    // ne anlama geldiği belli
            UyariGonder(kitap);

        if (kitap.Fiyat > StokKurallari.OzelIndirimFiyatEsigi)
            OzelIndirimUygula(kitap);
    }
}
```

**Kritik stok eşiği 5'ten 10'a çıktı?** `StokKurallari.KritikStokEsigi = 10` — bir değişiklik, tüm yerler güncellendi.

---

## 8. SOLID ile Bağlantı

Her anti-pattern aslında bir SOLID ihlali:

| Anti-Pattern | Hangi Prensip | Somut Zarar |
|---|---|---|
| Anemic Domain Model | SRP | İş kuralı entity yerine serviste → kopyalanıyor |
| God Class | SRP | Tek sınıf çok şey yapıyor → her değişiklik oraya dokunuyor |
| Service Locator | DIP | Bağımlılıklar gizli → test edilemiyor |
| Shotgun Surgery | SRP + OCP | Değişim dağınık → birini unutma riski |
| Feature Envy | SRP | Metod yanlış sınıfta → ilgisiz bağımlılık |
| Primitive Obsession | DRY | Aynı kontrol her yerde → tutarsızlık |
| Magic Numbers | — | Okunaksız kod → bakım maliyeti |

**Kural:** Anti-pattern görünce doğrudan düzeltme. Önce hangi SOLID prensibini ihlal ettiğini bul — düzeltme yolu oradan çıkar.

---

## 500 vs 50k — Ne Zaman Önemli?

| Anti-Pattern | 500 kullanıcı | 50k kullanıcı |
|---|---|---|
| **Anemic Domain Model** | Kural az → tolere edilebilir | ❌ Kural 10+ yerde kopyalanıyor, bug kaçınılmaz |
| **God Class** | Tek geliştirici → idare edilir | ❌ Ekip büyüyünce merge conflict felaketi |
| **Service Locator** | Her ölçekte kötü | Her ölçekte kötü |
| **Shotgun Surgery** | 2-3 yer → dikkatli ol | ❌ 10+ yer → her değişiklik riskli |
| **Primitive Obsession** | 1-2 kontrol → primitive yeter | ❌ Aynı kontrol her yerde → Value Object şart |
| **Feature Envy** | Fark edilmez | ❌ Sınıflar birbirine kilitlenir, test edilemez |
| **Magic Numbers** | Her ölçekte kötü | Her ölçekte kötü |

**Genel sinyal — refactor zamanı geldi mi?**
- Bir kuralı değiştirmek için 3+ dosya açıyorsan → Shotgun Surgery
- Bir dosyayı açtığında "hangi satıra bakacağım?" diye arıyorsan → God Class
- Test yazmak için "neyi mock'lamalıyım?" diye kodun içini okuyorsan → Service Locator
- `if (fiyat <= 0)` kontrolünü 4. kez yazıyorsan → Primitive Obsession / Value Object zamanı
