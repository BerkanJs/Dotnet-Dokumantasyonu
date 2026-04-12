# Gün 54 — Behavioral Patterns 1: Strategy, Observer, Command

---

## 1. Strategy

### Günlük hayat

Google Maps: A'dan B'ye gideceksin. Araba mı, yürüyüş mü, toplu taşıma mı? Rota hesaplama algoritması aynı harita verisiyle çalışıyor ama strateji runtime'da seçiliyor.

### Faz2'de böyle yaptık

Gün 47'de `IIndirimStrategy` ile zaten uyguladık. Aynı fikrin farklı alanı: sıralama.

Faz2'de `KategoriyeGoreGetirAsync` içinde sıralama sabit:

```csharp
// Faz2: EfKitapServisi — sıralama kodun içine gömülü
.OrderBy(k => k.Baslik)   // strateji değiştirilemez, kodu açmadan
```

50k kullanıcıda "kullanıcı seçsin" özelliği gelince bu if/switch'e dönüşür:

```csharp
if (siralama == "ada") query.OrderBy(k => k.Baslik)
else if (siralama == "fiyata") query.OrderBy(k => k.Fiyat)  // OCP ihlali
```

### Büyük projede böyle yapmalısın

```csharp
// Strategy/ISiralama.cs
public interface ISiralama
{
    List<string> Sirala(List<string> kitaplar);
}

public class AdaSirala    : ISiralama { ... }
public class TersAdaSirala : ISiralama { ... }
// yeni sıralama = yeni class, KitapListeServisi'ne dokunulmaz

// Strategy/KitapListeServisi.cs
public class KitapListeServisi
{
    public List<string> Listele(ISiralama siralama)
        => siralama.Sirala(_kitaplar);
    // hangi algoritmanın çalışacağını bilmiyor — runtime'da belirleniyor
    // bunu yazmasaydık → if/switch zinciri (OCP ihlali)
}
```

Kullanım — runtime'da strateji seçimi:

```csharp
var siralama = request.SiralamaParametresi switch
{
    "ada"   => (ISiralama)new AdaSirala(),
    "fiyat" => new FiyataSirala(),
    _       => new AdaSirala()
};
listele(siralama);
```

### 500 vs 50k

| | 500 | 50k |
|---|---|---|
| **1-2 sabit algoritma** | if/switch yeterli | — |
| **Kullanıcı seçiyor / sık değişiyor** | Strategy düşün | ✅ Şart |
| **Overengineering** | Hiç değişmeyecek tek algoritma için interface | — |

---

## 2. Observer

### Günlük hayat

YouTube'da bir kanala abone oldun. Kanal yeni video yüklediğinde sana bildirim geliyor. Kanal kimin abone olduğunu bilmiyor — sadece "video yüklendi" eventi fırlatıyor.

### Faz2'de böyle yaptık

`SiparisOnayKanali` → `Channel<T>` ile producer/consumer zaten loose-coupled Observer mantığı:

```csharp
// Program.cs
builder.Services.AddSingleton<SiparisOnayKanali>();
// Producer: siparişi kanala yazar → consumer kim olduğunu bilmez
// Consumer (SiparisOnayServisi): kanalı dinler → producer'ı bilmez
```

C# `event` keyword Observer pattern'in dil seviyesinde uygulaması. MediatR `INotificationHandler<T>` de Observer — bunu Gün 35'te gördün.

### Büyük projede böyle yapmalısın

```csharp
// Observer/KitapEklendi.cs

// Publisher
public class KitapDepo
{
    public event EventHandler<KitapEklendiEventArgs>? KitapEklendi;
    // bunu yazmasaydık → KitapDepo içinde email + stok kodu olurdu (SRP ihlali)
    // kim abone olduğunu bilmek zorunda kalırdı

    public void Ekle(string baslik, decimal fiyat)
    {
        // ... kayıt işlemi ...
        KitapEklendi?.Invoke(this, new KitapEklendiEventArgs { Baslik = baslik, Fiyat = fiyat });
        // bunu yazmasaydık → aboneler değişiklikten haberdar olamazdı
        // ?. operatörü: abone yoksa null exception atmaz
    }
}

// Subscriber — KitapDepo'yu bilmiyor, sadece event'e abone
public class EmailBildirimServisi
{
    public void Abone(KitapDepo depo)
        => depo.KitapEklendi += (_, e) =>
            Console.WriteLine($"[EMAIL] Yeni kitap: '{e.Baslik}'");
}
```

Yeni subscriber eklemek:

```csharp
// Yeni class yaz, KitapDepo'ya dokunma — OCP
public class AnalizServisi
{
    public void Abone(KitapDepo depo)
        => depo.KitapEklendi += (_, e) => _db.KaydetKitapEklenmesi(e.Baslik);
}
```

### 500 vs 50k

| | 500 | 50k |
|---|---|---|
| **Sonuç bekleniyorsa (sync)** | Direkt metod çağrısı daha basit | — |
| **Loose-coupled bildirim, birden fazla alıcı** | ✅ Observer uygun | ✅ Şart — yeni alıcı = yeni class |
| **Overengineering** | Tek alıcı, hiç değişmeyecek → event gereksiz | — |

---

## 3. Command

### Günlük hayat

Restoranda siparişi kağıda yazıyorsun (komut nesnesi). Kağıdı mutfağa veriyorsun (invoker). Şef yapıyor (receiver). Kağıt sayesinde: sıralayabilirsin, iptal edebilirsin, tekrarlayabilirsin.

### Faz2 ile bağlantı

MediatR `IRequest<T>` + `IRequestHandler<T>` tam Command pattern:

```csharp
// Faz2 Gün 35: CQRS + MediatR
public record KitapEkleCommand(string Baslik, decimal Fiyat) : IRequest<int>;
// → Komut nesnesi

public class KitapEkleHandler : IRequestHandler<KitapEkleCommand, int>
// → Receiver (işi yapan)
```

`_mediator.Send(command)` → Invoker. Faz3'te bunu tam olarak kodlayacağız.

### Büyük projede böyle yapmalısın

```csharp
// Command/SiparisKomutlari.cs

public interface IKomut
{
    void Calistir();
    void GeriAl();   // undo desteği — komut nesne olduğu için mümkün
}

public class KitapSiparisKomutu : IKomut
{
    private readonly string _baslik;
    private readonly SiparisDepo _depo;
    private int _siparisId;  // undo için saklıyoruz

    public void Calistir()
    {
        _siparisId = _depo.SiparisKaydet(_baslik);
        // sonucu sakla — GeriAl buna ihtiyaç duyacak
    }

    public void GeriAl() => _depo.SiparisIptal(_siparisId);
    // bunu yazmasaydık → undo mümkün olmazdı
}

// Invoker: komut geçmişini tutar
public class SiparisIslemcisi
{
    private readonly Stack<IKomut> _gecmis = new();
    // Stack: LIFO — son giren ilk çıkar, undo için doğal yapı
    // bunu yazmasaydık → hangi komutun çalıştığını bilemezdik

    public void Calistir(IKomut komut)
    {
        komut.Calistir();
        _gecmis.Push(komut);
    }

    public void SonGeriAl()
    {
        if (_gecmis.TryPop(out var komut))
            komut.GeriAl();
    }
}
```

### 500 vs 50k

| | 500 | 50k |
|---|---|---|
| **Basit CRUD, undo yok** | Direkt servis çağrısı yeterli | — |
| **Undo/redo, audit log, queue** | ✅ Command şart | ✅ Şart |
| **CQRS** | Overkill olabilir | ✅ Read/Write ayrımı çok değerliyse |
| **Overengineering** | Her metod için komut nesnesi | — |

---

## 4. Chain of Responsibility (Kısa)

ASP.NET Middleware pipeline tam bu:

```
Request → AuthMiddleware → LogMiddleware → RateLimitMiddleware → Controller
                                                                      ↓
Response ← AuthMiddleware ← LogMiddleware ← RateLimitMiddleware ← Controller
```

Her middleware ya işler ya da `next()` ile zinciri devam ettirir. Faz2 Gün 15'te bunu uyguladın.

MediatR Pipeline Behavior da Chain of Responsibility — Faz3'te göreceğiz.

---

## Pattern'lerin CQRS ile Bağlantısı

```
Strategy  → hangi sorgu stratejisi? IQueryable dinamik zinciri
Observer  → domain event: sipariş oluşturulunca email gönder
Command   → CQRS'in C'si: KitapEkleCommand, SiparisOlusturCommand
Chain     → validation → logging → authorization pipeline behavior
```

Bu 4 pattern Onion + CQRS bölümünde tekrar karşına çıkacak — o zaman "bunu zaten görmüştüm" diyeceksin.

---

## Sorular

1. Strategy ve if/switch'in farkı sadece "kod güzelliği" mi? Başka avantajı var mı?
2. C# `event` ile MediatR `INotificationHandler` arasındaki fark ne?
3. MediatR `IRequest<T>` Command pattern'in hangi parçası? `IRequestHandler<T>` hangisi?
