# Gün 123 — Service Discovery ve Load Balancing

## Teorik

### Problem: Servis Adresi Sabit Değil

Faz3'te tek uygulama vardı, adresi belliydi: `localhost:5000`. Bitti.

Faz5'te CatalogService 3 farklı container'da çalışıyor olabilir. Bu container'lar her restart'ta farklı IP alabilir. Ölçekleme yapınca yeni instance'lar eklenir, bazı instance'lar kapanır.

```
Dün:
  CatalogService  →  192.168.1.10:5001

Bugün (scale out yapıldı):
  CatalogService  →  192.168.1.10:5001
  CatalogService  →  192.168.1.11:5001
  CatalogService  →  192.168.1.12:5001

Yarın (container restart):
  CatalogService  →  192.168.1.20:5001  ← IP değişti!
```

Gateway `appsettings.json`'a `192.168.1.10` yazdıysan, container yeniden başlayıp IP değişince bağlantı kopar.

**Soru:** "CatalogService şu an nerede çalışıyor?" sorusunu kim cevaplayacak?

---

### Service Discovery — Servis Rehberi

**Analoji: Sarı Sayfalar / Google Maps**

İstanbul'da pizza yemek istiyorsun. Pizzacının adresini ezberlemedin. Google Maps'e "pizza" yazıyorsun, o sana yakındaki açık pizzacıları buluyor.

Service Discovery da aynı şey:
- Servisler ayağa kalkınca kendilerini **kaydeder**: "Ben CatalogService'im, şu IP'deyim, şu portum"
- Diğer servisler isim üzerinden sorar: "CatalogService nerede?"
- Discovery sistemi güncel adresi döner

```
[CatalogService başladı]
    │
    ▼
[Service Registry]  ← "catalog-service = 192.168.1.10:5001"
    ▲
    │ "catalog-service nerede?"
[OrderService]
    │
    ▼
"192.168.1.10:5001"  →  [CatalogService]
```

---

### DNS-Based Service Discovery — En Basit Yöntem

Docker Compose ve Kubernetes kendi DNS çözümlemesini yapar. Servis adını doğrudan hostname olarak kullanabilirsin.

```yaml
# docker-compose.yml
services:
  catalog-service:
    image: kitabevi/catalog
    ports:
      - "5001:5001"

  order-service:
    image: kitabevi/order
    ports:
      - "5002:5002"
```

```csharp
// OrderService içinde — IP yazmıyoruz, servis adını yazıyoruz
var bookInfo = await _httpClient.GetFromJsonAsync<BookDto>(
    "http://catalog-service:5001/api/books/42"
    //       ↑ Docker Compose bu adı IP'ye çevirir
);
```

Docker Compose'da servisler birbirini **isimle** bulur. IP sabit olmak zorunda değil. Bu Kitabevi'mizde kullanacağımız yöntem.

---

### Client-Side vs Server-Side Discovery

**Client-Side Discovery**

İstemci (OrderService) registry'e sorar, adresi öğrenir, direkt bağlanır.

```
OrderService → Registry: "CatalogService nerede?"
Registry → OrderService: "192.168.1.10:5001, 192.168.1.11:5001"
OrderService → hangisine bağlanacağına kendisi karar verir
```

✅ Esneklik — istemci load balancing kararını kendisi verir  
❌ Her servis registry ile nasıl konuşacağını bilmek zorunda

**Server-Side Discovery**

İstemci sadece isteği gönderir, router/gateway nereye gönderileceğine karar verir.

```
OrderService → Gateway/Load Balancer: "CatalogService'e git"
Load Balancer → Registry'ye sorar, uygun instance'ı seçer, yönlendirir
```

✅ İstemci hiçbir şey bilmek zorunda değil  
✅ Kubernetes bu modeli kullanır — `Service` objesi arkada pod'ları bulur  
❌ Ek infrastructure

**Kitabevi'mizde:** Docker Compose DNS = Server-Side discovery. `catalog-service` adresini yazıyoruz, Docker hallediyor.

---

### Consul, Kubernetes DNS, Eureka

| | Consul | Kubernetes DNS | Eureka |
|---|---|---|---|
| Kim yapar | HashiCorp | Kubernetes (built-in) | Netflix |
| Nasıl çalışır | Agent her node'da, health check yapar | Service objesi → DNS | Her servis register olur |
| Health check | ✅ Güçlü, HTTP/TCP/script | ✅ Liveness/readiness probe | ✅ Heartbeat |
| Ne zaman? | Kubernetes dışı, multi-DC | Kubernetes'te standart | Java/Spring ekosistemi |
| .NET'te kullanım | `Steeltoe` veya direkt API | Direkt — özel kütüphane gerekmez | `Steeltoe` |

Kitabevi'mizde önce Docker Compose → Kubernetes'e geçince Kubernetes DNS otomatik devreye girer.

---

### Load Balancing — Hangi Instance'a Gideyim?

CatalogService 3 instance'da çalışıyor. Gelen istek hangisine gidecek?

**Analoji: Süpermarket kasaları**

4 kasa açık. "En kısa kuyruğa gir" diyorsun. Bu least connections.
Ya da her kasaya sırayla giriyorsun — round-robin.

