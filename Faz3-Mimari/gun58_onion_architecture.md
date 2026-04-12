# Gün 58 — Onion Architecture

Onion Architecture'ı anlamak için önce şu soruyu sormak gerekiyor: **"Bağımlılık nereye bakmalı?"**

Faz2'de `Controller → Service → Repository → Database` zinciri vardı. Herkes bir alta bakıyordu. Sorun şu: Database değişince Repository değişiyor, Repository değişince Service değişiyor, Service değişince Controller değişiyor. Domino etkisi.

Onion'da kural tek: **her şey merkeze — Domain'e — doğru bakmalı. Dışarıdaki katmanlar içeridekilere bağımlı, içeridekiler dışarıdakilere bağımlı değil.**

---

## Katmanlar ve Sorumluluklar

```
         ┌─────────────────────────────┐
         │      Infrastructure         │  ← EF Core, SMTP, S3, HTTP clients
         │   ┌─────────────────────┐   │
         │   │    Application      │   │  ← Use case'ler, CQRS handlers
         │   │  ┌─────────────┐   │   │
         │   │  │   Domain    │   │   │  ← Entity, Value Object, Repository interface
         │   │  └─────────────┘   │   │
         │   └─────────────────────┘   │
         └─────────────────────────────┘
              API / Presentation Layer    ← Controller, minimal API, gRPC endpoint
```

**Domain (merkez):** Hiçbir şeye bağımlı değil. Saf C# sınıfları. EF Core import yok, ASP.NET import yok.

**Application:** Domain'e bağımlı. Dış dünyayı interface üzerinden görür: `IOrderRepository`, `IEmailService`. Hangisi implement ediyor? Umursamıyor.

**Infrastructure:** Application'ın interface'lerini implement eder. EF Core, RabbitMQ, SMTP burada. Application'a bağımlı (interface implement etmek için), Domain'e bağımlı.

**Presentation (API):** Application'ı çağırır. MediatR query/command gönderir, sonucu döner.

---

## Faz2 ile Karşılaştırma

Faz2 `KitabeviMVC` projesinde:

```csharp
// Controllers/KitapController.cs — Faz2
public class KitapController : Controller
{
    private readonly KitabeviDbContext _context;  // Controller doğrudan DB'ye bağımlı
    
    public async Task<IActionResult> Index()
    {
        var kitaplar = await _context.Kitaplar
            .Include(k => k.YazarNavigation)
            .ToListAsync();
        return View(kitaplar);
    }
}
```

Sorunlar:
- Controller DB'yi biliyor → SQL Server'dan PostgreSQL'e geçince Controller da değişmesi gerekiyor
- Test etmek için gerçek DB gerekiyor → integration test bile yazamıyorsun, unit test hiç
- Sipariş mantığı (fiyat hesaplama, stok kontrolü) Controller ya da DbContext'e gömülüyor → iş kuralı değişince her yeri arıyorsun

Onion'da aynı senaryo:

```csharp
// Domain/Entities/Kitap.cs
public class Kitap
{
    public int Id { get; private set; }
    public string Baslik { get; private set; }
    public Fiyat Fiyat { get; private set; }   // Value Object — Faz2'de decimal vardı
    public int Stok { get; private set; }

    public bool StokVarMi() => Stok > 0;      // iş kuralı Domain'de — EF Core yok, Controller yok

    public void StokAzalt(int adet)
    {
        if (adet > Stok)                       // Domain kural ihlali Domain'de yakalanıyor
            throw new DomainException("Yetersiz stok");
        Stok -= adet;
    }
}

// Application/Interfaces/IKitapRepository.cs
public interface IKitapRepository           // Application katmanı bu interface'i tanımlıyor
{
    Task<Kitap> GetByIdAsync(int id);       // Infrastructure'ın ne kullandığını bilmiyor
    Task AddAsync(Kitap kitap);
}

// Infrastructure/Repositories/KitapRepository.cs
public class KitapRepository : IKitapRepository   // interface'i implement eden taraf Infrastructure
{
    private readonly AppDbContext _context;         // EF Core sadece burada
    
    public async Task<Kitap> GetByIdAsync(int id)
        => await _context.Kitaplar.FindAsync(id);
}

// Application/UseCases/SiparisOlusturHandler.cs
public class SiparisOlusturHandler
{
    private readonly IKitapRepository _kitapRepo;   // interface görüyor, EF Core görmüyor
    private readonly ISiparisRepository _siparisRepo;

    public async Task Handle(SiparisOlusturCommand cmd)
    {
        var kitap = await _kitapRepo.GetByIdAsync(cmd.KitapId);
        kitap.StokAzalt(cmd.Adet);                 // iş kuralı çalışıyor, DB bilgisi yok
        // ...
    }
}
```

