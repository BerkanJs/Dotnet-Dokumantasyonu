# Gün 71 — Modüler Monolith Mimarisi

Bugünün amacı: "tek deployment ama sınırları çizilmiş" modüler yapının ne anlama geldiğini, mikroservisten farkını ve mikroservise geçiş yolunu anlamak.

---

## Gerçek Hayatta Bu Nerede Karşına Çıkar?

Şirkete yeni katıldın. Sistem büyüdü, takım büyüdü, "mikroservise geçelim" konuşmaları başladı.  
Ama codebase tek bir dev API — her şey iç içe, hiçbir sınır yok.

Kitap servisi, sipariş servisi, kullanıcı servisi... hepsi aynı DbContext'te, aynı namespace'te.

Bir şeyi değiştirince başka yer kırılıyor. Mikroservise taşımak için önce modülleri ayırmak gerekiyor — ama nasıl?

İşte tam burada **Modüler Monolith** yaklaşımı başlar.

---

## Modüler Monolith Nedir?

Tek bir deployment (tek process, tek binary) ama içi modüllere bölünmüş.

```
Tek Binary (KitabeviApi.exe)
├── Modül: Katalog       ← kitaplar, kategoriler
├── Modül: Siparis        ← siparişler, sepet
├── Modül: Kullanici      ← üyelik, adresler
└── Shared/               ← ortak altyapı
```

Her modül:
- Kendi klasöründe yaşar
- Kendi DbContext'ine (veya schema'sına) sahiptir
- Diğer modüllere sadece interface üzerinden konuşur
- Direkt tablo erişimi yoktur

---

## Mikroservis ile Farkı

| Konu | Modüler Monolith | Mikroservis |
|---|---|---|
| Deployment | Tek binary | Her servis ayrı deploy |
| Network | Yok — in-process | Var — HTTP/gRPC/mesaj kuyruğu |
| Distributed transaction | Yok | Zor — Saga/Outbox gerekir |
| Operasyon karmaşıklığı | Düşük | Yüksek (K8s, service mesh, tracing) |
| Modüller arası hata yönetimi | Try/catch yeterli | Circuit breaker, retry gerekir |
| Takım ölçeği | 2-15 kişi | 15+ kişi, bağımsız ekipler |
| Geliştirme hızı | Hızlı başlangıç | Yavaş başlangıç, sonra hızlanır |

**Gerçek hayatta kural:**  
→ Domain henüz netleşmemişse mikroservise geçme — sınırları yanlış çizersin.  
→ Önce modüler monolith kur, sınırları keşfet, sonra mikroservise taşı.

---

## Bounded Context = Modül

DDD'den: her bounded context kendi dilini, modelini ve sınırlarını tanımlar.  
Modüler Monolith'te her bounded context bir modül olur.

```
Katalog modülü:   Kitap → { Id, Ad, Fiyat, StokAdedi, Aktif }
Siparis modülü:   Kitap → { KitapId, Ad, BirimFiyat }   ← farklı model!
```

Aynı "Kitap" kavramı iki modülde farklı şekilde temsil edilir.  
Bu kasıtlıdır — her modül kendi bağlamında ne bilmesi gerekiyorsa onu bilir.

---

## Klasör Yapısı

```
KitabeviApi/
  Modules/
    Katalog/
      Features/
        Kitaplar/
          Listele/
          Ekle/
          Guncelle/
      Domain/
        Kitap.cs
      Persistence/
        KatalogDbContext.cs     ← sadece bu modülün tabloları
        KatalogMappings.cs
      Public/
        IKatalogService.cs      ← dışarıya açık arayüz (diğer modüller bunu kullanır)
        KatalogService.cs
    Siparis/
      Features/
        Siparisler/
          Olustur/
          IptalEt/
      Domain/
        Siparis.cs
        SiparisKalemi.cs
      Persistence/
        SiparisDbContext.cs     ← sadece bu modülün tabloları
      Public/
        ISiparisService.cs
    Kullanici/
      ...
  Shared/
    Behaviors/
      ValidationBehavior.cs
    Exceptions/
      DomainException.cs
  Program.cs
```

---

## Modüller Arası İletişim — Interface Üzerinden

Siparis modülü, kitap bilgisine ihtiyaç duyuyor.  
**Yanlış yol:** Siparis modülü `KatalogDbContext`'e veya `Kitap` tablosuna direkt eriş.  
**Doğru yol:** Katalog modülünün public interface'ini kullan.

```csharp
// Modules/Katalog/Public/IKatalogService.cs
public interface IKatalogService
{
    Task<KitapBilgisiDto?> KitapGetirAsync(int kitapId, CancellationToken ct);
    // bunu interface olarak tanımlamasaydık → Siparis modülü Katalog'un implementasyonuna bağlanır
    // implement değişince Siparis de kırılır
}

public record KitapBilgisiDto(int Id, string Ad, decimal Fiyat, int StokAdedi);
// DTO olarak tanımladık — Katalog'un iç Kitap entity'sini değil, dışarıya açık modeli taşıyoruz
// bunu entity olarak verseydk → Siparis modülü Katalog'un domain modeline bağımlı olur
```

