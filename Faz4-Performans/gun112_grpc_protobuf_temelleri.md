# Gün 112 — gRPC Temelleri ve Protocol Buffers

---

## Bu Ders Neden Var?

REST/JSON çoğu API için yeterli. Ama bazı senaryolarda yetersiz kalıyor:
- Microservice'ler arası iletişim — saniyede binlerce çağrı, JSON yavaş ve büyük
- Streaming senaryoları — sürekli veri akışı (canlı borsa, video, telemetri)
- Çok dilli sistem — Go, Python, Java, .NET servisleri konuşmalı, manuel API kontratı yorucu

**gRPC** bu sorunlara yanıt veriyor. Google'ın geliştirdiği, Protocol Buffers tabanlı, HTTP/2 üstünde çalışan yüksek performanslı RPC framework.

Bugün gRPC'nin neyi farklı yaptığını, Protocol Buffers'ın ne olduğunu, ne zaman kullanılması gerektiğini ele alacağız.

---

## RPC Nedir? — Temel Kavram

**RPC = Remote Procedure Call** (Uzak Yordam Çağrısı). Adı uzun ama fikri basit: başka bir bilgisayardaki fonksiyonu, sanki kendi bilgisayarındaymış gibi çağırmak.

```csharp
// REST yaklaşımı — manuel HTTP:
var http = new HttpClient();
var response = await http.GetAsync("https://api.com/kitaplar/42");
var json = await response.Content.ReadAsStringAsync();
var kitap = JsonSerializer.Deserialize<Kitap>(json);

// RPC yaklaşımı — sanki yerel fonksiyon:
var kitap = await _catalogClient.GetKitapAsync(new GetKitapRequest { Id = 42 });
// arka planda HTTP olmuş, network'e gitmiş, dönmüş — ama sen düşünmedin
```

RPC felsefesi: "Network detaylarıyla uğraşma, fonksiyon çağırıyormuş gibi yaz."

REST de sonuçta RPC'nin bir formu (HTTP üzerinden) ama daha düşük seviyede — URL'ler, HTTP metodları, status code'lar elle yönetilir.

gRPC daha yüksek seviye soyutlama sağlıyor: kontrat yaz, kod otomatik üretilsin, "yerel fonksiyon çağırıyormuş gibi" konuş.

---

## gRPC'nin REST'ten Farkları

### 1. Binary Protocol — JSON Değil, Protobuf

REST genelde JSON gönderiyor:
```json
{
  "id": 42,
  "ad": "Clean Code",
  "fiyat": 150.00
}
```

Bu metin. İnsan tarafından okunabilir ama:
- Tip bilgisi yok (`150` int mi double mu?)
- Field isimleri her seferinde gönderiliyor (`"ad":` boşa byte)
- Parse etmek için JSON parser gerekiyor (CPU maliyeti)

gRPC binary protokol (Protobuf) kullanıyor:
```
0x08 0x2A 0x12 0x0A 43 6C 65 61 6E ...
```

Bu byte'lar:
- Çok daha küçük (3-10x daha az boyut)
- Hızlı parse edilir (5-10x daha hızlı)
- Tip-safe (sayı her zaman sayı, metin her zaman metin)

İnsan okuyamıyor → debug zor. Ama performans çok daha iyi.

### 2. HTTP/2 — Tek Bağlantı, Multipleksleme

REST genelde HTTP/1.1 üstünde. HTTP/1.1'in sınırı: bir bağlantı üzerinden bir istek/yanıt aynı anda.

HTTP/2 ile:
- Tek TCP bağlantısı üstünde birden fazla istek paralel
- Header compression (her istekte tekrar gönderilmez)
- Server push

gRPC her zaman HTTP/2 üstünde. Bu yüzden yüksek throughput'ta REST'ten daha verimli.

### 3. Contract-First — IDL ile Kontrat Tanımı

REST'te genelde önce kodu yazıyorsun, sonra Swagger/OpenAPI dokümanı üretiyorsun. Kontrat sonradan çıkarılıyor.

gRPC'de önce kontrat (`.proto` dosyası) yazıyorsun, sonra kod üretiliyor. Bu yaklaşıma **contract-first** denir.

Avantajı: Server ve client'lar farklı diller olsa bile (Go server, .NET client) aynı kontrattan üretildiği için kesinlikle uyumlu. Manuel manuel uyumluluk testi gerekmiyor.

### 4. Streaming — Doğal Destek

REST'te streaming için ek mekanizmalar (SSE, WebSocket) gerekiyor. gRPC'de streaming protokolün doğal bir parçası — sunucudan client'a, client'tan sunucuya, veya iki yönlü.

### Karşılaştırma Tablosu

