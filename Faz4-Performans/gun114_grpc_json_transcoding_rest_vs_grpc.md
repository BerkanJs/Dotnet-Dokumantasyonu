# Gün 114 — gRPC-JSON Transcoding ve gRPC vs REST Karar Rehberi

---

## Bu Ders Neden Var?

Gün 112-113'te gRPC'nin gücünü gördük. Ama bir gerçek var: **dış dünya REST/JSON ile çalışıyor.**

- Frontend JavaScript developer'ları curl/fetch ile test etmek istiyor
- Mobil ekibin REST client library'lerine alışkın
- 3. parti developer'lar Swagger/OpenAPI doc bekliyor
- Webhook gönderen sistemler JSON gönderiyor

İç servislerin için gRPC kullanmak istiyorsun (performans) ama dışa REST sunmak zorundasın. İki ayrı API yazmak gereksiz iş tekrarı.

**gRPC-JSON Transcoding** bu sorunu çözüyor: tek bir gRPC servisinden hem gRPC hem REST/JSON endpoint'leri otomatik üretiyor.

Bugün bunu, ardından "ne zaman gRPC, ne zaman REST" sorusunun detaylı cevabını ele alacağız.

---

## gRPC-JSON Transcoding Nedir?

Aynı `.proto` tanımından iki ayrı sunucu davranışı:
- gRPC client → HTTP/2 + Protobuf binary
- REST client → HTTP/1.1 + JSON

Aynı kod, aynı business logic, aynı endpoint. ASP.NET Core gelen isteğin formatına göre cevap veriyor.

```
gRPC client:
  POST /catalog.CatalogService/GetKitap
  Content-Type: application/grpc
  [binary protobuf]
  ──────────────────────────▶ ASP.NET Core ◀──────────────────
  Aynı handler:                              REST client:
  GetKitap(request)                           GET /v1/kitaplar/42
                                              Content-Type: application/json
                                              ──────────────────────────▶
```

Bir servis. İki protokol. Tek codebase.

---

## HTTP Annotation — .proto'da REST Eşlemesi

`.proto` dosyasına Google'ın HTTP annotation'larını ekliyorsun. Her gRPC metoduna karşılık gelen REST endpoint'ini tanımlıyorsun.

```proto
syntax = "proto3";

import "google/api/annotations.proto";

service CatalogService {
    rpc GetKitap (GetKitapRequest) returns (Kitap) {
        option (google.api.http) = {
            get: "/v1/kitaplar/{id}"
        };
    }

    rpc CreateKitap (CreateKitapRequest) returns (Kitap) {
        option (google.api.http) = {
            post: "/v1/kitaplar"
            body: "*"
        };
    }

    rpc UpdateKitap (UpdateKitapRequest) returns (Kitap) {
        option (google.api.http) = {
            put: "/v1/kitaplar/{id}"
            body: "kitap"
        };
    }

    rpc DeleteKitap (DeleteKitapRequest) returns (Empty) {
        option (google.api.http) = {
            delete: "/v1/kitaplar/{id}"
        };
    }
}

message GetKitapRequest { int32 id = 1; }
message CreateKitapRequest { string ad = 1; double fiyat = 2; }
message UpdateKitapRequest {
    int32 id = 1;
    Kitap kitap = 2;
}
message DeleteKitapRequest { int32 id = 1; }
```

### Annotation'ları Okumak

```proto
option (google.api.http) = {
    get: "/v1/kitaplar/{id}"
};
```

Bu diyor ki:
- HTTP metodu: GET
- URL pattern: `/v1/kitaplar/{id}`
- `{id}` placeholder'ı → request mesajının `id` field'ından doldur

```proto
option (google.api.http) = {
    post: "/v1/kitaplar"
    body: "*"
};
```

- POST `/v1/kitaplar`
- `body: "*"` → tüm request mesajını JSON body olarak al

```proto
option (google.api.http) = {
    put: "/v1/kitaplar/{id}"
    body: "kitap"
};
```

- PUT `/v1/kitaplar/{id}`
- `{id}` → request.Id'den
- `body: "kitap"` → request.kitap field'ı body'den, geri kalan ID URL'de

### Üretilen REST API Örnekleri