Şimdi SQL Server'dan PostgreSQL'e geçilecek: sadece `KitapRepository`'nin içi değişiyor. Domain, Application, Controller — hiçbiri değişmiyor.

---

## Gerçek Hayatta Nerede Kullanılır?

### Kullanılması mantıklı olan yerler

**1. Karmaşık iş kuralları olan e-ticaret / fintech / ERP sistemleri**

Fiyat hesaplama, kampanya kuralları, stok yönetimi, sipariş durumu geçişleri gibi onlarca iş kuralı varsa bunların bir yerde yaşaması gerekiyor. Onion olmadan bu kurallar Controller'a, Service'e, hatta View'a dağılıyor. 3 ay sonra "kampanya hesabı nerede?" sorusunu soramazsın.

Örnek: Trendyol, Hepsiburada gibi platformların sipariş servisi. Kampanya motoru ayrı bir domain nesnesi, fiyat hesaplama ayrı, lojistik kurallara göre kargo seçimi ayrı.

**2. Birden fazla delivery channel olan sistemler**

Aynı iş mantığını hem REST API hem gRPC hem de background job üzerinden çağırmanız gerekiyorsa Application katmanı zaten bunu karşılıyor. Handler bir kez yazılıyor, üç kanaldan da çağrılıyor.

Örnek: Bir banka uygulaması — EFT hem mobil API'dan hem otomatik ödeme scheduler'dan hem de teller arayüzünden tetikleniyor. İş kuralı tek yerde.

**3. Altyapı bağımlılıklarının değişebileceği uzun ömürlü projeler**

"İleride AWS'den Azure'a geçebiliriz" ya da "şu an SQL Server var, Mongo da gelebilir" deniyorsa. Interface arkasına gizlenmiş infrastructure kolayca swap edilebiliyor.

**4. Takım büyük, paralel geliştirme olacak**

Domain takımı entity yazıyor, Application takımı handler yazıyor, Infrastructure takımı repository yazıyor — birbirini beklemeden. Interface kontrat görevi görüyor.

---

### Kullanılmaması gereken yerler

**1. Admin paneli / CRUD ağırlıklı araçlar**

Sadece "şunu veritabanına yaz, şunu listele" yapıyorsunuz. Sipariş al, ürün ekle, kullanıcı yönet — saf CRUD. Burada Domain davranışı yok, Value Object yok, karmaşık geçiş kuralı yok.

```
Gerçek: bir muhasebe firmasının iç araçları, bir okul kayıt sistemi, 
bir restoran menü yönetimi. 
→ Burada Onion = 5 katman sadece "INSERT INTO" yapmak için
```

**2. Prototip / MVP / 3 ay içinde ölecek proje**

Hızlı doğrulama istiyorsunuz. Market'e çıkacak, tutarsa büyütülecek. Önce Faz2 tarzı çalışan bir şey çıkar. Tutarsa refactor et. Onion ile başlamak burada haftalar harcatır.

**3. Tek geliştirici, küçük takım, basit domain**

Katmanlar arası geçiş kodunu yazan kişi de, okuyan kişi de siz oluyorsunuz. Ek klasörler, interface'ler, DI kayıtları — bunların hepsi sizin yazdığınız, sizin okuduğunuz şeyler. Fayda < maliyet.

**4. Microservice'in tek bir şeyi iyi yaptığı durumlar**

Bir microservice sadece "resim yükle ve CDN'e gönder" yapıyorsa — burada business logic yok. Onion katmanlı yapı gereksiz şişirme.

---

## Overengineering Sinyalleri

Bunları görüyorsanız Onion'ı yanlış uygulamış ya da gereksiz yere uygulamışsınızdır:

**1. Handler içinde sadece repository çağrısı var**

```csharp
// Bu handler'ın varlığı anlamsız — hiç business logic yok
public class KitapGetHandler
{
    public async Task<Kitap> Handle(KitapGetQuery query)
        => await _repo.GetByIdAsync(query.Id);   // sadece pass-through, hiç kural yok
}
```

