# Gün 40 — FluentAssertions ve Test Okunabilirliği

## Neden FluentAssertions?

xUnit'in yerleşik `Assert` sınıfı işlevsel ama minimal:

```csharp
// xUnit built-in: parametre sırası kafa karıştırır
Assert.Equal(expected, actual);   // önce beklenen, sonra gerçek — ters sezgisel
Assert.NotNull(nesne);
Assert.True(liste.Count == 3);    // hata mesajı: "Expected True but was False" — anlamsız

// FluentAssertions: İngilizce cümle gibi okunur
nesne.Should().NotBeNull();
liste.Should().HaveCount(3);
nesne.Baslik.Should().Be("Clean Code");
// Hata mesajı: "Expected Baslik to be 'Clean Code' but found 'Temiz Kod'"
```

**Hata mesajının önemi:**
Test başarısız olduğunda ne yanlış gittiğini anlamak için kodu okumak zorunda kalmadan
hata mesajı yeterli bilgiyi içermeli. FluentAssertions bunu otomatik sağlar.

**Gerçek hayat senaryosu:**
CI pipeline'ında gece çalışan 800 test arasından birinin başarısız olduğunu sabah görüyorsunuz.
`Assert.True(false)` size hiçbir şey söylemez.
`Expected kitap.Fiyat to be greater than 0, but found -5` sorunu anında belirtir.

---

## Temel Kurulum

```csharp
// KitabeviMVC.Tests/GlobalUsings.cs
global using FluentAssertions;
// global using: her test dosyasında using FluentAssertions; yazmaya gerek kalmaz.
// .Should() extension method'u her nesneye eklenir.
```

---

## Temel Assertion'lar

### Eşitlik

```csharp
// Değer eşitliği
kitap.Baslik.Should().Be("Clean Code");
// .Be(): ==operatörü gibi — değer eşitliği.
// Hata: "Expected Baslik to be 'Clean Code' but found 'Temiz Kod'"

kitap.Baslik.Should().NotBe("Yanlış Başlık");
// .NotBe(): eşit olmamalı.

// Numeric
kitap.Fiyat.Should().Be(89.90m);
kitap.StokAdedi.Should().BeGreaterThan(0);
// .BeGreaterThan(): >  operatörü
kitap.StokAdedi.Should().BeGreaterThanOrEqualTo(0);
// .BeGreaterThanOrEqualTo(): >= operatörü
kitap.Fiyat.Should().BeInRange(10m, 500m);
// .BeInRange(min, max): min <= değer <= max — fiyat makul aralıkta mı?
// Bunu yazmassaydık: negatif fiyatlar, milyon TL fiyatlar geçerdi.

kitap.StokAdedi.Should().BePositive();
// .BePositive(): > 0 shorthand
kitap.StokAdedi.Should().BeNegative();
// .BeNegative(): < 0 shorthand (hata senaryolarında)
```

---

### Null Kontrolleri

```csharp
// Null assertion
kitap.Should().NotBeNull(because: "GetByIdAsync geçerli ID için null döndürmemeli");
// because: "...": hata mesajında neden gerektiği açıklanır.
// Bunu yazmassaydık: NullReferenceException sonraki satırda atılır — hatalı yer işaret edilir.

kitap.YazarNavigation.Should().BeNull();
// .BeNull(): navigation property lazy load edilmedi → null beklenir.

// Nullable değer
int? nullableId = null;
nullableId.Should().BeNull();
nullableId.Should().NotHaveValue();
// .NotHaveValue(): HasValue == false — Nullable<T> için özel.

int? dolmuId = 42;
dolmuId.Should().HaveValue();
dolmuId.Should().HaveValue().Which.Should().BeGreaterThan(0);
// .Which: nullable değerin içine gir — zincirleme assertion.
```

---

### String Assertion'ları

