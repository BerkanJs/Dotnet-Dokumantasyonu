// GÜN 74 — Span<T> ve Memory<T>
// Span<T>: stack-only, slice işlemleri için sıfır-allocation view
// Memory<T>: heap'te yaşayabilen, async metodlarda kullanılabilen Span alternatifi

namespace Ornekler.gun74;

public static class SpanOrnekleri
{
    // --- 1. Dizi dilimlemek (kopyasız) ---
    public static void DiziDilimleme()
    {
        int[] dizi = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // ne yapar: dizinin 2. indeksten itibaren 5 elemanına pointer gibi bakar
        // bunu yazmasaydık: dizi.Skip(2).Take(5).ToArray() → yeni dizi oluşturulurdu (allocation)
        Span<int> dilim = dizi.AsSpan(2, 5);  // [3, 4, 5, 6, 7] — kopyalama yok

        // Span üzerinde değişiklik orijinal diziyi değiştirir
        dilim[0] = 99;
        Console.WriteLine(dizi[2]); // 99 — aynı bellek bölgesi
    }

    // --- 2. String'i kopyalamadan parse etmek ---
    public static int StringParseKopyasiz(string tarih)
    {
        // Örnek giriş: "2024-01-15"
        ReadOnlySpan<char> span = tarih.AsSpan();

        // ne yapar: substring oluşturmadan yıl kısmını okur
        // bunu yazmasaydık: tarih.Substring(0, 4) → yeni string nesnesi (allocation)
        ReadOnlySpan<char> yilSpan = span.Slice(0, 4);

        // ne yapar: Span<char>'ı doğrudan int'e parse eder — string oluşturmaz
        // bunu yazmasaydık: int.Parse(tarih.Substring(0, 4)) dememiz gerekirdi
        return int.Parse(yilSpan);
    }

    // --- 3. Stack üzerinde geçici buffer ---
    public static void StackBuffer()
    {
        // ne yapar: 128 byte'lık buffer'ı heap yerine STACK'te ayırır
        // bunu yazmasaydık: new byte[128] heap'te allocation yapardı → GC baskısı
        Span<byte> buffer = stackalloc byte[128];

        buffer.Fill(0);         // sıfırla
        buffer[0] = 0xFF;       // yaz
        // metod bitince stack otomatik temizlenir — GC yok
    }

    // --- 4. CSV satırını ayır (allocation-free) ---
    public static void CsvParse(ReadOnlySpan<char> satir)
    {
        // "Ali,25,Istanbul"
        int virgulIndex = satir.IndexOf(',');
        if (virgulIndex < 0) return;

        // ne yapar: ilk alanı kopyalamadan okur
        // bunu yazmasaydık: satir.ToString().Split(',') → yeni string[] + string nesneleri
        ReadOnlySpan<char> ad = satir.Slice(0, virgulIndex);
        ReadOnlySpan<char> kalan = satir.Slice(virgulIndex + 1);

        Console.WriteLine($"Ad: {ad.ToString()}");  // sadece burada string oluşur
    }
}

// Memory<T> farkı: async metodlarda Span kullanılamaz (stack-only kısıt)
public static class MemoryOrnekleri
{
    public static async Task BuyukDosyaOku(string yol)
    {
        using var dosya = File.OpenRead(yol);

        // ne yapar: heap'te buffer ayırır ama Span gibi slice yapılabilir
        // bunu yazmasaydık: async içinde Span<byte> kullanmaya çalışırsak derleme hatası alırız
        Memory<byte> buffer = new byte[4096];

        int okunan;
        while ((okunan = await dosya.ReadAsync(buffer)) > 0)
        {
            // ne yapar: sadece okunan kısmı işler, sıfır kopyalama
            // bunu yazmasaydık: buffer.ToArray().Take(okunan) → gereksiz allocation
            Memory<byte> veri = buffer.Slice(0, okunan);
            IsleAsync(veri);
        }
    }

    private static void IsleAsync(Memory<byte> veri)
    {
        // veri.Span ile Span<byte>'a dönüştür — sadece sync context'te
        var span = veri.Span;
        Console.WriteLine($"{span.Length} byte işlendi");
    }
}
