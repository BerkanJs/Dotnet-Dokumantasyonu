// GÜN 80 — Streams ve System.IO.Pipelines
// Stream: klasik, tampon yönetimi elle
// Pipelines: yüksek throughput için — backpressure + sıfır kopyalama

using System.IO.Pipelines;
using System.Text;

namespace Ornekler.gun80;

// --- 1. Stream zinciri: oku → sıkıştır → şifrele ---
public static class StreamZinciri
{
    public static async Task DosyaIsle(string kaynak, string hedef)
    {
        using var okuma = File.OpenRead(kaynak);
        using var yazma = File.Create(hedef);

        // ne yapar: okurken sıkıştırarak hedef dosyaya yazar — buffer'da hiçbir zaman tamamı yok
        // bunu yazmasaydık: tüm dosyayı belleğe çekmemiz gerekirdi (File.ReadAllBytes)
        using var gzip = new System.IO.Compression.GZipStream(
            yazma, System.IO.Compression.CompressionLevel.Optimal);

        // ne yapar: 81920 byte'lık buffer ile akışı kopyalar — optimal chunk boyutu
        // bunu yazmasaydık: CopyToAsync varsayılan 81920 kullanır ama açıkça belirtmek iyi pratik
        await okuma.CopyToAsync(gzip, bufferSize: 81_920);
    }
}

// --- 2. System.IO.Pipelines ile büyük veri okuma ---
public static class PipelineOrnegi
{
    public static async Task BuyukCsvOku(Stream stream)
    {
        // ne yapar: stream üzerinden verimli okuma için pipe oluşturur
        // bunu yazmasaydık: StreamReader ile satır satır okurduk — her satır string allocation
        var reader = PipeReader.Create(stream);

        while (true)
        {
            // ne yapar: mevcut buffer'ı okur — yeni allocation yok
            // bunu yazmasaydık: her read için yeni byte[] oluşturmak zorunda kalırdık
            ReadResult result = await reader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;

            // ne yapar: buffer içinde satır satır işler
            // bunu yazmasaydık: tüm içeriği string'e çevirip Split yapardık
            while (SatirBul(ref buffer, out ReadOnlySequence<byte> satir))
            {
                SatiriIsle(satir);
            }

            // ne yapar: işlediğimiz kısmı tükettik olarak işaretle
            // bunu yazmasaydık: aynı veriyi tekrar tekrar işlerdik
            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted) break;
        }

        await reader.CompleteAsync();
    }

    private static bool SatirBul(
        ref ReadOnlySequence<byte> buffer,
        out ReadOnlySequence<byte> satir)
    {
        var reader = new SequenceReader<byte>(buffer);

        // ne yapar: '\n' byte'ını arar — kopyalama yapmaz
        if (reader.TryReadTo(out satir, (byte)'\n'))
        {
            buffer = buffer.Slice(reader.Position);
            return true;
        }

        satir = default;
        return false;
    }

    private static void SatiriIsle(ReadOnlySequence<byte> satir)
    {
        // ne yapar: satırı UTF-8 string'e dönüştür (sadece işlemek için gerekli)
        // bunu yazmasaydık: tüm buffer'ı stringe çevirirdik
        string metin = Encoding.UTF8.GetString(satir);
        Console.WriteLine(metin.Trim());
    }
}

// --- 3. IAsyncEnumerable ile streaming response ---
public static class AsyncEnumerableOrnek
{
    // ne yapar: her satırı okuduğunda verir — tüm dosyayı belleğe almaz
    // bunu yazmasaydık: File.ReadAllLines → tüm dosya belleğe (1 GB dosya = 1 GB RAM)
    public static async IAsyncEnumerable<string> DosyaOku(string yol)
    {
        using var reader = new StreamReader(yol);
        string? satir;
        while ((satir = await reader.ReadLineAsync()) != null)
            yield return satir;
    }
}
