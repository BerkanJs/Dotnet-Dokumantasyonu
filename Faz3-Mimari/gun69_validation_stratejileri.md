# Gün 69 — Validation Stratejileri

Bugünün amacı: kullanıcıdan gelen verinin nerede, nasıl ve hangi araçla doğrulanacağını anlamak — ve her katmandaki validation'ın farklı bir amaca hizmet ettiğini görmek.

---

## Gerçek Hayatta Bu Nerede Karşına Çıkar?

Kullanıcı bir kitap siparişi veriyor:

```
POST /api/siparisler
{
  "kitapId": -5,
  "adet": 0,
  "musteriAdi": ""
}
```

Bu isteği aldığında üç farklı katmanda doğrulamaya ihtiyaç var:

1. **Controller katmanı (ASP.NET):** Format doğru mu? Alan boş mu?
2. **Application katmanı (MediatR):** İş kuralları mantıklı mı? Adet 0 olamaz.
3. **Domain katmanı (Entity):** Bu nesne geçersiz bir state'e girebilir mi?

Her katman farklı bir soruyu cevaplar. Tek yerden validation yapmak bu soruların bir kısmını cevapsız bırakır.

---

## DataAnnotations — Ne Zaman Kullanırsın?

Controller'a gelen request DTO'larında hızlı format kontrolü için kullanılır.

```csharp
public class SiparisOlusturRequest
{
    [Required(ErrorMessage = "Kitap ID zorunlu")]
    // bunu yazmasaydık → null gelirse model state hata vermez, service'e ulaşır
    
    [Range(1, int.MaxValue, ErrorMessage = "Kitap ID pozitif olmalı")]
    // bunu yazmasaydık → -5 gelirse domain'e kadar iner, orada patlayabilir
    public int KitapId { get; set; }

    [Range(1, 100, ErrorMessage = "Adet 1-100 arası olmalı")]
    public int Adet { get; set; }

    [Required]
    [MaxLength(100, ErrorMessage = "Müşteri adı 100 karakteri geçemez")]
    // MaxLength yazmasaydık → DB kolonunda varchar(100) varsa DB exception fırlar
    public string MusteriAdi { get; set; } = string.Empty;
}
```

**Gerçek hayatta ne zaman?**  
→ Basit CRUD API'larda yeterlidir.  
→ Karmaşık iş kuralı yoksa ekstra kütüphane gerekmez.  
→ Format/null/uzunluk kontrolü için idealdir.

**Sınırı nedir?**  
→ "Kitap aktif mi?", "Stok yeterli mi?" gibi iş kurallarını buraya yazamazsın çünkü DB'ye bakman gerekir.

---

## FluentValidation — Ne Zaman Kullanırsın?

Karmaşık, koşullu, birden fazla alana bağlı validasyon kuralları için kullanılır.

```csharp
public class SiparisOlusturCommandValidator : AbstractValidator<SiparisOlusturCommand>
{
    public SiparisOlusturCommandValidator()
    {
        RuleFor(x => x.KitapId)
            .GreaterThan(0)
            .WithMessage("Kitap ID pozitif olmalı");
            // bunu yazmasaydık → -5 gelirse handler'a ulaşır

        RuleFor(x => x.Adet)
            .InclusiveBetween(1, 100)
            .WithMessage("Adet 1 ile 100 arasında olmalı");

        RuleFor(x => x.MusteriAdi)
            .NotEmpty()
            .MaximumLength(100)
            .Matches(@"^[a-zA-ZğüşöçıİĞÜŞÖÇ\s]+$")
            // Matches yazmasaydık → "DROP TABLE" gibi girdiler geçer, temiz değil
            .WithMessage("Müşteri adı yalnızca harf içermeli");

        // Koşullu kural — DataAnnotations'da bu imkansız:
        RuleFor(x => x.KargoAdresi)
            .NotEmpty()
            .When(x => x.KargoIsteniyor);
            // When olmadan her zaman zorunlu tutardık — lojik yanlış olurdu
    }
}
```

**Gerçek hayatta ne zaman?**  
→ Koşullu kurallar (A doluysa B zorunlu).  
→ Birden fazla alana bağlı kontrol (şifre = şifre tekrar).  
→ Test edilmesi gereken karmaşık validation mantığı.  
→ Hata mesajlarını merkezden yönetmek istiyorsan.

---

## MediatR Pipeline Behavior ile FluentValidation Entegrasyonu

Gerçek hayatta validation'ı her handler'a yazmak yerine MediatR pipeline'ına takarsın — tek kod, tüm handler'lar için çalışır.

