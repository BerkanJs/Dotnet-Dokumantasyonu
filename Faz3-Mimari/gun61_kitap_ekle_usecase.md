# Gün 61 — Yeni Use Case: Kitap Ekle (Tam Onion + CQRS)

Gün 59–60'ta sipariş oluşturma use case'ini yazdık. Bugün **KitapEkle** use case'ini sıfırdan yazıyoruz — aynı katmanları geçiyoruz ama bu sefer yalnız.

Faz2'de:
```csharp
// KitapController.cs — Faz2
[HttpPost]
public async Task<IActionResult> Create(KitapCreateViewModel vm)
{
    var kitap = new Kitap { Baslik = vm.Baslik, Fiyat = vm.Fiyat }; // validation yok
    _context.Kitaplar.Add(kitap);
    await _context.SaveChangesAsync();
    return RedirectToAction("Index");
}
// ISBN kontrolü yok, fiyat negatif olabilir, duplikat ISBN DB'ye gidebilir
```

Bugün aynı işi 4 katmanda, tüm kurallar Domain'de, test edilebilir şekilde yapıyoruz.

---

## 1. Domain — `IKitapRepository`'ye metod ekle

```csharp
// Domain/Interfaces/IKitapRepository.cs
namespace KitabeviOnion.Domain.Interfaces;

public interface IKitapRepository
{
    Task<Kitap?> BulByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Kitap>> TumunuGetirAsync(CancellationToken ct = default);
    Task EkleAsync(Kitap kitap, CancellationToken ct = default);
    Task<bool> IsbnMevcutMu(Isbn isbn, CancellationToken ct = default);
    //          ↑ duplikat ISBN kontrolü için — Domain interface'i, DB bilgisi yok
    //            bunu yazmasaydık → Handler IsbnMevcutMu'yu nasıl soracaktı?
    Task KaydetAsync(CancellationToken ct = default);
    //   ↑ Gün59'da sadece SiparisRepository'de vardı — Kitap için de gerekli
}
```

---

## 2. Application — Command + Result + Validator + Handler

### `UseCases/KitapEkle/KitapEkleCommand.cs`

```csharp
namespace KitabeviOnion.Application.UseCases.KitapEkle;

public record KitapEkleCommand(
    string Baslik,
    string Isbn,
    decimal Fiyat,
    string ParaBirimi,
    int IlkStok
) : IRequest<KitapEkleResult>;
//  ↑ IRequest<KitapEkleResult>: MediatR "bu command gelince KitapEkleResult dön" biliyor
//    bunu yazmasaydık → _mediator.Send(cmd) hangi tip döndüreceğini bilemezdi
```

---

### `UseCases/KitapEkle/KitapEkleResult.cs`

```csharp
namespace KitabeviOnion.Application.UseCases.KitapEkle;

public record KitapEkleResult(
    int KitapId,
    string Baslik,
    string Isbn,
    decimal Fiyat,
    string ParaBirimi,
    int StokAdedi
);
// ↑ Kitap entity'sini olduğu gibi döndürmüyoruz
//   bunu yazmasaydık → Domain entity API'ye sızardı,
//   entity değişince API kontratı da bozulurdu
```

---

### `Validators/KitapEkleCommandValidator.cs`

