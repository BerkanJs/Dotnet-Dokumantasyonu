// GÜN 73 — BenchmarkDotNet: String Birleştirme Karşılaştırması
// Kurulum: dotnet add package BenchmarkDotNet
// Çalıştırma: dotnet run -c Release

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Text;

// ne yapar: BenchmarkDotNet bu attribute'ü görünce her metodu ayrı ayrı ölçer
// bunu yazmasaydık: sadece metodları çalıştırırdık, kaç ns sürdüğünü bilemezdik
[MemoryDiagnoser]   // allocation'ı da ölçer (byte cinsinden)
[SimpleJob]         // varsayılan: Release modda, ısınma + ölçüm döngüleri
public class StringBirlesirmeBenchmark
{
    // ne yapar: benchmark'ta kullanılacak parametre setini tanımlar
    // bunu yazmasaydık: sadece tek bir N değeriyle test yapabilirdik
    [Params(10, 100, 1000)]
    public int N;

    // --- YÖNTEM 1: + operatörü ile döngüde birleştirme ---
    [Benchmark(Baseline = true)]    // diğer benchmark'lar buna göre karşılaştırılır
    public string PlusOperatoru()
    {
        string sonuc = "";
        for (int i = 0; i < N; i++)
        {
            // ne yapar: her iterasyonda yeni bir string nesnesi oluşturur
            // bunu yazmasaydık: döngü içinde string birleştiremezdik
            // SORUN: N=1000 → 1000 geçici string nesnesi → GC baskısı
            sonuc += "x";
        }
        return sonuc;
    }

    // --- YÖNTEM 2: StringBuilder ---
    [Benchmark]
    public string StringBuilderIle()
    {
        // ne yapar: tek bir buffer üzerinde çalışır, string kopyalamaz
        // bunu yazmasaydık: + operatörüyle aynı GC baskısına maruz kalırdık
        var sb = new StringBuilder(N);  // başlangıç kapasitesi ver → resize olmasın
        for (int i = 0; i < N; i++)
            sb.Append('x');
        return sb.ToString();           // sadece burada bir string nesnesi oluşur
    }

    // --- YÖNTEM 3: string.Create (sıfır allocation) ---
    [Benchmark]
    public string StringCreate()
    {
        // ne yapar: sonuç string'i için tam doğru boyutta bellek ayırır, içine yazar
        // bunu yazmasaydık: StringBuilder gibi intermediate buffer kullanmak zorunda kalırdık
        // AVANTAJ: sadece bir allocation, StringBuilder'dan bile hızlı
        return string.Create(N, N, (span, length) =>
        {
            span.Fill('x');     // Span<char> üzerinden doğrudan yaz — kopyalama yok
        });
    }
}

// Program giriş noktası:
// BenchmarkRunner.Run<StringBirlesirmeBenchmark>();

// Beklenen sonuç (N=1000):
// | Method          | N    | Mean        | Gen0    | Allocated |
// |---------------- |----- |------------:|--------:|----------:|
// | PlusOperatoru   | 1000 | 45,123.0 ns | 85.0000 |  714.8 KB |  ← en yavaş, en çok allocation
// | StringBuilderIle| 1000 |    892.3 ns |  0.5000 |    4.1 KB |  ← orta
// | StringCreate    | 1000 |    134.7 ns |  0.1250 |    2.0 KB |  ← en hızlı, en az allocation
