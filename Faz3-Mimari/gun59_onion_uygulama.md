# Gün 59 — Onion Architecture Uygulama

Gün 58'deki teorik yapıyı sıfırdan implement ediyoruz. Hedef: **çalışan bir "Sipariş Oluştur" use case'i** — Domain → Application → Infrastructure → API zinciri.

Faz2'de `KitapController` doğrudan `KitabeviDbContext`'e bağımlıydı. Bugün aynı iş mantığını 4 katmana ayırıyoruz. Değiştirilecek tek şey göründüğünde sadece o katmanın içi değişecek.

---

## Proje Yapısı

```
KitabeviOnion/
├── src/
│   ├── KitabeviOnion.Domain/           ← hiçbir NuGet bağımlılığı yok
│   ├── KitabeviOnion.Application/      ← sadece Domain'e bağımlı
│   ├── KitabeviOnion.Infrastructure/   ← EF Core burada
│   └── KitabeviOnion.API/              ← ASP.NET Core burada
```

Proje referansları:
```
API          → Application + Infrastructure
Infrastructure → Application + Domain
Application  → Domain
Domain       → hiçbir şey
```

---

## 1. Domain Katmanı

### `Exceptions/DomainException.cs`

```csharp
namespace KitabeviOnion.Domain.Exceptions;

public class DomainException : Exception
//           ↑ Exception'dan kalıtım — catch bloklarında ayrı yakalanabilmesi için
//             bunu yazmasaydık → ArgumentException veya InvalidOperationException
//             fırlatırdık, Domain'den gelen hatayı Infrastructure hatasından ayırt edemezdik
{
    public DomainException(string mesaj) : base(mesaj) { }
    //                                    ↑ mesajı base Exception'a ilet
    //                                      bunu yazmasaydık → mesaj kaybedilirdi
}
```

---

### `ValueObjects/Fiyat.cs`

```csharp
namespace KitabeviOnion.Domain.ValueObjects;

public record Fiyat
// ↑ record: value semantics — iki Fiyat(100) nesnesi otomatik eşit
//   bunu yazmasaydık → class yazardık, == operatörünü elle override etmek zorunda kalırdık
{
    public decimal Deger { get; }
    public string ParaBirimi { get; }

    public Fiyat(decimal deger, string paraBirimi = "TRY")
    {
        if (deger <= 0)
            throw new DomainException("Fiyat 0'dan büyük olmalı");
        //  ↑ kural Domain'de — Controller veya Service yazmak zorunda değil
        //    bunu yazmasaydık → negatif fiyatlı kitap DB'ye gidebilirdi

        Deger = deger;
        ParaBirimi = paraBirimi;
    }

    public Fiyat KdvEkle(decimal oran = 0.18m)
        => new(Deger * (1 + oran), ParaBirimi);
    //  ↑ yeni Fiyat döner — mevcut Fiyat değişmez (immutable)
    //    bunu yazmasaydık → Fiyat.Deger * 1.18 her serviste tekrar yazılırdı,
    //    oran değişince her yeri bulmak zorunda kalırdık
}
```

---

### `ValueObjects/Isbn.cs`

```csharp
namespace KitabeviOnion.Domain.ValueObjects;

public record Isbn
{
    public string Deger { get; }

    public Isbn(string deger)
    {
        var temiz = deger.Replace("-", "").Replace(" ", "");
        //          ↑ kullanıcı "978-0-13-110362-7" yazsa da çalışsın

        if (temiz.Length != 13 || !temiz.All(char.IsDigit))
            throw new DomainException($"Geçersiz ISBN: {deger}");
        //  ↑ format kuralı tek yerde — Controller, Service bilmek zorunda değil
        //    bunu yazmasaydık → "abc" ISBN olarak DB'ye girebilirdi

        Deger = temiz;
    }

    public override string ToString() => Deger;
}
```

---

### `Entities/Kitap.cs`

**Faz2 ile karşılaştırma:**

```csharp
// Faz2 — KitabeviMVC/Models/Entities/Kitap.cs
public class Kitap
{
    public int Id { get; set; }
    public decimal Fiyat { get; set; }  // primitive, format/kural yok
    public int StokAdedi { get; set; }  // public set → dışarıdan -99 yazılabilir
}
// Stok kontrolü her serviste if yazıldı — 3 serviste 3 farklı kural
```

**Onion Domain entity:**

