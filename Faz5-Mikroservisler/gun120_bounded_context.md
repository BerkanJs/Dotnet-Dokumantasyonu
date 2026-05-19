# Gün 120 — Bounded Context ve Servis Sınırları

## Teorik

### Bounded Context Nedir?

DDD (Domain-Driven Design) strategic design'ın temel kavramı. Bir domain modelinin **tutarlı ve net anlam taşıdığı sınır**.

Aynı kelime farklı context'lerde farklı şey demek olabilir:

```
"Kitap" — CatalogContext'te:        "Kitap" — OrderContext'te:
  - ISBN                               - BookId (foreign key gibi)
  - Ad, Açıklama                       - Sipariş anındaki fiyat snapshot
  - Yazar, Kategori                    - Adet
  - Stok miktarı                       (güncel fiyat değişse de sipariş fiyatı sabit)
  - Kapak resmi
  - Aktif/Pasif durumu
```

Sipariş verildiğinde kitabın fiyatı 100 TL'dir. Ertesi gün fiyat 120 TL olur. Siparişin hâlâ 100 TL göstermesi gerekir. Bu yüzden OrderContext kendi `BookSnapshot`'ını tutar — CatalogService'e her seferinde sormaz.

---

### Context Map — Bounded Context'ler Arası İlişki

Kitabevi'mizdeki context'ler ve aralarındaki ilişki:

```
┌─────────────────┐         ┌─────────────────┐
│  CatalogContext │         │  OrderContext    │
│                 │         │                 │
│  Book           │◄────────│  BookSnapshot   │
│  Author         │  fiyat  │  Order          │
│  Category       │  snapshot│  OrderItem      │
│  Stock          │         │  Payment        │
└─────────────────┘         └────────┬────────┘
                                     │ sipariş tamamlandı event'i
                             ┌───────▼────────┐
                             │ Notification   │
                             │ Context        │
                             │                │
                             │ EmailTemplate  │
                             │ SendQueue      │
                             └────────────────┘
```

---

### Context Map İlişki Türleri

**Shared Kernel**
İki context ortak bir model paylaşır. Değişiklik her ikisini etkiler, koordinasyon zorunlu.
```
// İKİ serviste de aynı PaymentMethod enum kullanılıyor
// Biri değişince diğeri de değişmek zorunda — riskli
public enum PaymentMethod { CreditCard, BankTransfer }
```
Kitabevi'mizde **kullanmıyoruz** — servis bağımsızlığını bozar.

**Customer-Supplier**
Upstream servis (supplier) downstream servisin (customer) ihtiyaçlarını karşılar.
```
CatalogService (supplier) → OrderService (customer)
OrderService kitap fiyatını CatalogService'ten alır
CatalogService API'sini değiştirince OrderService etkilenir
```

**Conformist**
Downstream, upstream'in modelini olduğu gibi kabul eder — müzakere yok.
```
// Ödeme sağlayıcısı (Iyzico) kendi modelini dikte eder
// OrderService onun response'unu olduğu gibi kullanır
var paymentResponse = await _iyzicoClient.ProcessAsync(request);
```

**Anticorruption Layer (ACL)**
Dış sistemin modeli domain modeline kirletmeden çevrilir.
```
// Iyzico'nun PaymentResult'ı bizim domain modelimize dönüştürülür
// OrderService, Iyzico detaylarını bilmez
public PaymentResult TodomainResult(IyzicoResponse response)
{
    return new PaymentResult(
        Success: response.status == "success",
        TransactionId: response.paymentId
    );
}
```
Kitabevi'mizde dış ödeme entegrasyonunda **ACL kullanacağız**.

---

### Servis Sınırı Nasıl Çizilir?

**"Yalnız deploy edilebilir" testi:**

> Bu servisi diğer servisler olmadan deploy edebilir miyim?

- CatalogService deploy edildi, OrderService çalışmıyor → Kitap listeleme çalışmalı ✅
- OrderService deploy edildi, NotificationService çalışmıyor → Sipariş oluşturma çalışmalı ✅
- OrderService deploy edildi, CatalogService çalışmıyor → Yeni sipariş oluşturma bozulur ⚠️ (fiyat alınamaz)

Son durum kabul edilebilir — **fault isolation** sağlandı, servisler tamamen birbirinden bağımsız değil ama birinin çökmesi diğerini tamamen çökertmiyor.

**Yanlış sınır çizme örnekleri:**

```
❌ Çok ince granülerlik:
   UserNameService  →  sadece kullanıcı adını yönetir
   UserEmailService →  sadece email yönetir
   → Gereksiz network overhead, deployment karmaşıklığı

❌ Çok kaba granülerlik:
   BackendService → tüm iş mantığı tek serviste
   → Mikroservisin hiçbir avantajı yok, dağıtık monolith

✅ Doğru: Bounded Context = Servis
   CatalogService  →  kitap kataloğu, stok yönetimi
   OrderService    →  sipariş, ödeme
   NotificationService → bildirim gönderimi
```

