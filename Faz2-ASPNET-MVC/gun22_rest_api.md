# Gün 22 — REST API Tasarım Prensipleri

---

## 1. REST Nedir? Neden Var?

Web'in ilk dönemlerinde her API farklı kurallar kullanıyordu. Bir servis `getUser(id)` diyordu, bir diğeri `fetchUserData?userId=42` diyordu. Entegrasyon yazmak, her servis için ayrı mantık öğrenmek demekti.

**REST** (Representational State Transfer), 2000 yılında Roy Fielding'in doktora tezinde tanımladığı bir mimari stil. Kural değil — kısıtlamalar bütünü. Bu kısıtlamalara uyan servise **RESTful** denir.

Temel fikir: HTTP zaten güçlü bir protokol. URL'ler kaynakları tanımlar, HTTP metodları ne yapılacağını söyler, status code'lar sonucu bildirir. Bunları tutarlı kullanırsan başkası öğrenmeden anlayabilir.

Spring'de `@RestController` + `@GetMapping` ile REST servisleri yazdın. ASP.NET Core'da aynı fikir, farklı syntax — `[ApiController]` + `[HttpGet]`.

---

## 2. REST Kısıtlamaları

Fielding'in tanımladığı 6 kısıtlama:

**Stateless (Durumsuz):** Sunucu, istekler arasında istemci hakkında hiçbir şey hatırlamaz. Her istek kendi başına yeterli bilgiyi taşır. Token, API key — hepsi her istekte gönderilir.

```
❌ Sunucu: "bir önceki istekte Admin'din, hatırlıyorum"
✓ Sunucu: "bu istekte Authorization: Bearer ... var, okuyorum"
```

**Uniform Interface (Tekdüze Arayüz):** Tüm kaynaklar aynı arayüzle erişilir. URL + HTTP metod + status code kombinasyonu her yerde aynı anlama gelir.

**Client-Server:** İstemci ve sunucu birbirinden bağımsız gelişir. Frontend URL formatını bilir, sunucunun nasıl çalıştığını bilmez.

**Cacheable (Önbelleklenebilir):** Sunucu yanıtlar önbelleklenebilir mi değil mi söyler. GET yanıtları genellikle önbelleklenebilir, POST genellikle değil.

**Layered System:** İstemci arada kaç katman (proxy, load balancer, CDN) olduğunu bilmez. Son noktaya konuştuğunu sanır.

**Code on Demand (Opsiyonel):** Sunucu istemciye çalıştırılabilir kod gönderebilir (JavaScript gibi). Nadiren uygulanır.

Günlük pratikte en kritik ikisi: **Stateless** ve **Uniform Interface**.

---

## 3. Resource Modeling — Entity Değil Kaynak

REST'in en çok yanlış anlaşılan kısmı bu. URL'ler **eylem** değil **kaynak** temsil eder.

```
❌ Eylem odaklı (RPC tarzı):
GET  /getKitap?id=42
POST /kitapEkle
POST /kitapSil?id=42
POST /kitapGuncelle

✓ Kaynak odaklı (REST):
GET    /kitaplar/42
POST   /kitaplar
DELETE /kitaplar/42
PUT    /kitaplar/42
```

URL kaynağı tanımlar, HTTP metodu ne yapılacağını söyler. İkisi birleşince anlam tamamlanır.

**İç içe kaynaklar:**

```
GET /kitaplar/42/yorumlar       → 42 nolu kitabın tüm yorumları
GET /kitaplar/42/yorumlar/7    → 42 nolu kitabın 7 nolu yorumu
POST /kitaplar/42/yorumlar     → 42 nolu kitaba yorum ekle
```

**Eylem gereken durumlar:** Bazen saf kaynak odaklı yaklaşım zorlanır. "Siparişi iptal et" bir eylemdir. Bu durumda iki yaklaşım var:

```
// Seçenek 1: Alt kaynak gibi davran
POST /siparisler/99/iptal

// Seçenek 2: Durum güncellemesi olarak model
PATCH /siparisler/99
Body: { "durum": "iptal" }
```

İkisi de kabul görür — tutarlı olması önemli.

---

