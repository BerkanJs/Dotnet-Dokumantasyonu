# Gün 121 — Servisler Arası İletişim

## Teorik

### Önce Problemi Anlayalım

Faz3'te tüm kod tek bir uygulamaydı. Sipariş oluştururken catalog, order, email hepsi aynı process'teydi:

```csharp
// Faz3 — her şey aynı yerde, doğrudan method çağrısı
public async Task CreateOrderAsync(CreateOrderCommand cmd)
{
    var book = await _catalogRepo.GetAsync(cmd.BookId);   // aynı DB
    book.ReduceStock(cmd.Quantity);
    var order = Order.Create(cmd, book.Price);
    await _orderRepo.AddAsync(order);
    await _emailService.SendAsync(order.CustomerEmail);   // aynı process
    await _unitOfWork.SaveChangesAsync();
}
```

Faz5'te bu üç şey artık farklı bilgisayarlarda (container'larda) çalışıyor:

```
[CatalogService]    [OrderService]    [NotificationService]
  kendi DB'si         kendi DB'si         kendi DB'si
  :5001 portu         :5002 portu         :5003 portu
```

`OrderService` içinden `_catalogRepo.GetAsync()` diyemezsin — o kod başka bir bilgisayarda. Nasıl haberleşeceksin? İşte bu günün konusu bu.

---

### İki Temel Yol: Telefon mu, Mektup mu?

**Senkron iletişim = Telefon görüşmesi**

Ararsın, karşı tarafın açmasını beklersin, konuşursun, kaparsın. Cevap gelene kadar sen bekledesindir.

```
OrderService ────────── "Bu kitabın fiyatı ne?" ──────────► CatalogService
             ◄───────────── "149 TL" ─────────────────────
(OrderService bekledi, cevabı aldı, devam etti)
```

**Asenkron iletişim = Mesaj atmak**

"Sipariş oluşturuldu" diye bir mesaj atarsın ve devam edersin. Karşı taraf o mesajı ne zaman okuyacak bilmezsin, bilmen de gerekmez.

```
OrderService ── "Sipariş #42 oluşturuldu" mesajını bırakır ──► [Kuyruk]
(OrderService devam eder, işi bitti)

                                    [Kuyruk] ──► NotificationService
                                                 (mesajı okur, email gönderir)
```

---

### Senkron: REST ve gRPC

**REST** — zaten biliyorsun, Faz2'de yazdık. HTTP + JSON.

```
GET /api/books/42
→ {"id": 42, "title": "Clean Code", "price": 149.90}
```

Kullanıcıya açık API'ler için standart. Human-readable, Swagger ile dokümante edilebilir.

**gRPC** — REST'e benzer ama farklı bir protokol. HTTP/2 üzerinde çalışır, JSON yerine binary (Protocol Buffers) kullanır.

Neden binary? Düşün: `{"price": 149.90}` JSON olarak 15 byte. Aynı veri binary'de 5-6 byte. Binlerce servis çağrısı olduğunda bu fark önemli.

```
Kural:
  Kullanıcı ↔ API       → REST   (tarayıcı, mobil app)
  Servis ↔ Servis        → gRPC   (dahili iletişim, hız önemli)
```

Kitabevi'mizde:
- Kullanıcı → ApiGateway: REST
- ApiGateway → CatalogService: REST (gateway kullanıcıya yakın)
- OrderService → CatalogService: gRPC (servisler arası, dahili)

---

### Asenkron: Message Broker

Servisler direkt birbirini çağırmak yerine aralarına bir **aracı** koyar. Bu aracıya **message broker** denir. RabbitMQ, Kafka bunun örnekleri.

**Analoji: Ofis panosu**

Düşün bir ofis panosun var. Herkes oraya not bırakabilir, herkes not okuyabilir.

```
[Pano = RabbitMQ]

OrderService not bırakır:  "Sipariş #42, Ali, ali@mail.com, 2 kitap"
                                      ↓
NotificationService notu okur: "Ali'ye email at"
CatalogService notu okur:      "Stoku 2 düşür"
AnalyticsService notu okur:    "Satış raporunu güncelle"
```

OrderService bu servislerin hiçbirini tanımaz, nerede olduklarını bilmez. Sadece panoya not bırakır.

**Avantaj 1: Kopukluk toleransı**

NotificationService çöktü diyelim. OrderService senkron çağırsaydı, o da çökerdi. Ama mesaj broker varsa:

```
OrderService → pano ya not bırakır → devam eder (sipariş oluştu ✅)

NotificationService 10 dakika sonra yeniden başlar
→ panodaki notu okur → email gönderir ✅

Kullanıcı siparişini verdi, email biraz geç geldi ama sipariş kaydı
```

**Avantaj 2: Bağımsız ölçekleme**

Email yoğunlaştı, kuyrukta 10.000 mesaj birikti. NotificationService'i 5 instance'a çıkarırsın. OrderService'e dokunmadan.