| | REST/JSON | gRPC/Protobuf |
|---|-----------|---------------|
| Payload boyutu | Büyük (metin) | Küçük (binary) |
| Hız | Daha yavaş (parse) | Daha hızlı |
| HTTP versiyonu | Çoğunlukla HTTP/1.1 | HTTP/2 zorunlu |
| Kontrat | OpenAPI (sonradan) | .proto (önceden) |
| Streaming | Ek mekanizma gerekli | Doğal destek |
| Tarayıcı desteği | Direkt | gRPC-Web gerekli |
| Debug | Kolay (curl, browser) | Zor (binary) |
| İnsan okur mu? | Evet | Hayır |

---

## Protocol Buffers Nedir?

**Protocol Buffers (Protobuf)** Google'ın geliştirdiği bir veri serialization formatı. gRPC'nin kontrat dili ve veri biçimi olarak kullanılıyor.

Protobuf üç şey aslında:
1. **IDL (Interface Definition Language):** Veri yapısını tanımlama dili (`.proto` dosyaları)
2. **Wire format:** Binary serialization formatı
3. **Code generator:** `.proto` dosyalarından kod üretiyor

### .proto Dosyası Örneği

```proto
syntax = "proto3";

package KitapApp;

service CatalogService {
    rpc GetKitap (GetKitapRequest) returns (Kitap);
    rpc ListKitaplar (ListKitaplarRequest) returns (ListKitaplarResponse);
}

message Kitap {
    int32 id = 1;
    string ad = 2;
    string yazar = 3;
    double fiyat = 4;
    int32 stok = 5;
}

message GetKitapRequest {
    int32 id = 1;
}

message ListKitaplarRequest {
    int32 sayfa = 1;
    int32 boyut = 2;
}

message ListKitaplarResponse {
    repeated Kitap kitaplar = 1;
    int32 toplam = 2;
}
```

Bu dosyayı incelersek:

**`service CatalogService { ... }`** — Bir gRPC servisi tanımlıyor. İçindeki `rpc` satırları metotları.

**`message Kitap { ... }`** — Veri yapısı tanımı. C#'taki class/record karşılığı.

**`int32 id = 1;`** — Alan tanımı. Üç parça var:
- `int32` → tipi (32-bit integer)
- `id` → adı
- `= 1` → field number (kritik, birazdan açıklayacağım)

**`repeated Kitap kitaplar = 1;`** — Liste/array karşılığı. C#'ta `List<Kitap>` olur.

### Field Number Neden Var?

Her field'ın `= 1`, `= 2` diye bir sayısı var. Bu sayı **binary format'ta field'ı tanımlıyor**. Field adı değil, field number.

```
Kitap message'ı binary'de:
[field 1: id=42][field 2: ad="Clean Code"][field 4: fiyat=150]

JSON'da olsa:
{"id":42,"ad":"Clean Code","fiyat":150}  ← isim her seferinde gönderilir
```

Field number sayesinde:
- Field adı binary'de gönderilmiyor → çok daha küçük payload
- Field adını değiştirebilirsin (geriye uyumlu) — number aynı kalırsa kontrat bozulmaz
- Yeni field eklerken yeni number kullanırsın → eski clientlar bilmediği field'ı atlar, çalışmaya devam eder

**Önemli kural:** Field number'ları **asla değiştirme**. Eski client'lar eski number'ı bekliyor. Değiştirirsen breaking change.

### Protobuf Tipleri

Yaygın tipler:

| .proto | C# karşılığı | Açıklama |
|--------|--------------|----------|
| `int32`, `int64` | int, long | Sayı |
| `uint32`, `uint64` | uint, ulong | İşaretsiz sayı |
| `float`, `double` | float, double | Ondalık |
| `bool` | bool | Boolean |
| `string` | string | UTF-8 metin |
| `bytes` | byte[] | Ham byte dizisi |
| `repeated T` | `List<T>` | Liste |
| `map<K, V>` | `Dictionary<K, V>` | Map |
| `enum` | enum | Sabit değer kümesi |

**Önemli:** Protobuf'ta `null` yok. Tüm alanlar opsiyonel davranır — eksikse default değer (0, "", false) alır. Bu C# açısından kafa karıştırıcı olabilir.

### Nullable Olmadan Yaşamak

```proto
message Kitap {
    int32 id = 1;
    string aciklama = 2;   // hiç gönderilmezse "" olur, null değil
}
```

C#'ta `Kitap.Aciklama` her zaman string (null değil), ama boşsa "" döner. "Açıklama gönderildi mi?" sorusunu cevaplayamazsın — boş ile gönderilmeden arasında fark yok.

**Çözüm:** Protobuf'ta wrapper tipler (`google.protobuf.StringValue` gibi) veya `optional` keyword (proto3'ün son sürümlerinde):

```proto
message Kitap {
    int32 id = 1;
    optional string aciklama = 2;   // explicit nullable
}
```

C#'a `string?` olarak gelir.

