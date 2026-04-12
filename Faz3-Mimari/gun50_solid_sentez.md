# Gün 50 — SOLID Sentez

---

## 1. 5 Prensip Tek Bakışta

| Prensip | Tek cümle | Sinyali | Faz2'deki örnek |
|---------|-----------|---------|-----------------|
| **SRP** | Bir class'ın tek değişme sebebi olmalı | Class 150+ satır, farklı ekipler dokunuyor | `IKitapServisi` / `IKitapSorguServisi` / `IKitapBatchServisi` ayrımı |
| **OCP** | Yeni davranış = yeni class, eski kod dokunulmaz | Yeni özellik için var olan class açılıyor | `CachedKitapServisi` — `EfKitapServisi` açılmadı |
| **LSP** | Alt tip üst tipin yerine geçebilmeli | Override'da boş gövde veya `NotSupportedException` | `CachedKitapServisi`, `IKitapServisi` yerine geçiyor — controller fark etmiyor |
| **ISP** | Interface sadece kullananın ihtiyacı kadar | Implement etmek zorunda kalınan boş metodlar | `IKitapBatchServisi` ayrı — `CachedKitapServisi` batch metodlarını görmez |
| **DIP** | Yüksek seviye soyutlamaya bağımlı, somut tipe değil | `new SomutServis()` constructor içinde | `KitapController` → `IKitapServisi`, hiç `EfKitapServisi` bilmez |

---

## 2. Prensiplerin Birbirini Nasıl Desteklediğini Gör

Faz2'deki tek bir kararı — `CachedKitapServisi` eklenmesi — 5 prensip açısından incele:

```
SRP:  EfKitapServisi'ne caching eklenmedi → her class tek sorumluluk
OCP:  EfKitapServisi açılmadı → yeni davranış yeni class
LSP:  CachedKitapServisi, IKitapServisi'nin yerine geçiyor → controller değişmedi
ISP:  CachedKitapServisi sadece IKitapServisi'ni implement etti → batch yok
DIP:  Controller IKitapServisi'ni biliyor → hangi implementasyon geldiğini bilmiyor
```

**5 prensip aynı anda devreye girdi.** Biri uygulanmasa zincir kırılırdı.

---

## 3. SOLID İhlalleri Teknik Borca Nasıl Dönüşür?

### SRP ihlali → "dokunma korkusu"

```
EfKitapServisi içinde caching + validation + loglama + veri erişimi
→ "Sadece stok kuralını değiştirecektim ama 300 satırlık dosyayı açmak zorunda kaldım"
→ Yan etki riski → developer değiştirmekten kaçınıyor
→ Kod zamanla daha da kötüleşiyor (broken window effect)
```

### OCP ihlali → "her sprint'te aynı dosyada buluşma"

```
FiyatServisi içinde if/switch ile indirim hesaplama
→ Her yeni kampanya = FiyatServisi açılıyor
→ Mevcut if dalları bozulma riski
→ Regresyon testi her seferinde
→ Ekip büyüdükçe merge conflict
```

### LSP ihlali → "runtime sürprizleri"

```
DijitalKitap.StokDus() hiçbir şey yapmıyor
→ Sipariş kodu "stok düştü" sanıyor
→ Prod'da "neden stok ekside gitmedi?" soruşturması
→ Tip kontrolü if (x is DijitalKitap) kodlara giriyor → OCP de ihlal
```

### ISP ihlali → "değişmeyen NotSupportedException'lar"

```
CachedKitapServisi zorla TopluSil implement ediyor
→ throw new NotSupportedException()
→ Runtime'da beklenmedik exception
→ Veya sessizce boş gövde → daha tehlikeli (veri kayıpları)
```

### DIP ihlali → "test yazılamıyor"

```
KitapController içinde new EfKitapServisi(new KitabeviDbContext(...))
→ Test için gerçek DB şart
→ CI/CD pipeline'da DB bağlantısı kurmak gerekiyor
→ "Bu servisi nasıl test edeceğiz?" sorusu yanıtsız kalıyor
→ Test coverage %0'a doğru düşüyor
```

---

## 4. Test Edilebilirlik SOLID'in Ödülü

SOLID uygulanan kodun test edilmesi kolay — bu tesadüf değil:

