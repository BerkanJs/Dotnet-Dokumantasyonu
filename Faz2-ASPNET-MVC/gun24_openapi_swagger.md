# Gün 24 — OpenAPI / Swagger ve API Dokümantasyonu

---

## 1. OpenAPI Nedir? Neden Var?

API yazdın, başkası kullanacak. Kullanıcı şunu bilmek ister:

- Hangi endpoint'ler var?
- Her endpoint ne parametresi alıyor?
- Başarılıda ne döner, hata durumunda ne döner?
- Body nasıl formatlanmalı?

Bunu Word belgesiyle anlatmak sürdürülemez — kod değişir, belge eski kalır. **OpenAPI**, bu bilgiyi makine tarafından okunabilir bir formatta (JSON veya YAML) tanımlar. API'nin kendisiyle birlikte güncellenir.

**OpenAPI spec 3.x** — bir standart. Swagger ise bu standardı hayata geçiren araç ailesi.

```
OpenAPI → standart (kural kitabı)
Swagger → araç (UI, kod üretici, doküman üretici)
```

Spring Boot'ta `springdoc-openapi` veya `springfox` kullanıyordun. .NET ekosisteminde birden fazla seçenek var.

---

## 2. Üç Yaklaşım — Swashbuckle, NSwag, .NET 9 Built-in

**.NET 9 Built-in OpenAPI** (Microsoft.AspNetCore.OpenApi):
- .NET 9 ile birlikte geliyor, harici paket gerekmez
- Spec üretir ama UI sunmaz — Scalar veya Swagger UI ayrıca eklenir
- Lightweight, resmi destek

**Swashbuckle** (en yaygın, eski standart):
- `Swashbuckle.AspNetCore` paketi
- Hem spec üretir hem Swagger UI sunar
- `[ProducesResponseType]`, XML comment desteği
- .NET 9'da bazı uyumsuzluklar çıkmaya başladı

