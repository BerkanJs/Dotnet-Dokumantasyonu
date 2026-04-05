# Gün 16 — Kestrel, IIS ve Hosting Modeli

---

## 1. Kestrel Nedir?

Spring uygulamaları Tomcat gibi bir servlet container içinde çalışır. .NET'te bu iş farklı: ASP.NET Core kendi HTTP sunucusunu içinde taşır. Bu sunucunun adı **Kestrel**.

Kestrel, doğrudan HTTP bağlantılarını dinleyen, cross-platform çalışan bir HTTP sunucusu. Uygulaman başladığında Kestrel de başlar, sen durdurunca o da durur. Tomcat gibi ayrı bir süreç değil — uygulamanın bir parçası.

```
Tarayıcı → Kestrel → ASP.NET Core Pipeline → Controller
```

Neden önemli?
- Platform bağımsız: Windows, Linux, macOS — hepsi aynı şekilde çalışır
- Yüksek performanslı: .NET'in en hızlı bileşenlerinden biri
- Üretimde tek başına veya Nginx/IIS arkasında çalışabilir

---

## 2. IIS ile Birlikte Kullanım

Windows sunucuda IIS (Internet Information Services) hâlâ yaygın. Kestrel ile IIS'i birlikte kullanmanın iki yolu var:

**In-process hosting:**

```
İstek → IIS → [Kestrel devre dışı] → ASP.NET Core (IIS işlemi içinde)
```

IIS isteği alır, doğrudan .NET runtime'a geçirir. Kestrel yoktur — IIS'in kendi modülü (AspNetCoreModuleV2) HTTP'yi işler. Daha hızlı çünkü araya ekstra bir süreç girmiyor.

**Out-of-process hosting:**

```
İstek → IIS → Kestrel → ASP.NET Core (ayrı süreç)
```

IIS ters proxy gibi davranır — isteği alır, Kestrel'e iletir. .NET uygulaması ayrı bir süreçte çalışır. IIS crash olursa uygulama etkilenmez.

Üretimde genellikle **in-process** tercih edilir — daha hızlı ve daha az konfigürasyon.

Linux'ta IIS olmaz — orada Nginx veya doğrudan Kestrel kullanılır:

```
İstek → Nginx (reverse proxy) → Kestrel → ASP.NET Core
```

---

## 3. Environment: Dev, Staging, Production

Uygulama üç ortamda çalışır ve her ortamda farklı davranması gerekir:

- **Development:** Detaylı hata sayfası, hot reload, gevşek güvenlik
- **Staging:** Üretime yakın, test için
- **Production:** Hata detayları gizli, performans öncelikli, sıkı güvenlik

ASP.NET Core bunu `ASPNETCORE_ENVIRONMENT` ortam değişkeni ile belirler:

```csharp
if (app.Environment.IsDevelopment())
{
    // Detaylı hata sayfası — sadece geliştirmede
    app.UseDeveloperExceptionPage();
}
else
{
    // Üretimde hata detayı gizle
    app.UseExceptionHandler("/Home/Error");
}
```

VS Code veya Visual Studio'da `launchSettings.json` bu değişkeni otomatik set eder:

```json
{
  "profiles": {
    "KitabeviMVC": {
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

Sunucuya deploy ederken bu değişkeni `Production` yapman gerekir — aksi hâlde veritabanı bağlantı hataları, stack trace'ler kullanıcıya görünür.

---

## 4. appsettings.json — Konfigürasyon Dosyası

Veritabanı bağlantısı, API key, mail ayarları gibi değerleri koda gömmek yerine bu dosyaya yazarsın:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=KitabeviDb;Trusted_Connection=True;"
  },
  "Loglama": {
    "Seviye": "Information",
    "KonsolaYaz": true
  },
  "Jwt": {
    "SecretKey": "buraya-gizli-anahtar",
    "ExpiryMinutes": 60
  }
}
```

**Environment-specific override:**

`appsettings.json` temel değerleri içerir. Her ortam için ayrı dosya olabilir:

```
appsettings.json              → her ortamda geçerli
appsettings.Development.json  → sadece Development'ta üstüne yazar
appsettings.Production.json   → sadece Production'da üstüne yazar
```

Örnek: Development'ta SQLite, Production'da SQL Server kullanmak:

```json
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=prod-server;Database=KitabeviDb;..."
  }
}

// appsettings.Development.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=kitabevi.db"  // SQLite, lokal
  }
}
```

Development ortamında çalışırken `appsettings.Development.json` değerleri kazanır — veritabanı bağlantısı lokal SQLite olur.

---

## 5. Configuration Providers Hiyerarşisi

ASP.NET Core konfigürasyonu birden fazla yerden okur ve üst üste bindirir. Son okunan kazanır:

```
1. appsettings.json                 (en düşük öncelik)
2. appsettings.{Environment}.json
3. Ortam değişkenleri (ENV)
4. Komut satırı argümanları         (en yüksek öncelik)
```

Somut örnek:

