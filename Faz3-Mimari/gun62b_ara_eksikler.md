# Gün 62b — Ara: Atlanan Teorik Konular (Gün 58 + Gün 63–65)

Gün 58 State Pattern/Anemic-Rich teorisi ile Gün 63-65 arası MediatR Pipeline, Result Pattern ve Hafta 9 Özet pratiğe önceliklendirilerek atlandı. Bu ara gün eksikleri topluyor.

---

## Gün 58 — State Pattern ile Sipariş Durumu

### Sorun: string Durum

Faz2'de sipariş durumu `string` tipindeydi:

```csharp
// Faz2 — KitabeviMVC/Models/Siparis.cs
public class Siparis
{
    public string Durum { get; set; } = "Beklemede";
    // ↑ string: "Beklemede" mi yoksa "beklemede" mi? Typo fark edilmez
    //   bunu yazmasaydık → derlemede hata yok, runtime'da "ONAYLNDI" typo geçerli kabul edilir

    public void Onayla()
    {
        Durum = "Onaylandi"; // her zaman çalışır — iptal edilmiş sipariş de "onaylanır"
        // bunu yazmasaydık → hiç değişmezdi ama sorun devam ederdi:
        //   iptal → onayla → tekrar iptal → sipariş hâlâ "İptal" gibi davranmalı mıydı?
    }

    public void Iptal()
    {
        Durum = "Iptal";
        // iptal edilmiş sipariş tekrar iptal edilebilir — RuntimeError fırlatmıyor
    }
}
```

**Sorun:** Geçersiz geçişler (iptal → onayla, teslim → beklemede) hiçbir yerde engellenmez. Her geçiş kuralı servise dağılır:

```csharp
// SiparisServisi.cs — kural 5 farklı metotta tekrar tekrar
public async Task OnaylaAsync(int id)
{
    var s = await _repo.BulByIdAsync(id);
    if (s.Durum == "Iptal") return; // kural 1 — burada
    if (s.Durum == "Teslim Edildi") return; // kural 2 — burada
    s.Durum = "Onaylandi";
}

public async Task KargolaAsync(int id)
{
    var s = await _repo.BulByIdAsync(id);
    if (s.Durum != "Onaylandi") return; // kural 3 — burada (başka bir if, başka bir servis)
    s.Durum = "Kargolandi";
}
// 5 metot, 5 farklı if — yeni durum eklenince hangisini güncellediğini unutursun
```

---

### Çözüm: State Pattern

Her durum, kendi geçiş kurallarını taşıyan bir class olur:

```csharp
// Domain/States/ISiparisState.cs
namespace KitabeviOnion.Domain.States;

public interface ISiparisState
// ↑ interface: her durum bu sözleşmeyi uygular
//   bunu yazmasaydık → abstract class yazardık, C# tek kalıtım kısıtı bazı tasarımları kısıtlar
{
    void Onayla(Siparis siparis);
    // ↑ context nesnesini (Siparis) parametre alır — durum kendi başına değil, context üzerinde çalışır
    //   bunu yazmasaydık → her state Siparis'e erişemezdi, SetState çağıramazdı

    void Iptal(Siparis siparis);
    void Kargola(Siparis siparis);
}
```

```csharp
// Domain/States/BeklemedeState.cs
public class BeklemedeState : ISiparisState
// ↑ "Beklemede" durumu — sadece bu state geçerli geçişleri biliyor
//   bunu yazmasaydık → geçiş kuralları Siparis veya Service'e dağılırdı
{
    public void Onayla(Siparis siparis)
        => siparis.SetState(new OnaylandiState());
    //  ↑ geçiş başarılı — yeni state set ediliyor
    //    bunu yazmasaydık → BeklemedeState.Onayla() hiçbir şey yapmazdı veya hata fırlatırdı

    public void Iptal(Siparis siparis)
        => siparis.SetState(new IptalState());
    //  ↑ beklemedeyken iptal geçerli

    public void Kargola(Siparis siparis)
        => throw new DomainException("Onaylanmadan kargoya verilemez");
    //  ↑ geçersiz geçiş — rule burada, Siparis değil
    //    bunu yazmasaydık → kural serviste if yazılırdı, 3 servis üçü farklı check yazardı
}
```

```csharp
// Domain/States/OnaylandiState.cs
public class OnaylandiState : ISiparisState
{
    public void Onayla(Siparis siparis)
        => throw new DomainException("Zaten onaylandı");
    //  ↑ idempotent ihlali — "tekrar onayla" engeli burada
    //    bunu yazmasaydık → onaylanmış sipariş tekrar onaylanabilir, ödeme iki kez alınabilirdi

    public void Kargola(Siparis siparis)
        => siparis.SetState(new KargolandiState());
    //  ↑ onaylı → kargoya geçiş geçerli

    public void Iptal(Siparis siparis)
        => siparis.SetState(new IptalState());
    //  ↑ onaylı sipariş iptal edilebilir (para iadesi triggerlanabilir)
}
```