```csharp
namespace KitabeviOnion.Application.Validators;

public class KitapEkleCommandValidator : AbstractValidator<KitapEkleCommand>
{
    public KitapEkleCommandValidator()
    {
        RuleFor(x => x.Baslik)
            .NotEmpty().WithMessage("Başlık boş olamaz")
            .MaximumLength(200).WithMessage("Başlık 200 karakterden uzun olamaz");
        //  ↑ input validation — DB'ye gitmeden reddet
        //    bunu yazmasaydık → boş başlıklı kitap Domain'e ulaşırdı

        RuleFor(x => x.Isbn)
            .NotEmpty().WithMessage("ISBN boş olamaz")
            .Length(10, 17).WithMessage("ISBN 10 veya 13 haneli olmalı (tire dahil)");
        // ↑ format kontrolü — Isbn Value Object daha detaylı doğrular,
        //   ama 3 karakterlik saçmalığı DB'ye sokmadan reddet

        RuleFor(x => x.Fiyat)
            .GreaterThan(0).WithMessage("Fiyat sıfırdan büyük olmalı");
        // ↑ Fiyat Value Object da kontrol ediyor ama erken reddetmek daha iyi UX

        RuleFor(x => x.ParaBirimi)
            .NotEmpty()
            .Length(3).WithMessage("Para birimi 3 karakter olmalı (TRY, USD, EUR)");

        RuleFor(x => x.IlkStok)
            .GreaterThanOrEqualTo(0).WithMessage("Başlangıç stok negatif olamaz");
    }
}
```

---

### `UseCases/KitapEkle/KitapEkleHandler.cs`

```csharp
namespace KitabeviOnion.Application.UseCases.KitapEkle;

public class KitapEkleHandler : IRequestHandler<KitapEkleCommand, KitapEkleResult>
//                               ↑ MediatR: KitapEkleCommand → bu handler
{
    private readonly IKitapRepository _kitapRepo;
    //               ↑ Domain interface — Infrastructure bilgisi yok

    public KitapEkleHandler(IKitapRepository kitapRepo)
    {
        _kitapRepo = kitapRepo;
    }

    public async Task<KitapEkleResult> Handle(
        KitapEkleCommand request,
        CancellationToken cancellationToken)
    {
        // 1. ISBN Value Object oluştur — format kontrolü burada
        var isbn = new Isbn(request.Isbn);
        //          ↑ "978abc" → DomainException fırlar, handler'a ulaşmaz
        //            bunu yazmasaydık → string olarak saklanırdı, sonradan temizlenmesi zor

        // 2. Duplikat ISBN kontrolü
        var isbnVar = await _kitapRepo.IsbnMevcutMu(isbn, cancellationToken);
        if (isbnVar)
            throw new DomainException($"Bu ISBN zaten kayıtlı: {isbn.Deger}");
        //  ↑ DB'ye sormadan domain kuralını uygulama: aynı ISBN iki kez olamaz
        //    bunu yazmasaydık → aynı ISBN'li iki kitap DB'ye girebilirdi

        // 3. Fiyat Value Object oluştur
        var fiyat = new Fiyat(request.Fiyat, request.ParaBirimi);
        //           ↑ 0 veya negatif → DomainException fırlar
        //             bunu yazmasaydık → decimal kullanırdık, para birimi kaybolurdu

        // 4. Domain entity oluştur — tüm kurallar Domain constructor'da
        var kitap = new Kitap(request.Baslik, isbn, fiyat, request.IlkStok);
        //                                                   ↑ negatif stok → DomainException

        // 5. Kaydet
        await _kitapRepo.EkleAsync(kitap, cancellationToken);
        await _kitapRepo.KaydetAsync(cancellationToken);
        //                ↑ SaveChanges — entity ID burada atanır (DB identity)

        // 6. Result — entity → DTO dönüşümü Application'da
        return new KitapEkleResult(
            KitapId: kitap.Id,
            //               ↑ SaveChanges sonrası ID DB'den geldi
            Baslik: kitap.Baslik,
            Isbn: kitap.Isbn.Deger,
            //              ↑ Value Object → primitive: API kontratı düz tip bekliyor
            Fiyat: kitap.Fiyat.Deger,
            ParaBirimi: kitap.Fiyat.ParaBirimi,
            StokAdedi: kitap.StokAdedi);
    }
}
```

---

## 3. Infrastructure — Repository güncelle

### `Persistence/Repositories/KitapRepository.cs`

