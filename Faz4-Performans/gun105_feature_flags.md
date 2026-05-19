# Gün 105 — Feature Flags ve Dark Launching

---

## Feature Flag Nedir?

Kodun içine "if (özellikAcikMi) { yeni davranış } else { eski davranış }" şeklinde bir anahtar koymak. Bu anahtarı koddan değil — config dosyasından, veritabanından veya panel üzerinden açıp kapatabiliyorsun.

**Analoji:** Evdeki ışık. Elektrik tesisatı var (kod), ama lambayı yakmak senin elinde (flag). Yatak odasının lambasını söndürdün diye salonu yeniden kablolamıyorsun.

**Neden ihtiyacın var?**
- Yeni özelliği deploy ettin ama henüz kullanıcılara açmak istemiyorsun → flag'i kapalı tut, hazır olunca aç
- Yeni algoritmayı %5 kullanıcıda dene → bug çıkarsa anında kapat (redeploy yok)
- Premium özellik var → sadece ödeme yapan kullanıcılara aç
- Production'da bir özellik hata veriyor → kill switch ile anında kapat

---

## Deploy ≠ Release — En Önemli Kavram

Bu kavramı anlamadan feature flag'in değerini anlayamazsın.

**Deploy:** Kodu production'a yüklemek. Sunucularda yeni versiyon çalışıyor.
**Release:** Özelliği kullanıcıların görmesi. Trafik bu özelliğe akıyor.

Geleneksel yaklaşımda bu ikisi aynı şey: kodu deploy edersin → kullanıcılar görür. Hata olursa rollback. Rollback dakikalar sürer, kullanıcı etkilenir.

Feature flag ile bunlar ayrılır:
1. Kodu deploy edersin → ama flag kapalı, kimse görmez
2. Hazır olduğunda flag'i açarsın → kullanıcılar görür
3. Sorun çıkarsa flag'i kapatırsın → saniyeler içinde, redeploy yok

Bu yaklaşımın adı **dark launching** — özellik production'da ama "karanlıkta" duruyor, kullanıcılar farkında değil.

**Gerçek senaryo:** Yeni ödeme entegrasyonu yapıyorsun. Stripe → Iyzico'ya geçiş. İkisini de aynı anda deploy ediyorsun ama flag ile sadece Stripe aktif. Iyzico'yu içeride test ediyorsun, hazır olunca %1 kullanıcıda açıyorsun, sorun yoksa %10, %50, %100. Sorun olursa anında geri Stripe.

---

## Feature Flag Türleri

Aynı teknik, farklı amaçlar. Hangi tip flag yazdığını bilmek bakımı için önemli.

### 1. Release Flag — "Henüz Hazır Değil"

Yeni özelliği kapalı tut, hazır olunca aç. Kısa ömürlü — özellik tam yayılınca kaldırılır.

**Örnek:** Yeni sepet tasarımı 3 ay üzerinde çalıştın. Deploy ettin ama flag kapalı. QA testten geçirdi → flag aç → kullanıcılar yeni tasarımı görüyor. 1 ay sonra flag'i koddan çıkar.

### 2. Experiment Flag — A/B Test

Aynı anda iki versiyon, kullanıcı yarıya bölünmüş. Hangisi daha iyi sonuç veriyor ölçüyorsun.

**Örnek:** "Satın Al" butonu kırmızı mı yeşil mi olsun? Kullanıcıların %50'sine kırmızı, %50'sine yeşil göster. 1 hafta sonra hangisinde tıklama oranı yüksek bak. Kazanan kalsın.

### 3. Ops Flag (Kill Switch) — Acil Durum Anahtarı

Üretimde bir özellik sorun çıkarıyor. Kapatmak istiyorsun ama redeploy zaman alır. Ops flag ile saniyeler içinde kapat.

**Örnek:** Öneri algoritması DB'yi yavaşlatıyor. `RecommendationsEnabled = false` yap → algoritma kapanır, fallback statik öneri gösterilir. Sorun çözüldü, sonra düzeltip tekrar aç.

