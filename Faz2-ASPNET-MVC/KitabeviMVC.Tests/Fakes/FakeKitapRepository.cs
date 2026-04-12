using KitabeviMVC.Models.Entities;
using KitabeviMVC.Repositories;

namespace KitabeviMVC.Tests.Fakes;

// ─────────────────────────────────────────────────────────────────────────────
// FakeKitapRepository — IKitapRepository'nin test amaçlı gerçek implementasyonu.
//
// Neden Fake? Mock yerine ne zaman Fake kullanılır?
//   Mock: "AddAsync çağrıldı mı?" gibi davranış doğrulama soruları için.
//   Fake: "Ekle, sonra getir" gibi state bazlı test senaryoları için.
//
//   Gerçek hayat örneği:
//   Sipariş servisi kitap ekleyip hemen geri çekiyor.
//   Mock ile: sadece "Add çağrıldı" doğrulanır; ama gerçekten getirilip getirilmediği test edilemez.
//   Fake ile: gerçek ekle/bul döngüsü DB olmadan test edilir.
//
// Fake, production'da KULLANILMAZ — sadece test projesinde yaşar.
// EF Core InMemoryDatabase'den farkı: daha hafif, daha hızlı, daha öngörülebilir.
// ─────────────────────────────────────────────────────────────────────────────
public class FakeKitapRepository : IKitapRepository
{
    private readonly List<Kitap> _veri = new();
    // In-memory liste: gerçek veriyi saklar.
    // Bunu readonly yapmak: listeyi değiştiremeyiz demek değil, referansı değiştiremeyiz demek.
    // _veri = new List<Kitap>() yeniden ataması engellenmiş; .Add() ve .Remove() serbestçe çalışır.

    private int _sonId = 0;
    // Otomatik ID üreticisi: DB'nin IDENTITY/AUTO_INCREMENT kolonunu simüle eder.
    // Her AddAsync() çağrısında ++_sonId ile artırılır.
    // Bunu yazmassaydık tüm kayıtlar Id=0 kalırdı → GetById(1) hiç çalışmazdı.

    // ─── Temel CRUD ───────────────────────────────────────────────────────────

    public Task<Kitap?> GetByIdAsync(int id)
    {
        var kitap = _veri.FirstOrDefault(k => k.Id == id);
        // LINQ FirstOrDefault: koşula uyan ilk elemanı döner, yoksa null.
        // Bunu Single() yapmak: birden fazla eşleşme varsa exception fırlatır — gereksiz katı.
        return Task.FromResult(kitap);
        // Task.FromResult: async olmayan bir değeri Task'a sarar.
        // await kullanmak zorunda değiliz çünkü gerçek I/O yok (in-memory).
        // Bunu Task.CompletedTask yazmak: void dönüş için geçerli, T? dönüş için değil.
    }

    public Task<IList<Kitap>> GetAllAsync()
    {
        IList<Kitap> liste = _veri.ToList();
        // .ToList(): orijinal listenin KOPYASINI oluşturur.
        // Bunu return _veri yapmak: çağıran kod _veri referansını alır;
        // sonraki Add/Remove işlemleri direkt _veri'yi etkiler → test izolasyonu bozulur.
        return Task.FromResult(liste);
    }

    public Task AddAsync(Kitap entity)
    {
        entity.Id = ++_sonId;
        // ++ prefix: önce artır, sonra ata. İlk kayıt Id=1 olur (0+1=1).
        // Bunu _sonId++ yapmak: Id=0 atar, sonra _sonId=1 olur → ilk Id yanlış.

        entity.EklemeTarihi = DateTime.UtcNow;
        // EF Core production'da HasDefaultValueSql("GETUTCDATE()") ile DB atar.
        // Fake'de biz elle atıyoruz — davranışı simüle ediyoruz.

        _veri.Add(entity);
        return Task.CompletedTask;
        // Task.CompletedTask: tamamlanmış boş Task — void async'in Fake eşdeğeri.
    }

    public void Update(Kitap entity)
    // Update senkron: EF Core'da tracking zaten var, ayrı SaveChanges gerekir.
    // Fake'de entity referansını listede güncelliyoruz.
    {
        var index = _veri.FindIndex(k => k.Id == entity.Id);
        // FindIndex: koşula uyan ilk elemanın indeksini döner, bulamazsa -1.
        if (index >= 0)
            _veri[index] = entity;
        // Entity'yi yenisiyle değiştir. index < 0 ise: sessizce devam et.
        // Exception fırlatmak: IRepository<T>.Update kontratı exception belirtmiyor.
    }

    public void Delete(Kitap entity)
    {
        _veri.Remove(entity);
        // Remove: referans eşitliği ile karşılaştırır — aynı instance verilmeli.
        // RemoveAll(k => k.Id == entity.Id) daha güvenli ama aynı sonuç.
    }

    public Task SaveAsync()
    {
        // Fake'de kaydetme işlemi yok — in-memory liste zaten güncel.
        // EF Core'da SaveChanges() transaction boundary; burada no-op.
        return Task.CompletedTask;
    }

    // ─── Kitap'a özgü sorgular ────────────────────────────────────────────────

    public Task<IList<Kitap>> GetStokluKitaplarAsync()
    {
        IList<Kitap> stoklu = _veri
            .Where(k => k.StokAdedi > 0)
            // StokAdedi > 0: gerçek SQL'in WHERE koşulunu simüle eder.
            .OrderBy(k => k.Baslik)
            .ToList();
        return Task.FromResult(stoklu);
    }

    public Task<IList<Kitap>> GetKategoriyleAsync(string kategori)
    {
        IList<Kitap> sonuc = _veri
            .Where(k => k.Kategori == kategori)
            // Case-sensitive: EF Core SQL'de collation bağımlı; Fake'de büyük/küçük harf eşleşmeli.
            // Gerçek DB'de "Roman" ile "roman" eşleşebilir — bu fark integration testte çıkar.
            .OrderBy(k => k.Baslik)
            .ToList();
        return Task.FromResult(sonuc);
    }

    public Task<Kitap?> GetYazarlıAsync(int id)
    {
        // Fake'de YazarNavigation doldurulamaz (gerçek Yazar entity'si gerekirdi).
        // Bu metod integration test gerektirir — TestContainers veya InMemoryDB ile.
        var kitap = _veri.FirstOrDefault(k => k.Id == id);
        return Task.FromResult(kitap);
        // YazarNavigation null kalacak — bu sınırlamayı test eden kod bunu bilmeli.
    }

    // ─── Test yardımcı metodları ──────────────────────────────────────────────

    /// <summary>Test setup'ında birden fazla kitabı hızlıca eklemek için.</summary>
    public void SeedData(params Kitap[] kitaplar)
    {
        foreach (var kitap in kitaplar)
        {
            kitap.Id = ++_sonId;
            _veri.Add(kitap);
        }
    }

    /// <summary>Veri sayısını doğrulamak için test assertion'larında kullanılır.</summary>
    public int Count => _veri.Count;
}
