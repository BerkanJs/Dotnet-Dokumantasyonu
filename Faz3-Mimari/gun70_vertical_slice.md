# Gün 70 — Vertical Slice Architecture

Bugünün amacı: katman bazlı değil, özellik bazlı organizasyonun ne anlama geldiğini, Onion ile farkını ve ne zaman hangisini seçmek gerektiğini anlamak.

---

## Gerçek Hayatta Bu Nerede Karşına Çıkar?

Onion/Clean Architecture ile bir proje kurdun. 3 ay sonra yeni özellik ekliyorsun: "Sipariş iptal et."

Ne dokunman gerekiyor?

```
Domain/       → SiparisIptalEvent ekle
Application/  → IptalSiparisCommand, Handler, Validator ekle
Infrastructure/ → gerekirse repo metodu
API/          → endpoint ekle
```

4 farklı klasör, 4 farklı dosya. Özellik "horizontal" katmanlara yayılmış.

Vertical Slice yaklaşımında aynı özellik:

```
Features/
  Siparisler/
    IptalEt/
      IptalSiparisCommand.cs   ← request
      IptalSiparisHandler.cs   ← handler
      IptalSiparisValidator.cs ← validation
      IptalSiparisEndpoint.cs  ← API endpoint
```

Hepsi tek klasörde. Değişiklik bu "slice" içinde kalır.

---

## Vertical Slice Nedir?

Vertical Slice, bir uygulamayı katmanlara değil **özelliklere (feature)** göre böler.

Her slice:
- Kendi request'ini tanımlar
- Kendi handler'ını içerir
- Kendi validation'ını barındırır
- DB'ye direkt ulaşır
- Kendi response'unu döner

```
Onion:     [Domain] → [Application] → [Infrastructure] → [API]   (yatay)
Vertical:  [Feature A: request → handler → db → response]         (dikey)
           [Feature B: request → handler → db → response]
           [Feature C: request → handler → db → response]
```

---

## Klasör Yapısı

```
KitabeviApi/
  Features/
    Kitaplar/
      Listele/
        KitapListeleQuery.cs
        KitapListeleHandler.cs
        KitapListeleResponse.cs
      Ekle/
        KitapEkleCommand.cs
        KitapEkleHandler.cs
        KitapEkleValidator.cs
      Sil/
        KitapSilCommand.cs
        KitapSilHandler.cs
    Siparisler/
      Olustur/
        SiparisOlusturCommand.cs
        SiparisOlusturHandler.cs
        SiparisOlusturValidator.cs
      IptalEt/
        IptalSiparisCommand.cs
        IptalSiparisHandler.cs
  Shared/
    DbContext/
      KitabeviDbContext.cs
    Behaviors/
      ValidationBehavior.cs
    Exceptions/
      DomainException.cs
```

**Neden böyle?**  
→ "Kitap Ekle" özelliğini silmek istersen → `Ekle/` klasörünü sil, başka hiçbir şeye dokunma.  
→ "Kitap Ekle"yi anlamak istersen → tek klasöre bak, projenin geri kalanını bilmene gerek yok.

---

## MediatR ile Vertical Slice — Doğal Birliktelik

MediatR zaten Vertical Slice'ın altyapısını kurar: her özellik bir `IRequest<T>` + `IRequestHandler<,>` çiftidir.

```csharp
// Features/Kitaplar/Listele/KitapListeleQuery.cs
public record KitapListeleQuery(int Sayfa, int Boyut) : IRequest<IReadOnlyList<KitapDto>>;
// bunu IRequest yapmasaydık → MediatR bu sınıfı handler ile eşleştiremez

// Features/Kitaplar/Listele/KitapListeleHandler.cs
public class KitapListeleHandler : IRequestHandler<KitapListeleQuery, IReadOnlyList<KitapDto>>
{
    private readonly KitabeviDbContext _db;
    // Onion'da olsaydık → IKitapRepository inject edilirdi, Infrastructure katmanında implement edilirdi
    // Vertical Slice'da → DbContext direkt inject edilebilir, repository katmanı opsiyonel

    public KitapListeleHandler(KitabeviDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<KitapDto>> Handle(KitapListeleQuery query, CancellationToken ct)
    {
        return await _db.Kitaplar
            .AsNoTracking()                         // okuma — tracking gereksiz, performans kazanımı
            .Where(k => k.Aktif)
            .OrderBy(k => k.Ad)
            .Skip((query.Sayfa - 1) * query.Boyut) // bunu yazmasaydık → her seferinde ilk sayfayı döner
            .Take(query.Boyut)                      // bunu yazmasaydık → tüm kayıtları çeker, bellek patlar
            .Select(k => new KitapDto(k.Id, k.Ad, k.Fiyat))
            .ToListAsync(ct);
    }
}
```