**Round-Robin — Sırayla**

```
İstek 1 → Instance A
İstek 2 → Instance B
İstek 3 → Instance C
İstek 4 → Instance A (başa döndü)
```

Basit, eşit dağılım. Instance'ların eşit güçte olduğu varsayılır.

**Least Connections — En Az Bağlantılı**

```
Instance A: 10 aktif bağlantı
Instance B: 3 aktif bağlantı   ← yeni istek buraya
Instance C: 7 aktif bağlantı
```

Yavaş instance'lar daha az istek alır. Uzun süren işlemlerde daha adil.

**Consistent Hashing — Aynı İstemci Hep Aynı Instance'a**

```
Kullanıcı ID 42 → her zaman Instance B
Kullanıcı ID 17 → her zaman Instance A
```

Session veya cache durumunda işe yarar. Kullanıcının state'i hep aynı instance'ta.

**Kitabevi'mizde:** Docker Compose ve Kubernetes varsayılan olarak round-robin yapar. Özel bir şey yazmaya gerek yok.

---

### Health Check Entegrasyonu

Load balancer sağlıksız instance'a istek göndermemeli. Bunun için periyodik olarak instance'ları kontrol eder.

```
Load Balancer her 10 saniyede bir:
  → GET http://catalog-service-a:5001/health → 200 OK  ✅ trafiği al
  → GET http://catalog-service-b:5001/health → 503    ❌ devre dışı
  → GET http://catalog-service-c:5001/health → 200 OK  ✅ trafiği al
```

Faz2'de `/health` endpoint'i yazmıştık. Burada devreye giriyor.

```csharp
// CatalogService — Program.cs (Faz2'den hatırlıyoruz)
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

Kubernetes'te bu `livenessProbe` ve `readinessProbe` olarak tanımlanır (Gün 148'de göreceğiz).

---

### Service Mesh Nedir?

Servisler arası iletişimi yönetmek için ek bir altyapı katmanı. Her service'in yanına bir **sidecar proxy** eklenir (genellikle Envoy).

```
[CatalogService] ←→ [Envoy Proxy]
                          ↕
[OrderService]   ←→ [Envoy Proxy]
```

Bu proxy'ler:
- TLS şifreleme (mTLS) — servisler arası güvenli kanal
- Retry, circuit breaker — otomatik, kod yazmadan
- Distributed tracing — otomatik span oluşturma
- Traffic splitting — yeni version'a %10 trafik gönder

**Istio** ve **Linkerd** en yaygın service mesh'ler.

**Ne zaman gerekli?**

```
500 kullanıcı  → Service mesh overkill, karmaşıklık değmez
50K kullanıcı  → Hâlâ opsiyonel
Büyük ekip, çok servis, compliance gereksinimleri → Düşünülebilir
```

Kitabevi'mizde kullanmıyoruz. Ama kavramı bilmek gerekiyor — senior görüşmelerde sık sorulan konu.

---

### Faz3 ile Karşılaştırma

Faz3'te routing ve load balancing diye bir kavram yoktu:

```
Faz3:
  Kullanıcı → localhost:5000 → ASP.NET Core → Controller
  (tek adres, tek instance, routing framework içinde)

Faz5:
  Kullanıcı → gateway:8080
    → DNS/Discovery: "catalog-service nerede?"
    → Load Balancer: "hangi instance?"
    → Instance: istek işlendi
```

Faz3'te bu karmaşıklık yoktu çünkü scale etme ihtiyacı yoktu. Faz5'te her servis bağımsız ölçeklenebilir — bu karmaşıklığın bedeli.

---

### 500 vs 50K Kullanıcı

| Karar | 500 kullanıcı/ay | 50K kullanıcı/ay |
|-------|-----------------|-----------------|
| DNS-based discovery | ✅ Docker Compose ile yeterli | ✅ Kubernetes DNS yeterli |
| Consul | ❌ Overkill | ⚠️ Kubernetes dışı multi-DC için |
| Load balancing | ✅ Round-robin yeterli | ✅ Round-robin genelde yeterli |
| Least connections | ❌ Gerekmez | ⚠️ Uzun işlemler varsa |
| Service mesh (Istio) | ❌ Kesinlikle overkill | ⚠️ Büyük ekip, compliance gerekiyorsa |
| Health check entegrasyonu | ✅ Her zaman | ✅ Her zaman zorunlu |

---

### Kontrol Soruları

1. Docker Compose'da `catalog-service` adresini kullanabiliyorsun ama production'da IP değişebilir. Bu sorunu Docker Compose nasıl çözüyor?
2. Round-robin ve least connections arasındaki fark ne? Kitap arama (hızlı) ve rapor üretme (yavaş) için hangisi daha uygun?
3. Health check olmadan load balancer çalışan ve çalışmayan instance'ı nasıl ayırt eder? Ayırt edemezse ne olur?
4. Service mesh hangi problemleri otomatik çözer? Biz bu problemleri olmadan nasıl çözüyoruz?
5. Kitabevi'nde CatalogService 3 instance'a çıkarıldı, biri çöktü. Health check olmadan ne kadar süre hatalı istekler gider?