```csharp
namespace KitabeviOnion.Domain.Entities;

public class Kitap
{
    public int Id { get; private set; }
    //              ↑ private set: dışarıdan Id değiştirilemez
    //                bunu yazmasaydık → başka kod kitap.Id = 999 yapabilirdi

    public string Baslik { get; private set; } = null!;
    public Isbn Isbn { get; private set; } = null!;
    //          ↑ string değil Isbn Value Object — format kuralı otomatik geliyor
    //            bunu yazmasaydık → geçersiz ISBN DB'ye girebilirdi

    public Fiyat Fiyat { get; private set; } = null!;
    //           ↑ decimal değil Fiyat Value Object — negatif fiyat engelleniyor
    //             bunu yazmasaydık → fiyat.KdvEkle() metodu olmazdı

    public int StokAdedi { get; private set; }
    //                     ↑ private set: stok değişikliği sadece metodlar üzerinden

    // EF Core için protected constructor (dışarıdan new Kitap() kapatıyoruz)
    protected Kitap() { }
    //         ↑ bunu yazmasaydık → EF Core reflection ile nesne oluşturamazdı

    public Kitap(string baslik, Isbn isbn, Fiyat fiyat, int ilkStok)
    {
        if (string.IsNullOrWhiteSpace(baslik))
            throw new DomainException("Başlık boş olamaz");

        if (ilkStok < 0)
            throw new DomainException("Stok negatif olamaz");

        Baslik = baslik;
        Isbn = isbn;
        Fiyat = fiyat;
        StokAdedi = ilkStok;
    }

    public bool StokVarMi() => StokAdedi > 0;
    //  ↑ iş kuralı Domain'de — Application veya Controller yazmak zorunda değil
    //    bunu yazmasaydık → if (kitap.StokAdedi > 0) her yerde tekrar yazılırdı

    public void StokAzalt(int adet)
    {
        if (adet <= 0)
            throw new DomainException("Azaltılacak adet sıfırdan büyük olmalı");

        if (adet > StokAdedi)
            throw new DomainException($"Yetersiz stok. Mevcut: {StokAdedi}, İstenen: {adet}");
        //  ↑ invariant: stok negatife düşemez — bu kural Kitap'ın sorumluluğu
        //    bunu yazmasaydık → stok kontrolü her sipariş servisinde tekrar yazılırdı

        StokAdedi -= adet;
    }

    public void FiyatGuncelle(Fiyat yeniFiyat)
    {
        Fiyat = yeniFiyat;
        // Fiyat Value Object — yeni Fiyat nesnesi atanıyor, eski kayboluyor
    }
}
```

---

### `Entities/Siparis.cs`

