# Gün 99 — EF Core Interceptors ve SaveChanges Pipeline

---

## Interceptor Nedir?

EF Core'da her şey bir pipeline'dan geçer. Veri kaydedilirken, SQL çalıştırılırken, connection açılırken — her adımda EF Core sana "araya girme" hakkı verir. Bu araya girme noktalarına **interceptor** denir.

**Analoji:** Kargo firmasında paket gönderiyorsun. Paket depoya gitmeden önce sırayla kontrol noktalarından geçer:
1. İçerik kontrolü (yasak madde var mı?) → **SavingChanges** — kaydetmeden önce entity'leri incele
2. Etiket basma (gönderici bilgisi) → audit alanları otomatik doldurma
3. Tartı (ağırlık ölçümü) → validation
4. Teslimat sonrası bildirim → **SavedChanges** — başarılı kayıt sonrası event tetikle

Her kontrol noktası bağımsız çalışır. Geliştirici bunların varlığından habersiz — paketi (entity) teslim eder, gerisini pipeline halleder.

**Neden interceptor?**
- **Merkezi kontrol:** Audit, soft delete, validation — her yerde ayrı ayrı yazmak yerine tek interceptor yaz, tüm projede geçerli
- **Geliştirici hatası önleme:** "CreatedAt'ı set etmeyi unuttum" diye bug oluşmaz — interceptor otomatik yapar
- **Separation of concerns:** İş mantığı entity'de, altyapı işleri interceptor'da — birbirine karışmaz
- **Ekip büyüdüğünde:** Yeni geliştirici kuralları bilmek zorunda değil — interceptor garanti sağlar

---

## Interceptor Türleri — Hangi Aşamada Araya Girersin?

EF Core'un 4 farklı yaşam döngüsü noktasında interceptor koyabilirsin:

### 1. ISaveChangesInterceptor — Kaydetme Anı

**Ne zaman devreye girer?** `SaveChangesAsync()` çağrıldığında — entity'ler veritabanına yazılmadan hemen önce ve hemen sonra.

**Ne yapabilirsin?**
- Kaydetmeden ÖNCE: entity'leri değiştir (audit doldur, soft delete dönüştür, validation yap)
- Kaydettikten SONRA: domain event'leri tetikle, cache temizle, bildirim gönder
- Hata durumunda: loglama, alert, retry

**Gerçek hayat kullanımları:**
- Gün 95'teki soft delete → `Remove()` çağrılınca DELETE yerine `IsDeleted = true` yapıyorduk
- Gün 96'daki audit → `CreatedAt`, `UpdatedBy` otomatik doluyordu
- Bugün: domain event dispatch — kitap fiyatı değişince ilgili handler'ları tetikle

### 2. IDbCommandInterceptor — SQL Komutu Anı

**Ne zaman devreye girer?** EF Core'un ürettiği SQL veritabanına gönderilmeden hemen önce ve döndükten hemen sonra.

**Ne yapabilirsin?**
- SQL'i logla (development'ta hangi sorgu üretiliyor?)
- SQL'i değiştir (tenant bilgisi ekle, hint ekle)
- Yavaş sorguları tespit et (1 saniyeden uzun süreni logla)
- Sorguyu iptal et (belirli koşullarda çalışmasını engelle)

**Gerçek hayat kullanımları:**
- Slow query monitoring — production'da hangi sorgular yavaş?
- Query tagging — DBA'ya "bu sorgu hangi servisten geliyor?" bilgisi
- Row count kontrolü — "bu sorgu 10.000+ satır döndürecek, uyar"

### 3. IDbConnectionInterceptor — Bağlantı Anı

**Ne zaman devreye girer?** Veritabanı bağlantısı açılırken ve kapanırken.

**Ne yapabilirsin?**
- Bağlantı açıldığında session değişkeni set et (PostgreSQL RLS için tenant_id)
- Connection süresini ölç
- Bağlantı havuzu durumunu izle

**Gerçek hayat kullanımları:**
- Multi-tenant RLS (Gün 98) — her bağlantıda `SET app.tenant_id = 'acme'`
- Connection leak tespiti — bağlantı kapatılmıyorsa uyar

