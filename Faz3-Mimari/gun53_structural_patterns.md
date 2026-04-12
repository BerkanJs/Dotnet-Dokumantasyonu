# Gün 53 — Structural Patterns

Structural pattern'ler class'ları ve nesneleri nasıl bir araya getireceğini gösterir.

---

## 1. Decorator

### Günlük hayat

Kahve düşün. Sade kahve var. Üstüne süt ekle → sütlü kahve. Üstüne şeker ekle → şekerli sütlü kahve. Orijinal kahveye dokunmadın — sardın.

### Faz2'de böyle yaptık

`Faz2-ASPNET-MVC/KitabeviMVC/Services/CachedKitapServisi.cs` — Faz2'nin en temiz pattern örneği:

```csharp
public class CachedKitapServisi : IKitapServisi
{
    private readonly IKitapServisi _gercekServis;  // EfKitapServisi sarılıyor

    // EfKitapServisi'ne dokunulmadı (OCP)
    // Controller hangi implementasyonun geldiğini bilmiyor (LSP + DIP)
}
```

`DelegatingHandler` da aynı pattern — `AuthHandler` her HTTP isteğine token ekliyor, `HttpClient`'a dokunmuyor:

```csharp
// Faz2: HttpHandlers/AuthHandler.cs
public class AuthHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, ct);  // sonraki handler'a delege et
    }
}
```

Middleware pipeline da Decorator zinciri: her middleware bir sonrakine delege ediyor.

### Büyük projede böyle yapmalısın

```csharp
// Decorator/CachedKitapServisi.cs
public class CachedKitapServisi : IKitapServisi
{
    private readonly IKitapServisi _gercekServis;
    // bunu yazmasaydık → EfKitapServisi'ne direkt bağımlı olurduk
    // yarın LoggingKitapServisi eklemek istersen aynı şekilde sar

    private List<string>? _cache;

    public CachedKitapServisi(IKitapServisi gercekServis) => _gercekServis = gercekServis;

    public List<string> HepsiniGetir()
    {
        if (_cache is not null) return _cache;          // cache varsa DB'ye gitme
        _cache = _gercekServis.HepsiniGetir();          // yoksa gerçek servise delege et
        return _cache;
    }
}
```

Zincir genişletilebilir:

```
Controller → LoggingKitapServisi → CachedKitapServisi → EfKitapServisi → DB
```

Her katman bir davranış ekliyor, altındakini bilmiyor.

### 500 vs 50k kullanıcı

| | 500 | 50k |
|---|---|---|
| **Caching gerekli mi?** | DB yavaşlamıyorsa hayır | ✅ Yüksek okuma trafiğinde şart |
| **Logging decorator?** | Tek servis varsa direkt ekle | Ayrı decorator → her servis için tekrar kullanılır |
| **Overengineering sinyali** | 1 implementasyon, hiç değişmeyecek → decorator gereksiz | — |

---

## 2. Adapter

### Günlük hayat

Yurt dışından getirilen bir cihazın fişi Türk prizine uymuyor. Adaptör tak — cihaz çalışıyor. Cihazı değiştirmedin, prizi değiştirmedin.

### Faz2'de böyle yaptık

`Faz2-ASPNET-MVC/KitabeviMVC/Services/KitapApiIstemcisi.cs` — dış API'yi domain interface'ine adapt ediyor:

```csharp
// Dış API: GetBookPrice(string isbn) → decimal
// Bizim domain: FiyatGetir(string isbn) → decimal

// KitapApiIstemcisi araya giriyor — uygulama kodu dış API'yi bilmiyor
// Tedarikçi değişince sadece KitapApiIstemcisi değişir
```

### Büyük projede böyle yapmalısın

```csharp
// Adapter/IFiyatSaglayici.cs — bizim domain interface'imiz
public interface IFiyatSaglayici
{
    decimal FiyatGetir(string isbn);
    bool StokVarMi(string isbn);
    // Türkçe, domain diline uygun
    // bunu yazmasaydık → uygulama kodu DisTedarikciApi'ye bağımlı olurdu
}

// Adapter/TedarikciAdapter.cs
public class TedarikciAdapter : IFiyatSaglayici
{
    private readonly DisTedarikciApi _api;

    public decimal FiyatGetir(string isbn) => _api.GetPrice(isbn);
    // İngilizce metod adı → Türkçe domain metoduna çevir

    public bool StokVarMi(string isbn) => _api.CheckAvailability(isbn);
    // tedarikçi değişince → yeni Adapter yaz, çağıran kod dokunulmaz
}
```