```csharp
namespace KitabeviOnion.Domain.Entities;

public enum SiparisDurumu { Beklemede, Onaylandi, Iptal }
//           ↑ string değil enum — geçersiz durum derlemede yakalanır
//             bunu yazmasaydık → siparis.Durum = "ONAYLNDI" typo fark edilmezdi

public class Siparis
{
    public int Id { get; private set; }
    public string KullaniciId { get; private set; } = null!;
    public SiparisDurumu Durum { get; private set; }

    private readonly List<SiparisKalemi> _kalemler = [];
    public IReadOnlyList<SiparisKalemi> Kalemler => _kalemler;
    //     ↑ IReadOnlyList: dışarıdan .Add() / .Remove() çağrılamaz
    //       bunu yazmasaydık → herkes siparis.Kalemler.Add() yapabilirdi, kural devre dışı

    private readonly List<string> _domainEvents = [];
    public IReadOnlyList<string> DomainEvents => _domainEvents;
    //                            ↑ basit string event — gerçek projede INotification

    protected Siparis() { }

    public Siparis(string kullaniciId)
    {
        if (string.IsNullOrWhiteSpace(kullaniciId))
            throw new DomainException("Kullanıcı Id boş olamaz");

        KullaniciId = kullaniciId;
        Durum = SiparisDurumu.Beklemede;
        //      ↑ yeni sipariş her zaman Beklemede başlar — bu kural burada
    }

    public void KalemEkle(int kitapId, string kitapBaslik, Fiyat birimFiyat, int adet)
    {
        if (Durum != SiparisDurumu.Beklemede)
            throw new DomainException("Onaylanmış veya iptal siparişe kalem eklenemez");
        //  ↑ durum geçişi kuralı — Application veya Controller yazmak zorunda değil
        //    bunu yazmasaydık → onaylanmış siparişe kalem eklenebilirdi

        if (adet <= 0)
            throw new DomainException("Adet sıfırdan büyük olmalı");

        _kalemler.Add(new SiparisKalemi(kitapId, kitapBaslik, birimFiyat, adet));
    }

    public void Onayla()
    {
        if (!_kalemler.Any())
            throw new DomainException("Boş sipariş onaylanamaz");
        //  ↑ invariant: içeriksiz sipariş onaylanamaz
        //    bunu yazmasaydık → 0 TL sipariş ödeme servisine gidebilirdi

        if (Durum != SiparisDurumu.Beklemede)
            throw new DomainException("Sadece beklemedeki sipariş onaylanabilir");

        Durum = SiparisDurumu.Onaylandi;

        _domainEvents.Add($"SiparisOnaylandi:{Id}:{KullaniciId}");
        //             ↑ "ne oldu" haberi — kim dinlerse o tepki verir (email, bildirim)
        //               bunu yazmasaydık → email göndermeyi burada yapmak zorunda kalırdık,
        //               Siparis email servisine bağımlı hale gelirdi (SRP ihlali)
    }

    public decimal ToplamTutar()
        => _kalemler.Sum(k => k.BirimFiyat.Deger * k.Adet);
    //  ↑ hesap root'ta — her zaman güncel, dışarıdan değiştirilemez
}

public class SiparisKalemi
{
    public int Id { get; private set; }
    public int KitapId { get; private set; }
    public string KitapBaslik { get; private set; } = null!;
    public Fiyat BirimFiyat { get; private set; } = null!;
    public int Adet { get; private set; }

    protected SiparisKalemi() { }

    internal SiparisKalemi(int kitapId, string kitapBaslik, Fiyat birimFiyat, int adet)
    //       ↑ internal: sadece Siparis.KalemEkle çağırabilir
    //         bunu yazmasaydık → dışarıdan new SiparisKalemi() ile Siparis bypass edilirdi
    {
        KitapId = kitapId;
        KitapBaslik = kitapBaslik;
        BirimFiyat = birimFiyat;
        Adet = adet;
    }

    public decimal SatirToplami() => BirimFiyat.Deger * Adet;
}
```

---

### `Interfaces/IKitapRepository.cs`

```csharp
namespace KitabeviOnion.Domain.Interfaces;

public interface IKitapRepository
// ↑ interface Domain katmanında tanımlanıyor — kim implement ettiğini bilmiyor
//   bunu yazmasaydık → Application, EF Core'u doğrudan çağırmak zorunda kalırdı
//   SQL Server'dan PostgreSQL'e geçince Application da değişirdi
{
    Task<Kitap?> BulByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Kitap>> TumunuGetirAsync(CancellationToken ct = default);
    Task EkleAsync(Kitap kitap, CancellationToken ct = default);
    Task<bool> IsbnMevcutMu(Isbn isbn, CancellationToken ct = default);
}
```

---

### `Interfaces/ISiparisRepository.cs`

```csharp
namespace KitabeviOnion.Domain.Interfaces;

public interface ISiparisRepository
{
    Task<Siparis?> BulByIdAsync(int id, CancellationToken ct = default);
    Task EkleAsync(Siparis siparis, CancellationToken ct = default);
    Task KaydetAsync(CancellationToken ct = default);
    // ↑ Unit of Work — değişiklikleri tek seferde kaydet
    //   bunu yazmasaydık → her repository kendi SaveChanges çağırırdı,
    //   transaction yönetimi dağılırdı
}
```

---

## 2. Application Katmanı

Application, Domain'i biliyor. Infrastructure'ı bilmiyor — sadece interface görüyor.

### `Interfaces/IEmailService.cs`

```csharp
namespace KitabeviOnion.Application.Interfaces;

public interface IEmailService
// ↑ Application katmanında tanımlanıyor — Infrastructure implement edecek
//   bunu yazmasaydık → Handler doğrudan SmtpClient çağırırdı, test için gerçek SMTP gerekirdi
{
    Task GonderAsync(string alici, string konu, string govde, CancellationToken ct = default);
}
```

---

### `DTOs/KitapDto.cs`