```csharp
// Domain/States/IptalState.cs
public class IptalState : ISiparisState
{
    public void Onayla(Siparis siparis)
        => throw new DomainException("İptal edilen sipariş onaylanamaz");
    //  ↑ terminal state — bu geçiş hiç mümkün değil
    //    bunu yazmasaydık → iptal sonrası tekrar onayla çağrıldığında sessizce geçerdi

    public void Iptal(Siparis siparis)
        => throw new DomainException("Zaten iptal edildi");

    public void Kargola(Siparis siparis)
        => throw new DomainException("İptal edilen sipariş kargoya verilemez");
}
```

```csharp
// Domain/Entities/Siparis.cs (State pattern ile)
public class Siparis
{
    private ISiparisState _state = new BeklemedeState();
    //                              ↑ başlangıç state: her yeni sipariş Beklemede
    //                                bunu yazmasaydık → state null olur, ilk çağrıda NullReferenceException

    public string Durum => _state.GetType().Name.Replace("State", "");
    //             ↑ durum adını class adından türetiyoruz — "BeklemedeState" → "Beklemede"
    //               bunu yazmasaydık → ayrı bir Durum property tutmak zorunda kalırdık, state ile sync'i yönetirdik

    internal void SetState(ISiparisState yeniState)
    //       ↑ internal: sadece State class'ları çağırabilir (aynı assembly)
    //         bunu yazmasaydık → public olurdu, herkes siparis.SetState(new IptalState()) yapardı
    {
        _state = yeniState;
    }

    public void Onayla() => _state.Onayla(this);
    //                              ↑ this: context'i (Siparis) state'e geç — state geri SetState çağıracak
    //                                bunu yazmasaydık → state, Siparis'e erişemezdi

    public void Iptal() => _state.Iptal(this);
    public void Kargola() => _state.Kargola(this);
}
```

---

### Faz2 vs Faz3 Karşılaştırma — State Pattern

```
Faz2:
- Siparis.Durum = "Onaylandi"    → string, typo riski
- if (s.Durum == "Iptal") return → kural serviste, dağınık
- Yeni durum = tüm servislerde yeni if blokları

Faz3 (State Pattern):
- siparis.Onayla()               → kural IptalState içinde
- Yeni durum = yeni class, OCP korunuyor
- Test: IptalState.Onayla() tek başına test edilebilir
```

---

### Anemic vs Rich Domain Model

**Anemic (Faz2):**

```csharp
// Faz2 — Kitap sadece data kabı
public class Kitap
{
    public int Id { get; set; }
    public int StokAdedi { get; set; } // public set — dışarıdan -99 yazılabilir
    public decimal Fiyat { get; set; } // kural yok, negatif geçerli
}

// Kural serviste — 3 serviste 3 farklı stok kontrolü
public class KitapServisi
{
    public void StokDus(int kitapId, int adet)
    {
        var kitap = _repo.BulById(kitapId);
        if (kitap.StokAdedi < adet) throw new Exception("Yetersiz stok"); // kural burada
        kitap.StokAdedi -= adet;
    }
}

public class SiparisServisi
{
    public void SiparisVer(int kitapId, int adet)
    {
        var kitap = _repo.BulById(kitapId);
        if (kitap.StokAdedi < adet) throw new Exception("Stok yok"); // aynı kural tekrar
        kitap.StokAdedi -= adet;
        // ↑ iki serviste iki farklı hata mesajı, iki farklı if — hangisi doğru?
    }
}
```

**Rich Domain (Faz3):**

```csharp
// Faz3 — Kitap kendi kurallarını taşıyor
public class Kitap
{
    public int StokAdedi { get; private set; }
    //                     ↑ private set: dışarıdan doğrudan değiştirilemez
    //                       bunu yazmasaydık → herkes kitap.StokAdedi = -999 yapardı

    public void StokAzalt(int adet)
    //          ↑ kural Kitap içinde — kopyalanmaz, dağılmaz, tek yer
    {
        if (adet <= 0)
            throw new DomainException("Azaltılacak adet sıfırdan büyük olmalı");
        //  ↑ invariant: negatif adet isteği mantıksız
        //    bunu yazmasaydık → StokAdedi += (-5) ile artabilirdi

        if (adet > StokAdedi)
            throw new DomainException($"Yetersiz stok. Mevcut: {StokAdedi}");
        //  ↑ invariant: stok negatife düşemez
        //    bunu yazmasaydık → stok -3 olabilirdi, fiziksel anlamsız

        StokAdedi -= adet;
    }

    public bool StokVarMi() => StokAdedi > 0;
    // ↑ soru domain'de — servislerin if (kitap.StokAdedi > 0) yazması gerekmez
    //   bunu yazmasaydık → her servis kendi if'ini yazardı, eşik değişince hepsini güncellirdin
}
```

---

### Ne Zaman Hangisi?

| Kriter | Anemic | Rich Domain |
|---|---|---|
| **Domain karmaşıklığı** | Basit CRUD | Karmaşık iş kuralları |
| **İş kuralı sayısı** | Az, nadiren değişir | Çok, sık değişir |
| **Ekip büyüklüğü** | 1-3 kişi | 5+ kişi |
| **Test ihtiyacı** | Integration test yeterli | Unit test: domain kural testi |
| **500 kullanıcı** | ✅ Anemic yeterli | ✅ İkisi de çalışır |
| **50k kullanıcı** | ❌ Kural 5 servise dağılır | ✅ Kural domain'de, servis şişmez |