---

## Code Generation — .proto'dan C#'a

`.proto` dosyasını yazdın. Şimdi bu nasıl C# kodu oluyor?

Protobuf derleyicisi (`protoc`) .proto dosyasını okur ve target dil için kod üretir. .NET'te bu işi `Grpc.Tools` NuGet paketi otomatik yapıyor.

### Üretilen Kod

```csharp
// Bu kod sen yazmıyorsun — protoc üretiyor:

// Kitap message → C# class:
public sealed partial class Kitap : pb::IMessage<Kitap>
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public string Yazar { get; set; } = "";
    public double Fiyat { get; set; }
    public int Stok { get; set; }

    // Serialize/Deserialize metotları, equality, ToString...
}

// CatalogService → abstract base class:
public abstract partial class CatalogServiceBase
{
    public virtual Task<Kitap> GetKitap(GetKitapRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, ""));
    }
}

// CatalogService → client class:
public partial class CatalogServiceClient
{
    public virtual Kitap GetKitap(GetKitapRequest request, CallOptions options) { ... }
    public virtual AsyncUnaryCall<Kitap> GetKitapAsync(GetKitapRequest request, CallOptions options) { ... }
}
```

Server tarafında `CatalogServiceBase`'i extend edip metotları implement ediyorsun. Client tarafında `CatalogServiceClient`'ı kullanıyorsun — sanki yerel metot çağrısı.

### Hangi Diller İçin?

Protobuf code generation şu dilleri destekler: C#, Java, Python, Go, JavaScript, C++, Ruby, PHP, Dart, Kotlin, Swift, Objective-C...

**Anahtar avantaj:** Aynı `.proto` dosyasından farklı diller için kod üretebilirsin. Go ile yazılmış microservice, .NET ile yazılmış microservice — ikisi de aynı kontratı paylaşır.

---

## Wire Format — Binary Nasıl Görünüyor?

Curious? Bir Kitap nesnesinin binary'de nasıl göründüğüne bakalım.

```
Kitap { id: 42, ad: "Clean Code", fiyat: 150.0 }

Binary (yaklaşık):
08 2A 12 0A 43 6C 65 61 6E 20 43 6F 64 65 21 00 00 00 00 00 C0 62 40
└─┘└─┘└──────────────────────────────────┘└─────────────────────────┘
field1   field2 (string "Clean Code")     field4 (double 150.0)
(id=42)

Toplam: 23 byte
```

Aynı veri JSON'da:
```json
{"id":42,"ad":"Clean Code","fiyat":150.0}
```
Toplam: 41 byte (UTF-8 olarak).

Yaklaşık %44 daha küçük. Büyük payload'larda fark katlanır.

**Önemli:** Bu binary insan okuyamaz. Debug için Protobuf'ı text formata çevirmek gerekiyor (debug tool'ları var).

---

## gRPC Status Code'ları

HTTP'de 200, 404, 500 gibi status code'lar var. gRPC'nin kendi status code'ları:

| Code | Anlamı | HTTP karşılığı |
|------|--------|----------------|
| `OK` | Başarılı | 200 |
| `INVALID_ARGUMENT` | Geçersiz girdi | 400 |
| `NOT_FOUND` | Kaynak yok | 404 |
| `ALREADY_EXISTS` | Zaten var | 409 |
| `PERMISSION_DENIED` | Yetki yok | 403 |
| `UNAUTHENTICATED` | Kimlik doğrulanmadı | 401 |
| `RESOURCE_EXHAUSTED` | Rate limit | 429 |
| `UNAVAILABLE` | Servis çalışmıyor | 503 |
| `DEADLINE_EXCEEDED` | Timeout | 504 |
| `INTERNAL` | Server hatası | 500 |
| `UNIMPLEMENTED` | Metot yok | 501 |

Hatayı dönerken bu code'ları kullanırsın:

```csharp
public override Task<Kitap> GetKitap(GetKitapRequest request, ServerCallContext context)
{
    var kitap = _repo.Get(request.Id);
    if (kitap is null)
        throw new RpcException(new Status(StatusCode.NotFound, $"Kitap {request.Id} bulunamadı"));

    return Task.FromResult(MapToProto(kitap));
}
```

---

## gRPC'nin Sınırları

Her teknoloji bir trade-off. gRPC'nin de zorlukları var.

### 1. Tarayıcı Desteği Sınırlı

Tarayıcı HTTP/2'yi yarım destekler — gRPC için yeterli değil. Tarayıcıdan direkt gRPC çağıramazsın.

Çözüm: **gRPC-Web** — gRPC'nin tarayıcı uyumlu varyantı. Limited streaming desteği var. Veya proxy: tarayıcı REST/JSON kullanır, gateway gRPC'ye çevirir.

### 2. Debug Zorluğu