```csharp
// DIP sayesinde: KitapServisi'ni test etmek için DB yok
var servis = new KitapServisi(new InMemoryKitapRepository());

// SRP sayesinde: SifreServisi'ni izole test edebilirsin
var sifreServis = new SifreServisi();
Assert.True(sifreServis.Dogrula("sifre123", sifreServis.Hash("sifre123")));

// ISP sayesinde: sadece IKitapOkuma mock'lamak yeterli
// IKitapBatch, IKitapYazma mock'lamaya gerek yok
var mock = new Mock<IKitapOkuma>();
```

**Pratik kural:** Bir class'ı unit test etmek zorken — büyük ihtimalle SOLID ihlali var.

---

## 5. SOLID ve Design Patterns İlişkisi

SOLID prensipler **neden** sorusunu cevaplar. Design patterns **nasıl** sorusunu.

| Pattern | Hangi SOLID prensibini uygular |
|---------|-------------------------------|
| Strategy | OCP — yeni davranış yeni class |
| Decorator | OCP + SRP — `CachedKitapServisi` |
| Repository | DIP — servis somut DB'yi bilmez |
| Factory | DIP — nesne oluşturma soyutlanır |
| Observer | OCP + SRP — yeni subscriber = yeni class |

Yarın Gün 51'de bu pattern'leri kodlayacaksın. SOLID'i anlamadan pattern'ler "neden böyle yapıyoruz?" sorusuna cevap veremez.

---

## 6. Ne Zaman SOLID'i İhlal Etmek Makul?

SOLID bir amaç değil, araç. Bazı durumlarda bilinçli ihlal etmek doğrudur:

### Protip veya spike kod

```
Amacın: "Bu fikir çalışır mı?" sorusunu 2 saatte test etmek
→ God class yaz, hızlı dene, çalışıyorsa yeniden yaz
→ SOLID'i çöpe at, sadece sonucu doğrula
```

### Çok küçük projeler

```
500 kullanıcı, 3 ay ömürlü, 1 developer
→ Repository pattern overhead katıyor
→ if/switch 2 dal — Strategy pattern ceremony fazla
→ DIP ile 3 interface açmak: KitapServisi için aşırı
```

### Script ve araçlar

```
Tek seferlik migration scripti, CLI aracı
→ Interface + DI container: gereksiz
→ Doğrudan new, doğrudan çağrı: yeterli
```

### Bilinçli ihlal vs bilgisizlik

```
✅ "Bu projede SRP'yi kasıtlı ihlal ediyorum çünkü ömrü 3 ay"
❌ "SRP nedir bilmiyorum, god class yazıyorum"
```

**Fark:** Prensibi bilip bilinçli ihlal etmek, teknik borcu bilerek almaktır. Bilmeden ihlal etmek, borcun farkında olmamaktır.

---

## 7. Faz3'te Sonrası

SOLID prensipleri öğrendik. Bundan sonra:

```
Gün 51-57: Design Patterns
  → SOLID'i somutlaştıran kalıplar
  → Strategy, Decorator, Observer, Command, Mediator

Gün 58+:   Onion Architecture
  → DIP'in katmanlara yansıması
  → IKitapRepository domain'de, EfKitapRepository infrastructure'da
  → Faz2 KitabeviMVC → KitabeviOnion refactor
```

Onion Architecture aslında DIP + SRP'nin mimari seviyedeki uygulaması. Şimdi DIP'i anladıktan sonra Onion'ı görmek çok daha kolay olacak.

---

## 8. 500 vs 50k — SOLID Genel Karar Rehberi

| Soru | 500 kullanıcı | 50k kullanıcı |
|------|--------------|--------------|
| Ekip kaç kişi? | 1-2 → basit tutabilirsin | 3+ → SOLID şart, ortak dil gerekiyor |
| Proje ömrü? | 3-6 ay → teknik borç kabul edilebilir | 2+ yıl → teknik borç faizi öldürür |
| Test coverage? | Yok → basit kalabilir | Var / olması gerekiyor → DIP + SRP zorunlu |
| Değişim hızı? | Gereksinimler sabit → overengineering riski var | Her sprint yeni özellik → OCP + Strategy şart |

---

## Sorular

1. Faz2'de `IKitapBatchServisi` ayrıldığında aynı anda hangi 3 SOLID prensibi devreye girdi?
2. "SOLID uygulamak projeyi yavaşlatır" diyen bir ekip arkadaşına ne cevap verirsin?
3. Hangi prensip olmadan diğerleri anlamsız? Neden?