---

### MediatR neden Service Locator değil?

**Service Locator (anti-pattern):**

```csharp
// Handler bağımlılığını container'dan kendisi çekiyor — hidden dependency
public class SiparisOlusturHandler
{
    public async Task<int> Handle(SiparisOlusturCommand cmd)
    {
        var repo = ServiceLocator.Get<ISiparisRepository>(); // ← hidden dependency
        //         ↑ kim baksa handler'ın ne istediğini bilemez
        //           bunu yazmasaydık → test için ServiceLocator'ı mock'lamak zorunda kalırsın
        //           ServiceLocator.Get = global state, paralel testte race condition riski

        var emailSvc = ServiceLocator.Get<IEmailService>(); // bir tane daha hidden
        // constructor'a bak → hiçbir şey görmüyorsun
    }
}
```

**MediatR — açık bağımlılık:**

```csharp
// Bağımlılık constructor'da görünür — test edilebilir
public class SiparisOlusturHandler : IRequestHandler<SiparisOlusturCommand, int>
{
    private readonly ISiparisRepository _siparisRepo;
    private readonly IEmailService _emailService;
    //               ↑ her iki bağımlılık görünür — "ne gerekiyor?" sorusunun cevabı burada
    //                 bunu yazmasaydık → ServiceLocator.Get içinde saklı kalırdı

    public SiparisOlusturHandler(ISiparisRepository siparisRepo, IEmailService emailService)
    //                            ↑ DI container bunu constructor'a enjekte eder
    //                              test: new SiparisOlusturHandler(mockRepo, mockEmail) → kolay mock
    {
        _siparisRepo = siparisRepo;
        _emailService = emailService;
    }
}
// MediatR'ın yaptığı: "SiparisOlusturCommand geldi → SiparisOlusturHandler'ı çöz ve çağır"
// Bu ROUTING, bağımlılık gizleme değil
```

---

## Gün 63 — MediatR Pipeline Behaviors

### Sorun: Cross-cutting Concerns

Her handler'da logging, validation, transaction aynı kod tekrarlanıyor:

```csharp
// MediatR olmadan — her handler aynı kodu tekrar yazıyor
public class KitapEkleHandler
{
    public async Task<int> Handle(KitapEkleCommand cmd, CancellationToken ct)
    {
        _logger.LogInformation("KitapEkle başladı");  // tekrar
        // validation — tekrar
        if (string.IsNullOrEmpty(cmd.Baslik)) throw new ValidationException(...);

        await using var tx = await _context.Database.BeginTransactionAsync(ct); // tekrar
        try
        {
            // asıl iş
            var kitap = new Kitap(cmd.Baslik, ...);
            await _repo.EkleAsync(kitap, ct);
            await tx.CommitAsync(ct);
            _logger.LogInformation("KitapEkle tamamlandı"); // tekrar
            return kitap.Id;
        }
        catch { await tx.RollbackAsync(ct); throw; } // tekrar
    }
}
// 10 handler = 10 kez aynı logging + 10 kez aynı transaction kodu
```

**Çözüm:** Pipeline Behavior — her request geçerken davranışı middleware gibi sar.

---

### Transaction Behavior

Command'i transaction'a sarar — handler başarısız olursa rollback:

```csharp
// Application/Behaviors/TransactionBehavior.cs
namespace KitabeviMediatr.Application.Behaviors;

public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
//                                ↑ generic — tüm TRequest tiplerinde çalışır
//                                  bunu yazmasaydık → her Command için ayrı behavior yazmak zorunda kalırdık
    where TRequest : IRequest<TResponse>
//  ↑ constraint: sadece IRequest uygulayan tipler (MediatR command/query'si)
//    bunu yazmasaydık → MediatR dışı tipler de bu behavior'ı tetikleyebilirdi
{
    private readonly AppDbContext _context;
    //               ↑ Infrastructure — transaction başlatmak için DbContext gerekiyor
    //                 bunu yazmasaydık → transaction açamazdık

    public TransactionBehavior(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        //                                ↑ next: zincirdeki bir sonraki halka (handler veya başka behavior)
        //                                  bunu yazmasaydık → handler hiç çalışmazdı
        CancellationToken ct)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        //               ↑ await using: transaction Dispose edilince otomatik rollback (commit edilmemişse)
        //                 using (non-async) yazarsak → sync Dispose, async context'te risk

        try
        {
            var response = await next();
            //             ↑ handler çalışıyor — tüm DB operasyonları bu satırda gerçekleşiyor
            //               bunu yazmasaydık → handler çalışmaz, hiçbir şey olmaz

            await tx.CommitAsync(ct);
            //  ↑ handler başarıyla bitti → kalıcı hale getir
            //    bunu yazmasaydık → using bloğu biterken rollback yapılır, handler etkisi sıfırlanır

            return response;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            //  ↑ herhangi bir hata → tüm değişiklikleri geri al
            //    bunu yazmasaydık → await using zaten rollback yapardı ama explicit rollback niyeti açık gösterir
            throw;
            //    ↑ exception'ı tekrar fırlat — sessizce yutma
            //      bunu yazmasaydık → hata kaybolur, caller başarılı sanır
        }
    }
}
```

