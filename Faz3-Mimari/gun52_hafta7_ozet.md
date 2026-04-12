# Gün 52 — Hafta 7 Özet

---

## Bu Hafta Ne Öğrendik?

| Gün | Konu | Anahtar Karar |
|-----|------|---------------|
| 46 | SRP | "Bu class'ı kaç farklı sebepten değiştiririm?" |
| 47 | OCP | "Yeni davranış için var olan kodu açıyor muyum?" |
| 48 | LSP + ISP | "Alt tip üst tipin yerine geçebiliyor mu? Interface'de kullanmadığım metod var mı?" |
| 49 | DIP | "Yüksek seviye somut tipe mi bağımlı?" |
| 50 | Sentez | SOLID ihlali = teknik borç, test edilemezlik |
| 51 | Creational | Factory / Builder / Singleton — nesne oluşturma kararları |

---

## Mimari Soru 1 — Ödeme Sistemi

**Senaryo:** Kredi kartı, PayPal, crypto desteklenecek. SOLID nasıl uygulanır?

```csharp
// OCP + DIP: Yeni ödeme tipi = yeni class, OdemeServisi açılmaz
public interface IOdemeYontemi
{
    Task<OdemeSonucu> OdeAsync(decimal tutar);
    string Ad { get; }
}

public class KrediKartiOdemesi : IOdemeYontemi { ... }
public class PayPalOdemesi    : IOdemeYontemi { ... }
public class CryptoOdemesi    : IOdemeYontemi { ... }

// SRP: OdemeServisi sadece akışı orkestra eder — ödeme mantığını bilmez
public class OdemeServisi
{
    private readonly IOdemeYontemi _yontem;
    // DIP: somut tipe değil, interface'e bağımlı

    public OdemeServisi(IOdemeYontemi yontem) => _yontem = yontem;

    public async Task<OdemeSonucu> OdeAsync(decimal tutar)
    {
        // iş kuralları burada: limit kontrolü, log, retry
        return await _yontem.OdeAsync(tutar);
    }
}

// Factory: hangi yöntemin geleceği dışarıdan belirlenir
// Program.cs'te veya bir factory class'ta
```

Yarın crypto eklenince: `CryptoOdemesi` class'ı yaz, `Program.cs`'te kayıt et. `OdemeServisi`'ne dokunma.

---

## Mimari Soru 2 — 500 Satırlık UserService

**Senaryo:** `UserService` içinde 500 satır var. SRP'ye göre nasıl bölersin?

Adım 1 — "Kim değiştirir?" sorusunu sor:

```
Güvenlik ekibi    → şifre hash/doğrulama    → SifreServisi
Pazarlama         → e-posta şablonları      → EmailServisi
Ürün ekibi        → kayıt kuralları         → KullaniciValidator
IT / DevOps       → audit log formatı       → AuditLogServisi
Veri ekibi        → DB erişimi              → KullaniciRepository
```

Adım 2 — `UserService` sadece akışı orkestra eder:

```csharp
public class UserService
{
    public async Task KayitOlAsync(string email, string sifre)
    {
        _validator.Dogrula(email, sifre);
        var hash = _sifreServisi.Hash(sifre);
        await _repository.KaydetAsync(email, hash);
        await _emailServisi.HosgeldinGonderAsync(email);
        _auditLog.Kayit("Yeni kullanıcı", email);
    }
}
```

500 satır → 5 class, her biri ~50-100 satır, tek sorumluluk.

---

## Faz2 Bağlantısı — Bu Haftanın Tamamı Faz2'de Vardı

```
SRP  → IKitapServisi / IKitapSorguServisi / IKitapBatchServisi ayrımı
OCP  → CachedKitapServisi: EfKitapServisi açılmadı
LSP  → CachedKitapServisi, IKitapServisi'nin yerine geçiyor
ISP  → CachedKitapServisi batch metodlarını görmüyor
DIP  → KitapController hiçbir zaman EfKitapServisi'ni new'lemedi
```

Faz2'yi yazarken SOLID'i kasıtlı uyguladın. Şimdi neden öyle yaptığının adını biliyorsun.

---

## Gelecek Hafta

**Hafta 8 — Structural & Behavioral Patterns**

SOLID'i design pattern'lerin içinde göreceğiz:
- Decorator → OCP + SRP (CachedKitapServisi'ni pattern olarak adlandırıyoruz)
- Strategy → OCP (indirim hesaplama)
- Observer → OCP + SRP (event-driven)
- Command → SRP + DIP (CQRS'e zemin hazırlıyor)