Postman/curl gibi araçlarla kolay debug edemezsin. Özel araçlar gerekli:
- **grpcurl** (curl benzeri ama gRPC için)
- **BloomRPC, Kreya** (GUI tool'lar)
- Wireshark gibi network analyzer'lar binary'i decode edebilir

### 3. Load Balancer'larda Sorun

HTTP/2 long-lived connection kullanıyor. Geleneksel load balancer'lar (HTTP/1.1 odaklı) bunu doğru handle edemiyor. L4 (TCP-level) load balancer veya HTTP/2-aware proxy (Envoy, Linkerd) gerekiyor.

### 4. Learning Curve

REST herkesin bildiği bir şey. gRPC ekibe yeni öğretmek zaman alıyor. Tooling daha karmaşık.

### 5. Schema Evrimi Disiplini

Field number'ları sabit, breaking change yapmadan değiştiremezsin. Ekibin bu kurallara uyması lazım. Yanlış değişiklik production'da uyumsuzluk yaratır.

---

## gRPC Ne Zaman Kullanılmalı?

### Kullan

- **Microservice'ler arası iletişim:** Backend-to-backend, yüksek throughput, low latency
- **Streaming senaryoları:** Sürekli veri akışı (telemetri, canlı feed, video stream)
- **Çok dilli sistemler:** Go + .NET + Python karışık ekosistem
- **Mobile uygulamalar:** Bandwidth tasarrufu önemli, native client desteği var
- **IoT cihazlar:** Düşük bandwidth, küçük payload kritik

### Kullanma

- **Public API:** 3. parti developer'lar REST/JSON bekliyor
- **Browser-first uygulama:** gRPC-Web ile boğuşmak yerine REST kullan
- **Basit CRUD:** REST yeterli, gRPC overkill
- **Yoğun debug ihtiyacı:** Curl ile kolay test ettiğin yerler

---

## REST vs gRPC — Karar Akışı

Yeni bir API yazıyorsun, hangisini seçeceksin?

**Soru 1: Browser tüketici mi?**
- Evet (SPA, web app) → REST veya GraphQL
- Hayır → devam

**Soru 2: Microservice'ler arası mı?**
- Evet → gRPC ciddi aday
- Hayır → devam

**Soru 3: 3. parti developer için mi?**
- Evet → REST (ekosistem desteği)
- Hayır → devam

**Soru 4: Streaming gerekli mi?**
- Evet → gRPC (doğal destek)
- Hayır → ikisi de OK, ekibin tercihine bırak

**Soru 5: Performans kritik mi (yüksek RPS, low latency)?**
- Evet → gRPC avantajlı
- Hayır → REST daha pratik

Genellikle: **iç servisler için gRPC, dış API için REST.** İkisini birlikte kullanmak yaygın pattern.

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC tek monolit, REST API yok bile (MVC web app). 500 kullanıcıda gRPC alakasız.

50K kullanıcıda 5 microservice ortamında:
- Servisler arası REST + JSON yavaş ve verbose
- gRPC ile 3-5x daha hızlı, 50% daha az network trafiği
- Streaming senaryolar (canlı dashboard) için gRPC doğal
- Çok dilli microservice ekosistemde ortak kontrat

---

## 500 vs 50K Kullanıcı

| Senaryo | 500 kullanıcı/ay | 50K kullanıcı/ay |
|---------|-------------------|-------------------|
| Tek monolit, basit CRUD | REST yeterli, gRPC gereksiz | REST yeterli |
| Microservice + frontend | REST | REST (frontend) + gRPC (backend-to-backend) |
| Yüksek RPS internal API | İhtimal düşük | gRPC ciddi avantaj |
| Streaming (canlı veri) | Nadir ihtiyaç | gRPC veya SignalR |
| Multi-language ekip | Tek dil çoğunluk | gRPC kontrat birliği için değerli |

---

## Kontrol Soruları

1. RPC felsefesi ne demek? REST'ten temel farkı nedir?
2. Protocol Buffers'da `int32 id = 1;` ifadesindeki `= 1` neyi temsil eder? Neden önemli?
3. Protobuf wire format'ı JSON'dan neden daha küçük? Field adları nasıl handle ediliyor?
4. Protobuf'ta `null` neden yok? Bunun çevresinde nasıl çalışıyoruz?
5. gRPC'nin HTTP/2 zorunluluğu hangi avantajları getiriyor?
6. Contract-first yaklaşımı (önce .proto, sonra kod) sonradan dokümantasyondan ne farkı var?
7. gRPC'nin tarayıcıda doğrudan desteklenmeme nedeni nedir? Çözüm yolu nedir?
8. Field number'ı değiştirmek neden tehlikeli? Yeni alan eklemenin doğru yolu nedir?
9. Hangi senaryolarda gRPC, hangilerinde REST tercih edilmeli?
