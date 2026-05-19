# Gün 63 — MediatR: Pipeline Behaviors

CQRS'te command ve query'leri ayırdın. Şimdi sıradaki kritik adım: her handler'a tekrar tekrar yazdığın ortak işleri tek yerde toplamak.

---

## Pipeline Behavior Nedir?

`IPipelineBehavior<TRequest, TResponse>` MediatR'da request ile handler arasına giren bir ara katmandır.

Bunu günlük hayattan şöyle düşünebilirsin: bir hastanede doktora gitmeden önce kayıt, kimlik kontrolü, ölçüm, ödeme gibi adımlar var. Doktor (handler) sadece muayeneyi yapar; geri kalan ortak adımlar "akışın etrafında" yürür.

```csharp
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // 1) Handler'dan önce çalışır
        Console.WriteLine($"Handling {typeof(TRequest).Name}");

        var response = await next();

        // 2) Handler'dan sonra çalışır
        Console.WriteLine($"Handled {typeof(TRequest).Name}");
        return response;
    }
}
```

Mantık basit: `next()` çağrılmadan önce "önce" kısmı, `next()` sonrası "sonra" kısmı çalışır.

Yani behavior, handler'ın "ön odası ve arka odası" gibidir.

---

## Neden Gerekli?

Behavior olmasa:
- Her handler'da log kodu
- Her handler'da validation kodu
- Her handler'da try/catch kodu
- Her handler'da transaction kodu

Behavior ile:
- Handler sadece use case mantığına odaklanır
- Cross-cutting concern'ler merkezi yönetilir
- Kod tekrarını ve hata riskini azaltırsın

Kısaca: aynı işi 20 handler'a 20 kere yazmak yerine bir kez yazıp her istekte otomatik çalıştırırsın.

---

## Günlük Hayat Benzetmesi

Bir e-ticaret sipariş akışını düşün:

- Müşteri sipariş verir (`SiparisOlusturCommand`)
- Sistem önce kuralları kontrol eder (stok, adres, ödeme bilgisi)
- Sonra siparişi kaydeder
- Sonra log yazar, gerekirse cache temizler

Bu adımların bir kısmı iş kuralı, bir kısmı "ortak süreç".  
MediatR Pipeline tam olarak bu ortak süreci yönetir.

---

## En Sık Kullanılan Behavior'lar

## 1) Logging Behavior

Request başladı/bitti, süre ne kadar sürdü, hangi handler çalıştı gibi bilgileri loglar.

```csharp
var sw = Stopwatch.StartNew();
var response = await next();
sw.Stop();
_logger.LogInformation("{Request} completed in {Elapsed}ms",
    typeof(TRequest).Name, sw.ElapsedMilliseconds);
```

Kitabevi için örnek log:

```csharp
_logger.LogInformation(
    "Request={RequestName} User={UserId}",
    typeof(TRequest).Name,
    _currentUser.UserId);
```

## 2) Validation Behavior (FluentValidation)

Handler çalışmadan önce request doğrulanır. Hatalı request handler'a hiç gitmez.

```csharp
var context = new ValidationContext<TRequest>(request);
var failures = (await Task.WhenAll(
    _validators.Select(v => v.ValidateAsync(context, cancellationToken))))
    .SelectMany(r => r.Errors)
    .Where(f => f is not null)
    .ToList();

if (failures.Count != 0)
    throw new ValidationException(failures);
```

`SiparisOlusturCommand` için kısa validator örneği:

```csharp
public class SiparisOlusturCommandValidator
    : AbstractValidator<SiparisOlusturCommand>
{
    public SiparisOlusturCommandValidator()
    {
        RuleFor(x => x.KullaniciId).NotEmpty();
        RuleFor(x => x.KitapId).GreaterThan(0);
        RuleFor(x => x.Adet).GreaterThan(0);
    }
}
```

## 3) Caching Behavior (Genelde Query için)

Sadece okunur query'lerde cache'e bakılır:
- cache hit -> handler çalışmaz, direkt döner
- cache miss -> handler çalışır, sonuç cache'e yazılır

Command tarafında genelde cache temizleme (invalidation) yapılır.

`KitapListeleQuery` için kısa cache anahtarı örneği:

```csharp
var cacheKey = $"kitaplar:liste:{request.Sayfa}:{request.SayfaBoyutu}";
var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
if (cached is not null)
    return JsonSerializer.Deserialize<TResponse>(cached)!;

var response = await next();
await _cache.SetStringAsync(
    cacheKey,
    JsonSerializer.Serialize(response),
    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
    cancellationToken);

return response;
```

## 4) Transaction Behavior (Genelde Command için)

Command'i bir transaction içine sarar:
- handler başarıyla biterse `Commit`
- hata olursa `Rollback`

Bu sayede bir use case içindeki çoklu DB değişikliği atomik kalır.

`SiparisOlusturCommand` için örnek transaction akışı:

```csharp
await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
try
{
    var response = await next();
    await tx.CommitAsync(cancellationToken);
    return response;
}
catch
{
    await tx.RollbackAsync(cancellationToken);
    throw;
}
```

## 5) Exception Handling Behavior

Domain/validation istisnalarını tek noktada yakalayıp standart hata modeline dönüştürmeye yardımcı olur.

Not: HTTP'ye dönüştürme çoğunlukla API katmanındaki global exception middleware'de yapılır.

Domain exception'ı standart hata modeline dönüştürme örneği:

```csharp
try
{
    return await next();
}
catch (DomainException ex)
{
    throw new ApplicationException($"DomainError: {ex.Message}", ex);
}
```

---

## Çalışma Sırası

Registration sırası önemlidir. Tipik akış:

1. Logging (başlangıç)
2. Validation
3. Transaction (command ise)
4. Handler
5. Logging (bitiş)

Sıra bozulursa davranış da bozulur.  
Örneğin validation transaction'dan sonra çalışırsa, boşuna transaction açmış olursun.

Örnek kayıt:

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
});
```

Kitabevi API'de tipik kullanım:

```csharp
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
});
```

---

## Kritik Uyarılar

- Handler içinde başka handler çağırma (`_mediator.Send(...)`) anti-pattern'e dönüşebilir.
- Behavior katmanına iş kuralı koyma; iş kuralı handler/domain'de kalmalı.
- Her query'yi cache'leme; düşük hit oranlı/çok değişen veride cache zararlı olabilir.
- Validation'ı hem controller'da hem behavior'da kör tekrar etme; sorumluluğu net ayır.

Ek not:
- "Her şeye behavior yazayım" yaklaşımı da antipattern olabilir; gerçekten ortak olan konuları behavior'a taşı.
- Handler içinde dış servis çağrıları çok fazlaysa timeout/retry stratejisini behavior değil, uygun katmanda (ör. HttpClient resilience) çöz.

Kısa kural:
- Query handler -> mümkünse `AsNoTracking()`
- Command handler -> tek use case, tek transaction sınırı
- Behavior -> teknik/ortak süreç; iş kuralı değil

---

## 500 vs 50K Kullanıcı

| Konu | 500 kullanıcı | 50K kullanıcı |
|---|---|---|
| Logging behavior | Basit ama faydalı | Zorunlu |
| Validation behavior | Önerilir | Zorunlu |
| Caching behavior | Seçili query'lerde | Yoğun query'lerde kritik |
| Transaction behavior | Komplex command'de | Zorunluya yakın |
| Exception strategy | Middleware yeterli | Standartlaştırılmış hata akışı şart |

---

## Mini Özet

MediatR Pipeline Behavior, CQRS mimarisinde "ortak işleri tek noktada toplama" aracıdır.
Handler sade kalır, mimari kurallar daha sürdürülebilir hale gelir.

En pratik bakış açısı:
- Handler = işin özü
- Behavior = işin etrafındaki standart prosedür

Bu ayrım oturduğunda kod hem daha okunur olur hem de büyüdükçe daha az dağılır.

---

## Kontrol Soruları

1. Validation neden handler içinde değil behavior içinde olmalı?
2. Caching behavior neden çoğunlukla query tarafında çalıştırılır?
3. Transaction behavior'ı query'lere uygulamak neden çoğu durumda gereksizdir?
4. Behavior sırası yanlış olursa ne tür üretim hataları çıkabilir?
