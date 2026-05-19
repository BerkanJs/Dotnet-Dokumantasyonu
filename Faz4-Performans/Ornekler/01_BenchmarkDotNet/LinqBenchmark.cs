// GÜN 73 — BenchmarkDotNet: LINQ vs For Döngüsü Karşılaştırması
// LINQ okunabilir ama her zaman en hızlı değil — ne zaman fark önemli?

using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class LinqVsForBenchmark
{
    private int[] _dizi = null!;

    // ne yapar: her benchmark koşusundan önce veriyi hazırlar
    // bunu yazmasaydık: benchmark her iterasyonda veri oluşturur, ölçümü bozar
    [GlobalSetup]
    public void Hazirla()
    {
        _dizi = Enumerable.Range(1, 10_000).ToArray();
    }

    // --- YÖNTEM 1: LINQ zinciri ---
    [Benchmark(Baseline = true)]
    public double LinqZinciri()
    {
        // ne yapar: diziyi filtrele, karesi al, ortalamasını bul
        // bunu yazmasaydık: for döngüsüyle 3 ayrı geçiş yapardık
        // SORUN: her LINQ operatörü bir IEnumerable<T> wrapper oluşturur → delegate çağrısı maliyeti
        return _dizi
            .Where(x => x % 2 == 0)
            .Select(x => (double)(x * x))
            .Average();
    }

    // --- YÖNTEM 2: For döngüsü ---
    [Benchmark]
    public double ForDongusu()
    {
        // ne yapar: tek geçişte filtre + kare + toplam yapar
        // bunu yazmasaydık: LINQ zinciriyle 3 ayrı geçiş yapılırdı
        // AVANTAJ: delegate maliyeti yok, IEnumerable wrapper yok, inlined kod
        long toplam = 0;
        int sayi = 0;
        foreach (var x in _dizi)
        {
            if (x % 2 != 0) continue;
            toplam += (long)x * x;
            sayi++;
        }
        return sayi == 0 ? 0 : (double)toplam / sayi;
    }

    // --- YÖNTEM 3: LINQ ama ToList()/ToArray() önce ---
    [Benchmark]
    public double LinqMaterialize()
    {
        // ne yapar: filtreyi önce somutlaştırır, sonra LINQ uygular
        // bunu yazmasaydık: her Where/Select lazy evaluate olurdu
        // NOT: bu genelde DAHA YAVAŞ — ekstra allocation var
        var ciftler = _dizi.Where(x => x % 2 == 0).ToArray();
        return ciftler.Select(x => (double)(x * x)).Average();
    }
}

// Beklenen sonuç:
// | Method            | Mean       | Gen0   | Allocated |
// |------------------ |-----------:|-------:|----------:|
// | LinqZinciri       | 48.2 μs    | -      |    40 B   |  ← lazy, az allocation ama delegate maliyeti
// | ForDongusu        | 12.1 μs    | -      |     0 B   |  ← en hızlı, sıfır allocation
// | LinqMaterialize   | 89.4 μs    | 9.8145 |    80 KB  |  ← en yavaş, ekstra ToArray allocation

// SONUÇ: Hot path'de (saniyede binlerce kez çağrılan) for döngüsü tercih et.
// Okunabilirlik önemli ve hot path değilse LINQ kullan — fark ihmal edilebilir.
