using System.ComponentModel.DataAnnotations;

namespace KitabeviMVC.Endpoints;

// Gün 23: EndpointFilter — Minimal API'nin Action Filter karşılığı.
//
// Controller'larda [ApiController] ModelState'i otomatik doğrular.
// Minimal API'de bu yoktur — bu filter o boşluğu kapatır.
//
// Endpoint pipeline'ına .AddEndpointFilter<ValidationEndpointFilter>() ile eklenir.
public class ValidationEndpointFilter : IEndpointFilter
{
    // "InvokeAsync" → IEndpointFilter'ın tek metodu.
    // "context.Arguments" → endpoint lambda/metoduna gelen tüm parametreler.
    // "next" → bir sonraki filter veya endpoint'in kendisi.
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // Her parametreyi tara — class tipinde olanları DataAnnotation ile doğrula.
        // int, string gibi primitive'leri atla — bunlarda DataAnnotation olmaz.
        foreach (var arg in context.Arguments)
        {
            if (arg is null) continue;

            var tip = arg.GetType();
            if (tip.IsPrimitive || tip == typeof(string)) continue;

            // "ValidationContext" → DataAnnotation validator'ın çalışması için gereken bağlam.
            // "validateAllProperties: true" → tüm property'leri kontrol et (sadece Required değil).
            var validasyonBaglami = new ValidationContext(arg);
            var validasyonSonuclari = new List<ValidationResult>();

            var gecerli = Validator.TryValidateObject(
                arg,
                validasyonBaglami,
                validasyonSonuclari,
                validateAllProperties: true);

            if (!gecerli)
            {
                // Hataları "alan adı → hata mesajı" sözlüğüne çevir.
                // "MemberNames.FirstOrDefault()" → hangi property'de hata var?
                // "?? "genel"" → alan adı yoksa "genel" key kullan.
                var hatalar = validasyonSonuclari.ToDictionary(
                    v => v.MemberNames.FirstOrDefault() ?? "genel",
                    v => new[] { v.ErrorMessage ?? "Geçersiz değer" });

                // "Results.ValidationProblem" → 400 + RFC 9457 formatında hata.
                // Controller'daki [ApiController]'ın ürettiğiyle aynı format.
                return Results.ValidationProblem(hatalar);
            }
        }

        // Tüm parametreler geçerli — bir sonraki adıma geç.
        return await next(context);
    }
}
