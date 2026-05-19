# Gün 97 — Optimistic Concurrency ve Conflict Resolution

---

## Problem: "Son Yazan Kazanır"

İki kullanıcı aynı kitabı aynı anda düzenliyor:

```
Berkan: kitap fiyatını 100 → 150 yaptı
Ayşe:   kitap adını "Clean Code" → "Clean Code 2nd Ed" yaptı (ama eski fiyat 100'ü gördü)

İkisi de "Kaydet" tıkladı:
  Berkan kaydetti → fiyat 150 ✓
  Ayşe kaydetti  → fiyat tekrar 100'e döndü! (Ayşe eski veriyi gönderdi)
```

Berkan'ın değişikliği kayboldu — haberi bile yok. Buna "lost update" denir.

**Analoji:** İki kişi aynı Word belgesini açtı. İkisi de düzenledi. Son kaydeden diğerinin değişikliklerini ezdi. Google Docs bunu çözmüş — sen de çözmelisin.

---

## Optimistic Concurrency Nedir?

"İyimser eşzamanlılık" — çatışma olmayacağını **varsay**, ama kaydetme anında kontrol et. Çatışma varsa hata fırlat, kullanıcıya sor.

**Nasıl çalışır?**
1. Kaydı oku → bir "versiyon damgası" (RowVersion) al
2. Kullanıcı düzenler
3. Kaydet derken: "benim okuduğum versiyon hâlâ aynı mı?" kontrol et
4. Aynıysa → kaydet, versiyonu artır
5. Değilse → başkası araya girmiş → `DbUpdateConcurrencyException` fırlat

```
Berkan okur: Kitap { Fiyat: 100, Version: 1 }
Ayşe okur:   Kitap { Fiyat: 100, Version: 1 }

Berkan kaydeder: UPDATE ... SET Fiyat=150 WHERE Id=42 AND Version=1 → başarılı, Version=2
Ayşe kaydeder:   UPDATE ... SET Ad='...' WHERE Id=42 AND Version=1 → 0 satır etkilendi!
  → Version artık 2, Ayşe'nin bildiği 1 → ÇATIŞMA → exception
```

---

## RowVersion / Timestamp — EF Core'da Kurulum

```csharp
public class Kitap
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;
    public decimal Fiyat { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;
    // ne yapar → her UPDATE'te SQL Server otomatik yeni değer üretir
    // bunu yazmasaydık → concurrency kontrolü yok, son yazan kazanır
    // SQL Server'da "rowversion" tipi — 8 byte binary, her yazımda değişir
}

// Fluent API alternatifi:
modelBuilder.Entity<Kitap>()
    .Property(k => k.RowVersion)
    .IsRowVersion();
    // ne yapar → aynı şey, attribute yerine fluent syntax
```

**EF Core ne üretir?**
```sql
UPDATE Kitaplar
SET Ad = @newAd, Fiyat = @newFiyat
WHERE Id = @id AND RowVersion = @originalRowVersion
-- WHERE'de RowVersion var → satır değişmişse 0 row affected → exception
```

---

## ConcurrencyCheck — Belirli Kolon Üzerinde

Tüm tablo için RowVersion istemiyorsan, sadece belirli kolonları "çatışma noktası" olarak işaretleyebilirsin:

```csharp
public class Kitap
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;

    [ConcurrencyCheck]
    public decimal Fiyat { get; set; }
    // ne yapar → UPDATE WHERE'ine Fiyat'ın orijinal değerini ekler
    // fiyat aradan değişmişse → 0 row affected → exception
    // bunu yazmasaydık → fiyat üzerinde çatışma kontrolü yok
}
```

```sql
-- EF Core üretir:
UPDATE Kitaplar SET Fiyat = 150 WHERE Id = 42 AND Fiyat = 100
-- Fiyat artık 120 ise (başkası değiştirmiş) → 0 satır etkilenir → exception
```

**RowVersion vs ConcurrencyCheck:**
- `RowVersion` → herhangi bir kolon değişirse çatışma algılanır (daha güvenli)
- `ConcurrencyCheck` → sadece işaretlediğin kolon(lar) kontrol edilir (daha esnek)

---

## DbUpdateConcurrencyException — Çatışma Yakalandı, Ne Yaparsın?