```csharp
namespace KitabeviOnion.Application.DTOs;

public record KitapDto(
//            ↑ record: immutable DTO, değer eşitliği otomatik
    int Id,
    string Baslik,
    string Isbn,
    decimal Fiyat,
    string ParaBirimi,
    int StokAdedi
);
// Domain entity'yi olduğu gibi döndürmiyoruz — API sözleşmesi ayrı
// bunu yazmasaydık → internal entity alanları (private set) API'ye sızardı,
// domain modeli değişince API kontratı da bozulurdu
```

---

### `UseCases/KitapListele/KitapListeleQuery.cs`

```csharp
namespace KitabeviOnion.Application.UseCases.KitapListele;

public record KitapListeleQuery();
// ↑ CQRS Query — okuma isteği, side effect yok
//   record: değer eşitliği, immutable — query nesneleri değişmemeli
```

---

### `UseCases/KitapListele/KitapListeleHandler.cs`

**Faz2 ile karşılaştırma:**

```csharp
// Faz2 — KitapController.cs içinde doğrudan
public async Task<IActionResult> Index()
{
    var kitaplar = await _context.Kitaplar.ToListAsync(); // Controller DB'yi biliyor
    return View(kitaplar);
}
```

**Onion Application handler:**

```csharp
namespace KitabeviOnion.Application.UseCases.KitapListele;

public class KitapListeleHandler
{
    private readonly IKitapRepository _kitapRepo;
    //               ↑ Domain interface — EF Core görmüyor
    //                 bunu yazmasaydık → DbContext inject etmek zorunda kalırdık

    public KitapListeleHandler(IKitapRepository kitapRepo)
    {
        _kitapRepo = kitapRepo;
    }

    public async Task<IReadOnlyList<KitapDto>> Handle(
        KitapListeleQuery query,
        CancellationToken ct = default)
    {
        var kitaplar = await _kitapRepo.TumunuGetirAsync(ct);
        //                              ↑ interface metodu — hangi DB olduğunu bilmiyor

        return kitaplar
            .Select(k => new KitapDto(
                k.Id,
                k.Baslik,
                k.Isbn.Deger,        // Value Object → primitive'e çevir
                k.Fiyat.Deger,
                k.Fiyat.ParaBirimi,
                k.StokAdedi))
            .ToList()
            .AsReadOnly();
        // ↑ Domain entity → DTO dönüşümü Application'da — Controller bilmek zorunda değil
        //   bunu yazmasaydık → Controller mapping yapardı, domain değişince Controller da değişirdi
    }
}
```

---

### `UseCases/SiparisOlustur/SiparisOlusturCommand.cs`

```csharp
namespace KitabeviOnion.Application.UseCases.SiparisOlustur;

public record SiparisOlusturCommand(
    string KullaniciId,
    int KitapId,
    int Adet
);
// ↑ CQRS Command — yazma isteği, side effect var (DB değişecek)
//   record: immutable — command nesneleri uçuşta değişmemeli
```

---

### `UseCases/SiparisOlustur/SiparisOlusturResult.cs`

```csharp
namespace KitabeviOnion.Application.UseCases.SiparisOlustur;

public record SiparisOlusturResult(int SiparisId, decimal ToplamTutar);
// ↑ Hangi sipariş oluştu, ne kadar tuttu — Controller bu bilgiyi API response'a çevirir
```

---

### `UseCases/SiparisOlustur/SiparisOlusturHandler.cs`

