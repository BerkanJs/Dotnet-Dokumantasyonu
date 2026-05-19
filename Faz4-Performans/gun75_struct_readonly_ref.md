# Gün 75 — Struct, readonly struct, ref struct

Değer tiplerini doğru kullanmak hem allocation'ı hem de gereksiz kopyalamayı önler.

---

## Struct vs Class — Temel Fark

```csharp
// Class → heap allocation, referans kopyalanır
class NokteClass { public double X; public double Y; }

// Struct → stack'te yaşar, değer kopyalanır, heap allocation yok
struct Nokta { public double X; public double Y; }
```

Struct ne zaman mantıklı:
- Küçük veri (genellikle < 16 byte)
- Kısa ömürlü, çok sayıda oluşturulan (koordinat, renk, para birimi)
- Değer semantiği istiyorsun — kopyalanınca bağımsız olsun

---

## Defensive Copying — Gizli Kopyalama Sorunu

`readonly` olmayan struct'ı `in` ile geçersen derleyici koruma amacıyla gizlice kopyalar.

```csharp
struct Sayac
{
    public int Deger;
    public void Artir() => Deger++;     // struct'ı değiştiren metot
}

void Yazdir(in Sayac s)                 // in → "kopyalama" diyorsun
{
    s.Artir();                          // ama Sayac readonly değil → derleyici gizli kopya oluşturur
}                                       // orijinal s hiç değişmedi, kopya çöpe gitti
```

Bu "defensive copy" — sessiz, fark etmesi zor, gereksiz allocation.

---

## readonly struct — Defensive Copy'yi Önler

```csharp
readonly struct Nokta
{
    public double X { get; }            // init-only — değiştirilemez
    public double Y { get; }

    public Nokta(double x, double y) { X = x; Y = y; }

    public double Uzaklik() => Math.Sqrt(X * X + Y * Y);
    // readonly struct'ta metot → derleyici kopyalamaz, güvenli
}

void Hesapla(in Nokta n)               // in + readonly struct → sıfır kopya garantisi
{
    Console.WriteLine(n.Uzaklik());    // kopyalama yok, referans gibi davranır
}
```

**Kural:** `in` parametresi kullanacaksan struct'ı `readonly` yap, yoksa defensive copy'den kurtulamazsın.

---

## ref struct — Sadece Stack

`ref struct` heap'e hiçbir şekilde gidemez. Bu onun gücü ve kısıtı.

```csharp
ref struct StackBuffer
{
    public Span<byte> Veri;
}

// ref struct kullanamadığın yerler:
class Foo
{
    StackBuffer _buf;                   // ❌ class field olamaz — heap'e gider
}

async Task IsleAsync()
{
    StackBuffer buf = default;          // ❌ async metodda kullanılamaz
    await Task.Delay(1);               //    await noktasında heap'e kaçabilir
}
```

**`Span<T>` neden ref struct?**  
Stack'e veya native memory'e işaret ediyor. Heap'e giderse işaret ettiği bellek geçersiz kalabilir. `ref struct` bunu derleyici seviyesinde engeller.

---

## Struct Array vs Class Array — Cache Locality

```csharp
// Class array → heap'te sadece referanslar yan yana, nesneler dağınık
NokteClass[] classlar = new NokteClass[1000];   // 1000 heap nesnesi, her yerde

// Struct array → veriler bellekte yan yana (contiguous)
Nokta[] structs = new Nokta[1000];              // X,Y,X,Y,X,Y... sıralı bellek
```

CPU cache satırı 64 byte. Struct array'de ardışık elemanlara erişim cache'te kalır.  
Class array'de her elemana erişim farklı bellek konumuna atlayabilir → cache miss.

```csharp
// Benchmark farkı büyük olabilir — 1000 elemanlı dizi üzerinde toplama:
double toplam = 0;
for (int i = 0; i < structs.Length; i++)
    toplam += structs[i].X;             // struct array → cache dostu, hızlı
```

---

## Özet Tablo

| | `struct` | `readonly struct` | `ref struct` |
|---|---|---|---|
| Nerede yaşar | Stack / inline | Stack / inline | Yalnızca stack |
| Heap allocation | Yok (boxing hariç) | Yok | Asla |
| Async'te kullanım | ✓ | ✓ | ❌ |
| Class field olabilir | ✓ | ✓ | ❌ |
| Defensive copy | Var (in ile) | Yok | — |

---

## 500 vs 50K Kullanıcı

| | 500 | 50K |
|---|---|---|
| readonly struct | İstediğin zaman, iyi alışkanlık | Hot path'te fark yaratır |
| ref struct | Nadiren gerekir | Parser/serializer yazıyorsan kritik |
| Cache locality | Genelde fark edilmez | Büyük veri işlemede belirgin kazanım |

---

## Kontrol Soruları

1. `readonly` olmayan struct'ı `in` ile geçince neden gizli kopya oluşur?
2. `Span<T>` neden `ref struct` olmak zorunda?
3. Struct array, class array'den neden cache açısından daha verimli?
4. `ref struct`'ı async metodda kullanamamanın teknik sebebi nedir?