```csharp
// Features/Kitaplar/Ekle/KitapEkleCommand.cs
public record KitapEkleCommand(string Ad, decimal Fiyat, int StokAdedi) : IRequest<int>;

// Features/Kitaplar/Ekle/KitapEkleValidator.cs
public class KitapEkleValidator : AbstractValidator<KitapEkleCommand>
{
    public KitapEkleValidator()
    {
        RuleFor(x => x.Ad).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Fiyat).GreaterThan(0);
        RuleFor(x => x.StokAdedi).GreaterThanOrEqualTo(0);
        // bunu yazmasaydık → negatif fiyat ve boş isimle kayıt oluşturulur
    }
}

// Features/Kitaplar/Ekle/KitapEkleHandler.cs
public class KitapEkleHandler : IRequestHandler<KitapEkleCommand, int>
{
    private readonly KitabeviDbContext _db;

    public KitapEkleHandler(KitabeviDbContext db) => _db = db;

    public async Task<int> Handle(KitapEkleCommand command, CancellationToken ct)
    {
        var kitap = new Kitap
        {
            Ad = command.Ad,
            Fiyat = command.Fiyat,
            StokAdedi = command.StokAdedi,
            Aktif = true
        };

        _db.Kitaplar.Add(kitap);
        await _db.SaveChangesAsync(ct);     // bunu yazmasaydık → nesne bellekte kalır, DB'ye yazılmaz
        return kitap.Id;
    }
}
```

---

## Paylaşılan Kod Nereye Gider?

İki slice aynı şeyi kullanmaya başlarsa `Shared/` veya `Common/` klasörüne taşı.

```
Shared/
  KitabeviDbContext.cs        ← tüm slice'lar kullanır
  Behaviors/
    ValidationBehavior.cs     ← tüm command'lar için otomatik çalışır
  Extensions/
    PaginationExtensions.cs   ← sayfalama logic'i birden fazla slice'ta tekrar ettiyse
  Exceptions/
    DomainException.cs
    NotFoundException.cs
```

**Kural:**  
→ Bir slice'a özel kod → o slice'ın klasöründe kalır.  
→ İki veya daha fazla slice kullanıyorsa → `Shared/`'a taşı.  
→ `Shared/` büyümeye başlarsa → bir Onion katmanına mı dönüşüyor diye sorgula.

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC MVC katmanıyla çalışıyordu, klasik MVC organizasyonu:

```
Controllers/
  KitapController.cs     ← tüm kitap işlemleri burada
  SiparisController.cs
Models/
  Kitap.cs
  Siparis.cs
Views/
  Kitap/
    Index.cshtml
    Create.cshtml
```

`KitapController.cs` içinde 10+ action vardı. "Kitap Ekle" özelliğini anlamak için controller'a, model'e, view'a bakmak gerekiyordu.

Faz3 Vertical Slice'ta:

```
Features/Kitaplar/Ekle/ → command + handler + validator tek klasörde
```

Özellik bağımsız, bakımı kolay, başkası dokunmadan geliştirebilir.

---

## Vertical Slice vs Onion — Ne Zaman Hangisi?

| Durum | Öneri |
|---|---|
| CRUD ağırlıklı API, özellikler bağımsız | Vertical Slice |
| Karmaşık domain logic, birden fazla özellikte paylaşılan kurallar | Onion / Clean |
| Hızlı prototyping, küçük ekip | Vertical Slice |
| Domain'in test edilmesi kritik | Onion (domain ayrı katman) |
| Domain yoksa veya çok basitse | Vertical Slice + direkt DbContext |

**İkisi birlikte:**  
Modüler Monolith içinde her modül Vertical Slice kullanabilir:

```
Modules/
  Katalog/        ← Vertical Slice içinde
    Features/
      Kitaplar/
        Listele/
        Ekle/
  Siparis/        ← Vertical Slice içinde
    Features/
      Siparisler/
        Olustur/
        IptalEt/
  Shared/
```

---

## 500 vs 50K Kullanıcı

| Konu | 500 kullanıcı/ay | 50K kullanıcı/ay |
|---|---|---|
| Vertical Slice uygunluğu | Çok uygun — basit ve hızlı | Modüler Monolith içinde idealdir |
| Repository katmanı | Opsiyonel — DbContext direkt | Sorgu karmaşıklaşırsa repository düşünülebilir |
| Onion'a ihtiyaç | Nadiren | Domain karmaşıklaşınca geçiş mantıklı |
| Feature izolasyonu | Güzel avantaj | Ekip büyüyünce kritik — çakışma azalır |

**Overengineering sinyali:** Vertical Slice projesine Onion'daki gibi `IRepository<T>` + `UnitOfWork` + ayrı Domain katmanı eklemek. Slice zaten yeterince soyutlama sağlar.

---

## Mini Özet

- Vertical Slice, uygulamayı özellik bazında böler — her özellik kendi içinde tamdır.
- MediatR bu yapıya doğal oturur: bir özellik = bir command/query + handler.
- DbContext direkt kullanılabilir — repository katmanı zorunlu değil.
- Paylaşılan kod `Shared/`'a gider.
- Domain karmaşıklaşırsa Onion'a geçilir; ikisi birlikte de kullanılabilir.

---

## Kontrol Soruları

1. "Kitap Sil" özelliğini Onion mimaride eklemek ile Vertical Slice'da eklemek arasında pratik fark nedir?
2. İki farklı slice aynı yardımcı fonksiyonu kullanmaya başlarsa ne yaparsın?
3. Vertical Slice'da neden repository katmanı zorunlu değildir?
4. Domain logic karmaşıklaşmaya başlarsa Vertical Slice içinde bunu nasıl idare edersin?