### 500 vs 50k kullanıcı

| | 500 | 50k |
|---|---|---|
| **Tek dış API, değişmez** | Direkt çağır — adapter overhead | — |
| **Birden fazla tedarikçi veya değişebilir** | ✅ Adapter şart | ✅ Şart — tedarikçi swap'ı kolay |
| **Overengineering sinyali** | Hiç değişmeyecek wrapper için adapter | — |

---

## 3. Facade

### Günlük hayat

Uçak bileti sitesi: "Satın Al" düğmesine basıyorsun. Arkada koltuk rezervasyonu, ödeme, bagaj kaydı, e-bilet gönderimi oluyor. Sen sadece tek düğme görüyorsun.

### Faz2'de böyle yaptık

`KitapController` aslında bir Facade — DB, cache, validation, auth'u tek bir `HepsiniGetir()` çağrısı arkasına gizliyor. Controller HTTP katmanıyla ilgileniyor, alt sistemleri bilmiyor.

### Büyük projede böyle yapmalısın

```csharp
// Facade/SiparisFacade.cs
// 4 alt sistemi tek basit interface arkasına gizliyor
public class SiparisFacade
{
    private readonly StokServisi _stok;
    private readonly OdemeServisi _odeme;
    private readonly KargoServisi _kargo;
    private readonly BildirimServisi _bildirim;

    public string SiparisVer(string isbn, decimal fiyat, string adres, string email)
    {
        if (!_stok.StokDus(isbn))
            throw new InvalidOperationException("Stok yok");

        if (!_odeme.OdemeAl(fiyat))
            throw new InvalidOperationException("Ödeme başarısız");

        var takipNo = _kargo.KargoOlustur(adres);
        _bildirim.GonderAsync(email, $"Siparişiniz alındı. Takip: {takipNo}");
        return takipNo;
        // 4 adım, 1 çağrı — controller karmaşıklığı görmüyor
        // bunu yazmasaydık → controller'da 4 servis inject + koordinasyon
    }
}
```

Controller:

```csharp
// Controller sadece SiparisFacade biliyor
var takipNo = await _facade.SiparisVer(isbn, fiyat, adres, email);
```

### 500 vs 50k kullanıcı

| | 500 | 50k |
|---|---|---|
| **2 adımlı basit akış** | Facade gereksiz | — |
| **3+ servis koordinasyonu, birden fazla yerde tekrar** | ✅ Facade'a al | ✅ Test kolaylığı + tek değişiklik noktası |
| **Overengineering sinyali** | Tek kullanımlık 2 satır için Facade | — |

---

## 4. Diğer Structural Pattern'ler (Kısa)

**Proxy** — EF Core lazy loading bunu kullanır. `kitap.Yazar`'a erişince EF arka planda ayrı SQL atar. N+1 problemi bu yüzden çıkar (Faz2 Gün 31).

**Composite** — kategori ağacı: kategori hem yaprak (alt kategori yok) hem dal (alt kategorisi var) olabiliyor. İkisi aynı interface'i implement eder → ağaç üzerinde tekdüze işlem yapılır.

**Flyweight** — string interning: `string.Intern("abc")` aynı string'i bellekte tek tutar. Milyonlarca tekrar eden string varsa bellek tasarrufu.

---

## Pattern'lerin SOLID ile Bağlantısı

```
Decorator  → OCP (sarmala, değiştirme) + SRP (her katman tek sorumluluk)
Adapter    → DIP (uygulama somut API'ye bağımlı değil, interface'e bağımlı)
Facade     → SRP (controller koordinasyonu bilmez) + ISP (tek basit interface)
```

---

## Sorular

1. `DelegatingHandler` neden Decorator pattern? `AuthHandler` neyi sarmıyor?
2. `KitapApiIstemcisi` Adapter mı, Facade mı? Farkı ne?
3. `SiparisFacade` içindeki servisleri unit test etmek istersen ne değiştirirsin?
