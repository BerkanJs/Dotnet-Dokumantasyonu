// GÜN 73 — Allocation Analizi: BenchmarkDotNet
// Kurulum: dotnet add package BenchmarkDotNet
// Çalıştırma: dotnet run -c Release   ← Release ZORUNLU, Debug'da sonuçlar güvenilmez

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Text;

[MemoryDiagnoser]   // ne yapar: her benchmark'ın kaç byte allocation yaptığını da raporlar
[SimpleJob]         // ne yapar: tek bir job tanımlar — ısınma + ölçüm döngüleri
public class StringBirlesirmeBenchmark
{
    [Params(10, 100, 1000)]     // ne yapar: benchmark'ı 3 farklı N değeri için çalıştırır
    public int N;               // bunu yazmasaydık: sadece sabit bir boyutla test yapabilirdik

    [Benchmark(Baseline = true)]
    public string PlusOperatoru()
    {
        string sonuc = "";
        for (int i = 0; i < N; i++)
            // ne yapar: her adımda yeni bir string nesnesi oluşturur ve kopyalar
            // bunu yazmasaydık: string birleştiremezdik
            // SORUN: N=1000 → 1000 geçici string → GC baskısı → yavaşlık
            sonuc += "x";
        return sonuc;
    }

    [Benchmark]
    public string StringBuilderIle()
    {
        var sb = new StringBuilder(N); // ne yapar: başlangıç kapasitesi ver, resize engelle
        for (int i = 0; i < N; i++)
            sb.Append('x');
        // ne yapar: sadece burada tek bir string nesnesi üretilir
        // bunu yazmasaydık: + operatörü gibi N tane geçici nesne üretilirdi
        return sb.ToString();
    }

    [Benchmark]
    public string StringCreate()
    {
        // ne yapar: sonuç için tam boyutlu bellek ayırır, doğrudan içine yazar
        // bunu yazmasaydık: StringBuilder gibi intermediate buffer gerekirdi
        // AVANTAJ: tek allocation, sıfır kopyalama
        return string.Create(N, N, static (span, _) => span.Fill('x'));
    }
}

// Beklenen çıktı (N=1000):
// | Method           | N    | Mean       | Allocated |
// |----------------- |----- |-----------:|----------:|
// | PlusOperatoru    | 1000 | 45,000 ns  | 714 KB    |  ← en yavaş
// | StringBuilderIle | 1000 |    890 ns  |   4 KB    |
// | StringCreate     | 1000 |    135 ns  |   2 KB    |  ← en hızlı