```csharp
namespace KitabeviOnion.Infrastructure.Persistence.Repositories;

public class KitapRepository : IKitapRepository
{
    private readonly AppDbContext _context;

    public KitapRepository(AppDbContext context) => _context = context;

    public async Task<Kitap?> BulByIdAsync(int id, CancellationToken ct = default)
        => await _context.Kitaplar.FirstOrDefaultAsync(k => k.Id == id, ct);

    public async Task<IReadOnlyList<Kitap>> TumunuGetirAsync(CancellationToken ct = default)
        => await _context.Kitaplar.AsNoTracking().ToListAsync(ct);

    public async Task EkleAsync(Kitap kitap, CancellationToken ct = default)
        => await _context.Kitaplar.AddAsync(kitap, ct);

    public async Task<bool> IsbnMevcutMu(Isbn isbn, CancellationToken ct = default)
        => await _context.Kitaplar
            .AnyAsync(k => k.Isbn.Deger == isbn.Deger, ct);
    //      ↑ EF Core owned entity query — Value Object'in Deger alanına bakıyor
    //        bunu yazmasaydık → string karşılaştırma yapardık, tire/boşluk sorunu çıkardı

    public async Task KaydetAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
```

---

## 4. API — Controller ve Request DTO

### `Controllers/KitapController.cs`

```csharp
namespace KitabeviOnion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KitapController : ControllerBase
{
    private readonly IMediator _mediator;

    public KitapController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Listele(CancellationToken ct)
        => Ok(await _mediator.Send(new KitapListeleQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> Ekle(
        [FromBody] KitapEkleRequest request,
        CancellationToken ct)
    {
        var cmd = new KitapEkleCommand(
            Baslik: request.Baslik,
            Isbn: request.Isbn,
            Fiyat: request.Fiyat,
            ParaBirimi: request.ParaBirimi,
            IlkStok: request.IlkStok);
        //  ↑ API request → Application command dönüşümü
        //    bunu yazmasaydık → Command doğrudan [FromBody] alırdı,
        //    API sözleşmesi Application'a sızardı

        var sonuc = await _mediator.Send(cmd, ct);
        //                           ↑ Pipeline: Logging → Validation → Handler

        return CreatedAtAction(
            nameof(Listele),
            new { id = sonuc.KitapId },
            sonuc);
        // ↑ 201 Created
        //   Location: /api/kitap?id=42
    }
}

public record KitapEkleRequest(
    string Baslik,
    string Isbn,
    decimal Fiyat,
    string ParaBirimi,
    int IlkStok
);
// ↑ API DTO — Command'den ayrı
//   Gelecekte API'ye "YayinYili" eklenebilir, Command değişmeyebilir
//   ya da Command'e "EkleyenKullaniciId" header'dan eklenebilir
```

---

## 5. Test — Handler Unit Test

