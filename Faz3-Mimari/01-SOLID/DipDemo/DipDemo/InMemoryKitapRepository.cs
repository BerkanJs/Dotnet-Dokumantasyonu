namespace DipDemo;

// Test repository — DB bağlantısı yok, bellekte çalışır
// KitapServisi bunu bilmiyor, IKitapRepository gördüğü için kabul eder
public class InMemoryKitapRepository : IKitapRepository
{
    private readonly List<string> _store = [];

    public List<string> HepsiniGetir() => _store;

    public void Ekle(string baslik) => _store.Add(baslik);
    // sessiz — console yok, DB yok, sadece liste
    // test yazarken assert için _store'u doğrudan kontrol edebilirsin
}