```
GET    /v1/kitaplar/42                  → GetKitap(id=42)
POST   /v1/kitaplar                     → CreateKitap(ad="X", fiyat=100)
       body: { "ad": "X", "fiyat": 100 }
PUT    /v1/kitaplar/42                  → UpdateKitap(id=42, kitap={...})
       body: { "ad": "Y", "fiyat": 150 }
DELETE /v1/kitaplar/42                  → DeleteKitap(id=42)
```

REST developer için tipik REST API. Backend developer için tek bir gRPC servisi.

---

## ASP.NET Core'da Setup

```xml
<!-- .csproj -->
<ItemGroup>
    <PackageReference Include="Grpc.AspNetCore" Version="..." />
    <PackageReference Include="Microsoft.AspNetCore.Grpc.JsonTranscoding" Version="..." />
    <PackageReference Include="Google.Api.CommonProtos" Version="..." />
</ItemGroup>

<ItemGroup>
    <Protobuf Include="Protos\catalog.proto" GrpcServices="Server" />
</ItemGroup>
```

```csharp
// Program.cs
builder.Services.AddGrpc().AddJsonTranscoding();
// ne yapar → standart gRPC servisi + JSON transcoding eklenir
// bunu yazmasaydık → sadece gRPC çalışırdı, REST çağrıları 404

var app = builder.Build();
app.MapGrpcService<CatalogServiceImpl>();
app.Run();
```

Servis implementasyonu **değişmiyor**. Aynı `CatalogServiceImpl.GetKitap()` metodu hem gRPC hem REST çağrılarını işliyor.

---

## OpenAPI/Swagger Entegrasyonu

REST API'n var ama dokümantasyonu yok — bu eksik. Neyse ki gRPC-JSON transcoding Swagger ile çalışıyor:

```xml
<PackageReference Include="Microsoft.AspNetCore.Grpc.Swagger" Version="..." />
```

```csharp
builder.Services.AddGrpcSwagger();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Catalog API", Version = "v1" });
    var filePath = Path.Combine(AppContext.BaseDirectory, "Catalog.xml");
    c.IncludeGrpcXmlComments(filePath, includeControllerXmlComments: true);
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
```

Artık `/swagger` adresinde tam OpenAPI dokümantasyonu var. REST client'lar bunu kullanarak SDK üretebilir, "try it out" yapabilir.

---

## Streaming ve Transcoding — Kısıtlamalar

Transcoding her şeyi destekleyemiyor. Streaming'de sınırlar var:

| gRPC Model | REST karşılığı | Destek |
|-----------|---------------|--------|
| Unary | Standart REST | Tam destek |
| Server streaming | HTTP chunked / SSE-benzeri | Sınırlı destek |
| Client streaming | — | Desteklenmiyor |
| Bidirectional | WebSocket gerekir | Desteklenmiyor |

**Pratik:** Transcoding unary için harika. Streaming endpoint'leri için ya REST tarafına farklı bir gateway koy, ya da streaming'i sadece gRPC ile yap.

Çoğu uygulama mantığı zaten unary. Streaming özel senaryolar için.

---

## gRPC vs REST — Detaylı Karar Matrisi

Bu sorunun tek doğru cevabı yok. Senaryoya bağlı. Birkaç açıdan inceleyelim.

### Performans

**gRPC kazanır:**
- Payload boyutu (binary vs JSON): 3-10x daha küçük
- Serialize/deserialize hızı: 5x daha hızlı
- HTTP/2 multipleksleme: birden fazla istek paralel
- Connection reuse: tek TLS handshake

**Ne kadar fark eder?**
- Basit küçük JSON (< 1 KB): farkı hissetmezsin
- Büyük listeler, sık çağrılar (1000+ RPS): gRPC açık ara üstün
- Mikroservice'ler arası iletişim: yıllık binlerce dolar maliyet farkı

### Geliştirici Deneyimi

**REST kazanır:**
- Curl/Postman/browser ile debug kolay
- JSON insan okuyabilir
- Her dilde, her framework'te yaygın
- Tarayıcı doğal destek

**gRPC zorlukları:**
- Binary debug için özel araç
- Code generation pipeline'ı (build setup)
- HTTP/2 + TLS zorunluluğu
- Tooling daha karmaşık

### Type Safety ve Kontrat