## 4. HTTP Method Semantikleri

Her metodun net bir anlamı var ve bu anlam **idempotency** (aynı isteği birden çok göndermek aynı sonucu verir mi?) ile ilgili:

| Metod  | Anlamı                    | Idempotent | Safe |
|--------|---------------------------|-----------|------|
| GET    | Kaynağı oku               | Evet      | Evet |
| POST   | Yeni kaynak oluştur       | Hayır     | Hayır |
| PUT    | Kaynağı tamamen güncelle  | Evet      | Hayır |
| PATCH  | Kaynağı kısmen güncelle   | Hayır*    | Hayır |
| DELETE | Kaynağı sil               | Evet      | Hayır |

**Safe:** Sunucu durumunu değiştirmez (GET okur, değiştirmez).
**Idempotent:** Aynı isteği 10 kez göndermek 1 kez göndermekle aynı sonucu verir.

`DELETE /kitaplar/42` → ilk çağrıda siler. İkinci çağrıda kaynak yok ama **sonuç** aynı: kaynak yok. Idempotent.

`POST /kitaplar` → her çağrıda yeni kayıt oluşur. İdempotent değil.

**PUT vs PATCH farkı:**

```json
// Mevcut kayıt:
{ "id": 42, "baslik": "Dune", "yazar": "Frank Herbert", "fiyat": 120 }

// PUT /kitaplar/42 → tüm alanları gönder, eksik alanlar null olur
{ "baslik": "Dune", "yazar": "Frank Herbert", "fiyat": 150 }

// PATCH /kitaplar/42 → sadece değişeni gönder
{ "fiyat": 150 }
```

---

## 5. Status Code Seçimi

Yanlış status code döndürmek API'yi yanıltıcı yapar. Frontend hata mı, başarı mı bilemez.

**2xx — Başarı:**

```
200 OK          → GET, PUT, PATCH başarılı. Body'de güncel kayıt var.
201 Created     → POST başarılı, yeni kayıt oluştu.
                  Location header'da yeni kaynağın URL'i olmalı:
                  Location: /kitaplar/43
204 No Content  → DELETE başarılı. Body yok — silinen şeyi döndürme.
```

**4xx — İstemci Hatası:**

```
400 Bad Request      → Geçersiz istek. Eksik alan, yanlış format.
401 Unauthorized     → Kimlik doğrulaması gerekli (giriş yapmamış).
                       İsim yanıltıcı — aslında "authentication gerekli".
403 Forbidden        → Kimlik doğrulandı ama yetki yok.
                       Giriş yapmış ama bu kaynağa erişemez.
404 Not Found        → Kaynak bulunamadı.
409 Conflict         → Çakışma. "Bu e-posta zaten kayıtlı."
422 Unprocessable    → Format doğru ama iş kuralı ihlali.
                       "Stok miktarı negatif olamaz."
```

**401 vs 403 farkı** sık karıştırılır:

```
401 → "Kim olduğunu bilmiyorum, kendini tanıt"   (giriş yapmamış)
403 → "Kim olduğunu biliyorum, ama giremezsin"   (yetkisiz)
```

**5xx — Sunucu Hatası:**

```
500 Internal Server Error → Beklenmedik hata. Stack trace'i asla döndürme.
503 Service Unavailable   → Servis geçici olarak kullanılamıyor (bakım, aşırı yük).
```

---

## 6. HATEOAS — Ne Zaman Gerekli?

HATEOAS (Hypermedia As The Engine Of Application State): API yanıtları içinde bir sonraki adımların linkleri de bulunur. İstemci URL'leri hard-code etmek zorunda kalmaz.

```json
{
  "id": 42,
  "baslik": "Dune",
  "fiyat": 120,
  "_links": {
    "self":     { "href": "/kitaplar/42" },
    "duzenle":  { "href": "/kitaplar/42", "method": "PUT" },
    "sil":      { "href": "/kitaplar/42", "method": "DELETE" },
    "yorumlar": { "href": "/kitaplar/42/yorumlar" }
  }
}
```

**Ne zaman gerekli?** Teorik olarak "true REST" için şart. Pratikte çok az proje uygular çünkü karmaşıklığı artırır, istemciler genellikle bunu kullanmaz.