```csharp
public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    // IEnumerable — birden fazla validator inject edilebilir, hepsini topla

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;               // DI container bunları otomatik çözer
                                                 // bunu yazmasaydık → validator'lar asla çalışmaz
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var context = new ValidationContext<TRequest>(request);

        var failures = _validators
            .Select(v => v.Validate(context))   // tüm validator'ları çalıştır
            .SelectMany(r => r.Errors)           // tüm hataları düzleştir
            .Where(f => f != null)               // null hataları ele
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);
        // Exception fırlatmak yerine Result döndürmek istersen → bir sonraki başlık

        return await next();                    // hata yoksa devam et — handler çalışır
        // next() olmasaydık → handler hiç çalışmaz
    }
}
```

**Kayıt (Program.cs):**

```csharp
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    // AddBehavior olmasaydık → ValidationBehavior DI'a kayıtlı ama MediatR bilmez
});

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
// bu satır olmasaydık → FluentValidation validator'ları DI'a inject edilmez
```

**Gerçek hayatta sonuç:**  
Artık her `IRequest` için otomatik validation çalışır. Yeni bir command yazarsan sadece validator sınıfı eklersin — pipeline kodu dokunulmaz.

---

## Application Layer vs Domain Layer Validation Farkı

Bu iki katman farklı soruları cevaplar:

| Soru | Katman | Örnek |
|---|---|---|
| "Kullanıcı doğru format mı gönderdi?" | Application / Controller | Adet null mı, negatif mi? |
| "Bu nesne geçersiz bir state'e girebilir mi?" | Domain Entity | Stok 0'ın altına düşebilir mi? |

**Application layer validation:**
```csharp
// Command'da gelen veri mantıklı mı?
RuleFor(x => x.Adet).GreaterThan(0);       // kullanıcı hatası — 422 Unprocessable Entity dön
```

**Domain layer validation (guard clause):**
```csharp
public class Kitap
{
    public int StokAdedi { get; private set; }  // private set — dışarıdan doğrudan değiştirilemez
                                                  // public set yazsaydık → domain kuralı bypass edilir

    public void StokuDus(int adet)
    {
        if (adet <= 0)
            throw new ArgumentException("Adet pozitif olmalı");
            // bunu yazmasaydık → negatif adet ile stok şişirilebilir

        if (StokAdedi < adet)
            throw new DomainException("Yetersiz stok");
            // bunu yazmasaydık → stok eksi değere düşer, veri tutarsız olur

        StokAdedi -= adet;                  // kural geçtikten sonra güvenle değiştir
    }
}
```

**Gerçek hayatta kural:**  
→ "Kullanıcı yanlış bir şey gönderdi" → Application validation (FluentValidation)  
→ "Bu nesnenin kendisi bu state'e giremez" → Domain validation (guard clause / throw)

---

## Guard Clauses — Ardalis.GuardClauses

Guard clause, "geçersiz girdide hemen dur" felsefesidir.

**Manuel guard (kütüphanesiz):**
```csharp
public Siparis(int kitapId, int adet, string musteriAdi)
{
    if (kitapId <= 0) throw new ArgumentException("KitapId pozitif olmalı");
    if (adet <= 0) throw new ArgumentException("Adet pozitif olmalı");
    if (string.IsNullOrWhiteSpace(musteriAdi)) throw new ArgumentException("Ad boş olamaz");

    KitapId = kitapId;
    Adet = adet;
    MusteriAdi = musteriAdi;
}
```

**Ardalis.GuardClauses ile:**
```csharp
using Ardalis.GuardClauses;

public Siparis(int kitapId, int adet, string musteriAdi)
{
    KitapId = Guard.Against.NegativeOrZero(kitapId, nameof(kitapId));
    // bunu yazmasaydık → -5 ile Siparis nesnesi oluşturulabilir, DB'de bozuk kayıt
    // manuel if yazsaydık → aynı şey ama daha verbose

    Adet = Guard.Against.NegativeOrZero(adet, nameof(adet));
    MusteriAdi = Guard.Against.NullOrWhiteSpace(musteriAdi, nameof(musteriAdi));
    // NullOrWhiteSpace yazmak manuel 3 satır if demek — Guard tek satıra indirir
}
```

**Gerçek hayatta ne zaman?**  
→ Domain entity constructor'larında.  
→ Factory method'larda (`Siparis.Olustur()`).  
→ "Bu nesne geçersiz state ile var olmasın" kuralı olan her yerde.

---

## Validation Sonucunu Nasıl Döndürürsün? Exception vs Result

İki yaklaşım var, her birinin yeri farklı:

**Exception fırlatmak:**
```csharp
// Domain entity içinde — nesne invariant'ı korunuyor
if (StokAdedi < adet)
    throw new DomainException("Yetersiz stok");
// Bunu Result döndürseydin → caller her zaman kontrol etmek zorunda
// Exception burada doğal — bu durum "olmamalıydı"
```