```csharp
namespace KitabeviOnion.Application.UseCases.SiparisOlustur;

public class SiparisOlusturHandler
{
    private readonly IKitapRepository _kitapRepo;
    private readonly ISiparisRepository _siparisRepo;
    private readonly IEmailService _emailService;
    // ↑ üç bağımlılığın hepsi interface — Infrastructure'ın ne kullandığını bilmiyor
    //   bunu yazmasaydık → test için gerçek DB ve gerçek SMTP gerekirdi

    public SiparisOlusturHandler(
        IKitapRepository kitapRepo,
        ISiparisRepository siparisRepo,
        IEmailService emailService)
    {
        _kitapRepo = kitapRepo;
        _siparisRepo = siparisRepo;
        _emailService = emailService;
    }

    public async Task<SiparisOlusturResult> Handle(
        SiparisOlusturCommand cmd,
        CancellationToken ct = default)
    {
        // 1. Kitabı getir
        var kitap = await _kitapRepo.BulByIdAsync(cmd.KitapId, ct);

        if (kitap is null)
            throw new DomainException($"Kitap bulunamadı: {cmd.KitapId}");
        //  ↑ uygulama akışı kuralı — var mı yok mu kontrolü

        // 2. Domain kuralı: stok yeterli mi?
        kitap.StokAzalt(cmd.Adet);
        // ↑ "adet > stok ise hata fırlat" kuralı Kitap.StokAzalt içinde
        //   Handler bu if'i yazmak zorunda değil — kurala uymak zorunda olan Domain

        // 3. Sipariş oluştur
        var siparis = new Siparis(cmd.KullaniciId);
        //                        ↑ Domain entity constructor — durum Beklemede başlar

        siparis.KalemEkle(kitap.Id, kitap.Baslik, kitap.Fiyat, cmd.Adet);
        //      ↑ "onaylanmış siparişe kalem eklenemez" kuralı Siparis içinde
        //        Handler bu kontrolü yazmak zorunda değil

        siparis.Onayla();
        // ↑ "boş sipariş onaylanamaz" kuralı Siparis içinde
        //   aynı zamanda domain event toplandı: "SiparisOnaylandi:..."

        // 4. Kaydet
        await _siparisRepo.EkleAsync(siparis, ct);
        await _siparisRepo.KaydetAsync(ct);
        // ↑ transaction: iki repo aynı SaveChanges'te — ya ikisi de ya hiçbiri

        // 5. Bildirim — domain event'e tepki
        await _emailService.GonderAsync(
            alici: cmd.KullaniciId,
            konu: "Siparişiniz Alındı",
            govde: $"Sipariş #{siparis.Id} oluşturuldu. Tutar: {siparis.ToplamTutar():C}",
            ct: ct);
        // ↑ email burada — ama Siparis sınıfı EmailService'i bilmiyor (SRP korundu)
        //   bunu yazmasaydık → email Siparis.Onayla() içinde olurdu, test için gerçek SMTP gerekirdi

        return new SiparisOlusturResult(siparis.Id, siparis.ToplamTutar());
    }
}
```

---

## 3. Infrastructure Katmanı

Infrastructure, Application + Domain'i biliyor. API'yi bilmiyor.

### `Persistence/AppDbContext.cs`

```csharp
namespace KitabeviOnion.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Kitap> Kitaplar => Set<Kitap>();
    public DbSet<Siparis> Siparisler => Set<Siparis>();
    public DbSet<SiparisKalemi> SiparisKalemleri => Set<SiparisKalemi>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Kitap konfigürasyonu
        modelBuilder.Entity<Kitap>(e =>
        {
            e.HasKey(k => k.Id);

            e.OwnsOne(k => k.Fiyat, f =>
            //  ↑ Value Object → owned entity: ayrı tablo değil, aynı satırda kolon
            //    bunu yazmasaydık → EF Core Fiyat'ı nasıl map edeceğini bilemezdi
            {
                f.Property(f => f.Deger).HasColumnName("Fiyat").HasPrecision(18, 2);
                f.Property(f => f.ParaBirimi).HasColumnName("ParaBirimi").HasMaxLength(3);
            });

            e.OwnsOne(k => k.Isbn, i =>
            {
                i.Property(i => i.Deger).HasColumnName("Isbn").HasMaxLength(13);
            });

            e.Property(k => k.Baslik).HasMaxLength(200).IsRequired();
        });

        // Siparis konfigürasyonu
        modelBuilder.Entity<Siparis>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Durum).HasConversion<string>();
            //                        ↑ enum → string olarak sakla: "Onaylandi" okunabilir
            //                          bunu yazmasaydık → 0, 1, 2 sayıları saklanırdı, DB'de anlamı yoktu

            e.HasMany<SiparisKalemi>("_kalemler")   // private field'ı backing field olarak tanımla
             .WithOne()
             .HasForeignKey("SiparisId");
            // ↑ EF Core private _kalemler listesini yönetecek
            //   bunu yazmasaydık → EF Core private listeyi göremez, kalemler yüklenmezdi

            e.Ignore(s => s.DomainEvents);
            // ↑ DomainEvents DB'ye kaydedilmez — sadece uçuş sırasında kullanılır
        });

        modelBuilder.Entity<SiparisKalemi>(e =>
        {
            e.HasKey(k => k.Id);
            e.OwnsOne(k => k.BirimFiyat, f =>
            {
                f.Property(f => f.Deger).HasColumnName("BirimFiyat").HasPrecision(18, 2);
                f.Property(f => f.ParaBirimi).HasColumnName("ParaBirimi").HasMaxLength(3);
            });
        });
    }
}
```

