# Gün 51 — Creational Patterns

---

## 1. Neden Creational Patterns?

`new SomutSinif()` yazmak basit görünür ama sorunlar çıkar:

```
new PdfExporter()  → format string'den geliyorsa kim karar verir?
new Kitap(id, baslik, yazar, fiyat, kategori, stok, isbn, yil)  → parametre sırası?
new TokenServisi() → her yerde ayrı instance mı, aynı instance mı?
```

Creational patterns bu üç soruyu çözer:
- **Factory:** Kim oluşturur?
- **Builder:** Nasıl oluşturulur?
- **Singleton:** Kaç tane oluşturulur?

---

## 2. Factory Method

### Günlük hayat

Kargo şirketi düşün. "Gönderi oluştur" diyorsun — motorlu kurye mu, drone mu, kamyon mu geleceğini bilmiyorsun. Şirket karar veriyor.

### Faz2'de böyle yaptık

`Faz2-ASPNET-MVC/KitabeviMVC/Services/KitapApiIstemcisi.cs` içinde `HttpClient` oluşturulmuyor, `Program.cs`'te `AddHttpClient<KitapApiIstemcisi>` ile factory'ye bırakılıyor:

```csharp
// Program.cs
builder.Services.AddHttpClient<KitapApiIstemcisi>(client => { ... });
// HttpClient'ı sen new'lemiyorsun → IHttpClientFactory oluşturuyor
// socket exhaustion, connection pooling → factory hallediyor
```

`IHttpClientFactory` tam anlamıyla Factory Method pattern.

### Büyük projede böyle yapmalısın

```csharp
// Factory/ExporterFactory.cs
public static class ExporterFactory
{
    public static IKitapExporter Olustur(string format) => format switch
    {
        "pdf"   => new PdfExporter(),
        "excel" => new ExcelExporter(),
        // yeni format: buraya bir satır — başka hiçbir yere dokunma (OCP)
        _ => throw new ArgumentException($"Bilinmeyen format: {format}")
    };
    // bunu yazmasaydık → her controller kendi switch'ini yazardı
    // yeni format eklenince 5 farklı yerde değişiklik
}
```

```csharp
// Çağıran kod format string alıyor — hangi sınıfın geldiğini bilmiyor
var exporter = ExporterFactory.Olustur("pdf");
exporter.Disa_Aktar(kitaplar);
```

### 500 vs 50k kullanıcı

| | 500 | 50k |
|---|---|---|
| **Format sayısı 1-2, sabit** | `new PdfExporter()` yeterli | — |
| **Format sayısı 3+, büyüyor** | Factory'ye taşı | ✅ Şart |
| **Overengineering sinyali** | Tek format için factory açmak | — |

---

## 3. Builder (Fluent)

### Günlük hayat

Bir burger siparişi: "Büyük boy, ekstra peynir, turşusuz, ketçaplı." Her kombinasyon farklı. 10 parametreli constructor yerine adım adım inşa et.

### Faz2'de böyle yaptık

`IQueryable` zinciri Builder pattern — her adım nesneyi döndürüyor, zincirleme çalışıyor:

```csharp
// Faz2: EfKitapServisi içinde IQueryable zinciri
_context.Kitaplar
    .Where(k => k.Kategori == kategori)     // adım 1 → IQueryable döner
    .Where(k => k.Fiyat >= minFiyat)        // adım 2 → IQueryable döner
    .OrderBy(k => k.Fiyat)                  // adım 3 → IQueryable döner
    .Take(limit)                            // adım 4 → IQueryable döner
    .ToListAsync()                          // Bitir() → SQL üretilir, çalışır
```

`StringBuilder` da aynı: `.Append().Append().ToString()`.

### Büyük projede böyle yapmalısın

```csharp
// Builder/KitapSorgu.cs
public class KitapSorgu
{
    public string? Kategori { get; private set; }
    public decimal? MinFiyat { get; private set; }
    public decimal? MaxFiyat { get; private set; }
    public int Limit { get; private set; } = 20;
    public bool SadeceStoktakiler { get; private set; }

    private KitapSorgu() { }
    // private constructor — dışarıdan new'lenemez
    // bunu yazmasaydık → 5 parametreli constructor, sıra karışabilir

    public static KitapSorguBuilder Olustur() => new();

    public class KitapSorguBuilder
    {
        private readonly KitapSorgu _sorgu = new();

        public KitapSorguBuilder Kategori(string kategori)
        {
            _sorgu.Kategori = kategori;
            return this;    // this döner → zincirleme mümkün
            // bunu yazmasaydık → her adımı ayrı değişkene atamak gerekir
        }

        public KitapSorguBuilder FiyatAraligi(decimal min, decimal max)
        {
            _sorgu.MinFiyat = min;
            _sorgu.MaxFiyat = max;
            return this;
        }

        public KitapSorgu Bitir() => _sorgu;
        // inşa tamamlandı — nesne teslim edildi
        // bunu yazmasaydık → yarım kalmış nesne kullanılabilirdi
    }
}
```