```csharp
// Tests/Application/KitapEkleHandlerTests.cs
namespace KitabeviOnion.Tests.Application;

public class KitapEkleHandlerTests
{
    private readonly Mock<IKitapRepository> _kitapRepoMock;
    //               ↑ Interface mock — gerçek DB yok, test hızlı
    //                 bunu yazmasaydık → gerçek AppDbContext kurardık, test yavaş + kırılgan

    private readonly KitapEkleHandler _handler;

    public KitapEkleHandlerTests()
    {
        _kitapRepoMock = new Mock<IKitapRepository>();
        _handler = new KitapEkleHandler(_kitapRepoMock.Object);
    }

    [Fact]
    public async Task Handle_GecerliKitap_KitapEklenir()
    {
        // Arrange
        var cmd = new KitapEkleCommand(
            Baslik: "Clean Code",
            Isbn: "9780132350884",
            Fiyat: 150,
            ParaBirimi: "TRY",
            IlkStok: 10);

        _kitapRepoMock
            .Setup(r => r.IsbnMevcutMu(It.IsAny<Isbn>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        //  ↑ "bu ISBN DB'de yok" → kaydetmeye devam et
        //    bunu yazmasaydık → mock null döner, NullReferenceException alırdık

        _kitapRepoMock
            .Setup(r => r.EkleAsync(It.IsAny<Kitap>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _kitapRepoMock
            .Setup(r => r.KaydetAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var sonuc = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        Assert.Equal("Clean Code", sonuc.Baslik);
        Assert.Equal("9780132350884", sonuc.Isbn);
        Assert.Equal(150, sonuc.Fiyat);
        Assert.Equal(10, sonuc.StokAdedi);

        _kitapRepoMock.Verify(
            r => r.EkleAsync(It.IsAny<Kitap>(), It.IsAny<CancellationToken>()),
            Times.Once);
        //  ↑ EkleAsync gerçekten bir kez çağrıldı mı?
        //    bunu yazmasaydık → handler çağırmadan testi geçebilirdi
    }

    [Fact]
    public async Task Handle_IsbnZatenKayitli_DomainExceptionFirlar()
    {
        // Arrange
        var cmd = new KitapEkleCommand("Clean Code", "9780132350884", 150, "TRY", 10);

        _kitapRepoMock
            .Setup(r => r.IsbnMevcutMu(It.IsAny<Isbn>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        //  ↑ "bu ISBN var" → DomainException bekliyoruz

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(cmd, CancellationToken.None));
        //  ↑ exception fırladı mı?

        _kitapRepoMock.Verify(
            r => r.EkleAsync(It.IsAny<Kitap>(), It.IsAny<CancellationToken>()),
            Times.Never);
        //  ↑ duplikat ISBN'de EkleAsync ÇAĞRILMAMALI
        //    bunu test etmeseydik → mock setup yanlış bile olsa test geçebilirdi
    }

    [Fact]
    public async Task Handle_GecersizIsbn_DomainExceptionFirlar()
    {
        // Arrange — ISBN format hatası: Isbn Value Object içinde patlar
        var cmd = new KitapEkleCommand("Clean Code", "YANLIS_ISBN", 150, "TRY", 10);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(cmd, CancellationToken.None));
        //  ↑ Isbn("YANLIS_ISBN") → DomainException: "Geçersiz ISBN"
        //    Handler'a ulaşmadan Domain kuralı devreye girdi

        _kitapRepoMock.Verify(
            r => r.IsbnMevcutMu(It.IsAny<Isbn>(), It.IsAny<CancellationToken>()),
            Times.Never);
        //  ↑ ISBN geçersizse DB'ye sorgu bile gitmemeli
    }

    [Fact]
    public async Task Handle_NegativeFiyat_DomainExceptionFirlar()
    {
        // Arrange
        var cmd = new KitapEkleCommand("Clean Code", "9780132350884", -10, "TRY", 5);
        //                                                               ↑ negatif fiyat

        _kitapRepoMock
            .Setup(r => r.IsbnMevcutMu(It.IsAny<Isbn>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(cmd, CancellationToken.None));
        //  ↑ new Fiyat(-10, "TRY") → DomainException: "Fiyat 0'dan büyük olmalı"
    }
}
```

---

## 6. Validator Test

```csharp
// Tests/Application/KitapEkleCommandValidatorTests.cs
namespace KitabeviOnion.Tests.Application;

public class KitapEkleCommandValidatorTests
{
    private readonly KitapEkleCommandValidator _validator = new();

    [Fact]
    public void Validate_GecerliCommand_Hatayok()
    {
        var cmd = new KitapEkleCommand("Clean Code", "9780132350884", 150, "TRY", 5);
        var sonuc = _validator.Validate(cmd);
        Assert.True(sonuc.IsValid);
    }

    [Theory]
    [InlineData("", "9780132350884", 150, "TRY", 5)]    // boş başlık
    [InlineData("Clean Code", "", 150, "TRY", 5)]       // boş ISBN
    [InlineData("Clean Code", "9780132350884", 0, "TRY", 5)]  // sıfır fiyat
    [InlineData("Clean Code", "9780132350884", 150, "TRY", -1)] // negatif stok
    [InlineData("Clean Code", "9780132350884", 150, "US", 5)]  // 2 harfli para birimi
    public void Validate_GecersizCommand_HataVar(
        string baslik, string isbn, decimal fiyat, string para, int stok)
    {
        var cmd = new KitapEkleCommand(baslik, isbn, fiyat, para, stok);
        var sonuc = _validator.Validate(cmd);
        Assert.False(sonuc.IsValid);
        //  ↑ her geçersiz kombinasyon en az bir hata vermeli
        //    Theory: aynı test 5 farklı girdiyle çalışır
    }
}
```