```csharp
// Modules/Katalog/Public/KatalogService.cs
public class KatalogService : IKatalogService
{
    private readonly KatalogDbContext _db;

    public KatalogService(KatalogDbContext db) => _db = db;

    public async Task<KitapBilgisiDto?> KitapGetirAsync(int kitapId, CancellationToken ct)
    {
        return await _db.Kitaplar
            .Where(k => k.Id == kitapId && k.Aktif)
            .Select(k => new KitapBilgisiDto(k.Id, k.Ad, k.Fiyat, k.StokAdedi))
            .AsNoTracking()                     // okuma — tracking gereksiz
            .FirstOrDefaultAsync(ct);
    }
}
```

```csharp
// Modules/Siparis/Features/Siparisler/Olustur/SiparisOlusturHandler.cs
public class SiparisOlusturHandler : IRequestHandler<SiparisOlusturCommand, int>
{
    private readonly SiparisDbContext _db;
    private readonly IKatalogService _katalog;   // Katalog modülüne interface üzerinden bağlanıyoruz
                                                  // direkt KatalogDbContext inject etseydk → modül sınırı ihlal

    public SiparisOlusturHandler(SiparisDbContext db, IKatalogService katalog)
    {
        _db = db;
        _katalog = katalog;
    }

    public async Task<int> Handle(SiparisOlusturCommand command, CancellationToken ct)
    {
        var kitap = await _katalog.KitapGetirAsync(command.KitapId, ct);
        // _katalog → in-process çağrı, network yok, hata yönetimi basit
        // mikroserviste bu HTTP/gRPC çağrısı olurdu → retry, timeout, circuit breaker gerekirdi

        if (kitap is null)
            return Result.Failure<int>("Kitap bulunamadı");

        var siparis = new Siparis
        {
            KitapId = kitap.Id,
            KitapAdi = kitap.Ad,            // sipariş anındaki adı kaydet — Katalog değişirse etkilenmesin
            BirimFiyat = kitap.Fiyat,       // aynı şekilde fiyatı snapshot al
            Adet = command.Adet,
            MusteriId = command.MusteriId
        };

        _db.Siparisler.Add(siparis);
        await _db.SaveChangesAsync(ct);
        return siparis.Id;
    }
}
```

---

## DB İzolasyonu — Ayrı Schema veya Ayrı DbContext

İki yöntem var:

**Yöntem 1: Ayrı Schema (aynı DB, farklı tablo prefix)**

```csharp
// KatalogDbContext
protected override void OnModelCreating(ModelBuilder mb)
{
    mb.HasDefaultSchema("katalog");         // bütün tablolar "katalog." prefix alır
    // bunu yazmasaydık → tablolar "dbo." altında, Siparis modülüyle karışık
}

// SiparisDbContext
protected override void OnModelCreating(ModelBuilder mb)
{
    mb.HasDefaultSchema("siparis");
}
```

Veritabanında:
```
katalog.Kitaplar
katalog.Kategoriler
siparis.Siparisler
siparis.SiparisKalemleri
kullanici.Kullanicilar
```

**Yöntem 2: Tek DbContext, Entity'leri izole et**

Küçük projede ayrı DbContext kurmak erken olabilir.  
Tek `AppDbContext` içinde her modülün entity'leri mantıksal olarak ayrılır, direkt cross-modül sorgu yazılmaz.