**gRPC kazanır:**
- `.proto` dosyası kesin kontrat — tip uyuşmazlığı compile-time'da yakalanır
- Server/client farklı dillerde olsa bile garanti uyumlu
- Field number'lar sayesinde versiyon yönetimi daha güvenli

**REST sınırı:**
- OpenAPI/Swagger var ama opsiyonel
- JSON'da tip muğlak (`"42"` mi string mi int?)
- Client/server uyumsuzluğu runtime'da patlıyor

### Ekosistem Desteği

**REST kazanır:**
- Her HTTP client desteklemekle yükümlü
- 3. parti entegrasyon araçları, gateway'ler, monitoring çoğunlukla REST düşünüyor
- Eski sistemlerle uyumluluk

**gRPC sınırı:**
- Görece yeni — eski sistemlerde destek yok
- Bazı bulut servisleri gRPC'yi geriden takip ediyor

### Streaming

**gRPC açık ara kazanır:**
- 4 farklı streaming modeli doğal desteği
- HTTP/2 üzerinde verimli
- Tip-safe stream mesajları

**REST çözümleri:**
- SSE (Server-Sent Events) — sadece server→client
- WebSocket — bi-yönlü ama farklı protokol
- HTTP/2 chunked — destek karmaşık

### Browser Desteği

**REST açık ara kazanır:**
- fetch/XHR ile direkt çağrı
- CORS doğal

**gRPC zorluğu:**
- Tarayıcı HTTP/2 trailer'ları desteklemiyor → direkt çalışmıyor
- gRPC-Web ile sınırlı destek (streaming yarım)
- Çoğu zaman REST gateway gerekli

---

## Tipik Mimari: Karma Yaklaşım

Modern büyük projelerde **karma yaklaşım** yaygın:

```
       Browser/Mobile
              │
              ▼ HTTPS / REST + JSON
       API Gateway (REST tarafı)
              │
              ▼ HTTP/2 / gRPC
    ┌─────────┴─────────┐
    │                   │
CatalogService     OrderService   ← gRPC ile birbirleriyle konuşuyorlar
    │                   │
    └─────────┬─────────┘
              ▼ gRPC
       UserService, PaymentService...
```

**Dış dünya:** REST/JSON — frontend, mobil, 3. parti developer için
**İç dünya:** gRPC — microservice'ler arası performans

Gateway aradaki çevirimi yapıyor. Veya yukarıdaki **gRPC-JSON transcoding** ile her servis kendi REST'ini de sunabiliyor.

---

## Karar Akışı — Pratik Sorular

Yeni bir endpoint yazıyorsun. Hangisini seçeceksin?

**Soru 1: Bu endpoint kim tüketecek?**

| Tüketici | Tercih |
|----------|--------|
| Sadece kendi backend microservice'lerin | gRPC |
| Frontend (SPA, mobil) | REST (veya gRPC-Web) |
| 3. parti developer'lar | REST |
| IoT cihazlar | gRPC (bandwidth tasarrufu) |
| Webhook receiver | REST (gönderen sistem REST atıyor) |

**Soru 2: Streaming gerekli mi?**
- Evet → gRPC (doğal destek)
- Hayır → her ikisi de OK

**Soru 3: Geliştirme hızı mı, performans mı öncelikli?**
- Geliştirme hızı (MVP, prototip) → REST
- Performans (yüksek RPS, scale) → gRPC

**Soru 4: Ekibin tecrübesi?**
- gRPC tecrübesi var → seç çekinme
- Sadece REST biliyorlar → REST kullan, gRPC öğrenme zamanı ekstra

**Soru 5: Cloud provider veya orchestration kısıtları?**
- Bazı serverless platformlar gRPC'yi yarım destekliyor
- Service mesh (Istio, Linkerd) gRPC'yi seviyor

---

## Production'da Gerçek Örnekler

**Netflix:** Internal service communication için gRPC. Dış API'lar REST.

**Google:** Hemen her şey gRPC. (Tabii kendi protokollerini yarattılar.)

**Square:** Mobile uygulamalarda gRPC kullanıyor — bandwidth tasarrufu kritik.

**Uber:** Çoğu microservice gRPC. Frontend için REST gateway.

**Spotify:** REST üzerinden başladı, performans kritik servislerde gRPC'ye geçiyor.