---

### Command vs Event — İki Farklı Mesaj Türü

Broker üzerinden iki farklı türde mesaj gönderilebilir. Farkı anlamak önemli.

**Command (Komut) — "Şunu yap"**

Belirli bir servise, belirli bir iş yaptırıyorsun. Tek alıcı var. Emir kipiyle isimlendirilir.

```csharp
// "NotificationService, şu emaili gönder" diyorsun
public record SendOrderConfirmationEmailCommand(
    string To,
    string OrderId,
    decimal TotalAmount
);

await _bus.Send(new SendOrderConfirmationEmailCommand(
    To: "ali@mail.com",
    OrderId: "42",
    TotalAmount: 299.80m
));
// Tek alıcı: NotificationService
```

**Event (Olay) — "Bu oldu, isteyen dinlesin"**

Bir şeyin gerçekleştiğini duyuruyorsun. Kimin dinleyeceğini bilmiyorsun, bilmen de gerekmiyor. Geçmiş zamanda isimlendirilir.

```csharp
// "Sipariş oluşturuldu" duyurusu — kim dinliyorsa dinlesin
public record OrderCreatedEvent(
    Guid OrderId,
    string CustomerEmail,
    Guid BookId,
    int Quantity,
    decimal TotalAmount,
    DateTime CreatedAt
);

await _bus.Publish(new OrderCreatedEvent(
    OrderId: order.Id,
    CustomerEmail: order.CustomerEmail,
    BookId: cmd.BookId,
    Quantity: cmd.Quantity,
    TotalAmount: order.TotalAmount,
    CreatedAt: DateTime.UtcNow
));
// NotificationService dinler → email gönderir
// CatalogService dinler → stok düşürür
// AnalyticsService dinler → rapor günceller
// OrderService bunların hiçbirini bilmez
```

**Kural: Ne zaman hangisi?**

```
Command → "Kim yapacağını biliyorum, ona direkt söylüyorum"
          Örnek: sipariş iptal edildi, iade sürecini başlat

Event   → "Bir şey oldu, ilgilenen servise duyuruyorum"
          Örnek: sipariş oluştu, kim ne yaparsa yapsın
```

---

### Request-Reply Pattern

"Async ama cevap lazım" durumu. Hem broker kullanmak istiyorsun (doğrudan HTTP bağımlılığı yok) hem de sonucu bilmen gerekiyor.

**Analoji: Resmi yazışma**

Dilekçe yazıp kuruma gönderirsin (async). Dilekçene referans numarası yazarsın. Kurum sana aynı referans numarasıyla cevap yazar. Cevabı beklersin ama telefon açmadın.

```
OrderService                   RabbitMQ              PaymentService
     │                             │                       │
     │─── "Ödeme al" ─────────────►│─── "Ödeme al" ───────►│
     │    correlationId: "abc-123" │                       │ işler
     │                             │◄── "Sonuç" ───────────│
     │◄─── "Başarılı/Başarısız" ───│    correlationId:     │
     │     correlationId: "abc-123"│    "abc-123"          │
     │ (kendi cevabını buldu)
```

MassTransit bu eşleştirmeyi otomatik yapar.

**Ne zaman kullanılır:** Senkron HTTP istemiyorum ama sonuç bilmem gerekiyor. Ödeme gibi kritik işlemlerde.

---

### Fire and Forget

"Gönder, unut." Sonucu bilmene gerek yok.

```csharp
// Sipariş oluşturuldu, email bildirimi gönder.
// Email gidip gitmediğiyle ilgilenmiyoruz — sipariş kaydedildi ya.
await _bus.Publish(new OrderCreatedEvent(...));

return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
// Kullanıcıya anında 201 Created dönüldü.
// Email arka planda gönderilecek.
```

**Ne zaman kullanılır:** Email, SMS, push notification, log, analytics güncellemesi. Başarısız olsa da kullanıcı deneyimi bozulmamalı.

**Dikkat:** "Fire and forget" ile mesajı kaybetmek aynı şey değil. Mesaj broker'da kalır, servis yeniden başlayınca işlenir. Gerçekten "gönder unut" değil, "gönder, sonucunu izleme."

---

### Choreography vs Orchestration

Sipariş akışı şöyle olsun: stok rezerve → ödeme al → kargo oluştur → email gönder. Bunu koordine etmenin iki yolu var.

**Choreography (Koreografi) — Herkes kendi adımını bilir**

Analoji: Düğün dansı. Kimse kimseye "şimdi sağa dön" demiyor. Herkes müziği duyuyor, kendi adımını atıyor, zincir ilerliyor.

```
OrderService        CatalogService      PaymentService      NotificationService
     │                   │                   │                    │
     │── OrderCreated ──►│                   │                    │
     │                   │ stok rezerve eder │                    │
     │                   │── StockReserved ─►│                    │
     │                   │                   │ ödeme alır         │
     │                   │                   │── PaymentDone ────►│
     │                   │                   │                    │ email gönderir
```