**NSwag:**
- Hem sunucu (spec üretme) hem istemci (C#/TypeScript SDK üretme)
- Swashbuckle'dan daha fazla özellik, daha karmaşık yapılandırma

**Scalar:**
- Modern Swagger UI alternatifi — daha temiz UI, daha iyi UX
- .NET 9 built-in OpenAPI ile iyi çalışır
- Swagger UI'nin yerini almaya başlıyor

Bu günde **.NET 9 built-in + Scalar** kombinasyonunu kullanacağız — modern ve hafif.

---

## 3. [ProducesResponseType] — Hangi Durum Kodu, Hangi Model?

Controller'da hangi HTTP kodu ve hangi body tipinin döneceğini OpenAPI'ye bildirmek için `[ProducesResponseType]` kullanılır.

```csharp
// Her olası dönüş senaryosu için bir attribute.
// "typeof(KitapResponse)" → 200'de body bu tip.
// "typeof(void)" veya yazılmaz → body yok (204 gibi).
[HttpGet("{id:int}")]
[ProducesResponseType(typeof(KitapResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public ActionResult<KitapResponse> Detay(int id) { ... }

[HttpPost]
[ProducesResponseType(typeof(KitapResponse), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public ActionResult<KitapResponse> Olustur([FromBody] KitapOlusturRequest request) { ... }

[HttpDelete("{id:int}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public IActionResult Sil(int id) { ... }
```

**.NET 8+ shorthand** — `[ProducesResponseType<T>]` generic versiyon:

```csharp
// Tip parametresi ile — typeof() yazmana gerek yok
[ProducesResponseType<KitapResponse>(StatusCodes.Status200OK)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public ActionResult<KitapResponse> Detay(int id) { ... }
```

---

## 4. XML Comment — Dokümantasyon Açıklamaları

Attribute'lara ek olarak XML comment'ler Swagger UI'da görünen açıklamaları üretir:

```csharp
/// <summary>
/// Belirtilen ID'ye sahip kitabı getirir.
/// </summary>
/// <param name="id">Kitabın veritabanı ID'si.</param>
/// <returns>Kitap detayları.</returns>
/// <response code="200">Kitap bulundu ve döndürüldü.</response>
/// <response code="404">Belirtilen ID'de kitap bulunamadı.</response>
[HttpGet("{id:int}")]
[ProducesResponseType<KitapResponse>(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public ActionResult<KitapResponse> Detay(int id) { ... }
```

XML comment'lerin Swagger'a taşınması için `.csproj`'da XML çıktısı etkinleştirilmeli:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <!-- Yorum yapılmamış public member için uyarı — isteğe bağlı -->
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

---

## 5. .NET 9 Built-in + Scalar Kurulumu

```csharp
// Program.cs

// OpenAPI spec üretimini aç — /openapi/v1.json endpoint'ini aktive eder
builder.Services.AddOpenApi();
```

```csharp
// app pipeline'ında — sadece development'ta aç
if (app.Environment.IsDevelopment())
{
    // /openapi/v1.json → raw JSON spec
    app.MapOpenApi();

    // Scalar UI → /scalar/v1 adresinde interaktif dokümantasyon
    app.MapScalarApiReference();
}
```

```
Tarayıcıda:
  /openapi/v1.json   → ham OpenAPI JSON
  /scalar/v1         → interaktif UI (Scalar)
```

---

## 6. OpenAPI Metadata — Controller ve Minimal API

**Controller'da** `[ProducesResponseType]` ile yapılır (Bölüm 3).

**Minimal API'de** `TypedResults` dönüş tipi Swagger'ı otomatik besler, ek olarak `.WithSummary()`, `.WithDescription()` ile zenginleştirilir:

```csharp
group.MapGet("/{id:int}", Detay)
     .WithSummary("Tek kitap getir")
     .WithDescription("Belirtilen ID'ye sahip kitabı döndürür. Bulunamazsa 404 döner.")
     .WithOpenApi(); // TypedResults → OpenAPI şemasına otomatik çevrilir
```

`TypedResults` kullandığında dönüş tipleri zaten belli — Swagger ekstra attribute gerektirmez:

```csharp
// Results<Ok<KitapResponse>, NotFound> → Swagger:
//   200: KitapResponse şeması
//   404: (body yok)
private static Results<Ok<KitapResponse>, NotFound> Detay(int id, IKitapServisi servis)
{ ... }
```

---

## 7. API Consumer için SDK Üretme

OpenAPI spec varsa istemci kodu üretebilirsin — TypeScript, C#, Python, vb.

**NSwag ile C# client:**

```bash
# spec dosyasından C# client üret
dotnet tool install -g NSwag.ConsoleX
nswag openapi2csclient /input:openapi.json /output:KitabeviClient.cs /namespace:KitabeviClient
```

**OpenAPI Generator ile TypeScript:**

```bash
npx @openapitools/openapi-generator-cli generate \
  -i http://localhost:5000/openapi/v1.json \
  -g typescript-fetch \
  -o ./src/api-client
```

Artık frontend ekibi backend'den bağımsız olarak üretilmiş tipler ve metodlarla çalışır. API değişince spec güncellenir, SDK yeniden üretilir.

---

## 8. Dikkat Edilmesi Gerekenler

**Sadece development'ta aç:** `/openapi/v1.json` production'da açık kalırsa API yapısı dışarıya görünür. `IsDevelopment()` kontrolü şart.

**`[ProducesResponseType]` gerçeği yansıtmalı:** Sadece dokümantasyon için yazmak yetersiz — gerçekte döndüğün kodlarla eşleşmeli. Aksi halde istemci yanıltılır.

**Swashbuckle .NET 9 uyumluluğu:** Swashbuckle'ın son sürümleri .NET 9 ile tam uyumlu değil. Yeni projeler için .NET 9 built-in + Scalar tercih edilmeli.

**XML comment bakımı:** Kod değiştiğinde XML comment'i güncellemeyi unutmak yaygın. `/// <summary>` yazmak zorunlu değil — sadece kritik endpoint'ler için yaz.

**Versioning + OpenAPI:** API versioning varsa her versiyon için ayrı spec üretilmeli:
```
/openapi/v1.json
/openapi/v2.json
```
`Asp.Versioning` paketi bu entegrasyonu destekler.

---

## 9. Kontrol Soruları

1. OpenAPI ve Swagger arasındaki fark nedir? Birbirinin yerine kullanılabilir mi?

2. `[ProducesResponseType]` neden önemlidir? Yazılmazsa ne olur?

3. `TypedResults` kullanan bir Minimal API endpoint'i için neden ayrıca `[ProducesResponseType]` yazmana gerek yok?

4. OpenAPI endpoint'ini (`/openapi/v1.json`) neden production'da kapatman gerekir?

5. Swashbuckle yerine .NET 9 built-in + Scalar tercih etmenin nedeni nedir?

6. API consumer SDK üretimi ne işe yarar? Frontend ekibi için ne değişir?
