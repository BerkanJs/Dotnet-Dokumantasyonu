# Gün 64 — Result Pattern ve Hata Yönetimi

Gün 63'te pipeline ile ortak süreçleri merkezileştirdin.  
Bugün bir üst seviyeye çıkıyoruz: hata akışını da okunur ve tahmin edilebilir hale getirmek.

---

## Problem: Her Şey Exception Olunca Ne Olur?

Klasik yaklaşımda:
- beklenen iş hataları da exception olur
- kod akışını okumak zorlaşır
- hangi hatanın "iş kuralı", hangisinin "sistem arızası" olduğu karışır

Özellikle CQRS handler'larında bu durum hızla dağılır.

---

## Result Pattern Nedir?

`Result<T>`, bir işlemin sonucunu iki olasılıkla taşır:
- Başarılı: `Value`
- Başarısız: `Error`

Bu sayede metod imzasına bakınca "bu işlem başarısız olabilir" bilgisini açıkça görürsün.

```csharp
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }

    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(Error error) => new(false, default, error);
}

public sealed record Error(string Code, string Message);
```

---

## Exception mı Result mı?

Kısa karar kuralı:

- **Result kullan:** beklenen iş akış hataları  
  (ör. kitap bulunamadı, stok yetersiz, sipariş geçersiz durumda)
- **Exception kullan:** beklenmeyen teknik hatalar  
  (ör. DB bağlantısı koptu, dış servis timeout oldu, serialization patladı)

Bu ayrım oturursa loglama ve API hata cevabı çok daha tutarlı olur.

---

## Kitabevi Üzerinden Basit Örnek

`KitapSilCommand` için handler sonucu:

```csharp
public record KitapSilCommand(int KitapId) : IRequest<Result<Unit>>;

public class KitapSilHandler : IRequestHandler<KitapSilCommand, Result<Unit>>
{
    private readonly IKitapRepository _repo;

    public KitapSilHandler(IKitapRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<Unit>> Handle(KitapSilCommand request, CancellationToken ct)
    {
        var kitap = await _repo.GetByIdAsync(request.KitapId, ct);
        if (kitap is null)
            return Result<Unit>.Failure(new Error("Kitap.NotFound", "Kitap bulunamadi."));

        await _repo.DeleteAsync(kitap, ct);
        return Result<Unit>.Success(Unit.Value);
    }
}
```

Burada "kitap yok" beklenen bir durum. Exception fırlatmak yerine kontrollü başarısız sonuç dönüyoruz.

---

## Controller/API Katmanında Mapping

Result dönen handler'ı HTTP'ye çevirmek kolaylaşır:

```csharp
[HttpDelete("{id:int}")]
public async Task<IActionResult> Sil(int id, CancellationToken ct)
{
    var result = await _mediator.Send(new KitapSilCommand(id), ct);

    if (result.IsSuccess)
        return NoContent();

    return result.Error!.Code switch
    {
        "Kitap.NotFound" => NotFound(new { error = result.Error.Message }),
        _ => BadRequest(new { error = result.Error.Message })
    };
}
```

---

## FluentResults / OneOf Ne Zaman?

Elle `Result<T>` yazmak öğretici ve hafiftir.  
Daha zengin ihtiyaçta:

- `FluentResults` -> birden fazla hata, reason zinciri, daha zengin model
- `OneOf<T1, T2>` -> union tipi gibi kullanım, pattern matching rahatlığı

Başlangıç için kendi sade `Result<T>` tipin çoğu projede yeterlidir.

---

## Pipeline ile Birlikte Kullanım

Gün 63 ile bağlayalım:

- Validation behavior -> invalid request'i erken yakalar
- Handler -> iş kuralı sonucunu `Result<T>` ile döner
- Exception behavior / middleware -> teknik exception'ları standardize eder

Böylece:
- iş hataları = kontrollü ve beklenen
- teknik hatalar = merkezi yakalanan arızalar

---

## Sık Hatalar

- Her şeyi Result yapmak (teknik exception'ı gizlemek)
- Her şeyi exception yapmak (beklenen iş akışını "hata" gibi yönetmek)
- `Error.Code` standardı oluşturmamak (dağınık stringler)
- Controller'da aynı mapping kodunu kopyalamak (merkezileştirmemek)

Küçük bir `ErrorCodes` sınıfı veya statik sabit seti kullanmak düzeni korur.

```csharp
public static class ErrorCodes
{
    public const string KitapNotFound = "Kitap.NotFound";
    public const string StokYetersiz = "Siparis.StokYetersiz";
}
```

---

## 500 vs 50K Kullanıcı

| Konu | 500 kullanıcı | 50K kullanıcı |
|---|---|---|
| Result pattern | Önerilir | Neredeyse zorunlu |
| Error code standardı | Basit tutulabilir | Kesinlikle standart olmalı |
| Exception politikası | Middleware yeterli olabilir | Katman bazlı net strateji gerekir |
| Hata gözlemlenebilirliği | Temel log yeterli | Trace + structured log + hata kataloğu gerekir |

---

## Mini Özet

Result Pattern, "beklenen iş başarısızlıklarını" exception'dan ayırır.  
Bu ayrım sayesinde CQRS handler'ları daha okunur olur, API hata cevapları daha tutarlı hale gelir.

---

## Kontrol Soruları

1. "Kitap bulunamadı" neden exception yerine `Result.Failure` olabilir?
2. Hangi durumda exception fırlatmak daha doğru olur?
3. `Error.Code` standardı olmazsa ekipte ne tür sorunlar çıkar?
4. Result mapping'i controller'da mı middleware'de mi toplamak daha doğru, neden?