Pattern net: dış dünya REST, iç dünya gRPC.

---

## Migration Stratejisi — REST'ten gRPC'ye

Mevcut REST API'n var, gRPC'ye geçmek istiyorsun. Big-bang yerine kademeli:

**Adım 1: Yeni servisleri gRPC ile yaz**
Mevcut REST'lere dokunma. Yeni geliştirilen microservice gRPC olsun.

**Adım 2: Mevcut REST'leri gRPC-JSON transcoding ile hibrit yap**
Aynı endpoint hem REST hem gRPC sunuyor. Eski client'lar REST kullanmaya devam. Yeni internal client'lar gRPC kullanıyor.

**Adım 3: Internal traffic'i gRPC'ye geçir**
Service-to-service çağrıları gRPC'ye al. Performans iyileşmesi hissedilir hale gelir.

**Adım 4: REST endpoint'leri kademeli deprecate et**
External REST client'ları gRPC-Web veya gateway'e yönlendir. Sunset tarihi belirle.

Hiç REST'i atmak zorunda değilsin — çoğu sistem ikisini birlikte kullanır kalıcı olarak.

---

## Maintenance Trade-Off

gRPC'nin az konuşulan bir maliyeti: **maintenance disiplini**.

Field number'lar sabit. Yeni alan eklerken yeni number kullanmalısın. Eski alanı silmek istersen `reserved` keyword'ü ile rezerve etmen lazım (yoksa biri aynı number'ı yeniden kullanır → uyumsuzluk).

```proto
message Kitap {
    int32 id = 1;
    string ad = 2;
    reserved 3, 4;             // bu number'lar kullanılmayacak
    reserved "eski_alan";      // bu alan adı kullanılmayacak
    double fiyat = 5;
}
```

REST'te yeni alan eklemek bu kadar disiplin gerektirmiyor — JSON serializer eksik alanı default'la dolduruyor. gRPC'de hata yapmak versiyon uyumsuzluğu demek.

**Ekip büyük ve disiplin gerekli:** PR review'larında `.proto` değişiklikleri özel dikkat gerektirir. Küçük ekiplerde sorun yok, büyük ekiplerde standart proseslere ihtiyaç var.

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC tek MVC monolit — gRPC alakasız. REST API bile yok.

50K kullanıcı + microservice ortamında:
- Servisler arası: gRPC daha verimli, daha hızlı
- Frontend: REST/JSON daha pratik
- gRPC-JSON transcoding sayesinde tek codebase iki dünyaya da hizmet edebilir
- Karar tek değil — her endpoint için "hangi tüketici" sorusuyla karar verilir

---

## 500 vs 50K Kullanıcı

| Senaryo | 500 kullanıcı/ay | 50K kullanıcı/ay |
|---------|-------------------|-------------------|
| REST yaklaşımı | Tek monolit için yeterli | Frontend + 3. parti için yeterli |
| gRPC | Genelde gereksiz | Internal microservice için ideal |
| gRPC-JSON transcoding | Overengineering | Hem internal hem external destek için değerli |
| OpenAPI/Swagger | Documentation için kullan | Zorunlu (gRPC tarafı için bile) |
| Migration stratejisi | İhtimal düşük | Kademeli geçiş normal |

---

## Kontrol Soruları

1. gRPC-JSON transcoding ne yapıyor? Tek codebase nasıl iki protokol sunuyor?
2. `.proto`'da `google.api.http` annotation'ı ne işe yarıyor? `body: "*"` ne anlama gelir?
3. Streaming endpoint'leri JSON transcoding ile çalışır mı? Hangi türler destekli, hangileri değil?
4. gRPC ve REST arasındaki performans farkı pratikte ne kadar belirgin? Hangi senaryolarda görülür?
5. Tipik "karma yaklaşım" nedir? Hangi katmanda hangi protokol?
6. REST'ten gRPC'ye migration nasıl kademeli yapılır? Big-bang yerine niye kademeli?
7. Field number disiplini neden gRPC'de daha kritik? `reserved` keyword'ü ne işe yarar?
8. Browser doğrudan gRPC çağıramıyor — neden? Çözüm yolları nelerdir?
9. Hangi durumlarda gRPC seçimi yanlış olur? REST hâlâ daha iyi mi?