**Sadece Command'lere uygulamak için:**

```csharp
// Marker interface — sadece Command'ler uygular
public interface ITransactionalCommand { }

// Command:
public record KitapEkleCommand(string Baslik, ...) : IRequest<int>, ITransactionalCommand;
//                                                    ↑ bu interface varsa behavior devreye girer

// Behavior'da constraint:
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ITransactionalCommand, IRequest<TResponse>
//  ↑ sadece ITransactionalCommand uygulayan request'ler bu behavior'ı tetikler
//    bunu yazmasaydık → Query'ler de transaction'a girerdi, gereksiz overhead
```

---

### Caching Behavior

Query sonucunu cache'ler — handler çalışmadan önce cache'e bakar:

```csharp
// Domain/Interfaces/ICacheableQuery.cs
public interface ICacheableQuery
// ↑ marker interface: "bu query cache'lenebilir" demek
//   bunu yazmasaydık → her query cache'lenirdi veya hiçbiri
{
    string CacheKey { get; }
    // ↑ cache anahtarı: "KitapListesi" gibi benzersiz
    //   bunu yazmasaydık → tüm query'ler aynı anahtarı paylaşır, yanlış data döner

    TimeSpan TTL { get; }
    // ↑ ne kadar süre cache'de kalacak — query kendisi karar veriyor
    //   bunu yazmasaydık → global TTL olmak zorunda kalırdı, query'ye göre ayarlanamazdı
}
```

```csharp
// Application/Behaviors/CachingBehavior.cs
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICacheableQuery, IRequest<TResponse>
//  ↑ sadece ICacheableQuery uygulayan request'ler — Command'ler bypass edilir
//    bunu yazmasaydık → Command'ler de cache'lenmeye çalışılırdı, yazma operasyonu yanlış data döner
{
    private readonly IMemoryCache _cache;
    //               ↑ in-memory cache — aynı process içinde hızlı erişim
    //                 bunu yazmasaydık → her request DB'ye giderdi, N+1 ve yavaş okuma

    public CachingBehavior(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var key = ((ICacheableQuery)request).CacheKey;
        //         ↑ cast: TRequest'in ICacheableQuery olduğunu biliyoruz (where constraint)
        //           bunu yazmasaydık → key'e erişemezdik, generic tip CacheKey'i bilmiyor

        if (_cache.TryGetValue(key, out TResponse? cached))
        {
            return cached!;
            //     ↑ cache'de var → handler hiç çalışmıyor, DB'ye gitmiyoruz
            //       bunu yazmasaydık → her zaman next() çağrılırdı, cache boşa giderdi
        }

        var response = await next();
        //             ↑ cache'de yok → handler çalıştı, DB'den data geldi

        var ttl = ((ICacheableQuery)request).TTL;
        _cache.Set(key, response, ttl);
        //          ↑ sonucu cache'e koy — sonraki çağrı DB'ye gitmez
        //            bunu yazmasaydık → her çağrı DB'ye giderdi, caching çalışmazdı

        return response;
    }
}
```

**Kullanım:**

```csharp
// Query ICacheableQuery uygular — kendi cache ayarını taşır
public record KitapListeleQuery() : IRequest<IReadOnlyList<KitapDto>>, ICacheableQuery
{
    public string CacheKey => "KitapListesi";
    //             ↑ sabit key — tüm kullanıcılar için aynı liste
    //               bunu yazmasaydık → her request farklı key üretirse cache hiç kullanılmaz

    public TimeSpan TTL => TimeSpan.FromMinutes(5);
    //               ↑ 5 dakika boyunca DB'ye gitme
    //                 bunu yazmasaydık → default TTL olur veya hiç expire olmaz, stale data döner
}
```

---

### Pipeline Sırası Önemlidir

```csharp
// Program.cs — behavior registration sırası = çalışma sırası
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
//               ↑ en dışta — her şeyi loglar
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
//               ↑ ortada — validation başarısız olursa handler ve transaction çalışmaz
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
//               ↑ en içte — sadece validation geçen command'ler transaction'a girer
```

```
Request gelir:
LoggingBehavior → "başladı"
  └→ ValidationBehavior → geçerli mi?
       └→ TransactionBehavior → transaction aç
            └→ Handler → asıl iş
       ↑← TransactionBehavior → commit
  ↑← ValidationBehavior
↑← LoggingBehavior → "tamamlandı: 43ms"
```

---

### 500 vs 50k — Pipeline Behaviors

| Konu | 500 | 50k |
|---|---|---|
| **Logging** | Handler içinde yeterli | ✅ Behavior — tüm handler'lar otomatik loglanır |
| **Validation** | Controller'da yeterli | ✅ Behavior — Domain validation + Use case validation ayrışır |
| **Transaction** | Handler'da manuel | ✅ Behavior — her command'de tekrar yazmak yok |
| **Caching** | Gereksiz karmaşıklık | ✅ Behavior — okuma yoğunsa DB baskısı azalır |
| **Behavior sayısı** | 1-2 yeterli | ⚠️ Çok behavior = anlaması güç pipeline |