### 4. Permission Flag — Kullanıcı Bazlı Erişim

Belirli kullanıcı/grup için açık, diğerleri için kapalı. Uzun ömürlü olabilir.

**Örnek:** "Beta tester" grubu yeni özellikleri önce görür. Premium kullanıcılara gelişmiş raporlama açık, ücretsiz kullanıcılara kapalı. Admin'lere debug panel görünür.

---

## Microsoft.FeatureManagement — .NET'in Built-in Çözümü

ASP.NET Core'un kendi feature management kütüphanesi. Ek SaaS gerekmiyorsa bununla başla.

### Kurulum

```csharp
// NuGet: Microsoft.FeatureManagement.AspNetCore
builder.Services.AddFeatureManagement();
// ne yapar → IFeatureManager servisini DI'ya kaydeder
// flag'leri appsettings.json'dan okur (varsayılan)
```

```json
// appsettings.json
{
  "FeatureManagement": {
    "YeniSepetTasarimi": true,
    "DenemeAlgoritmasi": false,
    "PremiumRapor": {
      "EnabledFor": [
        { "Name": "Percentage", "Parameters": { "Value": 25 } }
      ]
    }
  }
}
```

### Kod İçinde Kullanım

```csharp
public class SepetController
{
    private readonly IFeatureManager _features;

    public SepetController(IFeatureManager features) => _features = features;

    public async Task<IActionResult> Goster()
    {
        if (await _features.IsEnabledAsync("YeniSepetTasarimi"))
        {
            return View("SepetV2");
            // ne yapar → flag açıksa yeni tasarımı döndür
        }

        return View("SepetV1");
        // bunu yazmasaydık → her zaman tek bir tasarım, geçiş zorlaşır
    }
}
```

### Attribute ile — FeatureGate

```csharp
[FeatureGate("YeniRaporlamaEndpoint")]
[HttpGet("rapor/v2")]
public async Task<IActionResult> YeniRapor()
{
    return Ok(await _service.YeniRaporAsync());
}
// ne yapar → flag kapalıysa endpoint 404 döner, hiç çalışmaz
// bunu yazmasaydık → if check'i metot içinde yazardın, daha az temiz
```

```csharp
// Minimal API'da:
app.MapGet("/yeni-feature", () => Results.Ok("Yeni özellik"))
    .WithMetadata(new FeatureGateAttribute("YeniFeature"));
```

---

## Configuration-Based vs Database-Based Flag'ler

İki ana yaklaşım var, ne zaman hangisi?

### Configuration-Based (appsettings.json)

```json
{ "FeatureManagement": { "YeniTasarim": true } }
```

**Avantaj:**
- Basit, sıfır altyapı
- Versiyon kontrolünde — kim ne zaman değiştirmiş git log'da

**Dezavantaj:**
- Değişiklik için redeploy gerekir (veya `reloadOnChange: true` ile dosya değişikliği)
- Multi-instance'ta tutarsızlık — her sunucu kendi config'ini okur, biri reload olur diğeri olmaz

**Ne zaman:** Küçük proje, basit release flag'ler, kullanıcı bazlı flag yok.

### Database-Based

Flag durumunu DB'de tutuyorsun. Admin panel ile değiştirebilirsin, runtime'da hemen aktif.

```csharp
public class DbFeatureProvider : IFeatureDefinitionProvider
{
    private readonly AppDbContext _context;

    public async IAsyncEnumerable<FeatureDefinition> GetAllFeatureDefinitionsAsync()
    {
        var flags = await _context.FeatureFlags.ToListAsync();
        foreach (var flag in flags)
        {
            yield return new FeatureDefinition
            {
                Name = flag.Name,
                EnabledFor = flag.IsEnabled
                    ? new[] { new FeatureFilterConfiguration { Name = "AlwaysOn" } }
                    : Array.Empty<FeatureFilterConfiguration>()
            };
        }
    }
}
```