Eğer handler sadece repoyu çağırıp dönüyorsa — bu katman boş. Ya domain'de eksik bir şey var ya da Onion burada gerekmiyordu.

**2. Domain entity'lerinde hiç method yok, sadece property var**

```csharp
public class Siparis
{
    public int Id { get; set; }
    public string Durum { get; set; }     // Enum bile değil, string
    public decimal Tutar { get; set; }    // iş kuralı yok, doğrulama yok
}
```

Bu Entity değil, DTO. Domain davranışı olmayan bir şeyi Onion ile korumak için 4 katman açmak gereksiz.

**3. Her şey için interface var ama tek implementation var ve değişmeyecek**

```csharp
public interface IEmailSender { }
public class EmailSender : IEmailSender { }   // başka implement eden yok, olmayacak da
public interface ILogger { }
public class ConsoleLogger : ILogger { }      // test'te bile aynısı kullanılacak
```

Interface = değişim noktası. Değişmeyecekse interface overhead'dan başka bir şey değil. Gerçekten test etmek ya da swap etmek için mi yazıyorsunuz — buna dürüstçe cevap verin.

**4. DTO → Entity → Domain Model → ViewModel zinciri hiçbir şey değiştirmeden geçiyor**

```csharp
// Her katmanda map var ama hiçbirinde farklı bir şey olmuyor
CreateKitapDto → Kitap Entity → KitapDomainModel → KitapViewModel
// 4 class, 3 AutoMapper profili, 0 business value
```

Eğer map eden şeyler aynıysa — katmanlar gereksiz bölünmüş demektir.

---

## 500 vs 50k Kullanıcı Karar Tablosu

| Kriter | 500 kullanıcı/ay | 50k kullanıcı/ay |
|---|---|---|
| **Onion kullanmalı mısın?** | Genellikle hayır | Karmaşıklığa bağlı |
| **Ne zaman evet?** | Domain kurallar çok karmaşıksa | Neredeyse her zaman |
| **Ne zaman hayır?** | CRUD ağırlıklıysa | Domain basit, saf CRUD ise |
| **Overengineering riski** | Yüksek — 5 katman gereksiz | Düşük — karmaşıklık katmanı haklı kılıyor |
| **Tavsiye** | Faz2 tarzı başla, domain şişince refactor et | Baştan Onion — sonradan geçmek çok pahalı |

Gerçek karar sorusu: **"Domain'imde birden fazla iş kuralı var mı? Bu kurallar değişiyor mu?"**
- Hayır → Onion gerekmez
- Evet → Onion düşün

---

## Dizin Yapısı

```
KitabeviOnion/
├── src/
│   ├── KitabeviOnion.Domain/           ← hiçbir NuGet bağımlılığı yok
│   │   ├── Entities/
│   │   │   ├── Kitap.cs
│   │   │   └── Siparis.cs
│   │   ├── ValueObjects/
│   │   │   ├── Fiyat.cs
│   │   │   └── Isbn.cs
│   │   ├── Interfaces/                 ← repository interface'leri burada
│   │   │   ├── IKitapRepository.cs
│   │   │   └── ISiparisRepository.cs
│   │   └── Exceptions/
│   │       └── DomainException.cs
│   │
│   ├── KitabeviOnion.Application/      ← Domain'e bağımlı, Infrastructure'ı bilmiyor
│   │   ├── UseCases/
│   │   │   ├── KitapListele/
│   │   │   │   ├── KitapListeleQuery.cs
│   │   │   │   └── KitapListeleHandler.cs
│   │   │   └── SiparisOlustur/
│   │   │       ├── SiparisOlusturCommand.cs
│   │   │       └── SiparisOlusturHandler.cs
│   │   └── DTOs/
│   │       └── KitapDto.cs
│   │
│   ├── KitabeviOnion.Infrastructure/   ← EF Core, SMTP, HTTP client burada
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   └── Repositories/
│   │   │       ├── KitapRepository.cs
│   │   │       └── SiparisRepository.cs
│   │   └── Services/
│   │       └── EmailService.cs
│   │
│   └── KitabeviOnion.API/              ← Controller, DI kayıtları, Program.cs
│       ├── Controllers/
│       │   └── KitapController.cs
│       └── Program.cs
```

---

## Bir Sonraki Ders

Gün 59'da bu yapıyı sıfırdan implement edeceğiz: Domain entity'leri, repository interface'leri, Application handler'ları ve Infrastructure. Çalışan bir sipariş oluşturma use case'i yazacağız.