---

### `Persistence/Repositories/KitapRepository.cs`

```csharp
namespace KitabeviOnion.Infrastructure.Persistence.Repositories;

public class KitapRepository : IKitapRepository
// ↑ Domain'deki interface'i implement ediyor — Application'ın göreceği tek şey interface
{
    private readonly AppDbContext _context;
    //               ↑ EF Core sadece burada — Domain ve Application bilmiyor

    public KitapRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Kitap?> BulByIdAsync(int id, CancellationToken ct = default)
        => await _context.Kitaplar
            .FirstOrDefaultAsync(k => k.Id == id, ct);

    public async Task<IReadOnlyList<Kitap>> TumunuGetirAsync(CancellationToken ct = default)
        => await _context.Kitaplar
            .AsNoTracking()
            //  ↑ okuma sorgusu: change tracking kapalı → daha hızlı, daha az bellek
            //    bunu yazmasaydık → EF Core tüm nesneleri izler, gereksiz overhead
            .ToListAsync(ct);

    public async Task EkleAsync(Kitap kitap, CancellationToken ct = default)
        => await _context.Kitaplar.AddAsync(kitap, ct);

    public async Task<bool> IsbnMevcutMu(Isbn isbn, CancellationToken ct = default)
        => await _context.Kitaplar
            .AnyAsync(k => k.Isbn.Deger == isbn.Deger, ct);
}
```

---

### `Persistence/Repositories/SiparisRepository.cs`

```csharp
namespace KitabeviOnion.Infrastructure.Persistence.Repositories;

public class SiparisRepository : ISiparisRepository
{
    private readonly AppDbContext _context;

    public SiparisRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Siparis?> BulByIdAsync(int id, CancellationToken ct = default)
        => await _context.Siparisler
            .Include("_kalemler")
            //       ↑ private backing field adını string olarak belirt
            //         bunu yazmasaydık → kalemler yüklenmez, ToplamTutar() sıfır dönerdi
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task EkleAsync(Siparis siparis, CancellationToken ct = default)
        => await _context.Siparisler.AddAsync(siparis, ct);

    public async Task KaydetAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
    //   ↑ tek SaveChanges: kitap stok değişikliği + yeni sipariş tek transaction'da
    //     bunu yazmasaydık → her repository ayrı kaydetse, stok düşer ama sipariş kaydedilmeyebilirdi
}
```

---

### `Services/EmailService.cs`

```csharp
namespace KitabeviOnion.Infrastructure.Services;

public class EmailService : IEmailService
// ↑ Application'daki interface'i implement ediyor
//   bunu yazmasaydık → Application IEmailService'i çözemezdi, DI hata verirdi
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public async Task GonderAsync(string alici, string konu, string govde, CancellationToken ct = default)
    {
        // Gerçek projede: SmtpClient veya SendGrid buraya
        _logger.LogInformation("Email gönderildi → {Alici} | {Konu}", alici, konu);
        await Task.CompletedTask;
    }
}
```

---

## 4. API Katmanı

### `Controllers/KitapController.cs`

**Faz2 ile karşılaştırma:**

```csharp
// Faz2 — Controller hem DB'yi hem iş mantığını biliyor
public class KitapController : Controller
{
    private readonly KitabeviDbContext _context; // DB'ye doğrudan bağımlı
    public async Task<IActionResult> Index()
        => View(await _context.Kitaplar.ToListAsync());
}
```

**Onion API Controller:**

```csharp
namespace KitabeviOnion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KitapController : ControllerBase
{
    private readonly KitapListeleHandler _listeleHandler;
    //               ↑ Handler inject edildi — Controller DB'yi, EF Core'u bilmiyor
    //                 bunu yazmasaydık → Controller DbContext görürdü, katman ayrımı bozulurdu

    public KitapController(KitapListeleHandler listeleHandler)
    {
        _listeleHandler = listeleHandler;
    }

    [HttpGet]
    public async Task<IActionResult> Listele(CancellationToken ct)
    {
        var sonuc = await _listeleHandler.Handle(new KitapListeleQuery(), ct);
        //                                        ↑ query nesnesi oluştur — handler'a teslim et
        return Ok(sonuc);
        //         ↑ DTO listesi — domain entity değil, API kontratı korunuyor
    }
}
```

---

### `Controllers/SiparisController.cs`

