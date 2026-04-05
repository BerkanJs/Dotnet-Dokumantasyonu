# Gün 7 — Hafta 1 Özet ve Tekrar

---

## Hafta 1'de Ne Öğrendik?

| Gün | Konu | Kritik Nokta |
|-----|------|-------------|
| 1 | CLR, JIT, AOT | C# kodu IL'e derlenir, CLR çalışma zamanında makine koduna çevirir |
| 2 | Stack, Heap, Value/Reference Types | `struct` kopyalar, `class` adresi paylaşır; boxing'den kaçın |
| 3 | GC, IDisposable, Finalizer | DB bağlantısı için `using` kullan, finalizer yazma |
| 4 | struct, class, record | API modeli → `record`, entity/servis → `class` |
| 5 | String, IEnumerable, IQueryable | `ToList()` öncesi filtrele, `StringBuilder` loop'ta |
| 6 | Async/Await, Task, CancellationToken | `.Result` kullanma, `CancellationToken` geçir |

---

## Tekrar Soruları

### 1. Boxing neden performans sorunudur? Hangi durumlarda kaçınılmaz?

Boxing, bir value type'ı (`int`, `struct`) heap'te bir nesneye sarmalamak demek. Her boxing:
- Heap'te yeni nesne oluşturur
- GC'nin takip etmesi gereken nesne sayısını artırır
- Döngü içinde binlerce kez olursa ciddi GC baskısı yaratır

```csharp
// Kaçınılabilir boxing — generic kullan
ArrayList liste = new();
liste.Add(42);  // boxing

List<int> liste = new();
liste.Add(42);  // boxing YOK
```

**Kaçınılmaz mı?** Bir interface'e cast edersen boxing olur:
```csharp
IComparable c = 42;  // int → IComparable boxing
```
Ama günlük web geliştirmede buna düşmezsin. `List<T>` ve generic yapılar kullandığın sürece sorun yok.

---

### 2. Gen 2 GC ne zaman tetiklenir? Bunu nasıl minimize ederiz?

Gen 2 GC, Gen 1 heap dolduğunda tetiklenir. Gen 2 collection tüm heap'i tarar — en pahalı, en uzun süren GC.

Web API'de buna düşmemek için:
- **Kısa yaşayan nesneler oluştur** — request scope'unda doğan, request bitince ölen nesneler Gen 0'da ölmeli
- **Büyük array oluşturma döngüsü yapma** — LOH baskısı → Gen 2 tetikler
- **Static collection'lara sürekli ekleme yapma** — o nesneler Gen 2'ye terfi eder ve orada sıkışır
- **`ArrayPool<T>` kullan** — büyük buffer'ları tekrar kullan, her seferinde yeni oluşturma

---

### 3. async/await arka planda nasıl çalışır?

Derleyici `async` metodunu bir state machine'e dönüştürür. Her `await` noktasında metot duraklar, thread serbest kalır. İş bitince state machine kaldığı yerden devam eder.

```csharp
public async Task<string> GetDataAsync()
{
    var result = await httpClient.GetStringAsync(url);  // burada durur
    return result.ToUpper();  // işi bitince buradan devam eder
}
```

Pratikte bilmen gereken: `await` thread'i bloklamaz, serbest bırakır. `.Result` veya `.Wait()` kullanırsan thread'i bloklamış olursun — deadlock riski.

---

### 4. ValueTask\<T\> ne zaman Task\<T\>'den daha iyi?

`Task<T>` her zaman heap'te nesne oluşturur. `ValueTask<T>` sonuç zaten hazırsa allocation yapmaz.

```csharp
// Cache'den dönüyorsa allocation yok
public ValueTask<Kitap> GetAsync(int id)
{
    if (_cache.TryGetValue(id, out var k))
        return ValueTask.FromResult(k);  // allocation yok

    return new ValueTask<Kitap>(DbdenGetirAsync(id));
}
```

**Ne zaman kullanırsın?**
- Çok sık çağrılan, genellikle cache'den dönen metodlar
- High-throughput API'lerde GC baskısını azaltmak için

Günlük API kodunda `Task<T>` yeterli. `ValueTask` mikro-optimizasyon — Faz 4'te ölçerek karar veririz.

---

## Hafta 1'in Web Geliştirmeye Katkısı

Faz 2'de ASP.NET Core MVC ile Kitabevi uygulaması yazacağız. Hafta 1'de öğrendiklerimiz orada şöyle görünecek:

```
CLR / Managed code    → ASP.NET Core'un çalışma ortamı, bilmek zorundaydın
Stack / Heap / GC     → Her request'te nesne oluşturulur, GC temizler
IDisposable / using   → DbContext her request sonunda dispose edilir
record                → DTO, Request, Response modelleri
IQueryable            → EF Core sorgularında ToList() öncesi filtrele
async/await           → Controller → Service → Repository hepsi async
CancellationToken     → Controller'dan zincirin sonuna kadar geçer
```

---

## Faz 2'ye Geçmeden Bilmen Gerekenler

Aşağıdakileri rahatça açıklayabiliyorsan Faz 2'ye hazırsın:

- [ ] `class` ile `record` farkı ve hangisini nerede kullanacağın
- [ ] `IQueryable` ile `IEnumerable` farkı — `ToList()` zamanlaması
- [ ] `async Task<T>` nasıl yazılır, `CancellationToken` nasıl geçirilir
- [ ] `using var` ne yapar, neden önemli
- [ ] Boxing nedir, `List<int>` ile `ArrayList` farkı
