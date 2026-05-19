# Gün 72 — Hafta 10 Özet ve Faz 3 Kapanış

Bu gün teorik ders değil — Faz 3 boyunca öğrendiklerini tek bir büyük tasarım sorusunda bir araya getiriyorsun.

---

## Hafta 10 Hızlı Özet

| Gün | Konu | Tek Cümle |
|---|---|---|
| 68 | Unit of Work | DbContext zaten UoW'dir; explicit interface test edilebilirlik için yazılır |
| 69 | Validation Stratejileri | DataAnnotations format için, FluentValidation iş kuralı için, Guard clause domain invariant için |
| 70 | Vertical Slice | Özellik bazlı organizasyon — her slice kendi içinde tam |
| 71 | Modüler Monolith | Tek deployment, modül sınırları çizilmiş — mikroservise geçişin ön adımı |

---

## Faz 3 Genel Özet

| Hafta | Konular |
|---|---|
| Hafta 7 | Anti-pattern'lar, DDD taktiksel (Entity, Value Object, Aggregate) |
| Hafta 8 | Onion Architecture, CQRS, MediatR |
| Hafta 9 | MediatR Pipeline Behaviors, Result Pattern, Event-Driven, Specification |
| Hafta 10 | Unit of Work, Validation, Vertical Slice, Modüler Monolith |

---

## Büyük Mimari Soru — E-Ticaret Platformu Tasarla

Aşağıdaki senaryo için mimari kararlarını ver ve her kararı gerekçelendir.

**Senaryo:**  
Orta ölçekli bir e-ticaret platformu kuruyorsun. Müşteriler ürünlere göz atıyor, sipariş veriyor, ödeme yapıyor. Satıcılar ürün ekliyor ve stok takibi yapıyor. Ekip şu an 8 kişi.

---

### 1. Onion Architecture Katmanları

```
┌─────────────────────────────────────┐
│            API Katmanı              │  HTTP endpoint'leri, controller/minimal API
├─────────────────────────────────────┤
│        Application Katmanı          │  Command, Query, Handler, Validator, DTO
├─────────────────────────────────────┤
│          Domain Katmanı             │  Entity, Value Object, Aggregate, Domain Event
├─────────────────────────────────────┤
│       Infrastructure Katmanı        │  DbContext, Repository impl, harici servis
└─────────────────────────────────────┘
```

**Hangi sınıf nerede durur?**

| Sınıf | Katman | Neden? |
|---|---|---|
| `Siparis` entity | Domain | İş kurallarını taşıyor, DB'den bağımsız |
| `SiparisOlusturCommand` | Application | Kullanıcı niyetini taşıyor, iş kuralı değil |
| `SiparisOlusturHandler` | Application | Orchestration — domain + infrastructure koordine eder |
| `EfCoreSiparisRepository` | Infrastructure | EF Core detayı — domain bunu bilmemeli |
| `ISiparisRepository` | Domain | Domain bağımlılığı tersine çeviriyor |
| `SiparisController` | API | HTTP'ye özgü — application'a delege eder |
| `StripePlasimaServisi` | Infrastructure | Harici servis — domain bağımsız kalmalı |
| `IOdemeServisi` | Application | Application katmanı harici servisi interface üzerinden görür |

---

### 2. CQRS — Command mi, Query mi?

**Komutlar (Command) — veri değiştirir:**

```
SiparisOlusturCommand      → yeni sipariş kaydı
OdemeAlCommand             → ödeme işlemi başlat
UrunStokGuncelleCommand    → stok değiştir
SiparisIptalEtCommand      → sipariş durumu değişir
UrunEkleCommand            → yeni ürün kaydı
```

**Sorgular (Query) — sadece okur:**

```
UrunListeleQuery            → filtrelenmiş ürün listesi
SiparisDetayQuery           → sipariş + kalemler + durum
MusterisiparisGecmisiQuery  → müşteriye ait tüm siparişler
UrunAramaQuery              → metin bazlı arama
```