---

## Gün 64 — Result Pattern ve Hata Yönetimi

### Sorun: Exception ile Kontrol Akışı

```csharp
// Faz2 — beklenen durumu exception ile yönetmek
public async Task<IActionResult> BulById(int id)
{
    try
    {
        var kitap = await _kitapServisi.BulByIdAsync(id);
        return Ok(kitap);
    }
    catch (NotFoundException ex) // ← "bulunamadı" beklenen bir durum — exception değil
    {
        return NotFound(ex.Message);
    }
    catch (ValidationException ex) // ← başka beklenen durum
    {
        return BadRequest(ex.Message);
    }
    // try/catch her action'da tekrar — boilerplate
}
```

**Sorunlar:**
- Exception pahalı: stack trace oluşturma, unwinding
- "Bulunamadı" beklenen bir durum — exception programlama hatası için vardır
- Caller'ı try/catch yazmaya zorlar — kontrol akışı gizli

---

### Result Pattern

Başarı ve başarısızlık açık, exception yok:

```csharp
// Application/Common/Result.cs
namespace KitabeviMediatr.Application.Common;

public class Result
// ↑ base class — T olmayan versiyon (void command'ler için)
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    //           ↑ computed property — IsSuccess'in tersi, ayrı field tutmaya gerek yok
    //             bunu yazmasaydık → caller !result.IsSuccess yazardı, daha az okunabilir

    public string Error { get; }
    //            ↑ başarısızlık mesajı — IsSuccess true ise boş olmalı
    //              bunu yazmasaydık → hata mesajına nasıl erişeceğimizi bilmezdik

    protected Result(bool isSuccess, string error)
    {
        if (isSuccess && !string.IsNullOrEmpty(error))
            throw new InvalidOperationException("Başarılı Result hata mesajı taşıyamaz");
        //  ↑ invariant: başarı + hata mesajı tutarsız
        //    bunu yazmasaydık → Result.Success("hata var") denilebilirdi

        if (!isSuccess && string.IsNullOrEmpty(error))
            throw new InvalidOperationException("Başarısız Result hata mesajı taşımalı");
        //  ↑ invariant: başarısızlık + boş mesaj bilgi vermez
        //    bunu yazmasaydık → Result.Failure("") denilebilirdi

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success()
        => new(true, string.Empty);
    //         ↑ factory method: new Result(true, "") ile aynı ama daha okunabilir
    //           bunu yazmasaydık → caller new Result(true, "") yazardı, ne anlama geldiği belirsiz

    public static Result Failure(string error)
        => new(false, error);
}

public class Result<T> : Result
// ↑ generic: başarı durumunda değer taşır
//   bunu yazmasaydık → Value'ya erişmek için ayrı bir mekanizma gerekirdi
{
    public T? Value { get; }
    //       ↑ nullable: başarısız Result'ta Value null
    //         bunu yazmasaydık → başarısız Result'tan Value alınmaya çalışılırsa ne olur?

    private Result(bool isSuccess, T? value, string error) : base(isSuccess, error)
    {
        Value = value;
    }

    public static Result<T> Success(T value)
        => new(true, value, string.Empty);

    public static Result<T> Failure(string error)
        => new(false, default, error);
    //                  ↑ default(T): T tipi için varsayılan değer (null, 0, false)
    //                    bunu yazmasaydık → T için new() constraint gerekirdi (tüm tipler new() desteklemez)
}
```

---

### Handler'da Result Pattern

```csharp
// Faz2 — exception fırlatıyor
public class KitapBulHandler
{
    public async Task<KitapDto> Handle(KitapBulQuery query, CancellationToken ct)
    {
        var kitap = await _repo.BulByIdAsync(query.Id, ct);
        if (kitap is null)
            throw new NotFoundException($"Kitap bulunamadı: {query.Id}");
        //  ↑ caller try/catch yazmak zorunda — kontrol akışı exception
        return new KitapDto(...);
    }
}
```

```csharp
// Faz3 — Result Pattern
public class KitapBulHandler : IRequestHandler<KitapBulQuery, Result<KitapDto>>
//                                              ↑ dönüş tipi Result<T> — "başarısız olabilir" açık sözleşme
//                                                bunu yazmasaydık → caller try/catch zorunda, sözleşme belirsiz
{
    private readonly IKitapRepository _repo;

    public KitapBulHandler(IKitapRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<KitapDto>> Handle(KitapBulQuery query, CancellationToken ct)
    {
        var kitap = await _repo.BulByIdAsync(query.Id, ct);

        if (kitap is null)
            return Result<KitapDto>.Failure($"Kitap bulunamadı: {query.Id}");
        //  ↑ exception değil Result — caller if (result.IsFailure) yazıyor
        //    bunu yazmasaydık → exception fırlatmak zorunda kalırdık, kontrol akışı gizlenir

        var dto = new KitapDto(kitap.Id, kitap.Baslik, kitap.Isbn.Deger, kitap.Fiyat.Deger);
        return Result<KitapDto>.Success(dto);
        //     ↑ değer ile birlikte başarı — caller result.Value kullanır
        //       bunu yazmasaydık → başarı durumunda nasıl değer döndüreceğimizi bilmezdik
    }
}
```