---

## Tam Akış — KitapEkle

```
POST /api/kitap
{
  "baslik": "Clean Code",
  "isbn": "978-0-13-235088-4",
  "fiyat": 150,
  "paraBirimi": "TRY",
  "ilkStok": 10
}

1. KitapController.Ekle()
   KitapEkleRequest → KitapEkleCommand
   _mediator.Send(cmd)

2. LoggingBehavior → "→ KitapEkleCommand başladı"

3. ValidationBehavior
   Baslik boş mu? → "Clean Code" ✓
   ISBN uzunluk? → 17 char (tireli) ✓
   Fiyat > 0? → 150 ✓
   ParaBirimi 3 char? → "TRY" ✓

4. KitapEkleHandler.Handle()
   new Isbn("978-0-13-235088-4")
       → tire temizleme → "9780132350884" ✓ (13 haneli)
   kitapRepo.IsbnMevcutMu("9780132350884") → false ✓
   new Fiyat(150, "TRY") → ✓
   new Kitap("Clean Code", isbn, fiyat, 10) → ✓
   kitapRepo.EkleAsync(kitap)
   kitapRepo.KaydetAsync() → SaveChanges → Id: 42

5. LoggingBehavior → "← KitapEkleCommand tamamlandı: 24ms"

6. 201 Created
   Location: /api/kitap
   {
     "kitapId": 42,
     "baslik": "Clean Code",
     "isbn": "9780132350884",
     "fiyat": 150,
     "paraBirimi": "TRY",
     "stokAdedi": 10
   }
```

---

## Program.cs — Yeni Handler Register

```csharp
// MediatR zaten Assembly scan yapıyor — ekstra register YOK
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(KitapEkleHandler).Assembly));
// ↑ Assembly'deki tüm IRequestHandler'ları buluyor
//   KitapEkleHandler, KitapListeleHandler, SiparisOlusturHandler — hepsi otomatik
//   bunu yazmasaydık → her yeni handler için Program.cs'e satır eklemek zorunda kalırdık
```

---

## 500 vs 50k

| Konu | 500 | 50k |
|---|---|---|
| **ISBN duplikat kontrolü** | Try-catch DB unique constraint yeter | ✅ Uygulama katmanında erken reddet |
| **Value Object (Isbn, Fiyat)** | string/decimal yeterli | ✅ Format + para birimi zorunlu |
| **Unit test** | Integration test yeterli | ✅ Her handler için zorunlu |
| **Validator + Behavior** | Inline if yeterli | ✅ Cross-cutting, tek yer |
| **Theory test** | Tek case yeterli | ✅ Edge case'leri sistematik kap |

---

## Sorular

1. `KitapEkleHandler` neden `IsbnMevcutMu` kontrolünü `Isbn` Value Object oluşturduktan **sonra** yapıyor? Önce yapsaydı ne değişirdi?
2. `KitapEkleCommandValidator` `Fiyat > 0` kuralını yazıyor, `Fiyat` Value Object da aynı kuralı kontrol ediyor. Bu tekrar mı? İkisi de gerekli mi?
3. Test'te `Times.Never` assertion'ı neden önemli? Sadece exception fırlamasını test etmek yeterli değil mi?
