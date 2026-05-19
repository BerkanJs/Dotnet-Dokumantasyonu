# Gün 81 — Hafta 11 Özet

---

## Hafta 11 Hızlı Özet

| Gün | Konu | Tek Cümle |
|---|---|---|
| 73 | BenchmarkDotNet | Ölçmeden optimize etme — Release modda çalıştır, Allocated sütununa bak |
| 74 | Span / Memory / ArrayPool | Var olan belleği kopyalamadan dilimleme ve buffer kiralama |
| 75 | Struct / readonly struct / ref struct | Defensive copy'yi önle, cache locality kazan |
| 76 | String Optimizasyonları | Döngüde StringBuilder, tekrarlı regex'te GeneratedRegex, arama'da SearchValues |
| 77 | Async Performans | Cache'li metodlarda ValueTask, büyük veri setinde IAsyncEnumerable |
| 78 | Memory Leak | GC'ye rağmen leak olur — static koleksiyon, event, Dispose, closure, cache |
| 79 | Production Diagnostics | dotnet-counters → trace → gcdump → dump sıralaması |
| 80 | Streams / Pipelines | Büyük veriyi chunk'larla işle, stream'leri iç içe sar |

---

## Pratik Görev — CSV Parser

Bu görev hafta 11'deki birden fazla konuyu bir arada uygular:
- `ArrayPool<T>` ile buffer
- `Span<char>` ile parse
- BenchmarkDotNet ile karşılaştırma

```csharp
// Proje: Faz4-Performans/01-Benchmarks/CsvParserBenchmark.cs
[MemoryDiagnoser]
public class CsvParserBenchmark
{
    private readonly string _satir = "1;Dune;Frank Herbert;89.90;42";

    [Benchmark(Baseline = true)]
    public KitapDto SplitIle()
    {
        var parcalar = _satir.Split(';');           // 5 yeni string allocation
        return new KitapDto(
            int.Parse(parcalar[0]),
            parcalar[1],
            parcalar[2],
            decimal.Parse(parcalar[3]),
            int.Parse(parcalar[4]));
    }

    [Benchmark]
    public KitapDto SpanIle()
    {
        ReadOnlySpan<char> span = _satir.AsSpan();  // sıfır allocation
        Span<Range> araliklar = stackalloc Range[5]; // stack'te — heap yok
        int adet = span.Split(araliklar, ';');       // range'leri doldur

        return new KitapDto(
            int.Parse(span[araliklar[0]]),
            span[araliklar[1]].ToString(),           // string gerektiğinde bir kez allocation
            span[araliklar[2]].ToString(),
            decimal.Parse(span[araliklar[3]]),
            int.Parse(span[araliklar[4]]));
    }
}

public record KitapDto(int Id, string Ad, string Yazar, decimal Fiyat, int Stok);
```

**Beklenen sonuç:**
```
| Method   | Mean     | Allocated |
|--------- |---------:|----------:|
| SplitIle | 380 ns   | 280 B     |
| SpanIle  | 95 ns    | 64 B      |  ← 4x hızlı, çok daha az allocation
```

---

## Kontrol Soruları

1. `[MemoryDiagnoser]` olmadan benchmark çalıştırsan neyi kaçırırsın?
2. `stackalloc Range[5]` yerine `new Range[5]` yazsaydın ne değişirdi?
3. Gün 78'deki 5 leak senaryosunu sıradan hatırlayabilir misin?
4. Production'da bellek artıyor — hangi araçla başlarsın, sıradaki adım ne?