**Neden ayrı?**  
→ Query'ler `AsNoTracking()` kullanır — performans kazanımı.  
→ Command'lar domain logic çalıştırır, transaction yönetir.  
→ İleride Query tarafını ayrı okuma veritabanına (read replica) taşıyabilirsin.

---

### 3. Domain Event'ler — Nerede, Neden?

**Hangi olaylar domain event yayınlar?**

```csharp
// Siparis aggregate
SiparisOlusturulduEvent     → tetikler: stok rezerve et, bildirim gönder, analitik kaydet
SiparisIptalEdildiEvent     → tetikler: stok serbest bırak, iade başlat
OdemeAlindi Event           → tetikler: kargo süreci başlat, fatura oluştur

// Urun aggregate
StokTukendi Event           → tetikler: satıcıya uyarı gönder, ürünü pasif et
```

**Neden domain event?**  
→ `SiparisOlusturHandler` içine "stok düş + bildirim gönder + analitik kaydet" yazmak handler'ı şişirir.  
→ Her dinleyici (subscriber) kendi sorumluluğunu taşır, handler sadece siparişi oluşturur.  
→ Yeni iş kuralı gelince handler'a dokunmazsın, yeni subscriber yazarsın.

---

### 4. Aggregate'ler — Sınırlar Nerede Çizilir?

**Aggregate 1: Siparis**
```
Siparis (Aggregate Root)
  └── SiparisKalemi (Entity — Siparis dışında anlamsız)
  └── KargoAdresi (Value Object — değişmez, eşitlik değere göre)
  └── OdemeBilgisi (Value Object)
```

**Aggregate 2: Urun**
```
Urun (Aggregate Root)
  └── UrunResmi (Entity)
  └── Fiyat (Value Object — para birimi + miktar birlikte anlamlı)
  └── StokAdedi (primitive — basit sayı yeterli)
```

**Aggregate 3: Musteri**
```
Musteri (Aggregate Root)
  └── Adres (Value Object)
  └── IletisimBilgisi (Value Object)
```

**Neden bu sınırlar?**  
→ `SiparisKalemi`, `Siparis` olmadan var olamaz — aynı aggregate.  
→ `Urun` ve `Siparis` ayrı aggregate — sipariş oluştururken ürün entity'sini lock'lamak gerekmez, sadece o anki fiyatı snapshotla.  
→ `Musteri` ayrı aggregate — sipariş müşteri aggregate'ini direkt değiştirmez.

**Aggregate'ler arası referans:**  
```csharp
public class SiparisKalemi
{
    public int UrunId { get; private set; }     // ID ile referans — entity değil
    public string UrunAdi { get; private set; } // snapshot — Urun değişse bile sipariş etkilenmez
    public decimal BirimFiyat { get; private set; }
}
```

---

### 5. Repository Interface'leri Hangi Katmanda?

```
Domain katmanı:        ISiparisRepository, IUrunRepository, IMusteriRepository
Infrastructure katmanı: EfCoreSiparisRepository : ISiparisRepository
```

**Neden domain'de?**  
→ Dependency Inversion — domain dışarıya bağımlı değil, dışarısı domain'e bağımlı.  
→ Domain "veritabanına ihtiyacım var" demiyor, "bir sipariş deposuna ihtiyacım var" diyor.  
→ Test sırasında InMemory veya fake repository geçilebilir.

---

### 6. Vertical Slice Bu Domain İçin Uygun Olur muydu?

**Kısmen uygun — ama Onion daha iyi seçim.**

Neden Vertical Slice tam oturmaz:
- `Siparis` aggregate'i hem "Sipariş Oluştur" hem "Sipariş İptal" hem "Ödeme Al" slice'larında kullanılıyor → paylaşılan domain logic var.
- Stok kuralları birden fazla özellikte tekrar ediyor — merkezi domain olmadan bu kurallar çoğalır.
- Validation kuralları (Guard clause) entity'de yaşaması gerekiyor, slice'a dağıtılamaz.