---

### Controller'da Result Kullanımı

```csharp
// Controller — Result'a göre HTTP response seç
[HttpGet("{id}")]
public async Task<IActionResult> BulById(int id, CancellationToken ct)
{
    var result = await _mediator.Send(new KitapBulQuery(id), ct);
    //           ↑ Result<KitapDto> dönüyor — try/catch yok

    if (result.IsFailure)
        return NotFound(new { hata = result.Error });
    //          ↑ IsFailure açık kontrol — Exception try/catch değil
    //            bunu yazmasaydık → result.Value null'a erişmeye çalışırdık, NullReferenceException

    return Ok(result.Value);
    //         ↑ IsSuccess garantili — Value null olamaz
    //           bunu yazmasaydık → null kontrolü ayrıca yapmak zorunda kalırdık
}
```

---

### Ne Zaman Exception, Ne Zaman Result?

```csharp
// Exception: programlama hatası, beklenmeyen durum
public class Kitap
{
    public void StokAzalt(int adet)
    {
        if (adet <= 0)
            throw new DomainException("Adet sıfırdan büyük olmalı");
        //  ↑ bu çağrı yapılmamalıydı — programcı hatası, exception uygun
        //    caller'ın bunu Result olarak yakalaması mantıklı değil
    }
}

// Result: beklenen başarısızlık, iş akışının parçası
public async Task<Result<KitapDto>> Handle(KitapBulQuery query, CancellationToken ct)
{
    var kitap = await _repo.BulByIdAsync(query.Id, ct);
    if (kitap is null)
        return Result<KitapDto>.Failure("Kitap bulunamadı");
    //  ↑ "bulunamadı" beklenen durum — arama her zaman başarılı olmaz
    //    exception olsaydı → catch (NotFoundException) yazmak zorunda kalırdık, verbosity artar
}
```

| Durum | Exception | Result |
|---|---|---|
| Domain invariant ihlali (stok negatif) | ✅ | ❌ |
| Programlama hatası (null arg) | ✅ | ❌ |
| Kayıt bulunamadı | ❌ | ✅ |
| Yetki hatası | ❌ | ✅ |
| Validation hatası (use case) | ❌ | ✅ |

---

### Railway Oriented Programming

Sonuçlar "başarı rayı" ve "hata rayı" olarak akar — hata rayına düşen adımlar atlanır:

```csharp
// Zincirleme Result operasyonları
public async Task<Result<SiparisOlusturResult>> Handle(SiparisOlusturCommand cmd, CancellationToken ct)
{
    // Adım 1: Kitap var mı?
    var kitapResult = await _kitapService.BulAsync(cmd.KitapId, ct);
    if (kitapResult.IsFailure)
        return Result<SiparisOlusturResult>.Failure(kitapResult.Error);
    //  ↑ hata rayına düştü — sonraki adımlar çalışmaz
    //    bunu yazmasaydık → null kitap ile devam eder, başka bir yerde crash olurdu

    var kitap = kitapResult.Value!;

    // Adım 2: Stok yeterli mi? (Domain kural)
    if (!kitap.StokVarMi())
        return Result<SiparisOlusturResult>.Failure("Stok yok");
    //  ↑ hata rayı — ödeme adımına gitmez

    // Adım 3: Ödeme al
    var odemeResult = await _odemeService.OdemeAlAsync(cmd.KullaniciId, kitap.Fiyat, ct);
    if (odemeResult.IsFailure)
        return Result<SiparisOlusturResult>.Failure(odemeResult.Error);
    //  ↑ ödeme başarısızsa sipariş oluşturma adımına gitmez

    // Adım 4: Sipariş oluştur
    kitap.StokAzalt(cmd.Adet);
    var siparis = new Siparis(cmd.KullaniciId);
    siparis.KalemEkle(kitap.Id, kitap.Baslik, kitap.Fiyat, cmd.Adet);
    await _siparisRepo.EkleAsync(siparis, ct);

    return Result<SiparisOlusturResult>.Success(new SiparisOlusturResult(siparis.Id));
    //     ↑ başarı rayı — tüm adımlar geçildi
}
```

**FluentResults ile aynı şey:**

```csharp
// NuGet: FluentResults — hazır implementasyon
using FluentResults;

public async Task<Result<int>> Handle(KitapEkleCommand cmd, CancellationToken ct)
{
    if (await _repo.IsbnMevcutMu(new Isbn(cmd.Isbn), ct))
        return Result.Fail<int>("Bu ISBN zaten kayıtlı");
    //         ↑ FluentResults.Result.Fail — kendi Result<T> yazmaktan kurtulursun
    //           bunu yazmasaydık → elle Result<T> class yazardık (biz de yazdık, kavramak için iyi)

    var kitap = new Kitap(cmd.Baslik, new Isbn(cmd.Isbn), new Fiyat(cmd.Fiyat), cmd.Stok);
    await _repo.EkleAsync(kitap, ct);
    await _repo.KaydetAsync(ct);

    return Result.Ok(kitap.Id);
    //     ↑ FluentResults.Result.Ok — başarı
}
```