**Avantaj:**
- Runtime'da değişiklik — redeploy yok, anında aktif
- Tüm instance'lar aynı DB'yi okur — tutarlı
- Audit kolay (kim ne zaman değiştirmiş loglanır)

**Dezavantaj:**
- Her IsEnabled çağrısı DB'ye gidiyor (cache şart)
- Altyapı yazmak lazım (panel, DB şema, cache)

**Ne zaman:** Production'da sık değişen flag'ler, multi-instance, kill switch ihtiyacı.

---

## Flag Debt (Flag Borcu) — Temizlik Önemli

Feature flag eklemek kolay, **kaldırmak** sürekli ihmal edilir. Her flag bir if/else dallanması. 20 flag varsa kodun her yerinde 20 ayrı dallanma — kod karmaşığı, test zor.

**Flag debt nedir?** Artık kullanılmayan ama hâlâ koddaki flag'ler. Genelde release flag'lerinden birikir:

```csharp
// 6 ay önce eklendi, özellik %100 yayıldı, flag unutuldu:
if (await _features.IsEnabledAsync("YeniSepetTasarimi"))
{
    return View("SepetV2");
}
return View("SepetV1");  // ← bu kod artık ölü, asla çalışmıyor
```

**Sorun ne?**
- Eski kod (`SepetV1`) hâlâ duruyor, kimse bakmıyor — bug birikiyor
- Yeni geliştirici "iki versiyon var, hangisini değiştireyim?" diye kafası karışıyor
- Test coverage iki yolu da test etmek zorunda — yarısı boşa

**Çözüm: Flag temizliği disiplini**
1. Her release flag'e expire date koy (örn: "3 ay sonra kaldır")
2. Tooling: TODO comment'leri scan et, eski flag'leri raporla
3. Sprint planning'de "flag temizliği" task'ı

```csharp
// Kod yorumunda son tarih:
// FLAG-DEBT: Remove after 2026-08-01 (özellik %100 yayıldıktan sonra)
if (await _features.IsEnabledAsync("YeniSepetTasarimi"))
{
    return View("SepetV2");
}
```

---

## A/B Test — Yüzde Bazlı Rollout

Yeni özelliği herkese değil, %10 kullanıcıya açmak istiyorsun:

```json
{
  "FeatureManagement": {
    "YeniAlgoritma": {
      "EnabledFor": [
        {
          "Name": "Microsoft.Percentage",
          "Parameters": { "Value": 10 }
        }
      ]
    }
  }
}
```

```csharp
// Kullanıcı isteği geldiğinde:
if (await _features.IsEnabledAsync("YeniAlgoritma"))
{
    return await _yeniAlgoritma.HesaplaAsync();
}
return await _eskiAlgoritma.HesaplaAsync();
// ne yapar → kullanıcıların yaklaşık %10'una yeni algoritma, kalan %90'a eski
// her istekte rastgele mi? HAYIR — aynı kullanıcı her seferinde aynı sonucu görür
// Microsoft.Percentage filter session/user bazlı tutarlı atama yapar
```

**Neden kullanıcı bazlı tutarlı olmalı?** Aynı kullanıcı bir istekte yeni tasarımı, sonraki istekte eski tasarımı görürse → tutarsızlık → karmaşa. "Sepet butonu nereye gitti?" şikayetleri.

---

## Targeting Filter — Spesifik Kullanıcılara Aç

Belirli kullanıcı, grup veya yüzdelere göre:

```json
{
  "FeatureManagement": {
    "BetaFeature": {
      "EnabledFor": [
        {
          "Name": "Microsoft.Targeting",
          "Parameters": {
            "Audience": {
              "Users": ["berkan@mail.com", "admin@mail.com"],
              "Groups": [
                { "Name": "BetaTesters", "RolloutPercentage": 100 },
                { "Name": "PremiumUsers", "RolloutPercentage": 50 }
              ],
              "DefaultRolloutPercentage": 0
            }
          }
        }
      ]
    }
  }
}
```

