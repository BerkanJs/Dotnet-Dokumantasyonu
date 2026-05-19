# Gün 96 — Audit Trail: Kim, Ne Zaman, Ne Yaptı?

---

## Audit Trail Nedir?

Her veri değişikliğinin kaydını tutmak: kim yaptı, ne zaman yaptı, neyi değiştirdi, eski değer ne idi, yeni değer ne oldu.

**Analoji:** Bankadaki hesap hareketleri. Bakiyen 1000 TL → 800 TL oldu. Banka sadece "bakiye 800" tutsa — "200 TL nereye gitti?" sorusuna cevap veremez. Ama her hareketi kaydetmişse: "15 Nisan, saat 14:30, ATM çekim, 200 TL" — tam iz var.

**Neden gerekli?**
- **Hukuk/GDPR:** "Kullanıcının verisi ne zaman değiştirildi?" sorusuna yanıt vermek zorunlu
- **Finans:** Her para hareketi, fiyat değişikliği izlenebilir olmalı
- **Debug:** Production'da veri bozuldu — "kim, ne zaman değiştirmiş?" sorusu
- **Güvenlik:** Yetkisiz değişiklik tespiti — "admin bu kaydı neden silmiş?"

---

## Seviye 1: Temel Audit Alanları (CreatedAt, UpdatedBy)

En basit audit: her tabloda "kim oluşturdu, ne zaman güncellendi" bilgisi.

```csharp
// Interface tanımla:
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    string CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    string? UpdatedBy { get; set; }
}

// Entity'ye uygula:
public class Kitap : IAuditableEntity
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;
    public decimal Fiyat { get; set; }

    // Audit alanları:
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
```

---

## ICurrentUserService — Kim Yapmış?

Audit'e "kim" bilgisini eklemek için aktif kullanıcıyı bilmemiz lazım. Bunu DI ile inject ediyoruz:

```csharp
public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public string? UserId => _accessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public string? UserName => _accessor.HttpContext?.User.Identity?.Name;
    // ne yapar → JWT token'dan veya cookie'den aktif kullanıcıyı çeker
    // bunu yazmasaydık → audit "kim yaptı?" bilgisini alamazdık
}

// Program.cs:
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
```

---

## SaveChangesInterceptor ile Otomatik Audit

Her `SaveChanges` çağrısında interceptor araya girer, `IAuditableEntity` implemente eden entity'lerin audit alanlarını otomatik doldurur.

```csharp
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;

    public AuditInterceptor(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        var context = eventData.Context!;

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            var now = DateTime.UtcNow;
            var user = _currentUser.UserName ?? "system";

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = user;
                    // ne yapar → yeni kayıt eklenirken tarih ve kullanıcı otomatik set edilir
                    // bunu yazmasaydık → her servis metodunda elle CreatedAt = DateTime.UtcNow yazardık
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = user;
                    // ne yapar → güncelleme olduğunda UpdatedAt/By otomatik dolar
                    break;
            }
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }
}

// Kayıt:
builder.Services.AddDbContext<AppDbContext>((sp, opt) =>
{
    var currentUser = sp.GetRequiredService<ICurrentUserService>();
    opt.UseSqlServer(connectionString)
       .AddInterceptors(new AuditInterceptor(currentUser));
});
```

Artık geliştirici hiç düşünmeden:
```csharp
var kitap = new Kitap { Ad = "Clean Code", Fiyat = 150 };
_context.Kitaplar.Add(kitap);
await _context.SaveChangesAsync();
// CreatedAt = 2026-05-02T14:30:00Z  (otomatik)
// CreatedBy = "berkan"               (otomatik)
```

---

## Shadow Properties — Entity'yi Kirletmeden Audit

Audit alanlarını entity class'ında görmek istemiyorsan → EF Core shadow property kullan. Property C#'ta yok ama veritabanında ve EF Core'da var.

```csharp
// OnModelCreating:
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        // Tüm entity'lere shadow property ekle:
        modelBuilder.Entity(entityType.ClrType)
            .Property<DateTime>("CreatedAt");
        modelBuilder.Entity(entityType.ClrType)
            .Property<string>("CreatedBy").HasMaxLength(100);
        modelBuilder.Entity(entityType.ClrType)
            .Property<DateTime?>("UpdatedAt");
        modelBuilder.Entity(entityType.ClrType)
            .Property<string?>("UpdatedBy").HasMaxLength(100);
        // ne yapar → entity class'ında property olmadan DB'de kolon oluşturur
        // avantaj → domain model temiz kalır, audit detayı infrastructure katmanında
    }
}

// Interceptor'da shadow property'ye yaz:
entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
entry.Property("CreatedBy").CurrentValue = user;

// Okurken:
var createdAt = _context.Entry(kitap).Property<DateTime>("CreatedAt").CurrentValue;
```

**Ne zaman shadow property kullan?** Domain model'in audit'ten habersiz olmasını istiyorsan (clean architecture puristi). Ama pratikte `IAuditableEntity` daha yaygın ve daha okunabilir.

---

## Seviye 2: Değişiklik Loglama — Eski/Yeni Değer Kaydı

Temel audit "ne zaman, kim" der. Ama "eski fiyat ne idi, yeni ne oldu?" sorusuna cevap vermez. Bunun için ChangeTracker'dan property değişikliklerini yakala.

### Audit Log Tablosu

```csharp
public class AuditLog
{
    public long Id { get; set; }
    public string EntityName { get; set; } = null!;      // "Kitap"
    public string EntityId { get; set; } = null!;        // "42"
    public string Action { get; set; } = null!;          // "Update", "Insert", "Delete"
    public string? OldValues { get; set; }               // JSON: {"Fiyat": 100}
    public string? NewValues { get; set; }               // JSON: {"Fiyat": 150}
    public string? AffectedColumns { get; set; }         // "Fiyat,Ad"
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; } = null!;
}
```