```csharp
// İçerik kontrolleri
kitap.Baslik.Should().StartWith("Clean");
// .StartWith(): prefix kontrolü.

kitap.Baslik.Should().EndWith("Code");
// .EndWith(): suffix kontrolü.

kitap.Baslik.Should().Contain("an");
// .Contain(): substring içermeli.

kitap.Baslik.Should().NotContain("XYZ");

// Büyük/küçük harf
kitap.Baslik.Should().ContainEquivalentOf("clean code");
// .ContainEquivalentOf(): case-insensitive contain.
// .BeEquivalentTo("CLEAN CODE"): case-insensitive eşitlik.

// Uzunluk
kitap.Baslik.Should().HaveLength(10);
// .HaveLength(n): tam uzunluk.

kitap.Baslik.Should().NotBeNullOrEmpty();
// .NotBeNullOrEmpty(): null veya "" değil.

kitap.Baslik.Should().NotBeNullOrWhiteSpace();
// .NotBeNullOrWhiteSpace(): null, "" veya sadece boşluk değil.
// API validasyon testi için temel kontrol.

// Regex
kitap.Isbn.Should().MatchRegex(@"^\d{13}$");
// .MatchRegex(): ISBN 13 haneli mi?
```

---

### Koleksiyon Assertion'ları

```csharp
var kitaplar = new List<Kitap>
{
    new() { Id = 1, Baslik = "Clean Code", Fiyat = 89.90m, StokAdedi = 10 },
    new() { Id = 2, Baslik = "DDD",        Fiyat = 120m,  StokAdedi = 5  },
    new() { Id = 3, Baslik = "Sapiens",    Fiyat = 75m,   StokAdedi = 20 },
};

// Temel
kitaplar.Should().NotBeNull();
kitaplar.Should().NotBeEmpty();
kitaplar.Should().HaveCount(3);
// .HaveCount(n): tam eleman sayısı.

kitaplar.Should().HaveCountGreaterThan(2);
// .HaveCountGreaterThan(n): n'den fazla eleman.

kitaplar.Should().HaveCountLessThanOrEqualTo(10);

// İçerik
kitaplar.Should().Contain(k => k.Baslik == "Clean Code");
// .Contain(predicate): koşulu sağlayan eleman var mı?
// Bu yazmassaydık: liste oluşturuldu ama istenilen kitap eklendi mi bilinmez.

kitaplar.Should().NotContain(k => k.Fiyat < 0);
// Negatif fiyatlı kitap olmamalı — domain kuralı.

// Tüm elemanlar koşulu sağlamalı
kitaplar.Should().OnlyContain(k => k.StokAdedi >= 0);
// .OnlyContain(predicate): her eleman koşulu sağlamalı.
// Bunu yazmassaydık: negatif stok geçebilirdi.

kitaplar.Should().OnlyHaveUniqueItems(k => k.Id);
// .OnlyHaveUniqueItems(selector): ID'ler unique mi?

// Sıralama
kitaplar.Should().BeInAscendingOrder(k => k.Fiyat);
// .BeInAscendingOrder(): fiyata göre artan sırada mı?
// Senaryo: fiyata göre sıralanmış liste testi.

kitaplar.Should().BeInDescendingOrder(k => k.Id);

// Eşitlik (değer bazlı)
var beklenen = new List<Kitap> { /* ... */ };
kitaplar.Should().BeEquivalentTo(beklenen);
// .BeEquivalentTo(): derin değer eşitliği — referans değil.
// Özellik bazlı karşılaştırma: Id, Baslik, Fiyat hepsi eşit mi?
```

---

### BeEquivalentTo — Derin Karşılaştırma

```csharp
// Senaryo: API response objesini beklenen DTO ile karşılaştır.
var gercek = new KitapViewModel
{
    Id     = 1,
    Baslik = "Clean Code",
    Fiyat  = 89.90m,
};

var beklenen = new KitapViewModel
{
    Id     = 1,
    Baslik = "Clean Code",
    Fiyat  = 89.90m,
};

gercek.Should().BeEquivalentTo(beklenen);
// Tüm public property'ler eşit mi? — Equals() override gerektirmez.
// Bunu yazmassaydık: Assert.Equal(beklenen, gercek) referans karşılaştırır → hep false.

// Bazı alanları hariç tut
gercek.Should().BeEquivalentTo(beklenen, options =>
    options.Excluding(k => k.EklemeTarihi)
           .Excluding(k => k.RowVersion));
// .Excluding(): test ortamında kontrolümüzde olmayan alanları hariç bırak.
// EklemeTarihi: DateTime.Now ile set edildi — öngörülmez.
// RowVersion: concurrency token — test izole değil.

// Farklı tip ama aynı yapı
var entity = new Kitap { Id = 1, Baslik = "Clean Code" };
var dto    = new KitapViewModel { Id = 1, Baslik = "Clean Code" };

entity.Should().BeEquivalentTo(dto, options =>
    options.ExcludingMissingMembers());
// .ExcludingMissingMembers(): biri diğerinde olmayan property'leri atla.
// Entity → ViewModel mapping doğrulamak için ideal.
```