**Result döndürmek:**
```csharp
// Application handler içinde — beklenen iş akışı hatası
public async Task<Result<int>> Handle(SiparisOlusturCommand request, CancellationToken ct)
{
    var kitap = await _repo.GetByIdAsync(request.KitapId, ct);
    if (kitap is null)
        return Result.Failure<int>("Kitap bulunamadı");
        // Exception fırlatsaydık → her caller try/catch zorunda
        // Result dönersen → caller if ile kontrol eder, akış temiz kalır

    if (kitap.StokAdedi < request.Adet)
        return Result.Failure<int>("Yetersiz stok");
        // Bu bir domain hatası değil, beklenen bir iş senaryosu
        // Exception burada overreaction olur

    kitap.StokuDus(request.Adet);
    // ... siparis oluştur ...
    await _uow.SaveChangesAsync(ct);
    return Result.Success(siparis.Id);
}
```

**Gerçek hayatta kural:**  
→ "Nesnenin bu state'e girmemesi gereken durum" → Exception (guard clause)  
→ "Beklenen bir iş senaryosu hatası (stok yok, kayıt bulunamadı)" → Result Pattern

---

## ASP.NET Validation Pipeline vs MediatR Validation

| Özellik | ASP.NET ModelState | MediatR ValidationBehavior |
|---|---|---|
| Çalıştığı yer | Controller'a girmeden önce | Handler'a girmeden önce |
| Ne doğrular? | HTTP request DTO formatı | Command/Query iş kuralları |
| Araç | DataAnnotations / FluentValidation | FluentValidation |
| Hata nasıl döner? | 400 Bad Request (otomatik) | ValidationException → middleware yakalar |
| Test edilebilirlik | Integration test gerekir | Unit test ile test edilir |

**Gerçek hayatta tipik katman:**

```
HTTP Request
    → [ASP.NET ModelState] Format/null kontrolü → 400 döner
    → Controller → MediatR.Send(command)
        → [ValidationBehavior] İş kuralı kontrolü → ValidationException
            → GlobalExceptionMiddleware → 422 döner
        → Handler çalışır
```

---

## Faz2 ile Karşılaştırma

```csharp
// Faz2 KitabeviMVC — validation controller içinde dağınık
public async Task<IActionResult> SiparisOlustur(SiparisViewModel vm)
{
    if (!ModelState.IsValid)          // sadece DataAnnotations
        return View(vm);

    if (vm.Adet <= 0)                 // iş kuralı controller'da — nerede durur belirsiz
        ModelState.AddModelError("Adet", "Pozitif olmalı");

    // Stok kontrolü yok — domain entity doğrulama yok
    // Guard clause yok — constructor geçersiz state kabul eder
}
```

Faz3'te her katman kendi sorusunu cevaplar, controller sadece routing yapar:

```
Controller → (ince, sadece route) → MediatR Command → ValidationBehavior → Handler → Domain
```

---

## 500 vs 50K Kullanıcı

| Konu | 500 kullanıcı/ay | 50K kullanıcı/ay |
|---|---|---|
| DataAnnotations yeterli mi? | Çoğu zaman evet | Karmaşık kurallarda FluentValidation şart |
| FluentValidation | Güzel ama opsiyonel | Merkezi validation — zorunlu gibi |
| MediatR ValidationBehavior | Overengineering olabilir | Her command otomatik validate — çok değerli |
| Guard clauses | Küçükte bile kullanmalısın | Domain bütünlüğü kritik |
| Result pattern | Opsiyonel | Exception'dan daha temiz — önerilir |

**Overengineering sinyali:** 3 endpoint olan bir API'da MediatR + FluentValidation + ValidationBehavior + Result pattern birlikte kurmak. DataAnnotations + basit if yeterlidir.

---

## Mini Özet

- **DataAnnotations** → format/null/uzunluk kontrolü, controller katmanı, hızlı başlangıç.
- **FluentValidation** → koşullu ve karmaşık iş kuralları, test edilebilir.
- **MediatR ValidationBehavior** → tüm command/query'ler için otomatik validation merkezi.
- **Guard clauses** → domain entity'nin geçersiz state'e girmesini önler.
- **Result vs Exception** → beklenen iş hatası → Result, nesne invariant ihlali → Exception.

---

## Kontrol Soruları

1. DataAnnotations ile "Müşteri VIP ise indirim kodu zorunlu" kuralını yazabilir misin? Neden değil?
2. MediatR ValidationBehavior olmadan her handler'a validation eklemek ne soruna yol açar?
3. Guard clause ile Application layer validation arasındaki fark nedir?
4. "Kitap bulunamadı" durumunda Result mi döndürürsün, Exception mi fırlatırsın? Neden?
