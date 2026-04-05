# Gün 10 — Delegates, Events ve Functional Patterns

---

## 1. Delegate Nedir?

Delegate, **bir metodu temsil eden tip**. "Bu metodu bir değişkene atayabilir, parametre olarak geçebilir, sonra çağırabilirsin" demek.

JavaScript'ten tanıdık: fonksiyonu değişkene atayıp geçirmek. C#'ta bunu delegate sağlar.

```csharp
// Delegate tipi tanımı — "int alan, bool döndüren metot" tipi
delegate bool FiyatKontrol(decimal fiyat);

// Bu delegate'e uyan bir metot
bool UcuzMu(decimal fiyat) => fiyat < 50;

// Delegate değişkenine metodu ata
FiyatKontrol kontrol = UcuzMu;

// Çağır
bool sonuc = kontrol(45m);  // UcuzMu(45m) çağrıldı → true
```

Delegate'i neden kullanırsın? Başka bir metoda "çağrılacak metodu" parametre olarak geçmek için:

```csharp
List<Kitap> Filtrele(List<Kitap> kitaplar, FiyatKontrol kontrol)
{
    return kitaplar.Where(k => kontrol(k.Fiyat)).ToList();
}

// Farklı filtrelerle kullanım
var ucuzlar  = Filtrele(kitaplar, fiyat => fiyat < 50);
var pahalılar = Filtrele(kitaplar, fiyat => fiyat > 80);
```

---

## 2. Action, Func, Predicate — Hazır Delegate Tipleri

Kendi delegate tipini tanımlamana gerek yok. .NET'in hazır delegate tipleri var:

**`Action<T>`** — bir şey yapar, sonuç döndürmez (`void`):

```csharp
Action<string> yazdir = mesaj => Console.WriteLine(mesaj);
yazdir("Merhaba");

Action<Kitap, decimal> fiyatGuncelle = (kitap, yeniFiyat) =>
{
    kitap.Fiyat = yeniFiyat;
};
```

**`Func<T, TResult>`** — bir şey yapar, sonuç döndürür:

```csharp
Func<decimal, decimal> kdvHesapla = fiyat => fiyat * 1.20m;
decimal toplamFiyat = kdvHesapla(100m);  // 120

Func<Kitap, string> baslikAl = k => k.Baslik;
```

**`Predicate<T>`** — `Func<T, bool>`'un kısaltması:

```csharp
Predicate<Kitap> ucuzMu = k => k.Fiyat < 50;
bool sonuc = ucuzMu(new Kitap { Fiyat = 45m });  // true
```

LINQ'nun `Where`, `Select` gibi metodları bu delegate tipleri parametre olarak alır:

```csharp
// Where → Func<Kitap, bool> bekler
kitaplar.Where(k => k.Fiyat < 50);

// Select → Func<Kitap, TResult> bekler
kitaplar.Select(k => k.Baslik);
```

---

## 3. Lambda Expression — Kısa Metot Yazımı

Lambda, isimsiz bir metot. `=>` ile tanımlanır.

```csharp
// Uzun yol — isimli metot
bool UcuzMu(Kitap k) { return k.Fiyat < 50; }

// Lambda — aynı şey, kısa
Func<Kitap, bool> ucuzMu = k => k.Fiyat < 50;
```

Tek satırda yazılabilen basit ifadeler için lambda kullanılır. Birden fazla satır gerekiyorsa süslü parantezle:

```csharp
Func<Kitap, string> ozetle = k =>
{
    var kdv = k.Fiyat * 1.20m;
    return $"{k.Baslik} — KDV'li: {kdv:C}";
};
```

---

## 4. Closure — Dış Değişkeni Yakalamak

Lambda, tanımlandığı scope'taki değişkenleri "yakalayabilir". Buna **closure** denir.

```csharp
decimal sinir = 50m;  // dış değişken

Func<Kitap, bool> filtre = k => k.Fiyat < sinir;  // sinir yakalandı

sinir = 80m;  // sinir değişti

// filtre şimdi 80 kullanıyor — sinir'e referans var, kopyasına değil
var sonuc = filtre(new Kitap { Fiyat = 60m });  // true (60 < 80)
```

**Neden önemli?** Lambda, `sinir`'in kopyasını almaz — referansını tutar. `sinir` değişirse lambda'nın davranışı da değişir.

**Heap allocation!**

Closure bir değişkeni yakaladığında, o değişken stack'ten heap'e taşınır. Lambda'nın "arka planda" oluşturduğu nesne bu değişkeni tutar. Çok sık çağrılan kodda bu allocation birikir.

```csharp
// Her çağrıda closure allocation
for (int i = 0; i < 1000; i++)
{
    int esik = i * 10;
    kitaplar.Where(k => k.Fiyat > esik);  // esik heap'e taşındı
}
```

Pratikte web API'de büyük sorun olmaz. Ama hot-path kodunda (çok sık çağrılan endpoint) dikkat et.

---