### Interceptor — Detaylı Değişiklik Yakalama

```csharp
public class AuditLogInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;

    public AuditLogInterceptor(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        var context = eventData.Context!;
        var auditEntries = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Audit log tablosunun kendisini loglama (sonsuz döngü)
            if (entry.Entity is AuditLog) continue;
            if (entry.State is EntityState.Detached or EntityState.Unchanged) continue;

            var audit = new AuditLog
            {
                EntityName = entry.Entity.GetType().Name,
                EntityId = entry.Property("Id").CurrentValue?.ToString() ?? "",
                Timestamp = DateTime.UtcNow,
                UserId = _currentUser.UserName ?? "system"
            };

            switch (entry.State)
            {
                case EntityState.Added:
                    audit.Action = "Insert";
                    audit.NewValues = JsonSerializer.Serialize(
                        entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
                    // ne yapar → eklenen kaydın tüm değerlerini JSON olarak saklar
                    break;

                case EntityState.Modified:
                    audit.Action = "Update";
                    var oldVals = new Dictionary<string, object?>();
                    var newVals = new Dictionary<string, object?>();
                    var affected = new List<string>();

                    foreach (var prop in entry.Properties.Where(p => p.IsModified))
                    {
                        oldVals[prop.Metadata.Name] = prop.OriginalValue;
                        newVals[prop.Metadata.Name] = prop.CurrentValue;
                        affected.Add(prop.Metadata.Name);
                        // ne yapar → sadece DEĞİŞEN property'lerin eski/yeni değerini yakalar
                        // bunu yazmasaydık → hangi alanın değiştiğini bilemezdin
                    }

                    audit.OldValues = JsonSerializer.Serialize(oldVals);
                    audit.NewValues = JsonSerializer.Serialize(newVals);
                    audit.AffectedColumns = string.Join(",", affected);
                    break;

                case EntityState.Deleted:
                    audit.Action = "Delete";
                    audit.OldValues = JsonSerializer.Serialize(
                        entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue));
                    // ne yapar → silinen kaydın son halini JSON olarak saklar
                    break;
            }

            auditEntries.Add(audit);
        }

        // Audit log'ları aynı transaction'da kaydet:
        context.Set<AuditLog>().AddRange(auditEntries);
        // ne yapar → asıl değişiklik + audit log aynı anda yazılır (tutarlılık)
        // bunu ayrı SaveChanges yapsaydık → biri başarılı diğeri başarısız olabilir

        return base.SavingChangesAsync(eventData, result, ct);
    }
}
```

### Sonuç: Audit Log'da Ne Görürsün?

```json
{
  "id": 1,
  "entityName": "Kitap",
  "entityId": "42",
  "action": "Update",
  "oldValues": "{\"Fiyat\": 100, \"Ad\": \"Clean Code\"}",
  "newValues": "{\"Fiyat\": 150, \"Ad\": \"Clean Code 2nd Ed\"}",
  "affectedColumns": "Fiyat,Ad",
  "timestamp": "2026-05-02T14:30:00Z",
  "userId": "berkan"
}
```

Artık "kitap 42'nin fiyatı ne zaman, kim tarafından, neden değişti?" sorusuna cevabin var.

---

## Event Sourcing vs Audit Trail — Fark Ne?

| | Audit Trail | Event Sourcing |
|---|---|---|
| **Amaç** | Değişiklikleri kayıt altına al (loglama) | Durumu event'lerden yeniden oluştur |
| **Veri kaynağı** | Mevcut tablo + ayrı log tablosu | Event store tek kaynak (tablo yok) |
| **Mevcut durumu nereden alırsın?** | Asıl tablodan (`SELECT * FROM Kitaplar`) | Event'leri replay ederek hesaplarsın |
| **Karmaşıklık** | Düşük — interceptor ekle, bitsin | Yüksek — tüm mimari değişir |
| **Ne zaman** | %95 proje — loglama yeterli | Finans, undo/redo, tam geçmiş kritik |

**Pratik karar:** Audit trail ile başla. Event sourcing ancak "herhangi bir ana geri dönebilmem lazım" veya "state'i event'lerden türetmem lazım" diyorsan gerekli. Çoğu projede audit trail yeterli.

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de:
- Audit yok — kayıt değişti, kim yaptı bilinmiyor
- "Fiyat ne zaman değişti?" → cevap yok
- Kullanıcı şikayet etti "siparişim kayboldu" → log yok, ispat yok

50K kullanıcıda: müşteri destek ekibi "bu sipariş ne oldu?" diye soruyor → audit log'dan anında cevap.

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| CreatedAt/UpdatedBy (temel) | Her zaman yap — 0 maliyet | Her zaman yap |
| Detaylı değişiklik logu | Kritik tablolarda (sipariş, ödeme) | Tüm önemli entity'lerde |
| Audit log tablosu | Küçük kalır, sorun değil | Partition/archive stratejisi düşün |
| Event Sourcing | Overengineering | Finans/hukuk zorunluluğu varsa |

---

## Kontrol Soruları

1. IAuditableEntity ve SaveChangesInterceptor birlikte nasıl çalışır?
2. Shadow property ile audit alanını entity'de tutmak arasındaki trade-off nedir?
3. ChangeTracker.Entries() ile eski/yeni değer nasıl yakalanır?
4. Audit log ve asıl değişiklik neden aynı transaction'da olmalı?
5. Event Sourcing ile audit trail arasındaki temel fark nedir? Hangisi ne zaman?
6. ICurrentUserService neden Scoped olarak register edilmeli?
