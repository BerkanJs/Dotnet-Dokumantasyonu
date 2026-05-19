# Gün 65 — Hafta 9 Özet

Bu hafta Faz 3'te Onion/Clean + CQRS + MediatR Pipeline + Result Pattern çizgisini oturttuk.  
Amaç sadece kodu çalıştırmak değil, mimari kararları gerekçesiyle savunabilmek.

---

## Bu Hafta Ne Oturdu?

- Layered yaklaşımdan Onion'a geçiş mantığı
- Dependency Rule: bağımlılığın içe doğru akması
- CQRS ile command/query sorumluluk ayrımı
- MediatR pipeline behaviors ile cross-cutting concern yönetimi
- Result Pattern ile iş hatası ve teknik hatayı ayırma

---

## Kavram Haritası

```text
HTTP Request
   -> Controller
      -> MediatR Send
         -> Pipeline (Logging, Validation, Transaction)
            -> Command/Query Handler
               -> Domain + Repository
                  -> Infrastructure (EF Core, dış servisler)
```

Kritik nokta: Handler iş mantığını taşır; pipeline ortak teknik süreçleri taşır.

---

## Kısa Tekrar (Müfredat Uyumlu)

## 1) Onion Katmanları

- **Domain:** iş kuralları, entity/value object, dış bağımlılık yok
- **Application:** use case/handler, DTO, interface'ler
- **Infrastructure:** EF Core repository, dış servis implementasyonları
- **Presentation:** controller/minimal API, request/response

## 2) CQRS Prensibi

- **Command:** state değiştirir, side effect üretir
- **Query:** sadece okur, side effect üretmez
- Read model ile write model her zaman aynı olmak zorunda değildir

## 3) Pipeline Behaviors

- Validation handler öncesi çalışır
- Logging request yaşam döngüsünü görünür yapar
- Transaction command akışını atomik tutar
- Exception politikası merkezi yönetilir

## 4) Result Pattern

- Beklenen iş hataları: `Result.Failure`
- Beklenmeyen teknik arızalar: exception + merkezi yakalama

---

## Bu Haftadan Pratik Mimari Kurallar

- Handler içinde başka handler çağırma (gereksiz zincirler oluşturur)
- İş kuralını behavior'a koyma
- Error code standardı belirlemeden Result kullanma
- Controller'da mapping kopyalamak yerine ortaklaştır
- Command ve query'yi aynı handler'a sıkıştırma

---

## Mimari Soru (Müfredattaki Gün 65 Soruları)

1. E-ticaret uygulamasında sipariş oluşturma use case'ini Onion Architecture'da nasıl tasarlarsın?
2. CQRS olmadan ne kaybedersin?
3. MediatR Pipeline Behavior'ı Decorator pattern ile karşılaştır.

---

## Ek Çalışma Görevi (Kısa)

Kitabevi domain'inde aşağıdaki akışı kağıt üstünde tasarla:

- `SiparisOlusturCommand`
- Validation behavior kuralları
- Transaction behavior sınırı
- Handler sonucu: `Result<SiparisOlusturResult>`
- API mapping: success -> `201`, business failure -> `400/404`

Amaç: koddan önce tasarımın net olması.

---

## Mini Özet

Hafta 9 sonunda hedef:  
"Onion + CQRS + Pipeline + Result" birleşimini sadece uygulamak değil, neden bu şekilde kurduğunu açıklayabilmek.
