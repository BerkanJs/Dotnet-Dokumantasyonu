namespace DipDemo;

// Yüksek seviyeli modül — iş kuralları burada
// EfKitapRepository'den haberdar değil, sadece IKitapRepository biliyor
public class KitapServisi
{
    private readonly IKitapRepository _repository;
    // interface tipinde — somut tip değil
    // bunu yazmasaydık → test için gerçek DB şart, ORM değişince bu class açılırdı

    public KitapServisi(IKitapRepository repository) => _repository = repository;
    // kim verirse onunla çalışır: prod → EF, test → InMemory

    public List<string> HepsiniGetir() => _repository.HepsiniGetir();

    public void KitapEkle(string baslik)
    {
        if (string.IsNullOrWhiteSpace(baslik))
            throw new ArgumentException("Başlık boş olamaz");
        // iş kuralı burada — repository'ye taşınmaz

        _repository.Ekle(baslik);
    }
}
