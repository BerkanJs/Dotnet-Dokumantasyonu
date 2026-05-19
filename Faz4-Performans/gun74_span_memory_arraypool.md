# Gün 74 — Span\<T\>, Memory\<T\>, ArrayPool\<T\>

Heap allocation'ı sıfıra indirmenin temel araçları. Her biri farklı bir senaryoya hizmet eder.

---

## Span\<T\> — Allocation-Free Dilim

`Span<T>` var olan bir bellek bloğunun dilimini temsil eder. Yeni array kopyalamaz, heap'e gitmez.

```csharp
string veri = "2024-01-15";

// Kötü — her Substring yeni string allocation yapar
int yil  = int.Parse(veri.Substring(0, 4));   // yeni string → heap
int ay   = int.Parse(veri.Substring(5, 2));   // yeni string → heap
int gun  = int.Parse(veri.Substring(8, 2));   // yeni string → heap

// İyi — Span ile sıfır allocation
ReadOnlySpan<char> span = veri.AsSpan();
int yil2 = int.Parse(span[..4]);              // dilim — kopyalama yok, heap yok
int ay2  = int.Parse(span[5..7]);
int gun2 = int.Parse(span[8..]);
```

**Kısıt:** `Span<T>` bir `ref struct`'tır — async metotlarda ve class field'larında kullanılamaz.

---

## Memory\<T\> — Async Context İçin Span

Async metoduna `Span<T>` geçemezsin. Bunun yerine `Memory<T>` kullan.

```csharp
// Span → async metodda derlenmez
async Task IsleAsync(Span<byte> veri) { }  // ❌ derleme hatası

// Memory → async'te çalışır
async Task IsleAsync(Memory<byte> veri, CancellationToken ct)
{
    await _stream.WriteAsync(veri, ct);    // ✓
    // Span gerekirse: veri.Span ile erişirsin
}
```

---

## ArrayPool\<T\> — Geçici Array Kiralama

Kısa ömürlü büyük array'ler için her seferinde `new byte[4096]` yazmak GC baskısı yaratır. `ArrayPool` kirala-kullan-iade döngüsüyle allocation'ı sıfırlar.

```csharp
var pool = ArrayPool<byte>.Shared;

byte[] buffer = pool.Rent(4096);           // havuzdan al — yeni allocation yok
// bunu new byte[4096] yapsaydık → her çağrıda GC'ye yük

try
{
    int okunan = await _stream.ReadAsync(buffer.AsMemory(0, 4096), ct);
    // işlem yap...
}
finally
{
    pool.Return(buffer);                   // havuza iade et — zorunlu, unutursan leak
    // bunu yazmasaydık → buffer havuza dönmez, avantaj kaybolur
}
```

**Dikkat:** `pool.Rent(n)` tam `n` boyut garantisi vermez, daha büyük dönebilir. Her zaman gerçek boyutu takip et.

---

## stackalloc — Stack'te Dizi

Çok küçük, kısa ömürlü array'ler için stack'te yer ayır — GC hiç devreye girmez.

```csharp
// Küçük buffer için — heap allocation sıfır
Span<byte> buffer = stackalloc byte[128];
// bunu new byte[128] yapsaydık → heap allocation, GC takip eder
// 128 byte'tan büyük için stackalloc tehlikeli — stack overflow riski
```

---

## String Split Yerine AsSpan

```csharp
string satir = "Berkan;35;İstanbul";

// Kötü — her parça için yeni string
string[] parcalar = satir.Split(';');       // 3 yeni string allocation

// İyi — allocation yok
ReadOnlySpan<char> span = satir.AsSpan();
int ilkNoktalı = span.IndexOf(';');
ReadOnlySpan<char> ad  = span[..ilkNoktalı];
ReadOnlySpan<char> yas = span[(ilkNoktalı + 1)..span[(ilkNoktalı + 1)..].IndexOf(';') + ilkNoktalı + 1];
// Karmaşıklaşıyorsa → MemoryExtensions.Split() ile daha okunur
```

---

## Hangisini Ne Zaman Kullanırsın?

| Araç | Ne Zaman |
|---|---|
| `Span<T>` | Sync metot, var olan belleği dilimleme |
| `Memory<T>` | Async metot, Span geçilemeyen yer |
| `ArrayPool<T>` | Geçici büyük buffer, çok sık çağrılan yer |
| `stackalloc` | < 256 byte, çok kısa ömürlü, sync |

---

## 500 vs 50K Kullanıcı

| | 500 | 50K |
|---|---|---|
| Span/Memory | Erken optimizasyon olabilir | Parser, serializer hot path'te kritik |
| ArrayPool | Yalnızca büyük buffer varsa | Network/IO kodunda zorunlu gibi |
| stackalloc | İstediğin zaman — risksiz küçük buffer | Aynı |

---

## Kontrol Soruları

1. `Span<T>` async metodda neden kullanılamaz?
2. `ArrayPool.Rent()` sonrası `Return()` çağırmazsan ne olur?
3. `stackalloc` ne zaman tehlikeli hale gelir?
4. `Memory<T>` ile `Span<T>` arasında seçim yaparken belirleyici faktör nedir?