---

### 500 vs 50k — Result Pattern

| Konu | 500 | 50k |
|---|---|---|
| **Exception ile kontrol** | ✅ Küçük projede tolere edilir | ❌ 50 endpoint → 50 try/catch boilerplate |
| **Result Pattern** | ⚠️ Overhead görünür | ✅ Hata yönetimi standart, tutarlı |
| **FluentResults paketi** | ⚠️ Öğrenme maliyeti | ✅ Hazır implementasyon, test desteği |
| **Railway Oriented** | ❌ Overkill | ✅ Zincirleme operasyonlar temiz |

---

## Gün 65 — Hafta 9 Özet: Mimari Sentez

Gün 59-64 arası işlediğimiz tüm mimari kararların büyük resmini görüyoruz.

---

### Mimari Soru 1: Sipariş oluşturma Onion'da nasıl tasarlanır?

```
Request: POST /api/siparisler
    ↓
[Presentation] SiparisController
    ├─ HTTP → Command dönüşümü
    └─ _mediator.Send(SiparisOlusturCommand)
         ↓
[Application] SiparisOlusturHandler
    ├─ Domain kurallarını çağırır (Kitap.StokAzalt)
    ├─ Siparis aggregate oluşturur
    ├─ ISiparisRepository.EkleAsync (interface — DB bilmiyor)
    └─ IEmailService.GonderAsync (interface — SMTP bilmiyor)
         ↓
[Domain] Siparis, Kitap, SiparisDurumu
    ├─ İş kuralları burada (stok kontrolü, durum geçişi)
    └─ ISiparisRepository interface tanımı
         ↑ implement eder
[Infrastructure] SiparisRepository (EF Core)
    └─ AppDbContext.SaveChangesAsync
```

**Katman sorumlulukları:**

```csharp
// [Domain] — Siparis.cs: sadece iş kuralları, hiçbir dış bağımlılık
public class Siparis
{
    public void Onayla()
    {
        if (!_kalemler.Any()) throw new DomainException("Boş sipariş onaylanamaz");
        Durum = SiparisDurumu.Onaylandi;
        // email yok, repo yok, http yok — sadece kural
    }
}

// [Application] — SiparisOlusturHandler.cs: orkestrasyon
public class SiparisOlusturHandler
{
    // interface bağımlılıkları — concrete class yok
    private readonly ISiparisRepository _repo;  // Domain interface
    private readonly IEmailService _email;       // Application interface

    public async Task<Result<int>> Handle(SiparisOlusturCommand cmd, CancellationToken ct)
    {
        // Domain'i kullan, Infrastructure'ı bilme
    }
}

// [Infrastructure] — SiparisRepository.cs: EF Core detayı
public class SiparisRepository : ISiparisRepository
{
    private readonly AppDbContext _context; // EF Core sadece burada
}

// [Presentation] — SiparisController.cs: HTTP detayı
public class SiparisController : ControllerBase
{
    private readonly IMediator _mediator; // tek bağımlılık
    public async Task<IActionResult> Olustur([FromBody] SiparisOlusturRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new SiparisOlusturCommand(req.KullaniciId, req.KitapId, req.Adet), ct);
        return result.IsFailure ? BadRequest(result.Error) : CreatedAtAction(...);
    }
}
```

---

### Mimari Soru 2: CQRS olmadan büyük projede ne olur?

```csharp
// CQRS olmadan — tek servis her şeyi yapıyor
public class KitapServisi
{
    // Okuma metodları
    public Task<List<Kitap>> TumunuGetirAsync() { ... }
    public Task<Kitap?> BulByIdAsync(int id) { ... }
    public Task<List<Kitap>> YazaraGoreGetirAsync(int yazarId) { ... }
    public Task<List<Kitap>> KategoriFiltreleAsync(string kategori, decimal maxFiyat) { ... }

    // Yazma metodları
    public Task<int> EkleAsync(KitapEkleDto dto) { ... }
    public Task GuncelleAsync(int id, KitapGuncelleDto dto) { ... }
    public Task SilAsync(int id) { ... }
    public Task StokGuncelleAsync(int id, int yeniStok) { ... }
    public Task FiyatGuncelleAsync(int id, decimal yeniFiyat) { ... }

    // 50 metot → tek servis → test zorlaşıyor, değişiklik riski artıyor
}
```

```csharp
// CQRS ile — her use case kendi handler'ında
// Okuma:
public class KitapListeleHandler { ... }
public class KitapBulHandler { ... }
public class YazaraGoreKitapListeleHandler { ... }

// Yazma:
public class KitapEkleHandler { ... }
public class KitapSilHandler { ... }
public class StokGuncelleHandler { ... }

// Her handler:
// ✅ tek sorumluluğu var
// ✅ bağımsız test edilebilir
// ✅ yeni handler = Controller değişmiyor (mediator.Send)
// ✅ okuma modeli optimize edilebilir (JOIN, flat DTO, cache)
```