### 4. IMaterializationInterceptor — Entity Oluşturma Anı

**Ne zaman devreye girer?** Veritabanından okunan ham veri C# nesnesine dönüştürülürken.

**Ne yapabilirsin?**
- Şifreli alanları çöz (DB'de encrypted, C#'ta plain text)
- Lazy initialization
- Post-load hesaplamalar

**Gerçek hayat kullanımları:**
- Field-level encryption — kredi kartı numarası DB'de şifreli, okununca otomatik decrypt
- Computed değer — DB'deki ham veriden C# tarafında hesaplama

---

## SaveChanges Pipeline — Adım Adım

SaveChanges çağrıldığında interceptor'lar şu sırayla çalışır:

```
_context.SaveChangesAsync() çağrıldı
  │
  ▼
┌─────────────────────────────────────┐
│  SavingChangesAsync()               │ ← STEP 1: Kaydetmeden ÖNCE
│  • Entity'leri incele/değiştir      │
│  • Audit alanları doldur            │
│  • Soft delete dönüşümü yap        │
│  • Domain event'leri topla          │
│  • Validation kontrolleri           │
└─────────────────────────────────────┘
  │
  ▼
┌─────────────────────────────────────┐
│  SQL komutları DB'ye gönderilir     │ ← EF Core kendi işini yapıyor
│  (INSERT, UPDATE, DELETE)           │
└─────────────────────────────────────┘
  │
  ├── Başarılı ──▶ SavedChangesAsync()      ← STEP 2a: Başarı sonrası
  │                • Domain event dispatch
  │                • Cache invalidation
  │                • Bildirim gönder
  │
  └── Hata ─────▶ SaveChangesFailedAsync()  ← STEP 2b: Hata durumu
                   • Hata logla
                   • Alert gönder
```

**Kritik soru: Domain event neden save SONRASI dispatch edilir?**

Eğer save öncesi dispatch edersen → event handler çalışır → sonra save başarısız olur → ama event zaten gönderilmiş! Örnek: "Kitap oluşturuldu" event'ini gönderdik, e-posta attık — ama kayıt aslında DB'ye yazılmadı. Yanlış bildirim.

Save sonrası dispatch → sadece gerçekten kaydedilmiş veriler için event tetiklenir.

---

## ISaveChangesInterceptor — Kod

```csharp
public class FullPipelineInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;
    private readonly IMediator _mediator;
    private List<INotification> _pendingEvents = new();

    public FullPipelineInterceptor(ICurrentUserService currentUser, IMediator mediator)
    {
        _currentUser = currentUser;
        _mediator = mediator;
    }

    // STEP 1: Save öncesi — entity'leri incele, değiştir, event'leri topla
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        var context = eventData.Context!;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Audit alanları doldur:
            if (entry.Entity is IAuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAt = DateTime.UtcNow;
                    auditable.CreatedBy = _currentUser.UserName ?? "system";
                }
                if (entry.State == EntityState.Modified)
                {
                    auditable.UpdatedAt = DateTime.UtcNow;
                    auditable.UpdatedBy = _currentUser.UserName ?? "system";
                }
            }

            // Domain event'leri topla (henüz dispatch etme!):
            if (entry.Entity is IHasDomainEvents withEvents)
            {
                _pendingEvents.AddRange(withEvents.DomainEvents);
                withEvents.ClearDomainEvents();
            }
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }

    // STEP 2a: Save sonrası — event'leri şimdi güvenle dispatch et
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken ct = default)
    {
        foreach (var domainEvent in _pendingEvents)
        {
            await _mediator.Publish(domainEvent, ct);
            // ne yapar → KitapOlusturuldu, FiyatDegisti handler'ları tetiklenir
            // neden burada → save başarılı olduğu garantilenmiş durumda
        }
        _pendingEvents.Clear();

        return await base.SavedChangesAsync(eventData, result, ct);
    }

    // STEP 2b: Hata durumu
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken ct = default)
    {
        // Event'leri temizle — save başarısız, dispatch yapma
        _pendingEvents.Clear();
        // Hata logla, alert gönder vs.
        return base.SaveChangesFailedAsync(eventData, ct);
    }
}
```

