# Gün 67 — Specification Pattern

Bugünün amacı: iş kurallarını ve filtreleme mantığını controller/repository içine gömmek yerine, ayrı ve birleştirilebilir nesneler haline getirmek.

---

## Specification Pattern Nedir?

Specification, "şu koşulu sağlayan kayıtlar" bilgisini nesne olarak taşır.  
Böylece sorgu mantığı:

- tekrar kullanılabilir olur
- test edilebilir olur
- birleştirilebilir hale gelir (`And`, `Or`, `Not`)

---

## Neden İhtiyaç Duyarız?

Aynı filtreler farklı yerlerde tekrar etmeye başladığında:

- `Aktif kitaplar`
- `Stokta olan kitaplar`
- `Belirli fiyat aralığındaki kitaplar`

her seferinde repository'ye yeni metot açmak yerine specification ile kuralları modüler tutarız.

---

## Basit Specification Altyapısı

```csharp
public interface ISpecification<T>
{
    Expression<Func<T, bool>> Criteria { get; }
}

public abstract class Specification<T> : ISpecification<T>
{
    public abstract Expression<Func<T, bool>> Criteria { get; }
}
```

Bu kadar basit bir başlangıç çoğu eğitim/proje senaryosu için yeterli.

---

## Kitabevi İçin Somut Specification Örnekleri

```csharp
public sealed class AktifKitaplarSpecification : Specification<Kitap>
{
    public override Expression<Func<Kitap, bool>> Criteria
        => kitap => kitap.Aktif;
}

public sealed class StoktaOlanKitaplarSpecification : Specification<Kitap>
{
    public override Expression<Func<Kitap, bool>> Criteria
        => kitap => kitap.StokAdedi > 0;
}

public sealed class FiyatAraligiSpecification : Specification<Kitap>
{
    private readonly decimal _min;
    private readonly decimal _max;

    public FiyatAraligiSpecification(decimal min, decimal max)
    {
        _min = min;
        _max = max;
    }

    public override Expression<Func<Kitap, bool>> Criteria
        => kitap => kitap.Fiyat >= _min && kitap.Fiyat <= _max;
}
```

---

## Repository'de Kullanımı

```csharp
public async Task<List<Kitap>> ListAsync(ISpecification<Kitap> spec, CancellationToken ct)
{
    return await _db.Kitaplar
        .Where(spec.Criteria)
        .AsNoTracking()
        .ToListAsync(ct);
}
```

Artık repository tarafında filtre detayını değil, specification nesnesini geçiriyoruz.

---

## And / Or Birleştirme (Sade Versiyon)

```csharp
public sealed class AndSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;

    public AndSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> Criteria
        => x => _left.Criteria.Invoke(x) && _right.Criteria.Invoke(x);
}
```

Kullanım:

```csharp
var aktif = new AktifKitaplarSpecification();
var stokta = new StoktaOlanKitaplarSpecification();
var spec = new AndSpecification<Kitap>(aktif, stokta);

var kitaplar = await _kitapRepository.ListAsync(spec, ct);
```

Not: Gerçek EF çevirisinde `Invoke` bazen sorun çıkarabilir.  
Eğitim seviyesinde mantığı göstermek için bu yaklaşım yeterli; production'da expression birleştirme yardımcıları veya hazır kütüphane kullanılır.

---

## CQRS Query Handler İçinde Kullanım

```csharp
public class KitapListeleHandler
    : IRequestHandler<KitapListeleQuery, IReadOnlyList<KitapDto>>
{
    private readonly IKitapRepository _repo;

    public KitapListeleHandler(IKitapRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<KitapDto>> Handle(KitapListeleQuery request, CancellationToken ct)
    {
        var aktif = new AktifKitaplarSpecification();
        var fiyat = new FiyatAraligiSpecification(request.MinFiyat, request.MaxFiyat);
        var spec = new AndSpecification<Kitap>(aktif, fiyat);

        var kitaplar = await _repo.ListAsync(spec, ct);
        return kitaplar.Select(k => new KitapDto(k.Id, k.Ad, k.Fiyat)).ToList();
    }
}
```

---

## Ardalis.Specification Ne Zaman?

Kendi altyapın küçük projede yeterli.  
İhtiyaç büyürse `Ardalis.Specification` çok iş görür:

- include yönetimi
- pagination
- sorting
- projection
- evaluator altyapısı

Ama önce mantığı sade sürümle oturtmak daha öğreticidir.

---

## Sık Yapılan Hatalar

- Her sorguyu specification yapmak (gereksiz soyutlama)
- Repository içinde tekrar if-else yazarak specification'ı boşa çıkarmak
- Çok büyük "mega specification" yazmak
- Domain kuralı ile query filtresini karıştırmak

---

## 500 vs 50K Kullanıcı

| Konu | 500 kullanıcı | 50K kullanıcı |
|---|---|---|
| Specification kullanımı | Orta ölçekten itibaren faydalı | Güçlü şekilde önerilir |
| Basit spec altyapısı | Genelde yeterli | Kütüphane + standartlaşma daha iyi |
| Query tekrarını azaltma | Güzel avantaj | Kritik |
| Test edilebilir filtreleme | Faydalı | Zorunluya yakın |

---

## Mini Özet

Specification Pattern, sorgu/iş kuralı filtresini yeniden kullanılabilir bir nesneye çevirir.  
Doğru kullanıldığında repository sadeleşir, query handler'lar daha okunur olur.

---

## Kontrol Soruları

1. Repository'ye yeni metot eklemek yerine specification kullanmanın avantajı nedir?
2. `And/Or` ile birleşen specification yaklaşımı hangi sorunu çözer?
3. Domain kuralı ile query filtresi arasındaki fark nedir?
4. Hangi durumda specification overengineering'e dönüşür?
