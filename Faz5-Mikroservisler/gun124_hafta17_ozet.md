# Gün 124 — Hafta 17 Özeti

## Bu Hafta Ne Öğrendik?

| Gün | Konu | Ana Mesaj |
|-----|------|-----------|
| 119 | Monolith → Microservice | Mikroservis bir araç, hedef değil. Hazır değilsen monolith daha iyi |
| 120 | Bounded Context | Her servisin kendi verisi, kendi DB'si. Başka servisin tablosuna dokunma |
| 121 | Servisler Arası İletişim | Sync (REST/gRPC) vs Async (mesaj broker). Command vs Event farkı |
| 122 | API Gateway & YARP | Tek giriş noktası. Reverse proxy servisini gizler. İş mantığı gateway'de olmaz |
| 123 | Service Discovery | DNS-based yeterli. Docker Compose ismi IP'ye çevirir, Kubernetes da öyle |

---

## Mimari Soru: Kitabevi'nde 3 Bounded Context

Hafta boyunca teorisini gördük. Şimdi Kitabevi'ne uygulayalım.

**3 Bounded Context ve servis sınırları:**

```
┌─────────────────────────────────────────────────────┐
│                   API Gateway                        │
│              YARP — :8080                           │
└──────────┬──────────────┬──────────────┬────────────┘
           │              │              │
    /api/catalog    /api/orders   /api/notify
           │              │              │
    ┌──────▼──────┐ ┌─────▼──────┐ ┌────▼────────────┐
    │CatalogService│ │OrderService│ │NotificationSvc  │
    │             │ │            │ │                 │
    │ - Book      │ │ - Order    │ │ - EmailTemplate │
    │ - Author    │ │ - OrderItem│ │ - SendQueue     │
    │ - Category  │ │ - Payment  │ │                 │
    │ - Stock     │ │            │ │                 │
    └──────┬──────┘ └─────┬──────┘ └────┬────────────┘
           │              │              │
      catalog_db      order_db     notification_db
```

---

## Mimari Soru: Hangi İletişim Türü?

**Senaryo:** Sipariş oluşturuldu → stok düşürüldü → email gönderildi.

Her adımda hangi iletişim türü kullanılmalı? Neden?

```
1. Kullanıcı sipariş veriyor:
   Kullanıcı ──► OrderService       → REST (sync)
   Kullanıcı anında "sipariş alındı" görmek istiyor

2. Sipariş oluştururken kitap fiyatını öğrenmek:
   OrderService ──► CatalogService  → REST veya gRPC (sync)
   Fiyat bilmeden sipariş oluşturamam, sonuç lazım

3. Sipariş oluştuktan sonra stok düşürme:
   OrderService ──► [RabbitMQ]      → Event (async, fire-and-forget değil)
   OrderCreatedEvent yayılır
   CatalogService dinler, stok düşürür
   Hata olursa Saga devreye girer (Gün 128)

4. Email bildirimi:
   OrderService ──► [RabbitMQ]      → Event (async, fire-and-forget)
   OrderCreatedEvent dinler
   NotificationService email gönderir
   Email biraz geç gitse de sipariş etkilenmez
```

---

## Hafta Boyunca Öğrenilen Kararlar

**"Distributed monolith" tuzağından nasıl kaçınılır?**
- Her servisin kendi DB'si — başka servisin tablosuna JOIN yok
- Servisler birbirinin iç detayını bilmez — sadece public API veya event

**"Yalnız deploy edilebilir" testi:**
- CatalogService deploy, OrderService kapalı → kitap listeleme çalışır ✅
- OrderService deploy, NotificationService kapalı → sipariş oluşturulur ✅
- Email biraz geç gönderilir ama sistem çalışır ✅

**Gateway antipattern:**
- İndirim hesaplama gateway'de olmaz
- İş kuralı değişince gateway deploy edilmek zorunda kalınmaz
- Gateway sadece: token doğrula, yönlendir, logla

---

## Hafta 18'e Hazırlık

Hafta 17 teoriydi. Hafta 18'de elleri kirletiyoruz:

| Gün | Konu | Ne yazacağız |
|-----|------|-------------|
| 125 | RabbitMQ | Servis iskeletleri + docker-compose + ilk mesaj |
| 126 | Kafka | Kafka entegrasyonu |
| 127 | Outbox Pattern | OrderService'e Outbox eklenir |
| 128 | Saga Pattern | Sipariş akışı state machine |
| 129 | Hafta 18 özet | Senaryo: sipariş akışı uçtan uca |

---

## Kontrol Soruları

1. CatalogService'teki `Book` ile OrderService'teki `BookSnapshot` neden farklı struct? Bu farkı ortadan kaldırsan ne olur?
2. `OrderCreated` neden command değil event? Command olsaydı OrderService ne bilmek zorunda kalırdı?
3. Gateway'e indirim hesaplama mantığı girerse hangi sorunlar çıkar? Bunu hangi servise taşımalısın?
4. Sipariş akışında stok düşürme sync mi async mı olmalı? Kullanıcı "stok yok" hatasını ne zaman görmeli?
5. CatalogService çöktü. Kullanıcı sipariş vermeye çalışıyor. Ne olur? Bunu nasıl düzeltirsin? (İpucu: Gün 130 Circuit Breaker)
