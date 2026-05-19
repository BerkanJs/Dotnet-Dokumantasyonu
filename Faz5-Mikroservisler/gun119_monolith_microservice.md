# Gün 119 — Monolith → Microservice: Ne Zaman?

## Teorik

### Monolith Nedir?

Tüm uygulama tek bir deployable birim olarak çalışır. Faz2'de yazdığımız `KitabeviMVC`, Faz3'te Onion Architecture'a taşıdığımız `KitabeviApp` — ikisi de monolith. Tek proje, tek veritabanı, tek deploy.

```
┌─────────────────────────────────────┐
│           KitabeviApp               │
│                                     │
│  ┌─────────┐  ┌────────┐  ┌──────┐ │
│  │ Catalog │  │ Order  │  │ Auth │ │
│  └─────────┘  └────────┘  └──────┘ │
│                                     │
│         PostgreSQL (tek DB)         │
└─────────────────────────────────────┘
         ↓ tek binary deploy
```

---

### Monolith'in Avantajları

**1. Basitlik**
- Geliştirme ortamı: tek `dotnet run`
- Debug: breakpoint her yere konabilir, stack trace anlamlı
- Refactor: IDE cross-reference çalışır, rename güvenli

**2. Transaction**
- Sipariş oluştur + stok düş + log yaz → tek DB transaction'ı
- Hata olursa hepsi rollback olur, tutarlılık garantili

**3. Düşük Operasyon Yükü**
- Tek binary, tek Dockerfile, tek CI pipeline
- Log bir yerde, monitoring tek endpoint

```
// Monolith'te transaction — tek DB, sorunsuz
await using var tx = await _db.BeginTransactionAsync();
await _orderRepo.CreateAsync(order);      // ORDER tablosuna yaz
await _catalogRepo.ReduceStockAsync(bookId); // CATALOG tablosundan düş
await tx.CommitAsync();                   // ikisi birlikte commit veya rollback
```

---

### Microservice'in Avantajları

**1. Bağımsız Deploy**
- CatalogService değişti → sadece CatalogService deploy edilir
- OrderService çalışmaya devam eder, kullanıcı etkilenmez

**2. Bağımsız Ölçekleme**
- Kitap arama yoğun → CatalogService'i 5 instance'a çıkar
- NotificationService hafif → tek instance yeter
- Monolith'te tüm uygulamayı ölçekliyorsun

**3. Fault Isolation**
- NotificationService çöktü → sipariş oluşturma hâlâ çalışır
- Monolith'te bir modülün unhandled exception'ı tüm uygulamayı çökertebilir

**4. Teknoloji Bağımsızlığı**
- CatalogService: .NET + PostgreSQL
- RecommendationService: Python + Redis
- Her servis kendi stack'iyle yazılabilir

```
500 kullanıcı/ay  → monolith yeterli, mikroservis overkill
50K kullanıcı/ay  → bağımsız ölçekleme gerekirse mikroservis düşünülebilir
                     (ama önce profiling yap — belki tek servis yeterli)
```

---

### "Distributed Monolith" — En Kötü Dünya

Mikroservise geçildi ama yanlış yapıldı:

```
┌───────────────┐   sync REST   ┌───────────────┐
│ OrderService  │ ────────────► │ CatalogService│
└───────────────┘               └───────────────┘
        │                               │
        └──────────── AYNI DB ──────────┘
                   (shared database)
```

**Ne yanlış:**
- Servisler ortak DB kullanıyor → schema değişince ikisi birden deploy edilmeli
- Her request için senkron REST çağrısı → CatalogService düşünce OrderService de düşer
- Deploy bağımsız değil → monolith'ten hiçbir şey kazanılmadı

**Distributed monolith = mikroservisin karmaşıklığı + monolith'in kırılganlığı.**

---

### Martin Fowler'ın Microservice Prerequisites

Fowler, mikroservise geçmeden önce şu altyapının hazır olması gerektiğini söyler:

| Gereksinim | Neden Zorunlu |
|------------|---------------|
| **Hızlı provisioning** | Yeni servis dakikalar içinde ayağa kalkabilmeli |
| **Temel monitoring** | Her servisin sağlığını ayrı izleyebilmeli |
| **Hızlı uygulama deployment** | CI/CD her servis için bağımsız çalışmalı |
| **DevOps kültürü** | Geliştirici kendi servisini production'da takip eder |

Bunlar yoksa mikroservis kaosa dönüşür. **Monolith'i iyi yönetemiyorsan mikroservisi hiç yönetemezsin.**

