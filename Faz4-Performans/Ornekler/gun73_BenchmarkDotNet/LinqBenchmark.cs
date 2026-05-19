// GÜN 73 — BenchmarkDotNet: LINQ vs For Döngüsü

using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class LinqVsForBenchmark
{
    private int[] _dizi = null!;

    [GlobalSetup]   // ne yapar: benchmark başlamadan önce bir kez çalışır, veriyi hazırlar
    public void Hazirla() => _dizi = Enumerable.Range(1, 10_000).ToArray();

    [Benchmark(Baseline = true)]
    public double LinqZinciri()
    {
        // ne yapar: diziyi filtrele → karesi al → ortalama bul
        // bunu yazmasaydık: for döngüsüyle 3 ayrı geçiş yazardık
        // SORUN: her operatör bir IEnumerable wrapper + delegate çağrısı üretir
        return _dizi
            .Where(x => x % 2 == 0)
            .Select(x => (double)(x * x))
            .Average();
    }

    [Benchmark]
    public double ForDongusu()
    {
        // ne yapar: tek geçişte filtre + kare + toplam hesaplar
        // bunu yazmasaydık: LINQ zincirinde 3 ayrı lazy pass olurdu
        // AVANTAJ: delegate maliyeti yok, wrapper yok, inlined
        long toplam = 0;
        int sayi = 0;
        foreach (var x in _dizi)
        {
            if (x % 2 != 0) continue;
            toplam += (long)x * x;
            sayi++;
        }
        return sayi == 0 ? 0d : (double)toplam / sayi;
    }
}

// Beklenen çıktı:
// | Method      | Mean     | Allocated |
// |------------ |---------:|----------:|
// | LinqZinciri | 48.2 μs  |      40 B |
// | ForDongusu  | 12.1 μs  |       0 B |  ← 4x hızlı, sıfır allocation

// KURAL: hot path (saniyede binlerce çağrı) → for/foreach tercih et
//        okunabilirlik önemli + hot path değilse → LINQ yeterli