---

### Veri Sahipliği — Database per Service Pattern

Her servis **sadece kendi DB'sine** yazar ve okur. Başka servisin DB'sine doğrudan erişim yasak.

```
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│  CatalogService  │    │  OrderService    │    │NotificationService│
│                  │    │                  │    │                  │
│  catalog_db      │    │  order_db        │    │  notification_db │
│  ┌────────────┐  │    │  ┌────────────┐  │    │  ┌────────────┐  │
│  │   books    │  │    │  │  orders    │  │    │  │   emails   │  │
│  │  authors   │  │    │  │order_items │  │    │  │  templates │  │
│  │categories  │  │    │  │ payments   │  │    │  └────────────┘  │
│  └────────────┘  │    │  └────────────┘  │    │                  │
└──────────────────┘    └──────────────────┘    └──────────────────┘
```

**OrderService neden `books` tablosuna JOIN yapamaz?**

1. CatalogService DB şeması değişirse (kolon adı, tip) OrderService compile zamanında değil çalışma zamanında bozulur
2. CatalogService farklı DB teknolojisine geçerse (PostgreSQL → MongoDB) OrderService çöker
3. CatalogService kendi tablosunu kilitleyince OrderService de kilitlenir

---

### Shared Database Antipattern

```
// ❌ Yanlış — OrderService, CatalogService'in tablosuna doğrudan erişiyor
public async Task CreateOrderAsync(CreateOrderCommand cmd)
{
    // OrderService içinde catalog_db.books tablosuna sorgu atılıyor
    var book = await _catalogDb.Books
        .FirstOrDefaultAsync(b => b.Id == cmd.BookId);

    // stok düşürme de OrderService içinde yapılıyor
    book.Stock--;
    await _catalogDb.SaveChangesAsync();
}
```

Bu yaklaşım:
- CatalogService bypass ediliyor — iş kuralları (stok negatife düşebilir mi?) uygulanmıyor
- CatalogService ve OrderService aynı anda deploy edilmek zorunda
- Schema migration koordinasyonu gerekiyor

```
// ✅ Doğru — OrderService, CatalogService API'sini çağırıyor
public async Task CreateOrderAsync(CreateOrderCommand cmd)
{
    // CatalogService'e HTTP veya mesaj ile sor
    var bookInfo = await _catalogClient.GetBookAsync(cmd.BookId);

    // Stok düşürmeyi de CatalogService yapacak — kendi iş kuralıyla
    await _catalogClient.ReserveStockAsync(cmd.BookId, cmd.Quantity);
}
```

---

### Faz3 ile Karşılaştırma

Faz3'te Onion Architecture'da da katmanlar arası bağımlılık kuralı vardı:

```
Faz3 — Onion (tek uygulama):          Faz5 — Mikroservis:
  Domain → hiçbir şeye bağımlı değil    CatalogService → kendi DB'sine sahip
  Application → sadece Domain'e          OrderService → kendi DB'sine sahip
  Infrastructure → Application'a         Servisler arası: API veya mesaj
  
  Tek process içi kural                  Network üzerinde kural
  Compile-time kontrol                   Runtime kontrol (API sözleşmesi)
```

Onion'da "Infrastructure katmanı Domain'i import edemez" kuralı ne kadar katıysa, mikroserviste de "Servis başka servisin DB'sine erişemez" kuralı o kadar katı.

---

### 500 vs 50K Kullanıcı

| Karar | 500 kullanıcı/ay | 50K kullanıcı/ay |
|-------|-----------------|-----------------|
| Bounded context analizi | ✅ Yapılmalı — monolith modül sınırları için de gerekli | ✅ Zorunlu |
| Database per service | ❌ Tek DB yeterli | ✅ Bağımsız ölçekleme/deploy için |
| Anticorruption Layer | ✅ Dış servis entegrasyonunda her zaman | ✅ Her zaman |
| Shared Kernel | ❌ Kaçın | ❌ Kaçın |
| Context Map çizmek | ✅ Mimari netlik için | ✅ Takım koordinasyonu için zorunlu |

---

### Kontrol Soruları

1. OrderContext'teki `BookSnapshot` neden CatalogContext'teki `Book`'tan farklı? Bu farkı ortadan kaldırsak ne olur?
2. "Yalnız deploy edilebilir" testini Kitabevi'nin 3 servisi için uygula — hangi servis gerçekten bağımsız, hangi servis bağımlı?
3. Shared Kernel neden iki servis arasında tehlikelidir? Hangi koşulda kabul edilebilir?
4. OrderService neden `books` tablosuna doğrudan JOIN yapmamalı? Bir JOIN'in sağlayacağı performansı başka nasıl elde edersin?
5. Anticorruption Layer olmadan dış ödeme sağlayıcısını entegre etsen ne olur? ACL hangi değişiklikten seni korur?