---

### Exception Assertion'ları

```csharp
// Senaryo: Stok servisi geçersiz adet alınca exception fırlatmalı.
var servis = new KitapStokServisi();
var kitap  = new Kitap { StokAdedi = 5 };

// Temel exception
Action akt = () => servis.StokDus(kitap, -1);
akt.Should().Throw<ArgumentException>();
// .Throw<T>(): belirtilen tip exception fırlatılmalı.
// Bunu yazmassaydık: negatif adet sessizce kabul edilebilir.

// Exception mesajı
akt.Should().Throw<ArgumentException>()
    .WithMessage("*negatif*");
// .WithMessage(pattern): "*" wildcard — mesajın herhangi yerinde "negatif" geçmeli.
// Tam mesaj: kırılgan test. Wildcard: esnek ama anlamlı.

// Exception property
akt.Should().Throw<ArgumentException>()
    .And.ParamName.Should().Be("adet");
// .And: exception nesnesine zincirle — ParamName doğrula.
// "adet": ArgumentException hangi parametreden fırlatıldığını belirtir.

// İç exception
Func<Task> asyncAkt = async () => await servis.GetByIdAsync(-1);
await asyncAkt.Should().ThrowAsync<InvalidOperationException>()
    .WithInnerException<ArgumentException>();
// .ThrowAsync<T>(): async method exception testi — Func<Task> kullan.
// .WithInnerException<T>(): iç içe exception hiyerarşisi.

// Exception fırlatılmamalı
Action gecerliAkt = () => servis.StokDus(kitap, 3);
gecerliAkt.Should().NotThrow();
// .NotThrow(): herhangi bir exception olmamalı.
// Bunu yazmassaydık: başarılı senaryo test edilmemiş olur.
```

---

### SatisfyRespectively — Sıralı Detaylı Doğrulama

```csharp
// Senaryo: Her kitabı teker teker ve sırasıyla doğrula.
var kitaplar = await servis.GetAllAsync();

kitaplar.Should().SatisfyRespectively(
    ilk => {
        ilk.Baslik.Should().Be("Clean Code");
        ilk.Fiyat.Should().Be(89.90m);
    },
    ikinci => {
        ikinci.Baslik.Should().Be("DDD");
        ikinci.Fiyat.Should().BeGreaterThan(100m);
    },
    ucuncu => {
        ucuncu.Baslik.Should().Be("Sapiens");
        ucuncu.StokAdedi.Should().BePositive();
    }
);
// SatisfyRespectively: her lambda sıradaki elemanı alır.
// HaveCount(3) + içerik kontrolü birleşik → tek ifade.
// Bunu yazmassaydık: 3 ayrı [0], [1], [2] indexi + 9 ayrı assertion satırı.
```

---

### ContainSingle — Tam Bir Eleman

```csharp
// Senaryo: Kategori filtresi tam 1 kitap döndürmeli.
var sonuc = kitaplar.Where(k => k.Kategori == "Yazılım").ToList();

sonuc.Should().ContainSingle();
// .ContainSingle(): tam 1 eleman içermeli — HaveCount(1) kısayolu.

sonuc.Should().ContainSingle(k => k.Baslik == "Clean Code");
// .ContainSingle(predicate): koşulu sağlayan tam 1 eleman.
// Hem sayı hem içerik tek satırda.
```

---

## Projemizdeki Kullanım

`KitabeviMVC.Tests/Domain/KitapFluentTests.cs` dosyasında FluentAssertions'ın
tüm önemli API'leri demonstrasyon amaçlı kullanılmaktadır:

```csharp
// Fiyat aralığı
kitap.Fiyat.Should().BeInRange(10m, 500m,
    because: "Kitap fiyatı makul aralıkta olmalı");

// Başlık içerik
kitap.Baslik.Should().NotBeNullOrWhiteSpace()
    .And.HaveLength(10)
    .And.StartWith("Clean");
// .And: zincirleme — aynı subject üzerinde birden fazla assertion.

// Koleksiyon — stoklu kitaplar
stokluKitaplar.Should().OnlyContain(k => k.StokAdedi > 0,
    because: "Stoklu kitaplar filtresi yalnızca stoğu olan kitapları döndürmeli");

// DTO eşleşme — entity to ViewModel mapping
gercekViewModel.Should().BeEquivalentTo(beklenenViewModel,
    options => options.Excluding(vm => vm.EklemeTarihi));
```