## 5. Multicast Delegate — Birden Fazla Metot

Bir delegate'e birden fazla metot ekleyebilirsin. Çağrıldığında hepsi sırayla çalışır:

```csharp
Action<Kitap> islemler = null;

islemler += k => Console.WriteLine($"Log: {k.Baslik}");
islemler += k => Console.WriteLine($"Cache temizle: {k.Id}");
islemler += k => Console.WriteLine($"Bildirim gönder: {k.Baslik}");

islemler(kitap);  // üçü de çalışır
```

`+=` ile ekler, `-=` ile çıkarırsın. Bu event'lerin temelini oluşturur.

---

## 6. Event — Delegate'in Kontrollü Hali

`event` keyword'ü delegate üzerine bir koruma katmanı ekler:

```csharp
class KitapServisi
{
    // event — dışarıdan sadece += ve -= yapılabilir
    public event Action<Kitap>? KitapEklendi;

    public void KitapEkle(Kitap kitap)
    {
        // iş mantığı
        KitapEklendi?.Invoke(kitap);  // aboneleri bilgilendir
    }
}
```

Delegate olsaydı dışarıdan `KitapEklendi = null` yazılabilirdi — tüm aboneler silinirdi. `event` bunu engeller.

**Gün 5'teki memory leak buraya bağlanıyor:**

```csharp
// BildirimServisi, KitapServisi'ne abone oldu
kitapServisi.KitapEklendi += _bildirimServisi.Bildir;

// BildirimServisi artık kullanılmasa bile
// KitapServisi onu event üzerinden tutuyor → GC silemez → memory leak
```

Çözüm: `IDisposable` implement et, `Dispose()` içinde `-=` yap.

---

## 7. Web Geliştirmede Nerede Görünür?

**ASP.NET Core Middleware:**

```csharp
// app.Use → Action<HttpContext, Func<Task>> delegate alır
app.Use(async (context, next) =>
{
    // istek öncesi
    await next();  // sonraki middleware'e geç
    // istek sonrası
});
```

Middleware pipeline tamamen delegate zinciri üzerine kurulu.

**DI Extension Methods:**

```csharp
// Action<DbContextOptionsBuilder> delegate alır
builder.Services.AddDbContext<KitabeviDbContext>(options =>
    options.UseSqlServer(connectionString));
```

`options =>` bir lambda — `Action<DbContextOptionsBuilder>` delegate'i.

**LINQ:**

```csharp
kitaplar.Where(k => k.Fiyat < 50)   // Func<Kitap, bool>
        .Select(k => k.Baslik)       // Func<Kitap, string>
        .OrderBy(k => k.Baslik);     // Func<Kitap, string>
```

Delegate ve lambda kullanmadan LINQ yazamazsın.

---

## 8. Observer Pattern — Event ile

Event, Observer pattern'in C# natif implementasyonu:

```csharp
// Publisher (yayıncı)
class SiparisServisi
{
    public event Action<int>? SiparisOlusturuldu;

    public async Task SiparisVer(int kitapId)
    {
        // sipariş oluştur...
        SiparisOlusturuldu?.Invoke(kitapId);  // herkesi bilgilendir
    }
}

// Subscriber (abone)
class StokServisi
{
    public StokServisi(SiparisServisi siparisServisi)
    {
        siparisServisi.SiparisOlusturuldu += StokDus;
    }

    void StokDus(int kitapId) => Console.WriteLine($"Kitap {kitapId} stok azaldı");
}
```

Modern .NET'te bu pattern'i genellikle **MediatR** veya **domain events** ile yaparsın (Faz 3'te göreceğiz). Ama temelinde bu var.

---

## 9. Kontrol Soruları

1. `Action<T>` ile `Func<T, TResult>` arasındaki fark nedir?

action void geriye bir şey döndürmez 

2. Şu kodu düşün — `filtre` çağrıldığında `sinir` kaçtır?
   ```csharp
   int sinir = 100;
   Func<int, bool> filtre = x => x > sinir;
   sinir = 200;
   Console.WriteLine(filtre(150));
   ```

false 


3. Closure neden heap allocation yaratır?

dısarıdan aldığımız degisken heap'e tasınır eğer dönguye falan girerse bu sefer heap'e cok fazla memory yuklemis oluruz

4. `event` keyword'ü delegate'ten nasıl farklı? Neden sadece delegate kullanmak tehlikeli?

delegate değişkeni herkes tarafından invoke edilebilir ve set edilebilir → tehlikeli.
event ile sadece += veya -= yapılabilir, dışarıdan doğrudan çağrı engellenir.
Heap allocation ile doğrudan ilgisi yok, event sadece erişim kontrolü sağlar.

5. ASP.NET Core middleware'de `app.Use(async (context, next) => ...)` yazarken hangi delegate tipini kullanıyorsun?

app.Use(...) lambda’sı Func<HttpContext, Func<Task>, Task> delegate tipini kullanır, ve zincirdeki middleware’ler RequestDelegate tipinde organize edilir.