---

## IDbCommandInterceptor — Yavaş Sorgu Tespiti

Production'da "uygulama yavaş" şikayeti geldi. Hangi SQL yavaş? Normal loglama her sorguyu gösterir ama binlercesi var. Interceptor ile sadece yavaş olanları yakala:

```csharp
public class SlowQueryInterceptor : DbCommandInterceptor
{
    private readonly ILogger<SlowQueryInterceptor> _logger;
    private readonly TimeSpan _threshold = TimeSpan.FromSeconds(1);

    public SlowQueryInterceptor(ILogger<SlowQueryInterceptor> logger) => _logger = logger;

    // SQL çalıştırıldıktan SONRA — süreyi kontrol et:
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken ct = default)
    {
        if (eventData.Duration > _threshold)
        {
            _logger.LogWarning(
                "YAVAŞ SORGU ({Duration}ms): {Sql}\nParametreler: {Params}",
                eventData.Duration.TotalMilliseconds,
                command.CommandText,
                string.Join(", ", command.Parameters.Cast<DbParameter>()
                    .Select(p => $"{p.ParameterName}={p.Value}")));
            // ne yapar → 1 saniyeden uzun süren sorguyu detaylı loglar
            // bunu yazmasaydık → yavaş sorgular sessizce çalışır, kullanıcı şikayet edene kadar fark etmezsin
            // çıktı: "YAVAŞ SORGU (2340ms): SELECT * FROM Kitaplar WHERE..."
        }

        return await base.ReaderExecutedAsync(command, eventData, result, ct);
    }

    // SQL çalıştırılmadan ÖNCE — query tagging ekle:
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken ct = default)
    {
        // SQL'in başına açıklama ekle — DBA'nın işini kolaylaştırır:
        command.CommandText = $"/* Source: KitapAPI, User: {GetCurrentUser()} */\n{command.CommandText}";
        // ne yapar → DB Activity Monitor'da bu sorgunun nereden geldiği görünür
        // DBA "bu sorgu DB'yi yavaşlatıyor" dediğinde hangi servisten geldiğini bilirsin

        return base.ReaderExecutingAsync(command, eventData, result, ct);
    }
}
```

**Ne zaman DbCommandInterceptor?**
- Development: EF Core hangi SQL üretmiş görmek
- Production: yavaş sorgu tespiti, monitoring
- Multi-tenant: SQL'e tenant bilgisi ekleme (DBA için görünürlük)
- Güvenlik: belirli sorgu kalıplarını engelleme

---

## IDbConnectionInterceptor — Bağlantıda Araya Gir

