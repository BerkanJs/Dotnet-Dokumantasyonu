# Gün 8 — Generics: Tip Güvenli Kod

---

## 1. Generics Neden Var?

Generics olmadan şunu yazmak zorunda kalırsın:

```csharp
// Her tip için ayrı metot
int MaksimumBul(int a, int b) => a > b ? a : b;
string MaksimumBul(string a, string b) => ...;
decimal MaksimumBul(decimal a, decimal b) => ...;
```

Ya da boxing ile object kullanırsın — Gün 2'deki performans sorununa düşersin:

```csharp
// object kullanmak boxing getirir ve tip güvenliği yok
object MaksimumBul(object a, object b) => ...;
```

Generics ile bir kez yazarsın, her tipte çalışır, tip güvenliği korunur, boxing olmaz:

```csharp
T MaksimumBul<T>(T a, T b) where T : IComparable<T>
    => a.CompareTo(b) > 0 ? a : b;

int sonuc = MaksimumBul(3, 7);          // T = int
string uzun = MaksimumBul("ab", "abc"); // T = string
```

---

## 2. C# Generics vs Java Generics — Kritik Fark

Java'da generics **Type Erasure** ile çalışır. `List<String>` derleme sonrasında `List<Object>` olur. Runtime'da tip bilgisi kaybolur.

C#'ta bu yok. C# generics **reified** — runtime'da tip bilgisi korunur.

```csharp
// C#'ta çalışır
var tip = typeof(List<int>);
Console.WriteLine(tip.Name);  // List`1 — int bilgisi hâlâ var
Console.WriteLine(tip.GetGenericArguments()[0]);  // System.Int32
```

Java'da `new T()` yazamazsın — tip bilgisi yok. C#'ta `where T : new()` constraint'i ile yazabilirsin.

**Performans farkı:**

C# generics'te CLR, value type (`int`, `struct`) için **ayrı makine kodu üretir**. Yani `List<int>` ile `List<string>` farklı IL üretir — boxing olmaz.

Java'da her şey `Object`'e dönüşür, primitifler auto-box edilir.

---

## 3. Generic Constraints — Sınırlamalar

`<T>` yazdığında T her şey olabilir. Ama bazen T'nin belirli özelliklere sahip olmasını zorunlu kılmak istersin. Bunun için `where` kullanırsın.

**`where T : class`** — T bir reference type olmalı

```csharp
// Null check yapabilmek için T'nin nullable olması lazım
T? BulVeyaNull<T>(List<T> liste, int id) where T : class
{
    return liste.FirstOrDefault();
}
```

**`where T : struct`** — T bir value type olmalı

```csharp
// Boxing olmadan çalışmasını istiyorsan
void Yazdir<T>(T deger) where T : struct
{
    Console.WriteLine(deger);
}
```

**`where T : new()`** — T'nin parametresiz constructor'ı olmalı

```csharp
T Olustur<T>() where T : new()
{
    return new T();  // T'nin new() olmadan bunu yapamayız
}
```

**`where T : IEntity`** — T belirli bir interface'i implement etmeli

```csharp
// Web'de en sık kullanılan — generic repository pattern
class Repository<T> where T : class, IEntity
{
    public Task<T?> GetByIdAsync(int id) { ... }
    public Task AddAsync(T entity) { ... }
}
```

**Birden fazla constraint:**

```csharp
void Kaydet<T>(T nesne) where T : class, IEntity, new()
{ }
```

---

## 4. Generic Repository — Web'deki Gerçek Kullanım

Faz 3'te bu pattern'i tam göreceğiz ama şimdiden anlayalım. Her entity için ayrı ayrı CRUD yazmak yerine:

```csharp
// Her entity için tekrar tekrar yazmak zorunda kalmak:
class KitapRepository
{
    Task<Kitap?> GetByIdAsync(int id) { ... }
    Task<List<Kitap>> GetAllAsync() { ... }
    Task AddAsync(Kitap entity) { ... }
}

class YazarRepository
{
    Task<Yazar?> GetByIdAsync(int id) { ... }  // tekrar
    Task<List<Yazar>> GetAllAsync() { ... }    // tekrar
    Task AddAsync(Yazar entity) { ... }        // tekrar
}
```

Generic repository ile bir kez yaz:

```csharp
interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task AddAsync(T entity);
}

class Repository<T> : IRepository<T> where T : class
{
    private readonly DbContext _db;
    public Repository(DbContext db) { _db = db; }

    public Task<T?> GetByIdAsync(int id) => _db.Set<T>().FindAsync(id).AsTask();
    public Task<List<T>> GetAllAsync() => _db.Set<T>().ToListAsync();
    public Task AddAsync(T entity) { _db.Set<T>().Add(entity); return _db.SaveChangesAsync(); }
}

// Kullanım
var kitapRepo = new Repository<Kitap>(db);
var yazarRepo = new Repository<Yazar>(db);
```

---

## 5. Kovaryans — "Üst Tip Kabul Et"

Bu kısmı bir örnekle açıklayalım. Önce sorunu görelim:

```csharp
class Hayvan { }
class Kedi : Hayvan { }