```csharp
public async Task<Result> FiyatGuncelleAsync(int kitapId, decimal yeniFiyat)
{
    var kitap = await _context.Kitaplar.FindAsync(kitapId);
    kitap!.Fiyat = yeniFiyat;

    try
    {
        await _context.SaveChangesAsync();
        return Result.Success();
    }
    catch (DbUpdateConcurrencyException ex)
    {
        // Çatışma! Başka biri bu kaydı aradan değiştirmiş.
        // Şimdi ne yapacağına karar ver:
        var entry = ex.Entries.Single();
        // entry.OriginalValues → senin okuduğun değerler
        // entry.CurrentValues  → senin yazmak istediğin değerler
        // entry.GetDatabaseValues() → şu an DB'deki gerçek değerler

        // ... resolution stratejisi uygula (aşağıda)
    }
}
```

---

## Conflict Resolution Stratejileri

### 1. Database Wins — "Benim değişikliğimi unut, DB'deki kalsin"

```csharp
catch (DbUpdateConcurrencyException ex)
{
    var entry = ex.Entries.Single();
    var dbValues = await entry.GetDatabaseValuesAsync();
    // ne yapar → DB'deki güncel değerleri çeker

    entry.OriginalValues.SetValues(dbValues!);
    // ne yapar → "senin orijinalin artık DB'deki değer" der
    // bir sonraki SaveChanges'ta yeni RowVersion ile dener

    // Kullanıcıya bildir: "Başkası bu kaydı güncellemiş, değişikliğin uygulanmadı."
    return Result.Conflict("Kayıt başkası tarafından güncellenmiş. Lütfen tekrar deneyin.");
}
```

**Ne zaman:** Kullanıcıya "verin eski, tekrar dene" demek kabul edilebilir. Form sayfalarında yaygın.

### 2. Client Wins — "DB'yi ez, benim yazdığım geçerli"

```csharp
catch (DbUpdateConcurrencyException ex)
{
    var entry = ex.Entries.Single();
    var dbValues = await entry.GetDatabaseValuesAsync();

    // DB'deki RowVersion'ı al, original'e set et — artık WHERE eşleşecek:
    entry.OriginalValues.SetValues(dbValues!);
    // CurrentValues (senin değerlerin) aynı kalır

    await _context.SaveChangesAsync();  // bu sefer başarılı olur
    return Result.Success();
    // ne yapar → aradaki değişikliği ezer, senin değerin yazılır
}
```

**Ne zaman:** Son değişikliğin kesinlikle doğru olduğunu biliyorsun (admin override, otomatik sistem güncellemesi).

### 3. Merge — Kolonları Birleştir

```csharp
catch (DbUpdateConcurrencyException ex)
{
    var entry = ex.Entries.Single();
    var dbValues = await entry.GetDatabaseValuesAsync();
    var originalValues = entry.OriginalValues.Clone();

    // Her property için: değişen kimse onunki kazansın
    foreach (var prop in entry.Properties)
    {
        var original = originalValues[prop.Metadata.Name];
        var current = prop.CurrentValue;       // benim yazmak istediğim
        var database = dbValues![prop.Metadata.Name]; // DB'deki şu anki

        if (original?.Equals(current) == true)
        {
            // Ben bu kolonu değiştirmedim → DB'deki değer kalsın
            prop.CurrentValue = database;
        }
        // else: ben değiştirdim → benim değerim kalır
        // ne yapar → Berkan fiyatı değiştirmiş, Ayşe adı değiştirmiş → ikisi de korunur
    }

    entry.OriginalValues.SetValues(dbValues!);
    await _context.SaveChangesAsync();
    return Result.Success();
}
```

**Ne zaman:** Farklı kolonlar değiştirildiğinde ikisini de korumak istiyorsun. En karmaşık ama en kullanıcı dostu.

**Dikkat:** Aynı kolonu ikisi de değiştirdiyse → merge karar veremez, kullanıcıya sor.

---

## Pessimistic Locking — Kaydı Kilitle

Optimistic: "muhtemelen çatışma olmaz, olursa hallederiz."
Pessimistic: "bu kaydı ben okuyorum, kimse dokunmasın — bitene kadar kilitli."