---

### Mimari Soru 3: Pipeline Behavior vs Decorator — fark nedir?

```csharp
// Decorator — compile-time, belirli bir tip için
public class CachingKitapRepository : IKitapRepository
// ↑ sadece IKitapRepository'yi dekore eder — başka interface'e uygulanamaz
{
    private readonly IKitapRepository _inner;
    public CachingKitapRepository(IKitapRepository inner) { _inner = inner; }

    public async Task<IReadOnlyList<Kitap>> TumunuGetirAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue("kitaplar", out var cached)) return cached!;
        var result = await _inner.TumunuGetirAsync(ct);
        _cache.Set("kitaplar", result, TimeSpan.FromMinutes(5));
        return result;
    }
    // diğer metodlar _inner'a delegate eder
}
```

```csharp
// Pipeline Behavior — runtime dispatch, tüm request'lere uygulanabilir
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICacheableQuery, IRequest<TResponse>
// ↑ ICacheableQuery uygulayan TÜM query'lere otomatik uygulanır
//   yeni query gelince Behavior değişmez — sadece ICacheableQuery uygula yeterli
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var key = ((ICacheableQuery)request).CacheKey;
        if (_cache.TryGetValue(key, out TResponse? cached)) return cached!;
        var response = await next();
        _cache.Set(key, response, ((ICacheableQuery)request).TTL);
        return response;
    }
}
```

| Özellik | Decorator | Pipeline Behavior |
|---|---|---|
| **Uygulama kapsamı** | Belirli interface | Tüm matching request'ler |
| **Yeni tip ekleme** | Yeni decorator class | Marker interface yeterli |
| **Compile-time** | ✅ Tip güvencesi | ❌ Runtime dispatch |
| **Test** | Unit test — mock _inner | Integration test — pipeline |
| **Ne zaman tercih** | Repository seviyesi (cache, log) | Cross-cutting concern (transaction, validation) |

---

### Tüm Mimari Kararlar — Özet Tablosu

| Karar | Nerede? | Neden? |
|---|---|---|
| **Entity iş kuralları** | Domain | Tek yer, test edilebilir, dağılmaz |
| **IRepository interface** | Domain | Application bağımlılığını tersine çevirir |
| **Repository implementation** | Infrastructure | EF Core detayı izole |
| **IEmailService interface** | Application | Application kullanıyor, Infrastructure implement ediyor |
| **Handler (Use Case)** | Application | Orkestrasyon — Domain + Infrastructure koordinasyonu |
| **DTO** | Application | Entity API'ye sızmaz, kontrat ayrı |
| **Mapper (entity→DTO)** | Application Handler | Presentation bilmez, Infrastructure bilmez |
| **Validation (invariant)** | Domain | Nesne hiç geçersiz duruma giremez |
| **Validation (use case)** | Application Behavior | "ISBN var mı?" — DB kontrolü gerektiriyor |
| **Validation (input format)** | Presentation | Boş alan, max uzunluk — HTTP seviyesi |
| **Transaction** | Pipeline Behavior | Her Command'de tekrar yazmamak için |
| **Cache** | Decorator veya Behavior | Repository seviyesi veya Query seviyesi |
| **HTTP → Command dönüşümü** | Controller | HTTP detayı Presentation'a ait |
| **DI Composition** | Program.cs | Tüm bağımlılıkları tek yerde wire et |

---

### 500 vs 50k — Hafta 9 Özet

| Mimari Karar | 500 | 50k |
|---|---|---|
| **Onion katmanları** | 2-3 katman yeterli | ✅ 4 katman — her biri ayrı ekip, ayrı test |
| **CQRS** | Servis katmanı yeterli | ✅ Handler sayısı arttıkça servis şişmesi önlenir |
| **MediatR** | Overkill | ✅ Tüm handler'ları tek bağımlılıkla çöz |
| **Pipeline Behaviors** | 1-2 behavior | ✅ Transaction, validation, cache — handler'lar temiz kalır |
| **Result Pattern** | Exception kabul edilir | ✅ 50 endpoint → tutarlı hata yönetimi |
| **Decorator** | Gereksiz soyutlama | ✅ Cache swap, test izolasyonu |
| **Domain Event** | Tek alıcıysa direkt çağır | ✅ Birden fazla alıcı gelince event bus şart |

---

### Hafta 9 Kapanış

```
Hafta 9'da öğrendiklerimiz:

Gün 59: N-Layer'ın neden kırıldığını gördük → bağımlılık yönü sorunu
Gün 60: Onion'ın 4 katmanını inceledik → Domain, Application, Infrastructure, Presentation
Gün 61: Dependency Rule'u içselleştirdik → bağımlılık daima içe akar
Gün 62: CQRS ile okuma/yazma ayrıştı → her use case kendi handler'ında
Gün 63: Pipeline Behaviors ile cross-cutting concern'ler handler'dan çıktı
Gün 64: Result Pattern ile hata yönetimi açık sözleşmeye kavuştu

Bir sonraki adım: Domain Events, Outbox Pattern, eventual consistency
```
