using System.Threading.Channels;

namespace KitabeviMVC.BackgroundServices;

// Gün 25: Channel<T> — Producer/Consumer pattern.
//
// Senaryo: Kullanıcı sipariş verdi → HTTP request hızla dönmeli.
// Onay e-postası göndermek 1-2 saniye sürer — bunu HTTP thread'inde yapma.
// Channel'a yaz (microsaniye), background servis okur ve gönderir.
//
// Bu sınıf DI container'a Singleton olarak kaydedilir.
// Hem controller (producer) hem SiparisOnayServisi (consumer) aynı instance'a erişir.
public class SiparisOnayKanali
{
    // "Channel<T>" → thread-safe, async kuyruk.
    // "BoundedChannel" → maksimum 100 mesaj — 100 dolunca producer bekler (back-pressure).
    // "UnboundedChannel" olsaydı sınır yok → bellek tüketir.
    private readonly Channel<SiparisOnayMesaji> _kanal;

    public SiparisOnayKanali()
    {
        _kanal = Channel.CreateBounded<SiparisOnayMesaji>(
            new BoundedChannelOptions(capacity: 100)
            {
                // Kanal dolduğunda: yeni öğe eklemek isteyeni beklet (wait).
                // Alternatifler: DropOldest (eskiyi at), DropWrite (yeniyi at), Fail (exception).
                FullMode = BoundedChannelFullMode.Wait,

                // Tek consumer var — optimizasyon ipucu
                SingleReader = true
            });
    }

    // "ChannelWriter<T>" → sadece yazma — controller bu tarafı görür
    public ChannelWriter<SiparisOnayMesaji> Yazar => _kanal.Writer;

    // "ChannelReader<T>" → sadece okuma — background servis bu tarafı görür
    public ChannelReader<SiparisOnayMesaji> Okuyucu => _kanal.Reader;
}

// Channel'da taşınan mesaj tipi — sadece gerekli bilgiler, büyük nesne değil
public record SiparisOnayMesaji(
    int    SiparisId,
    string MusteriEposta,
    string KitapBaslik,
    decimal ToplamTutar,
    DateTime SiparisTarihi
);