**Gerçek dünya:** GitHub API HATEOAS kullanır. Çoğu kurumsal API kullanmaz. "REST level 3" olarak da bilinir. Ekip anlaşmışsa ve istemciler bunu tüketecekse uygula — aksi halde overkill.

---

## 7. API Versioning — Eski İstemcileri Kırma

API yayına alındıktan sonra bir endpoint'in formatını değiştirirsen, eski istemciler bozulur. Versioning bu sorunu çözer.

**Üç strateji:**

**URL Path (en yaygın):**
```
GET /api/v1/kitaplar
GET /api/v2/kitaplar
```
Avantajı: URL'den hemen görünür, cache dostu.
Dezavantajı: URL'de "versiyonu" işaret etmek REST prensibine aykırı — kaynaklar değişmez, representation değişir.

**Query String:**
```
GET /api/kitaplar?api-version=1.0
GET /api/kitaplar?api-version=2.0
```
Avantajı: Base URL değişmez.
Dezavantajı: Her istekte query string eklemek zorunda kalırsın.

**Header:**
```
GET /api/kitaplar
Api-Version: 1.0
```
Avantajı: URL temiz kalır.
Dezavantajı: Browser'da test etmek zorlaşır, cache başlıkları karmaşıklaşır.

**Asp.Versioning paketi** üç stratejiyi de destekler, hangisini seçersen seç:

```csharp
// Program.cs
builder.Services.AddApiVersioning(options =>
{
    // İstemci versiyon belirtmezse varsayılanı kullan
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;

    // Yanıtta hangi versiyonların desteklendiğini bildir
    options.ReportApiVersions = true;
})
.AddApiExplorer(options =>
{
    // URL'de versiyon formatı: v1, v2
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
```

```csharp
// Controller
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/kitaplar")]
public class KitapApiController : ControllerBase
{
    [HttpGet]
    [MapToApiVersion("1.0")]
    public IActionResult ListeV1() { ... }

    [HttpGet]
    [MapToApiVersion("2.0")]
    public IActionResult ListeV2() { ... } // yeni format, ek alanlar
}
```

---

## 8. Dikkat Edilmesi Gerekenler

**Fiil kullanma, isim kullan:** `/kitapSil`, `/getKitap` gibi URL'ler REST değil RPC. URL kaynak, metod eylem.

**Çoğul isim:** `/kitap` mı `/kitaplar` mı? Endüstri standardı çoğul — `/kitaplar`. Tekil kullanım da yaygın ama tutarlı ol.

**Status code'u doğru seç:** Validation hatası için `200 OK` içinde `{ "error": "..." }` döndürme — bu API'yi kör eder. `400` veya `422` kullan, body'de detay ver.

**`DELETE` sonrası ne döner?** `204 No Content` ile body yok döndürmek doğru. Bazı ekipler silinen kaydı `200` ile döndürür — her ikisi de kabul görür ama `204` daha yaygın.

**`PUT` tüm nesneyi ister:** PATCH kısmen günceller, PUT tamamen günceller. PUT ile sadece fiyat gönderirsen diğer alanlar null/sıfır olabilir. Bunu istemciler bilmeli.

**Versioning erken düşün:** API yayına alındıktan sonra versioning eklemek büyük iş. Baştan `/api/v1/` ile başla — ilerisi gelirse hazır olursun.

---

## 9. Kontrol Soruları

1. `POST /kitaplar/sil` ile `DELETE /kitaplar/42` arasındaki fark nedir? Hangisi REST'e uygundur?

2. Bir kitabın sadece fiyatını güncellemek istiyorsun. PUT mi, PATCH mi kullanırsın? Fark ne?

3. Kullanıcı giriş yapmamış ama korunan bir endpoint'e giderse hangi status code döner? Giriş yapmış ama yetkisi yoksa?

4. `POST /kitaplar` başarılı olduğunda `200 OK` mi, `201 Created` mı dönersin? Neden?

5. API versioning için URL path ve header stratejileri arasındaki trade-off nedir?

6. HATEOAS nedir? Her REST API'nin uygulaması gerekir mi?