Her veritabanı bağlantısı açıldığında bir şey yapmak istiyorsan — özellikle PostgreSQL Row Level Security (Gün 98'de anlattığımız) için:

```csharp
public class TenantConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ICurrentTenantService _tenantService;

    public TenantConnectionInterceptor(ICurrentTenantService tenantService)
        => _tenantService = tenantService;

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken ct = default)
    {
        // PostgreSQL RLS: bağlantı açıldığında tenant context set et
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SET app.tenant_id = '{_tenantService.TenantId}'";
        await cmd.ExecuteNonQueryAsync(ct);
        // ne yapar → bu bağlantıda çalışan TÜM sorgular otomatik tenant'a göre filtrelenir
        // RLS policy DB'de tanımlı: sadece tenant_id eşleşen satırlar dönülür
        // neden interceptor → her sorguya WHERE eklemek yerine bağlantıda bir kez set et

        await base.ConnectionOpenedAsync(connection, eventData, ct);
    }
}
```

**Bu yaklaşımın avantajı:** Uygulama katmanında Global Query Filter unuttuysan bile, DB seviyesinde veri sızıntısı engellenir (savunma derinliği).

---

## Interceptor Kayıt ve Sıralama

Birden fazla interceptor kaydettiğinde **sıra önemli** — ilk eklenen ilk çalışır:

```csharp
builder.Services.AddDbContext<AppDbContext>((sp, opt) =>
{
    opt.UseSqlServer(connectionString)
       .AddInterceptors(
           sp.GetRequiredService<AuditInterceptor>(),         // 1. Audit eski değerleri yakalar
           sp.GetRequiredService<SoftDeleteInterceptor>(),    // 2. Soft delete dönüşümü yapar
           sp.GetRequiredService<DomainEventInterceptor>()    // 3. Event'leri toplar
       );
});
// Neden bu sıra?
// 1. Audit ÖNCE çalışmalı → soft delete henüz dönüşmemiş, orijinal state'i yakalar
// 2. Soft delete → State.Deleted'ı State.Modified'a çevirir
// 3. Event → son halinden event toplar
// Sıra yanlış olsa → audit soft delete sonrası çalışır, "Delete" yerine "Modified" loglar (yanlış)
```

---

## Shadow Properties — Entity'yi Kirletmeden Veri Tut

Bazen entity class'ında görünmesini istemediğin ama DB'de olmasını istediğin kolonlar var. Shadow property: C#'ta property yok, EF Core ve DB biliyor.

**Ne zaman shadow property?**
- `CreatedAt`, `UpdatedBy` gibi audit alanları domain'in parçası değilse
- `TenantId` — entity API'sında görmek istemiyorsan
- Teknik metadata — domain logic'le ilgisi olmayan DB kolonları

**Ne zaman normal property daha iyi?**
- IntelliSense ile kolay erişim istiyorsan
- Ekip shadow property'leri bilmiyorsa (keşfedilebilirlik düşük)
- Entity üzerinde iş mantığında kullanılacaksa

```csharp
// Tanımlama — OnModelCreating'de:
modelBuilder.Entity<Kitap>()
    .Property<DateTime>("LastModified");
    // ne yapar → DB'de LastModified kolonu oluşur, Kitap class'ında property yok
    // EF migration çalışınca kolon tabloya eklenir

modelBuilder.Entity<Kitap>()
    .Property<string>("ModifiedBy").HasMaxLength(100);

// Interceptor'da değer atama:
foreach (var entry in context.ChangeTracker.Entries())
{
    if (entry.State == EntityState.Modified)
    {
        entry.Property("LastModified").CurrentValue = DateTime.UtcNow;
        entry.Property("ModifiedBy").CurrentValue = currentUser;
        // ne yapar → entity'de property olmadan interceptor değeri set eder
    }
}

// Sorgulama — EF.Property helper'ı ile:
var sonDegisimler = await _context.Kitaplar
    .OrderByDescending(k => EF.Property<DateTime>(k, "LastModified"))
    .Take(10)
    .ToListAsync();
// ne yapar → shadow property üzerinden sıralama/filtreleme
// dezavantaj → IntelliSense yok, string-based (typo riski)
```

---

## Owned Entities ve Value Objects

### Value Object Nedir?

**Kimliği olmayan**, değeriyle tanımlanan nesne. İki `Para(100, "TRY")` nesnesi birbirine eşittir — "hangi 100 TL?" sorusunun anlamı yok.

**Analoji:** Adres. "Atatürk Cad. No:5, İstanbul" bir adres. Bu adresin bir ID'si yok — değeri kendisi. Aynı adresi iki farklı müşteriye yazsan, iki farklı nesne ama "aynı adres."

Entity'den farkı:
- **Entity:** Id var, kimlikle tanınır. Kitap #42 ile Kitap #43 farklı — içerikleri aynı olsa bile.
- **Value Object:** Id yok, değerle tanınır. `Adres("İstanbul", "34000")` = `Adres("İstanbul", "34000")`

### Owned Entity — EF Core'da Value Object Mapping

Value Object'i EF Core'a nasıl anlarsın? `OwnsOne` ile: "bu nesne ayrı bir entity değil, sahibinin parçası."

```csharp
// Value Object:
public class Adres
{
    public string Sokak { get; init; } = null!;
    public string Sehir { get; init; } = null!;
    public string PostaKodu { get; init; } = null!;
    // Id YOK — bu entity değil, value object
}

public class Musteri
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;
    public Adres TeslimatAdresi { get; set; } = null!;
    public Adres FaturaAdresi { get; set; } = null!;
}

// OnModelCreating:
modelBuilder.Entity<Musteri>(builder =>
{
    builder.OwnsOne(m => m.TeslimatAdresi, a =>
    {
        a.Property(x => x.Sokak).HasColumnName("TeslimatSokak");
        a.Property(x => x.Sehir).HasColumnName("TeslimatSehir");
        a.Property(x => x.PostaKodu).HasColumnName("TeslimatPosta");
    });
    builder.OwnsOne(m => m.FaturaAdresi, a =>
    {
        a.Property(x => x.Sokak).HasColumnName("FaturaSokak");
        a.Property(x => x.Sehir).HasColumnName("FaturaSehir");
        a.Property(x => x.PostaKodu).HasColumnName("FaturaPosta");
    });
});
// ne yapar → Adres ayrı tabloya GİTMEZ
// Musteri tablosunda 6 kolon oluşur: TeslimatSokak, TeslimatSehir, ..., FaturaSokak, ...
// bunu yazmasaydık → Adres ayrı tablo + FK join gerekir (gereksiz karmaşıklık)
// neden OwnsOne → Adres'in kendi başına anlamı yok, her zaman bir Musteri'ye ait
```

### Complex Types (EF Core 8+) — Daha Basit Value Object

EF Core 8'de owned entity'nin daha hafif versiyonu geldi. Farkları:

```csharp
[ComplexType]
public class Para
{
    public decimal Miktar { get; init; }
    public string Birim { get; init; } = "TRY";
}

public class Kitap
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;
    public Para Fiyat { get; set; } = new();   // complex type — null OLAMAZ
}

// Fluent API:
modelBuilder.Entity<Kitap>().ComplexProperty(k => k.Fiyat);
// ne yapar → Kitap tablosunda Fiyat_Miktar ve Fiyat_Birim kolonları oluşur
```

**Owned Entity vs Complex Type — Hangisini Ne Zaman?**

| Soru | Owned Entity | Complex Type |
|------|-------------|--------------|
| Null olabilir mi? | Evet (`TeslimatAdresi = null` OK) | Hayır (her zaman değer olmalı) |
| Ayrı tabloda olabilir mi? | Evet (ToTable ile) | Hayır (her zaman aynı tablo) |
| Collection olabilir mi? | Evet (OwnsMany — adres listesi) | Hayır |
| Ne zaman tercih et? | Nullable, collection, karmaşık VO | Basit, her zaman dolu VO (Para, Koordinat) |

---

## Faz2 ile Karşılaştırma

Faz2'de interceptor yok. Audit alanlarını her serviste elle set ediyorsun, soft delete'i her yerde kontrol ediyorsun. Bir yerde unutursan → bug. 

50K kullanıcıda 5 kişilik ekip var → herkes kuralları bilmek zorunda. Interceptor ile: kurallar merkezi, geliştirici bilmese bile doğru çalışır.

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| SaveChangesInterceptor | İyi alışkanlık (audit/soft delete) | Zorunlu — ekip büyüdükçe merkezi garanti |
| DbCommandInterceptor | Development'ta SQL görme | Production monitoring (yavaş sorgu alert) |
| ConnectionInterceptor | Gereksiz | Multi-tenant RLS'de gerekli |
| Domain event dispatch | Basit projede gereksiz | Event-driven mimaride zorunlu |
| Owned Entity / Complex Type | Value object varsa kullan | Domain model zenginleştikçe zorunlu |
| Shadow Properties | Opsiyonel | Clean architecture istiyorsan tercih et |

---

## Kontrol Soruları

1. Interceptor nedir? Neden her serviste elle yazmak yerine interceptor kullanırız?
2. SavingChangesAsync ile SavedChangesAsync arasındaki fark nedir? Domain event neden save sonrası dispatch edilir?
3. DbCommandInterceptor ile yavaş sorguları nasıl tespit edersin?
4. Birden fazla interceptor kaydettiğinde sıralama neden önemli? Yanlış sırada ne olur?
5. Shadow property ne zaman tercih edilir, ne zaman normal property daha iyi?
6. Owned Entity ile Complex Type arasındaki temel fark nedir? Hangisini ne zaman seçersin?
7. Value Object nedir? Entity'den farkı ne?