List<Kedi> kediler = new List<Kedi> { new Kedi() };
List<Hayvan> hayvanlar = kediler;  // HATA — derlenmez
```

Neden hata? `List<Kedi>`, `List<Hayvan>`'ın alt tipi değil. Mantıklı gibi görünse de şöyle bir sorun var:

```csharp
// Eğer izin verseydi:
hayvanlar.Add(new Kopek());  // Kopek bir Hayvan, ama List<Kedi>'ye Kopek eklenemez!
```

Ama **okuma için** (sadece okuyacaksan, yazmayacaksan) izin verilebilir. İşte kovaryans bu:

**`IEnumerable<out T>`** — sadece okuma, T kovaryant:

```csharp
IEnumerable<Kedi> kediler = new List<Kedi>();
IEnumerable<Hayvan> hayvanlar = kediler;  // ÇALIŞIR — sadece okuma var
```

`IEnumerable<T>` interface'inde `out T` var — "T sadece dışarı çıkar, içeri girmez". Bu yüzden `IEnumerable<Kedi>`'yi `IEnumerable<Hayvan>` yerine kullanabilirsin.

---

## 6. Kontravaryans — "Alt Tip Kabul Et"

Tam tersi yön. "Hayvan işleyen bir şey, Kedi de işleyebilir" mantığı:

```csharp
Action<Hayvan> hayvaniIsle = h => Console.WriteLine(h.GetType().Name);
Action<Kedi>   kediIsle    = hayvaniIsle;  // ÇALIŞIR — Action<in T> kontravaryant
```

Neden mantıklı? `hayvaniIsle` her türlü Hayvan'ı işleyebiliyor. Kedi de bir Hayvan. Yani Kedi'yi hayvaniIsle'ye verebilirsin.

`Action<in T>` — "T sadece içeri girer, dışarı çıkmaz". Bu yüzden `Action<Hayvan>`, `Action<Kedi>` yerine kullanılabilir.

**Özet:**

| | Yön | Keyword | Örnek |
|---|---|---|---|
| Kovaryans | Üst tipe atama | `out` | `IEnumerable<out T>` |
| Kontravaryans | Alt tipe atama | `in` | `Action<in T>`, `IComparer<in T>` |

---

## 7. Web Geliştirmede Kovaryans Nerede Görünür?

Günlük kodda `out`/`in` yazmazsın — framework bunu halleder. Ama şunu yazdığında arka planda çalışıyor:

```csharp
// KitapResponse bir ApiResponse değil, ama IEnumerable kovaryantı sayesinde çalışır
IEnumerable<KitapResponse> kitaplar = await _service.GetirAsync();
IEnumerable<object> nesneler = kitaplar;  // kovaryans — çalışır
```

Ayrıca **LINQ metodlarının parametreleri** kontravaryans kullanır:

```csharp
// Func<Kitap, bool> bekleniyor
// Func<object, bool> de geçilebilir — kontravaryans
```

Daha sık karşılaştığın yer: **generic repository ve servis interface'leri**. Kovaryans sayesinde `IRepository<Kitap>`'ı `IRepository<IEntity>` yerine kullanabilirsin (doğru tanımlanmışsa).

---

## 8. Java ile Karşılaştırma Özeti

| | Java | C# |
|---|---|---|
| Runtime'da tip bilgisi | Yok (Type Erasure) | Var (Reified Generics) |
| Primitifler | Auto-box olur (`int` → `Integer`) | Boxing olmaz (`int` direkt) |
| Kovaryans/Kontravaryans | Wildcard (`? extends`, `? super`) | `out`/`in` keyword |
| `new T()` | Yapılamaz | `where T : new()` ile yapılır |

---

## 9. Kontrol Soruları

1. Java'da `List<String>` runtime'da ne olur? C#'ta farkı ne?
Generics compile-time bilgisi taşır
Runtime’da type erasure olur → List olarak çalışır
Örneğin, List<String> → aslında runtime’da sadece List
Tip güvenliği compile-time’da kontrol edilir
2. `where T : class, new()` ne anlama gelir? İkisini neden birlikte kullanmak gerekebilir?
class → T reference type olmalı
new() → T parametresiz constructor’a sahip olmalı
3. `IEnumerable<out T>` neden `IEnumerable<Kedi>`'yi `IEnumerable<Hayvan>` yerine kullanmana izin verir?
out T → kovaryans
Sadece okumaya izin veriyor (return)
Liste içinden Kedi çıkar → bunu Hayvan olarak alabilirsin → güvenli
4. Generic repository pattern ne problemi çözer? `Repository<T>` ile `KitapRepository` arasındaki fark nedir?

Kitap Repositoryde her seyi tek tek diğer repolarda bastan tanımlarız Repositoryde methodların iterfacelerini ve iş bilgilerini tanımlayıp generic olarak kitap vb verebiliriz 1 kere işi yapmıs oluruz 

5. `List<Kedi>` neden `List<Hayvan>` yerine kullanılamaz, ama `IEnumerable<Kedi>` kullanılabilir?