```csharp
// SQL Server — UPDLOCK ile:
var kitap = await _context.Kitaplar
    .FromSqlRaw("SELECT * FROM Kitaplar WITH (UPDLOCK) WHERE Id = {0}", id)
    .FirstOrDefaultAsync();
// ne yapar → bu satırı okurken kilitler, transaction bitene kadar başkası yazamaz
// bunu yazmasaydık → başkası aynı anda okuyup yazabilir

// PostgreSQL — SELECT FOR UPDATE:
var kitap = await _context.Kitaplar
    .FromSqlRaw("SELECT * FROM \"Kitaplar\" WHERE \"Id\" = {0} FOR UPDATE", id)
    .FirstOrDefaultAsync();
```

**Ne zaman pessimistic lock?**
- Çatışma olasılığı **çok yüksek** (aynı satır sürekli güncelleniyor)
- Çatışma maliyeti çok yüksek (retry kabul edilemez — ödeme işlemi)
- Kısa süreli işlem (lock'u hızlıca bırakacaksın)

**Ne zaman kullanMA?**
- Çatışma nadir → optimistic yeterli (lock performans düşürür)
- Uzun süreli işlem → lock tutmak diğer istekleri bloklar
- Distributed ortam → DB lock'u tek DB'de çalışır, birden fazla DB'de çalışmaz

---

## Distributed Lock — Redis RedLock (Uygulama Seviyesi)

DB lock'u tek veritabanında çalışır. Birden fazla servis aynı kaynağa erişiyorsa → uygulama seviyesinde distributed lock gerekir (Gün 92'de gördüğümüz RedLock).

```csharp
// Senaryo: Stok düşürme — iki servis aynı ürünün stokunu düşürmesin
public async Task<bool> StokDusAsync(int urunId, int miktar)
{
    var lockKey = $"lock:stok:{urunId}";
    var acquired = await _redis.StringSetAsync(lockKey, Environment.MachineName,
        TimeSpan.FromSeconds(10), When.NotExists);

    if (!acquired)
        return false;  // başka biri işliyor, tekrar dene

    try
    {
        var urun = await _repo.GetAsync(urunId);
        if (urun.Stok < miktar) return false;

        urun.Stok -= miktar;
        await _repo.UpdateAsync(urun);
        return true;
    }
    finally
    {
        await _redis.KeyDeleteAsync(lockKey);
        // ne yapar → lock'u serbest bırakır
        // TTL de var → servis çökse bile 10 sn sonra lock otomatik açılır
    }
}
```

---

## Hangisini Ne Zaman Kullan?

| Strateji | Çatışma Sıklığı | Kullanım Alanı |
|----------|-----------------|----------------|
| **Optimistic (RowVersion)** | Düşük-orta | Form güncellemeleri, CRUD — çoğu senaryo |
| **Pessimistic (UPDLOCK)** | Yüksek | Kısa işlem, tek DB, stok/bakiye |
| **Distributed Lock (Redis)** | Orta | Çoklu servis, aynı kaynak |
| **Hiçbiri (son yazan kazanır)** | Çok düşük / önemsiz | Log, istatistik — çatışma zararsız |

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de concurrency kontrolü yok — son yazan kazanır. 500 kullanıcıda:
- İki admin aynı kitabı aynı anda güncelleme ihtimali çok düşük → sorun çıkmaz
- 50K'da: e-ticaret sepeti, stok düşürme, fiyat güncelleme → lost update ciddi maddi kayıp

---

## 500 vs 50K Kullanıcı

| | 500 kullanıcı/ay | 50K kullanıcı/ay |
|---|---|---|
| Optimistic concurrency | Kritik tablolarda (sipariş, ödeme) | Tüm önemli entity'lerde |
| RowVersion | Yeterli ve basit | Yeterli ve basit |
| Pessimistic lock | Gereksiz | Stok/bakiye gibi hot resource'larda |
| Distributed lock | Gereksiz (tek instance) | Çoklu instance + paylaşılan kaynak |
| Merge stratejisi | Basit "database wins" yeterli | UX önemliyse merge düşün |

---

## Kontrol Soruları

1. "Son yazan kazanır" problemi nedir? Gerçek hayatta ne kayba yol açar?
2. RowVersion nasıl çalışır? EF Core hangi SQL'i üretir?
3. DbUpdateConcurrencyException yakaladığında "database wins" nasıl uygulanır?
4. Merge stratejisinde aynı kolon iki kişi tarafından değiştirilirse ne yaparsın?
5. Pessimistic lock ne zaman optimistic'ten daha uygun?
6. Distributed lock neden TTL ile oluşturulmalı?