```json
// appsettings.json
{ "Jwt": { "ExpiryMinutes": 60 } }
```

```bash
# Ortam değişkeni — appsettings.json'ın üstüne yazar
export Jwt__ExpiryMinutes=30
# __ (çift alt çizgi) nested property için ayraç
```

```bash
# Komut satırı — hepsinin üstüne yazar
dotnet run --Jwt:ExpiryMinutes=15
```

Neden bu hiyerarşi?
- `appsettings.json` → git'e gönderilir, temel değerler
- Ortam değişkeni → sunucuda set edilir, gizli değerler için ideal (şifreler burada)
- Komut satırı → hızlı test için

**Önemli:** Gizli değerleri (şifreler, API key'ler) asla `appsettings.json`'a yazma — git'e gider. Bunun için ortam değişkeni veya `dotnet user-secrets` kullan.

---

## 6. IOptions — Konfigürasyonu C# Sınıfına Bağlamak

`appsettings.json`'daki değerleri string olarak okumak yerine C# sınıfına bağlarsın:

```json
// appsettings.json
{
  "Jwt": {
    "SecretKey": "super-secret",
    "ExpiryMinutes": 60,
    "Issuer": "KitabeviAPI"
  }
}
```

```csharp
// Konfigürasyon sınıfı
public class JwtAyarlari
{
    public string SecretKey     { get; init; } = "";
    public int    ExpiryMinutes { get; init; }
    public string Issuer        { get; init; } = "";
}

// Program.cs — JSON → C# sınıfına bağla
builder.Services.Configure<JwtAyarlari>(
    builder.Configuration.GetSection("Jwt"));
```

Artık herhangi bir yerde DI ile kullanabilirsin:

```csharp
public class TokenServisi
{
    private readonly JwtAyarlari _ayarlar;

    public TokenServisi(IOptions<JwtAyarlari> options)
    {
        _ayarlar = options.Value;
    }

    public string TokenOlustur(string kullanici)
    {
        // _ayarlar.SecretKey, _ayarlar.ExpiryMinutes ...
    }
}
```

---

## 7. IOptions vs IOptionsMonitor vs IOptionsSnapshot

Üçü de konfigürasyon okur ama farkları önemli:

**`IOptions<T>`** — uygulama başlarken bir kez okunur, değişmez:

```csharp
public class TokenServisi
{
    private readonly JwtAyarlari _ayarlar;

    // Singleton servisler için uygun
    public TokenServisi(IOptions<JwtAyarlari> options)
        => _ayarlar = options.Value;
}
```

**`IOptionsSnapshot<T>`** — her HTTP request'inde yeniden okunur:

```csharp
// Scoped servisler için — request bazlı değişen konfigürasyon
public TokenServisi(IOptionsSnapshot<JwtAyarlari> options)
    => _ayarlar = options.Value;
```

**`IOptionsMonitor<T>`** — dosya değişince anında güncellenir (hot reload):

```csharp
// Singleton servisler için — uygulama yeniden başlatmadan konfigürasyon değişince
public TokenServisi(IOptionsMonitor<JwtAyarlari> monitor)
{
    _ayarlar = monitor.CurrentValue;

    // Değişince bildirim al
    monitor.OnChange(yeniAyarlar =>
        Console.WriteLine("Konfigürasyon değişti!"));
}
```

Hangisini kullanacaksın?

| Durum | Kullan |
|---|---|
| Ayarlar uygulama boyunca sabit | `IOptions<T>` |
| Her request'te farklı olabilir | `IOptionsSnapshot<T>` |
| Uygulama çalışırken dosya değişebilir | `IOptionsMonitor<T>` |

Pratikte çoğu durumda `IOptions<T>` yeterli.

---

## 8. Kontrol Soruları

1. Kestrel nedir? Tomcat ile ne farkı var?
ASP.NET Core kendi HTTP sunucusunu içinde taşır. Bu sunucunun adı **Kestrel**.

Kestrel, doğrudan HTTP bağlantılarını dinleyen, cross-platform çalışan bir HTTP sunucusu. Uygulaman başladığında Kestrel de başlar, sen durdurunca o da durur. Tomcat gibi ayrı bir süreç değil — uygulamanın bir parçası.

```
Tarayıcı → Kestrel → ASP.NET Core Pipeline → Controller
```
2. In-process ve out-of-process hosting arasındaki fark nedir? Hangisi daha hızlı ve neden?
IIS isteği alır, doğrudan .NET runtime'a geçirir. Kestrel yoktur — IIS'in kendi modülü (AspNetCoreModuleV2) HTTP'yi işler. Daha hızlı çünkü araya ekstra bir süreç girmiyor.

3. `appsettings.json` ile `appsettings.Production.json` aynı key'i içerirse hangisi kazanır?

4. Şifreyi neden `appsettings.json`'a yazmamalısın? Nereye yazarsın?

5. `IOptions<T>` ile `IOptionsMonitor<T>` arasındaki fark nedir?