```csharp
// ITargetingContextAccessor implement et:
public class CurrentUserTargetingAccessor : ITargetingContextAccessor
{
    private readonly IHttpContextAccessor _accessor;

    public ValueTask<TargetingContext> GetContextAsync()
    {
        var user = _accessor.HttpContext?.User;
        return ValueTask.FromResult(new TargetingContext
        {
            UserId = user?.FindFirst(ClaimTypes.Email)?.Value,
            Groups = user?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>()
        });
        // ne yapar → her istekte aktif kullanıcının bilgisini targeting'e besler
        // flag bu bilgiye göre "bu kullanıcı için aktif mi?" karar verir
    }
}
```

---

## LaunchDarkly, Azure App Configuration — Hazır Servisler

Kendi altyapını kurmak istemiyorsan SaaS çözümleri var:

### LaunchDarkly

Endüstri standardı. Feature flag için özelleşmiş platform.

**Avantaj:**
- Görsel panel (developer olmayanlar da yönetebilir)
- Gelişmiş targeting (lokasyon, cihaz, custom attribute)
- A/B test analitiği dahili
- Audit log, approval workflow

**Dezavantaj:**
- Pahalı (büyük ekiplerde aylık binlerce dolar)
- Üçüncü parti bağımlılık

### Azure App Configuration

Azure'un kendi flag servisi. Daha hafif, daha ucuz.

```csharp
builder.Configuration.AddAzureAppConfiguration(opt =>
{
    opt.Connect(connectionString)
       .UseFeatureFlags();
});
// ne yapar → Azure portal'dan flag'leri değiştirebilirsin, uygulama otomatik yenilenir
```

**Ne zaman SaaS kullan:**
- 50+ flag, ekipte developer olmayan paydaşlar
- Gelişmiş targeting/analitik ihtiyacı
- Audit/compliance zorunluluğu

**Ne zaman built-in yeterli:**
- 5-10 flag, sadece developer ekibi yönetiyor
- Basit on/off senaryolar
- Maliyet hassasiyeti

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de feature flag yok. Yeni özellik geliştirdiğinde:
- Branch oluştur → çalış → merge et → deploy → kullanıcılar görür
- Sorun çıkarsa rollback (dakikalar sürer, kullanıcı etkilenir)
- A/B test imkansız — tek versiyon var

50K kullanıcıda:
- Riski azaltmak için kademeli rollout şart (%1 → %10 → %100)
- Kill switch ihtiyacı (sorun çıkarsa saniyeler içinde kapat)
- A/B test ile kararlar verisel — "buton rengi" gibi şeylerde tahmin yerine ölçüm

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| Release flag | İyi alışkanlık — sıkıntısız rollout | Zorunlu — riski azalt |
| Kill switch (ops flag) | Nadiren ihtiyaç olur | Kritik özellikler için zorunlu |
| Yüzde bazlı rollout | Gereksiz | Zorunlu — büyük değişiklikleri kademeli yay |
| A/B test | Anlamlı istatistik için kullanıcı az | Kararları veriyle al |
| LaunchDarkly gibi SaaS | Pahalı, gereksiz | 50+ flag varsa değerli |
| Flag debt temizliği | Az flag var, kolay | Sprint task'ı olmalı |

---

## Kontrol Soruları

1. "Deploy ≠ Release" ne demek? Feature flag bu ayrımı nasıl sağlar?
2. Release flag, ops flag, experiment flag ve permission flag arasındaki fark nedir?
3. Configuration-based ve database-based flag arasındaki trade-off nedir?
4. Flag debt nedir? Nasıl önlenir?
5. A/B test'te aynı kullanıcı neden her zaman aynı sonucu görmeli?
6. Targeting filter ile yüzde bazlı rollout arasındaki fark nedir?
7. SaaS feature flag servisi ne zaman built-in yerine tercih edilir?
