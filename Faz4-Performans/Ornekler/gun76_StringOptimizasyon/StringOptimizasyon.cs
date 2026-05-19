// GÜN 76 — String Optimizasyonları
// String immutable → her değişiklik yeni nesne → çok string işleyen kodda GC baskısı

namespace Ornekler.gun76;

public static class StringOptimizasyon
{
    // --- 1. string.Format vs interpolation vs string.Create ---
    public static void FormatKarsilastirma(string ad, int yas)
    {
        // Hepsi aynı sonucu üretir ama farklı allocation
        var a = "Ad: " + ad + ", Yaş: " + yas;             // 3 geçici string
        var b = string.Format("Ad: {0}, Yaş: {1}", ad, yas); // boxing (int → object)
        var c = $"Ad: {ad}, Yaş: {yas}";                    // modern, compiler optimize eder

        // ne yapar: sıfır allocation ile string üretir — tam boyut bilinince kullan
        // bunu yazmasaydık: interpolation bile geçici string nesneleri üretebilir
        var d = string.Create(
            ad.Length + 12,
            (ad, yas),
            static (span, state) =>
            {
                "Ad: ".AsSpan().CopyTo(span);
                state.ad.AsSpan().CopyTo(span[4..]);
                ", Yaş: ".AsSpan().CopyTo(span[(4 + state.ad.Length)..]);
                state.yas.TryFormat(span[(11 + state.ad.Length)..], out _);
            });
    }

    // --- 2. String interning — aynı sabit string için tek nesne ---
    public static void Interning()
    {
        string a = "merhaba";
        string b = "merhaba";

        // ne yapar: compile-time sabitleri otomatik intern edilir → aynı referans
        // bunu yazmasaydık: her "merhaba" literal için ayrı nesne sanırdık
        Console.WriteLine(ReferenceEquals(a, b)); // True — aynı nesne

        string c = new string(new[] { 'm', 'e', 'r', 'h', 'a', 'b', 'a' });
        Console.WriteLine(ReferenceEquals(a, c)); // False — runtime'da oluşturuldu

        // ne yapar: runtime'da oluşturulan string'i intern havuzuna ekler
        // bunu yazmasaydık: aynı değeri taşıyan her string ayrı heap nesnesi olurdu
        string d = string.Intern(c);
        Console.WriteLine(ReferenceEquals(a, d)); // True
    }

    // --- 3. Char by char kontrol yerine Span kullan ---
    public static bool TurkceKarakterIceriyorMu(string metin)
    {
        ReadOnlySpan<char> span = metin.AsSpan();

        // ne yapar: her karakter üzerinden geçer, string kopyalamaz
        // bunu yazmasaydık: metin.ToCharArray() → yeni char[] allocation
        foreach (var c in span)
        {
            if (c is 'ç' or 'ğ' or 'ı' or 'ö' or 'ş' or 'ü' or
                     'Ç' or 'Ğ' or 'İ' or 'Ö' or 'Ş' or 'Ü')
                return true;
        }
        return false;
    }

    // --- 4. Split yerine MemoryExtensions.Split ---
    public static void KopyasizSplit(string csv)
    {
        ReadOnlySpan<char> span = csv.AsSpan();

        // ne yapar: satırı ayırırken string[] veya yeni string nesneleri oluşturmaz
        // bunu yazmasaydık: csv.Split(',') → string[] + N string nesnesi (her alan için)
        foreach (var aralik in span.Split(','))
        {
            ReadOnlySpan<char> alan = span[aralik];
            Console.WriteLine(alan.ToString()); // sadece yazdırmak için string oluştur
        }
    }

    // --- 5. Büyük-küçük harf kontrolü: ToLower allocation yapar ---
    public static bool BaslikEslesiyorMu(string baslik, string aranan)
    {
        // YANLIŞ: baslik.ToLower() yeni string oluşturur
        // return baslik.ToLower() == aranan.ToLower();

        // ne yapar: yeni string oluşturmadan case-insensitive karşılaştırır
        // bunu yazmasaydık: her karşılaştırmada 2 geçici string nesnesi oluşturulurdu
        return baslik.AsSpan().Equals(aranan.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }
}
