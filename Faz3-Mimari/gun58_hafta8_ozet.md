# Gün 58 — Hafta 8 Özet

Hafta 8'de GoF pattern'lerinin Structural ve Behavioral grubunu, Anti-Pattern'leri ve DDD taktiksel kavramlarını işledik. Bu özetle konuları bağlıyoruz.

---

## Mimari Sorular

### 1. Sipariş durumu State pattern ile nasıl modellenir?

Faz2'de:
```csharp
// string ile durum — geçersiz geçişe izin veriyor
public class Siparis
{
    public string Durum { get; set; } // "Beklemede", "Onaylandi", "Kargolandi"
    public void Onayla() { Durum = "Onaylandi"; } // iptal edilmiş sipariş de onaylanabilir
}
```

State pattern ile:
```csharp
// Her durum kendi geçiş kurallarını taşır
public abstract class SiparisState
{
    public abstract void Onayla(Siparis siparis);
    public abstract void Iptal(Siparis siparis);
    public abstract void Kargola(Siparis siparis);
}

public class BeklemedeState : SiparisState
{
    public override void Onayla(Siparis siparis)
        => siparis.SetState(new OnaylandiState());

    public override void Iptal(Siparis siparis)
        => siparis.SetState(new IptalState());

    public override void Kargola(Siparis siparis)
        => throw new DomainException("Onaylanmadan kargoya verilemez");
}

public class OnaylandiState : SiparisState
{
    public override void Onayla(Siparis siparis)
        => throw new DomainException("Zaten onaylı");

    public override void Kargola(Siparis siparis)
        => siparis.SetState(new KargolandiState());

    public override void Iptal(Siparis siparis)
        => siparis.SetState(new IptalState());
}

public class IptalState : SiparisState
{
    public override void Onayla(Siparis siparis)
        => throw new DomainException("İptal edilen sipariş onaylanamaz");

    public override void Iptal(Siparis siparis)
        => throw new DomainException("Zaten iptal edildi");

    public override void Kargola(Siparis siparis)
        => throw new DomainException("İptal edilen sipariş kargoya verilemez");
}
```

**Fark:** State pattern ile geçersiz durum geçişi derleme zamanında değil, runtime'da ama tek yerde yakalanır. Yeni durum eklemek = yeni class, mevcut kod dokunulmaz (OCP).

---

### 2. MediatR neden Service Locator antipattern'i değildir?

**Service Locator:**
```csharp
// Handler içinden container'a soruyorsun — hidden dependency
public class SiparisHandler
{
    public void Handle()
    {
        var repo = ServiceLocator.Get<IKitapRepository>(); // dependency görünmüyor
    }
}
```

**MediatR:**
```csharp
// Dependency constructor'da açık — container dışarıdan verir
public class SiparisHandler : IRequestHandler<SiparisOlusturCommand, SiparisOlusturResult>
{
    private readonly IKitapRepository _repo; // açık dependency

    public SiparisHandler(IKitapRepository repo) { _repo = repo; }
    // ↑ test edilebilir: mock geçilebilir
    // Service Locator'da bu mümkün değil
}
```

Fark: MediatR dispatch mekanizması — bağımlılığı saklamıyor, routing yapıyor.

---

### 3. Anemic vs Rich Domain Model — ne zaman hangisi?

| Kriter | Anemic | Rich |
|---|---|---|
| **Domain karmaşıklığı** | Basit CRUD | Karmaşık iş kuralları |
| **İş kuralı sayısı** | Az | Çok |
| **Değişim sıklığı** | Nadir | Sık |
| **Ekip büyüklüğü** | Küçük | Büyük |
| **Test ihtiyacı** | Düşük | Yüksek |

Faz2 KitabeviMVC'de Anemic vardı — `Kitap` sadece getter/setter. Küçük proje için kabul edilebilir. 50k kullanıcıda stok kuralı 5 farklı servise dağılınca Rich Domain Model zorunlu hale gelir.

---

## Hafta 8 Özet

| Konu | Anahtar Fikir |
|---|---|
| Decorator | Davranış ekle, sınıfı değiştirme |
| Adapter | Uyumsuz interface'i uyumlu yap |
| Strategy | Algoritma ailesi, runtime seçimi |
| Observer | C# event keyword bunun implementasyonu |
| Command | MediatR IRequestHandler = Command pattern |
| State | Durum geçişini nesne olarak temsil et |
| Anti-Pattern | Anemic Domain Model en sık görülen |
| DDD | Aggregate Root = transaction boundary |

---

## Sonraki Hafta

Hafta 9'da teoriden pratiğe geçiyoruz: Onion Architecture ile Kitabevi projesini 4 katmana böleceğiz.