---

### Strangler Fig Pattern — Monolith'i Parçalama Stratejisi

Var olan monolith'i bir günde mikroservise dönüştürmek risklidir. Strangler Fig (sarmaşık inciri) yaklaşımı: eski sistemi yavaş yavaş "boğ", yeni servisleri yanına ekle.

```
Başlangıç:
┌────────────────────────────────┐
│         Monolith               │
│  Catalog + Order + Auth + ...  │
└────────────────────────────────┘

1. Adım — API Gateway önüne koy:
    ┌─────────┐
    │ Gateway │
    └────┬────┘
         │
    ┌────▼───────────────────────┐
    │         Monolith           │
    └────────────────────────────┘

2. Adım — Catalog modülünü ayır:
    ┌─────────┐
    │ Gateway │
    └────┬────┘
    /catalog  \  /orders, /auth...
┌───▼──────┐  ┌▼──────────────────┐
│ Catalog  │  │    Monolith       │
│ Service  │  │  (Order + Auth)   │
└──────────┘  └───────────────────┘

3. Adım — Monolith küçülür, servisler büyür:
    ┌─────────┐
    │ Gateway │
    └────┬────┘
   ┌─────┼─────┐
   ▼     ▼     ▼
Catalog Order  Auth
```

**Kitabevi'mizde uygulayacağımız:** Faz3'te yazdığımız Onion kodu CatalogService'e taşınacak. OrderService, NotificationService sıfırdan yazılacak.

---

### Domain-Driven Design → Bounded Context → Microservice Sınırı

Bir mikroservisin sınırını **"ne iş yapar"** sorusuyla değil, **"kimin verisi"** sorusuyla çiziyoruz.

**Bounded Context:** Bir domain kavramının tutarlı anlam taşıdığı sınır.

```
Catalog Context:          Order Context:          Notification Context:
- Book (ad, fiyat,        - Order (müşteri,       - Email template
  stok)                     sipariş kalemleri,    - Gönderim kuyruğu
- Author                    durum)
- Category                - OrderItem
                          - Payment
```

`Book` kavramı Catalog Context'te stok/fiyat/metadata içerir. Order Context'te sadece `BookId + fiyat snapshot` var — sipariş anındaki fiyat değişmemeli, bu yüzden kopya tutulur.

**Kural:** Her Bounded Context → bir mikroservis. Her servisin **kendi DB'si** var. Başka servisin tablosuna doğrudan erişim yasak.

---

### Faz4 ile Karşılaştırma

Faz4'te tek binary üzerinde profiling, memory optimizasyonu, observability kurduk. Tüm bu altyapı servis bazında tekrarlanacak:

| Faz4 (monolith) | Faz5 (mikroservis) |
|---|---|
| Tek pprof endpoint | Her serviste ayrı `/metrics` |
| Tek PostgreSQL bağlantı havuzu | Her servisin kendi havuzu |
| Tek Dockerfile | Her servis için ayrı Dockerfile |
| Tek slog logger | Trace ID ile servisler arası korelasyon |
| Tek graceful shutdown | Her serviste bağımsız shutdown |

---

### 500 vs 50K Kullanıcı

| Karar | 500 kullanıcı/ay | 50K kullanıcı/ay |
|-------|-----------------|-----------------|
| Monolith mi mikroservis mi? | ✅ Monolith — mikroservis overkill | ⚠️ Ölçekleme ihtiyacına göre karar ver |
| Strangler Fig | ❌ Gerekmez | ✅ Varolan sistemi parçalamak için |
| Database per service | ❌ Tek DB yeterli | ✅ Bağımsız ölçekleme için gerekli |
| API Gateway | ❌ Overkill | ✅ Tek giriş noktası zorunlu |
| Distributed tracing | ❌ Tek log yeterli | ✅ Servisler arası debug için zorunlu |

---

### Kontrol Soruları

1. Distributed monolith neden "en kötü dünya"? Hem monolith hem mikroservis dezavantajlarını somut örnek üzerinden açıkla.
2. Strangler Fig pattern hangi riski önler? "Big bang" mikroservis dönüşümünden farkı ne?
3. CatalogService'teki `Book` ile OrderService'teki `Book` neden farklı yapıdadır? Bu fark hangi problemi çözer?
4. Martin Fowler'ın prerequisites listesinde monitoring neden "hızlı deployment"dan önce gelir?
5. "Her Bounded Context bir mikroservis" kuralı her zaman geçerli mi? Ne zaman iki bounded context tek serviste birleştirilebilir?
