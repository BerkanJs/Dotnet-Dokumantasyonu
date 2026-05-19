// GÜN 74 — ArrayPool<T>: Buffer'ları Yeniden Kullan
// Her istek için new byte[] yapmak yerine havuzdan al, kullan, geri ver.

using System.Buffers;

namespace Ornekler.gun74;

public static class ArrayPoolOrnegi
{
    // --- YANLIŞ: Her işlemde yeni dizi oluştur ---
    public static byte[] YanlisYol(int boyut)
    {
        // ne yapar: heap'te yeni dizi oluşturur
        // SORUN: her çağrıda GC baskısı — yüksek trafikte ciddi latency spike'ları
        var buffer = new byte[boyut];
        // ... işlem yap ...
        return buffer;
    }

    // --- DOĞRU: ArrayPool'dan al, kullan, geri ver ---
    public static void DogruYol(int boyut)
    {
        // ne yapar: shared pool'dan buffer kirala — büyük ihtimalle yeni allocation yok
        // bunu yazmasaydık: her çağrıda yeni byte[] heap allocation yapardık
        byte[] buffer = ArrayPool<byte>.Shared.Rent(boyut);

        try
        {
            // DİKKAT: Rent, istenen boyuttan BÜYÜK dizi verebilir
            // → sadece [0..boyut) aralığını kullan
            Span<byte> gercekVeri = buffer.AsSpan(0, boyut);
            gercekVeri.Fill(0);  // temizle — pool'daki buffer önceki veriden kirli olabilir

            // ... işlem yap ...
        }
        finally
        {
            // ne yapar: buffer'ı temizleyerek havuza geri verir
            // bunu yazmasaydık: buffer GC'ye kalır, pool anlamı kalmaz
            // clearArray: true → güvenlik — hassas veri varsa sıfırla
            ArrayPool<byte>.Shared.Return(buffer, clearArray: false);
        }
    }

    // --- Gerçek senaryo: HTTP response body tamponu ---
    public static async Task ResponseYaz(Stream cikis, string icerik)
    {
        int maxBoyut = icerik.Length * 4; // UTF-8 worst case
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxBoyut);

        try
        {
            // ne yapar: string'i buffer'a encode eder, yeni byte[] oluşturmaz
            // bunu yazmasaydık: Encoding.UTF8.GetBytes(icerik) → yeni byte[] allocation
            int yazilan = System.Text.Encoding.UTF8.GetBytes(
                icerik.AsSpan(), buffer.AsSpan());

            // ne yapar: sadece yazılan kısmı stream'e gönderir
            await cikis.WriteAsync(buffer.AsMemory(0, yazilan));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