**Gerçek hayatta ne zaman hangi yöntem?**  
→ Proje küçükse → tek DbContext, schema ayrımı yeterli  
→ Modüller farklı hızda büyüyorsa → ayrı DbContext (ileride ayrı DB'ye taşıma kolaylaşır)

---

## Modüller Arası Tablo Erişimi Neden Yasak?

```csharp
// YANLIŞ — Siparis modülü Katalog'un tablosuna direkt giriyor
public async Task Handle(...)
{
    var kitap = await _katalogDb.Kitaplar    // ← Siparis modülü Katalog'un DbContext'ini biliyor
        .FindAsync(command.KitapId);          // modül sınırı yok sayıldı
}
```

Bu yanlış çünkü:
- Katalog, `Kitap` entity yapısını değiştirirse Siparis kırılır
- Mikroservise taşırken Katalog'un tablosu erişilemez hale gelir — Siparis direkt patlar
- Test etmek için iki DbContext mock etmek gerekir

**Doğru yol:** Katalog'un public interface'i üzerinden git — implementasyon nasıl değişirse değişsin, contract sabit kalır.

---

## Modüler Monolith → Mikroservis Yolculuğu

```
1. Modülleri izole et
   → Ayrı DbContext, ayrı schema, interface üzerinden iletişim

2. Modüller arası çağrıları event-based yap
   → Direkt interface çağrısı yerine domain event yayınla
   → Katalog: KitapFiyatiGuncellendiEvent
   → Siparis modülü bu event'i subscribe eder

3. Hazır olunca modülü ayrı servise taşı (Strangler Fig)
   → Katalog modülü → KatalogApi olarak ayrılır
   → Interface çağrısı → HTTP çağrısına dönüşür
   → Geri kalan monolith değişmez
```

Bu yolculuğun çalışması için modüllerin baştan izole edilmesi şarttır.  
Sınırlar yoksa taşımak yeniden yazmak demektir.

---

## Kayıt — Program.cs

```csharp
// Her modülün kendi extension metodu var
builder.Services.AddKatalogModule(builder.Configuration);
// bunu yazmasaydık → Katalog modülü DI'a kayıtlı değil, IKatalogService inject edilemez

builder.Services.AddSiparisModule(builder.Configuration);
builder.Services.AddKullaniciModule(builder.Configuration);
```

```csharp
// Modules/Katalog/KatalogModuleExtensions.cs
public static class KatalogModuleExtensions
{
    public static IServiceCollection AddKatalogModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddDbContext<KatalogDbContext>(opt =>
            opt.UseSqlServer(config.GetConnectionString("Katalog")));
        // ayrı connection string → ileride ayrı DB'ye taşımak kolay

        services.AddScoped<IKatalogService, KatalogService>();
        // Scoped — her HTTP isteği için bir instance, DbContext ile uyumlu

        return services;
    }
}
```

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de tüm modeller tek `AppDbContext`'te, controller'lar birbirinin servislerini direkt çağırıyordu:

```csharp
// Faz2 — SiparisController direkt Kitap tablosuna giriyor
var kitap = await _db.Kitaplar.FindAsync(id);  // modül sınırı yok
kitap.StokAdedi -= adet;                        // başka "modül"ün verisini değiştiriyor
await _db.SaveChangesAsync();
```

500 kullanıcıda çalışır. 50k'da ekip 5 kişiye çıktığında herkes her şeye dokunur, çakışma kaçınılmaz.

Faz3 Modüler Monolith'te Siparis modülü Katalog'u interface üzerinden çağırır, stok düşürme Katalog'un kendi sorumluluğundadır.

---

## 500 vs 50K Kullanıcı

| Konu | 500 kullanıcı/ay | 50K kullanıcı/ay |
|---|---|---|
| Modüler Monolith kurma | Erken olabilir | Büyüme planı varsa baştan kur |
| Ayrı DbContext | Overengineering | Modül izolasyonu için mantıklı |
| Interface üzerinden iletişim | Güzel alışkanlık | Kritik — mikroservise geçiş için şart |
| Mikroservise geçiş | Kesinlikle erken | Ekip 15+ olunca gündemine girer |
| Event-based modül iletişimi | Erken | Sıkı bağımlılık kırmak için değerli |

**Overengineering sinyali:** 2 kişilik ekip, 1 yıllık proje, 3 modül için tam mikroservis altyapısı (K8s, service mesh, distributed tracing). Önce modüler monolith yeterlidir.

---

## Ne Zaman Modüler Monolith, Ne Zaman Mikroservis?

```
Modüler Monolith tercih et:
✓ Ekip küçük (< 15 kişi)
✓ Domain henüz netleşmemiş
✓ Operasyon karmaşıklığı istemiyorsun
✓ Mikroservise geçiş planı var ama hazır değilsin

Mikroservis geç:
✓ Modüller farklı ölçekleme ihtiyacı gösteriyor (Katalog 10x, Siparis 1x)
✓ Ekipler bağımsız deploy etmek istiyor
✓ Modül sınırları netleşti ve değişmiyor
✓ Operasyon olgunluğu var (DevOps, monitoring, tracing)
```

---

## Mini Özet

- Modüler Monolith: tek deployment, modül sınırları çizilmiş.
- Her modül kendi DbContext'i (veya schema'sı) ile izole çalışır.
- Modüller yalnızca public interface üzerinden birbirine konuşur — tablo erişimi yasak.
- Mikroservise geçişin ön koşulu: modüllerin baştan izole edilmesi.
- Küçük ekip için mikroservisten çok daha pratik başlangıç noktası.

---

## Kontrol Soruları

1. Siparis modülü neden Katalog modülünün `DbContext`'ini direkt kullanamaz?
2. Modüller arası iletişimde interface kullanmak, mikroservise geçişi nasıl kolaylaştırır?
3. "Ayrı schema" ile "ayrı DbContext" yaklaşımları arasındaki fark nedir, ne zaman hangisi?
4. Bir modülü mikroservise taşırken Strangler Fig ne anlama gelir?