---

## because Parametresi — Neden Önemli?

```csharp
// because: parametresi opsiyonel ama çok değerli.
kitaplar.Should().HaveCount(3,
    because: "Seed verisi 3 kitap içeriyor; bu sayı değişmeden önce seed güncellenmeli");

// Test başarısız olduğunda hata mesajı:
// "Expected kitaplar to have 3 item(s) because Seed verisi 3 kitap içeriyor;
//  this sayı değişmeden önce seed güncellenmeli, but found 2."

// "because" olmadan:
// "Expected kitaplar to have 3 item(s), but found 2."
// Neden 3 bekleniyor? Bilinmiyor → araştırmak gerekiyor.
```

**Ne zaman because kullanılmalı?**
- Sayı/değer iş kuralından geliyorsa: `because: "Maksimum 5 kategori kuralı"`
- Test ortamına özel durum varsa: `because: "Seed verisi 3 kitap ekliyor"`
- Koşul anlaşılır değilse: `because: "Ürün pasif olduğunda stok 0 olmalı"`

---

## Java JUnit/AssertJ Karşılaştırması

| Kavram | FluentAssertions (.NET) | AssertJ (Java) |
|--------|------------------------|----------------|
| Başlangıç | `actual.Should()` | `assertThat(actual)` |
| Eşitlik | `.Be(expected)` | `.isEqualTo(expected)` |
| Null değil | `.NotBeNull()` | `.isNotNull()` |
| Koleksiyon boyutu | `.HaveCount(3)` | `.hasSize(3)` |
| İçerir | `.Contain(x => ...)` | `.anyMatch(x -> ...)` |
| Hepsi sağlamalı | `.OnlyContain(x => ...)` | `.allMatch(x -> ...)` |
| Exception | `.Throw<T>()` | `.isThrownBy(() -> ...)` |
| Mesaj | `.WithMessage("*kelime*")` | `.hasMessageContaining("kelime")` |
| Derin eşitlik | `.BeEquivalentTo(obj)` | `.usingRecursiveComparison().isEqualTo(obj)` |
| Hariç tut | `.Excluding(x => x.Alan)` | `.ignoringFields("alan")` |
| Zincirleme | `.And.` | `.and` (aynı tip) / `satisfies(x -> ...)` |
| Sıralı | `.SatisfyRespectively(...)` | `.satisfiesExactly(...)` |

---

## Sık Yapılan Hatalar

### 1. Should() Zinciri Kesmek

```csharp
// YANLIŞ: Should() sonrası assertion yapılmadı
kitap.Should(); // Hiçbir şey doğrulamaz! Test her zaman geçer.

// DOĞRU
kitap.Should().NotBeNull();
```

### 2. Koleksiyonu Doğrudan Karşılaştırmak

```csharp
// YANLIŞ: referans eşitliği — her zaman başarısız
liste1.Should().Be(liste2);

// DOĞRU: değer eşitliği
liste1.Should().BeEquivalentTo(liste2);
// .BeEquivalentTo(): içerik eşitliği, referans değil.
```

### 3. Async Test Hatası

```csharp
// YANLIŞ: await eksik — exception yakalanmaz
Action akt = async () => await asyncMetod(); // Action async lambda almaz!

// DOĞRU
Func<Task> akt = async () => await asyncMetod();
await akt.Should().ThrowAsync<Exception>();
```

---

## Özet

FluentAssertions'ın sağladığı değer üç boyutludur:

1. **Okunabilirlik:** Test kodu dokümantasyon gibi okunur — `Should().Be()` bir cümle.
2. **Hata mesajları:** Başarısız test anında bağlamsal bilgi — "Expected X but found Y because Z".
3. **Koleksiyon gücü:** `OnlyContain`, `SatisfyRespectively`, `BeEquivalentTo` — tek satırda derin doğrulama.

Bir sonraki adım: **WebApplicationFactory** ile gerçek HTTP pipeline'ı test etmek (Gün 41).