Vertical Slice ne zaman oturur:
- CRUD ağırlıklı, özellikler gerçekten bağımsızsa (örn. blog platformu).
- Domain logic yoksa veya çok basitse.

**Uzlaşma:** Modüler Monolith içinde her modül Vertical Slice kullanabilir, ama modül içinde Domain katmanı korunur.

---

### 7. Modüler Monolith Olarak Tasarlasaydın Ne Değişirdi?

```
Modules/
  Katalog/           ← Urun, Stok, Kategori
  Siparis/           ← Siparis, SiparisKalemi, KargoSureci
  Odeme/             ← OdemeIslemi, Fatura
  Musteri/           ← Musteri, Adres, Bildirim
  Shared/            ← ortak altyapı
```

**Ne değişir:**
- Her modül kendi `DbContext`'i ve schema'sıyla izole çalışır (`katalog.Urunler`, `siparis.Siparisler`).
- `SiparisOlusturHandler` ürün bilgisini `IKatalogService` üzerinden alır — `Katalog.DbContext`'e direkt girmez.
- `OdemeAlindi` eventi yayınlanır → Siparis modülü subscribe eder — modüller event üzerinden konuşur.

**Ne değişmez:**
- Domain entity'leri, aggregate sınırları, repository interface'leri aynı kalır.
- Onion içindeki katman mantığı her modül içinde geçerlidir.

**Avantaj:** 8 kişilik ekip ileride büyüyünce `Odeme` modülünü ayrı servise taşımak için sadece interface çağrısını HTTP'ye çevirmek yeterlidir.

---

## Faz 3 Kazanımları — Özet Tablo

| Konu | Faz2'de nasıldı | Faz3'te ne değişti |
|---|---|---|
| Domain logic | Controller/service içinde dağınık | Entity + Aggregate → kendi metodları var |
| Transaction | Birden fazla SaveChanges, tutarsızlık riski | UoW ile tek commit noktası |
| Validation | Controller içinde if blokları | FluentValidation + MediatR Pipeline |
| Mimari | MVC katmanları, her şey bağımlı | Onion → bağımlılık içe doğru |
| Sorgu/komut | Aynı servis metodu | CQRS → okuma ve yazma ayrı |
| Cross-cutting concern | Her servise kopyalanan kod | Pipeline Behavior → tek yer |
| Özellik organizasyonu | Controller bazlı | Vertical Slice → özellik bazlı |
| Modül sınırı | Yok — her şey herkese erişir | Modüler Monolith → interface kontratı |

---

## Faz 4'e Girerken Zihinsel Çerçeve

Faz 3'te "doğru yazmak" öğrendik.  
Faz 4'te "hızlı ve verimli yazmak" öğreneceğiz.

Sorular değişiyor:

```
Faz 3: "Bu kod doğru mu? Sorumluluklar ayrılmış mı?"
Faz 4: "Bu kod yavaş mı? Neden allocation yapıyor? Nerede GC baskısı var?"
```

Faz 4'te karşılaşacakların:
- `BenchmarkDotNet` ile ölçmeden tahmin etmemek
- `Span<T>`, `ArrayPool<T>` ile allocation sıfıra indirmek
- EF Core N+1 sorununu tespit edip çözmek
- Caching stratejileri ve cache invalidation
- `async/await` yanlış kullanımının performansa etkisi

---

## Kontrol Soruları

1. E-ticaret senaryosunda `OdemeAlindi` eventi yerine handler içinde direkt kargo sürecini başlatsan ne sorun çıkar?
2. `Urun` ve `SiparisKalemi` neden ayrı aggregate'de değil?
3. Modüler Monolith'te `Siparis` modülü `Katalog` modülünün DB'sine direkt baksaydı, mikroservise geçişte ne olurdu?
4. Validation katmanı — "ürün aktif mi?" kontrolü application layer'da mı yapılmalı, domain layer'da mı? Neden?
