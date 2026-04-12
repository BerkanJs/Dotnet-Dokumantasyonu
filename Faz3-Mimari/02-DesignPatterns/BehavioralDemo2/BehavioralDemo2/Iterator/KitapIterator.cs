namespace BehavioralDemo2.Iterator;

// Iterator Pattern: koleksiyonun iç yapısını bilmeden elemanlar üzerinde gezilebilir
// C#'ta IEnumerable<T> + yield return bu pattern'in dil desteği

public class KitapKoleksiyonu
{
    private readonly List<string> _kitaplar =
        ["Suç ve Ceza", "Sefiller", "Dune", "1984", "Cesur Yeni Dünya"];

    // IEnumerable<T> dönen metot — foreach ile kullanılabilir
    // yield return: her çağrıda bir sonraki eleman üretilir, tüm liste bellekte tutulmaz
    public IEnumerable<string> PahaliBas(decimal limitFiyat)
    {
        foreach (var kitap in _kitaplar)
        {
            // gerçek projede fiyat kontrolü burada
            // demo'da harf sayısına göre filtre yapıyoruz
            if (kitap.Length > 5)
                yield return kitap;
                // bunu yazmasaydık → tüm liste filtrelenip bellekte tutulurdu
                // yield ile: her eleman tek tek üretilir (lazy evaluation)
        }
    }

    // IAsyncEnumerable<T>: async streaming — büyük veri setinde satır satır işle
    // DB'den binlerce kayıt çekerken: hepsini bellekte tutma, satır satır gönder
    public async IAsyncEnumerable<string> StreamKitaplar()
    {
        foreach (var kitap in _kitaplar)
        {
            await Task.Delay(10);   // DB I/O simülasyonu
            yield return kitap;
            // bunu yazmasaydık → tüm kayıtlar bellekte toplanırdı
            // 100k satırda bellek sorunu
        }
    }
}