Kullanımı:

```csharp
var sorgu = KitapSorgu.Olustur()
    .Kategori("Roman")
    .FiyatAraligi(50, 200)
    .Limit(10)
    .SadeceStoktakiler()
    .Bitir();
```

### 500 vs 50k kullanıcı

| | 500 | 50k |
|---|---|---|
| **3 alan, sabit** | Constructor yeterli | — |
| **5+ alan, opsiyonel kombinasyonlar** | Builder düşün | ✅ Okunabilirlik + hata azalır |
| **Overengineering sinyali** | 2 zorunlu alan için builder | — |

---

## 4. Singleton

### Günlük hayat

Şirketin tek bir muhasebe departmanı var. Herkes aynı departmana gidiyor — her seferinde yeni bir departman kurulmuyor.

### Faz2'de böyle yaptık

```csharp
// Program.cs
builder.Services.AddSingleton<SiparisOnayKanali>();
// Tüm uygulama boyunca tek instance
// Neden? Producer ve consumer aynı Channel<T>'yi paylaşmalı
// Her request için yeni instance olsaydı → farklı kanallar, mesajlar kaybolurdu

builder.Services.AddSingleton<TokenServisi>();
// JWT signing key bir kez yüklenir, sürekli aynı key kullanılır
```

### DI Container ile Singleton — elle yazmana gerek yok

.NET öncesi Singleton şöyle yazılırdı:

```csharp
// ❌ Eski yöntem — thread-safety, test edilemezlik sorunları
public class TokenServisi
{
    private static TokenServisi? _instance;
    private static readonly object _lock = new();

    private TokenServisi() { }

    public static TokenServisi Instance
    {
        get
        {
            lock (_lock) { return _instance ??= new TokenServisi(); }
        }
    }
}
```

.NET DI container ile:

```csharp
// ✅ Modern yöntem
builder.Services.AddSingleton<TokenServisi>();
// Thread-safety: DI container hallediyor
// Test: mock inject edebilirsin
// Lifecycle: uygulama kapanınca dispose edilir
```

**Sonuç:** .NET'te elle Singleton yazmana neredeyse hiç gerek yok. `AddSingleton` yeterli.

### Singleton tuzağı — Captive Dependency

```csharp
// ❌ Singleton içinde Scoped servis kullanmak → exception
builder.Services.AddSingleton<RaporServisi>();    // singleton
builder.Services.AddScoped<IKitapServisi, ...>(); // scoped

public class RaporServisi
{
    // HATA: Singleton, Scoped servis inject edemez
    // Scoped servis her request'te yenilenir, Singleton ise uygulama boyunca yaşar
    public RaporServisi(IKitapServisi kitapServisi) { ... }
}
```

Faz2'de bu yüzden `CachedKitapServisi` Singleton yapılmadı — içindeki `EfKitapServisi` Scoped.

### 500 vs 50k kullanıcı

| | 500 | 50k |
|---|---|---|
| **Paylaşılan state yok** | AddScoped yeterli | AddScoped yeterli |
| **Pahalı oluşturulan, stateless servis** | AddSingleton | ✅ AddSingleton |
| **Channel, Cache, Config** | AddSingleton | ✅ AddSingleton |
| **Dikkat: mutable shared state** | Thread-safety sorunu | Thread-safety sorunu |

---

## 5. Async Initialization Pattern

Constructor async olamaz. Başlangıçta async iş yapman gerekiyorsa:

```csharp
// ✅ Static async factory
public class VeriTabaniServisi
{
    private VeriTabaniServisi() { }

    public static async Task<VeriTabaniServisi> OlusturAsync()
    {
        var servis = new VeriTabaniServisi();
        await servis.BaslangicVerileriniYukleAsync();
        // bunu yazmasaydık → constructor'dan async çağrı yapamazdık
        return servis;
    }
}

// Kullanım
var servis = await VeriTabaniServisi.OlusturAsync();
```

Faz2'de `DbSeeder` tam bu — `IHostedService.StartAsync()` içinde async başlangıç:

```csharp
// Faz2: Program.cs
await dbSeeder.TohumlaAsync();
// app.Run() öncesinde çalışıyor → async factory mantığı
```

---

## Sorular

1. `IHttpClientFactory` neden Factory Method pattern? `new HttpClient()` yazmak neden tehlikeli?
2. Builder'da `return this` neden zorunlu? `void` dönseydi ne değişirdi?
3. Faz2'de `SiparisOnayKanali` neden Singleton, `IKitapServisi` neden Scoped?
