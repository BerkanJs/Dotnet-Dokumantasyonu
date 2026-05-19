# Gün 62 — CQRS: Command Query Responsibility Segregation

Onion katmanlarını oturttu. Şimdi o katmanlar içindeki use case'leri nasıl organize edeceğimize bakıyoruz: CQRS.

---

## Temel Fikir

**CQS (Command Query Separation)** — Bertrand Meyer:
> Bir metod ya bir şey yapar (Command) ya da bir şey döndürür (Query). İkisini aynı anda yapamaz.

**CQRS** — Greg Young bu fikri mimari seviyeye taşıdı:
> Okuma modeli ve yazma modeli ayrı nesnelerle temsil edilir.

---

## Command vs Query

```csharp
// Command — side effect var, DB değişir, genellikle ID döner
public record SiparisOlusturCommand(string KullaniciId, int KitapId, int Adet);
// ↑ "bir şey yap" — sonucu DB'deki değişiklik

// Query — side effect yok, sadece okur, değer döndürür
public record KitapListeleQuery();
// ↑ "bir şey getir" — DB değişmez
```

**Faz2'de fark yoktu:**
```csharp
// KitapServisi hem okuyor hem yazıyor — aynı nesne
public class KitapServisi
{
    public async Task<List<Kitap>> TumunuGetirAsync() { ... } // Query
    public async Task EkleAsync(Kitap kitap) { ... }          // Command
    public async Task SilAsync(int id) { ... }                // Command
    // ↑ tek serviste her şey → şişiyor, test zorlaşıyor
}
```

---

## Simple CQRS vs Full CQRS

### Simple CQRS — tek DB, farklı model
```
Write side: Command → Handler → Domain → DB
Read side:  Query  → Handler → DB (doğrudan, domain bypass)

Aynı DB, ama okuma için optimize edilmiş query (JOIN, flat DTO)
```

### Full CQRS — farklı DB
```
Write side: Command → Handler → Domain → SQL DB (normalize, tutarlı)
Read side:  Query  → Handler → Read DB (Redis, MongoDB, denormalize)

Event sourcing ile senkronize tutulur
```

50k kullanıcıda Simple CQRS genellikle yeterli. Full CQRS çok nadir gerekir.

---

## MediatR Pipeline

```
HTTP Request
    ↓
Controller (_mediator.Send(cmd))
    ↓
LoggingBehavior     → "→ SiparisOlusturCommand başladı"
    ↓
ValidationBehavior  → KullaniciId boş mu? Adet geçerli mi?
    ↓
SiparisOlusturHandler → Domain kuralları, DB kaydet
    ↓
LoggingBehavior     → "← SiparisOlusturCommand tamamlandı: 43ms"
    ↓
HTTP Response (201 Created)
```

---

## Faz2 Karşılaştırma

```csharp
// Faz2 — Controller her şeyi biliyor
public class SiparisController : Controller
{
    private readonly SiparisServisi _servis; // tek servis
    public async Task<IActionResult> Olustur(SiparisViewModel vm)
    {
        await _servis.OlusturAsync(vm.KullaniciId, vm.KitapId, vm.Adet);
        return RedirectToAction("Index");
    }
    // 50 action → 50 servis bağımlılığı — controller şişiyor
}

// CQRS + MediatR
public class SiparisController : ControllerBase
{
    private readonly IMediator _mediator; // tek bağımlılık
    public async Task<IActionResult> Olustur([FromBody] SiparisOlusturCommand cmd)
    {
        var sonuc = await _mediator.Send(cmd);
        return CreatedAtAction(..., sonuc);
    }
    // 50 endpoint → hepsi _mediator.Send() — yeni handler controller'ı değiştirmiyor
}
```

---

## 500 vs 50k

| Konu | 500 | 50k |
|---|---|---|
| CQRS gerekli mi? | ❌ Servis katmanı yeterli | ✅ Handler sayısı arttıkça şart |
| Simple vs Full CQRS | Simple yeterli | ⚠️ Full sadece read/write oranı çok farklıysa |
| MediatR | Overkill | ✅ Dispatch mekanizması |

---

## Sorular

1. CQS ile CQRS arasındaki fark ne?
2. Full CQRS ne zaman Simple CQRS'ten daha uygun?
3. CQRS olmadan büyük bir projede ne tür sorunlar çıkar?