Her servis bir öncekinin event'ini dinler, kendi işini yapar, kendi event'ini yayar.

✅ Servisler birbirini tanımaz — gevşek bağlı  
✅ Yeni adım eklemek kolay — yeni servis event'i dinlemeye başlar  
❌ "Sipariş nerede takıldı?" sorusu zor — akış dağınık  
❌ Test etmek zor — tüm servisleri ayağa kaldırmak gerekir

**Orchestration — Bir koordinatör yönetir**

Analoji: Orkestra şefi. Herkes ayrı çalar ama şef kimin ne zaman çalacağını yönetir.

```
OrderService (Saga Orchestrator)
     │
     ├──► CatalogService.ReserveStock()     → ✅ başarılı
     ├──► PaymentService.ChargePayment()    → ✅ başarılı
     ├──► ShippingService.CreateShipment()  → ✅ başarılı
     └──► NotificationService.SendEmail()   → ✅ başarılı
```

Tüm akış OrderService'in içinde görünür. Her adım nerede, ne durumda — hepsi tek yerden izlenebilir.

✅ Debug kolay — akış tek yerde  
✅ Hata yönetimi açık — hangi adım başarısız oldu?  
❌ Orchestrator merkezi bağımlılık noktası  
❌ Servisler arasına coupling girer

```
Kitabevi'mizde:
  Email bildirimi    → Choreography (OrderCreated event, fire-and-forget)
  Sipariş akışı      → Orchestration Saga (Gün 128'de yazacağız)
```

---

### Faz3'ten Faz5'e: Ne Değişti?

```csharp
// Faz3 — monolith, tek method çağrısı, tek transaction
public async Task CreateOrderAsync(CreateOrderCommand cmd)
{
    var book = await _catalogRepo.GetAsync(cmd.BookId);  // direkt DB
    book.ReduceStock(cmd.Quantity);
    var order = Order.Create(cmd, book.Price);
    await _orderRepo.AddAsync(order);
    await _emailService.SendAsync(order.CustomerEmail);  // direkt method
    await _unitOfWork.SaveChangesAsync();                // tek commit
}

// Faz5 — mikroservis, ağ üzerinden iletişim
public async Task CreateOrderAsync(CreateOrderCommand cmd)
{
    // HTTP/gRPC ile farklı servise sor
    var bookInfo = await _catalogClient.GetBookAsync(cmd.BookId);

    var order = Order.Create(cmd, bookInfo.Price);
    await _orderRepo.AddAsync(order);
    await _unitOfWork.SaveChangesAsync();                // sadece kendi DB'si

    // Mesaj broker üzerinden duyur — stok düşürme + email async yapılacak
    await _bus.Publish(new OrderCreatedEvent(order.Id, bookInfo.Id, cmd.Quantity));
}
```

**Fark:** Artık tek transaction yok. Sipariş kaydedildi ama stok henüz düşürülmedi. Ödeme alınmadı. Bu "yarım kalmış" durumu nasıl yönetiriz? Cevap: **Saga pattern** (Gün 128).

---

### 500 vs 50K Kullanıcı

| İletişim türü | 500 kullanıcı/ay | 50K kullanıcı/ay |
|---------------|-----------------|-----------------|
| REST (servisler arası) | ✅ Yeterli | ✅ Yeterli |
| gRPC (servisler arası) | ⚠️ Opsiyonel | ✅ Yüksek throughput için düşün |
| Message broker | ❌ Overkill — direkt HTTP yeter | ✅ Async işler için gerekli |
| Choreography | ❌ Overkill | ✅ Gevşek bağlı event akışı için |
| Orchestration Saga | ❌ Overkill | ✅ Kritik iş akışları için |
| Fire-and-forget event | ⚠️ Basit async için | ✅ Bildirim, log, analytics |

---

### Kontrol Soruları

1. Telefon analojisiyle senkron/asenkron farkını kendi cümleylerinle açıkla. Hangi durumlarda "mektup atmak" telefon açmaktan daha iyi?
2. `OrderCreatedEvent` neden command değil event'tir? Aynı mesajı `SendConfirmationEmailCommand` olarak yeniden tasarlarsan ne değişir, ne kaybedersin?
3. NotificationService çöktü. Fire-and-forget yaklaşımıyla gönderilen mesajlar kaybolur mu? Neden?
4. Choreography'de sipariş akışı 3. adımda takıldı. Hangi servis takıldığını nasıl anlarsın? Orchestration'da bu soruyu nasıl cevaplarsın?
5. Faz3'teki tek transaction artık yok. Sipariş kaydedildi ama stok düşürme mesajı broker'a ulaşamadı. Bu durumda ne olur? (İpucu: Gün 127 Outbox Pattern)