```csharp
namespace KitabeviOnion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SiparisController : ControllerBase
{
    private readonly SiparisOlusturHandler _siparisHandler;

    public SiparisController(SiparisOlusturHandler siparisHandler)
    {
        _siparisHandler = siparisHandler;
    }

    [HttpPost]
    public async Task<IActionResult> Olustur(
        [FromBody] SiparisOlusturRequest request,
        CancellationToken ct)
    {
        try
        {
            var cmd = new SiparisOlusturCommand(
                KullaniciId: request.KullaniciId,
                KitapId: request.KitapId,
                Adet: request.Adet);

            var sonuc = await _siparisHandler.Handle(cmd, ct);
            //                                ↑ handler tüm iş mantığını yönetiyor
            //                                  Controller sadece HTTP → Command dönüşümü yapıyor

            return CreatedAtAction(nameof(Olustur), new { id = sonuc.SiparisId }, sonuc);
            //     ↑ 201 Created + Location header: yeni kaynağın URL'i
        }
        catch (DomainException ex)
        {
            return BadRequest(new { hata = ex.Message });
            //      ↑ Domain hatası → 400 Bad Request
            //        bunu yazmasaydık → DomainException 500 Internal Server Error dönerdi
        }
    }
}

public record SiparisOlusturRequest(string KullaniciId, int KitapId, int Adet);
// ↑ API request DTO — Command'den ayrı
//   bunu yazmasaydık → Command'i doğrudan FromBody alsaydık, API kontratı Application'a sızardı
```

---

### `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=kitabevi.db"));
//                 ↑ Faz2'de SQL Server vardı, burada SQLite — Domain ve Application değişmedi

// Repository registrations
builder.Services.AddScoped<IKitapRepository, KitapRepository>();
//                          ↑ interface       → implementation
//                            Domain         → Infrastructure
//                            bunu yazmasaydık → DI container IKitapRepository'yi çözemezdi

builder.Services.AddScoped<ISiparisRepository, SiparisRepository>();

// Application services
builder.Services.AddScoped<IEmailService, EmailService>();
//                          ↑ Application interface → Infrastructure implementation

// Handlers
builder.Services.AddScoped<KitapListeleHandler>();
builder.Services.AddScoped<SiparisOlusturHandler>();
// ↑ MediatR yok — elle register ettik, her handler sınıfını açıkça görüyoruz
//   bunu yazmasaydık → Controller inject edemez, 500 hata alırdık

builder.Services.AddControllers();

var app = builder.Build();

// Migration — startup'ta otomatik uygula (dev ortamı için)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    //          ↑ production'da Migrate() kullanılır, burada hızlı başlatma
}

app.MapControllers();
app.Run();
```

---

## Bağımlılık Akışı — Özet

```
API (Controller)
    │ inject eder
    ▼
Application (Handler)
    │ interface üzerinden çağırır
    ▼
Domain (Interface tanımı)
    ▲
    │ implement eder
Infrastructure (Repository, Service)
```

```
SQL Server → PostgreSQL değişince: sadece KitapRepository içi değişir
SMTP → SendGrid değişince: sadece EmailService içi değişir
REST → gRPC eklince: yeni controller/endpoint, Handler değişmez
```

---

## 500 vs 50k Kullanıcı

| Karar | 500 | 50k |
|---|---|---|
| **Bu yapı gerekli mi?** | Domain kurallar basitse hayır | Evet — ekip büyüdükçe katman ayrımı şart |
| **Handler yerine doğrudan repo?** | Kabul edilebilir | Hayır — iş mantığı dağılır |
| **Email Infrastructure'da mı?** | Direkt SMTP kabul | ✅ Interface arkası — swap edilebilir |
| **AppDbContext API'ye sızabilir mi?** | Küçük projede tolere edilir | ❌ — migration değişince API çöker |
| **Domain event gerekli mi?** | Tek alıcıysa direkt çağır | ✅ Birden fazla alıcı gelince şart |

---

## Sorular

1. `IKitapRepository` neden Domain katmanında, `KitapRepository` neden Infrastructure'da? İkisi Infrastructure'da olsaydı ne bozulurdu?
2. `SiparisRepository.KaydetAsync()` neden ayrı bir metod? Her `EkleAsync()` kendi `SaveChanges()` çağırsaydı ne olurdu?
3. `SiparisController` neden `DomainException`'ı catch ediyor? Bu try/catch infrastructure middleware'e taşınabilir mi, nasıl?
