using KitabeviMVC.Data;
using KitabeviMVC.Models.Entities;
using KitabeviMVC.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMVC.Services;

// Gün 29: EF Core tabanlı servis implementasyonu.
// Gün 30: IKitapSorguServisi de implement edildi — Include, AsSplitQuery,
//         IQueryable dinamik zinciri, EF.Functions.Like örnekleri.
// Gün 31: Compiled query + N+1 korumalı eager loading metodları eklendi.
// Gün 33: IKitapBatchServisi eklendi — ExecuteUpdate/Delete, TagWith, concurrency.
//
// DI kaydı: Scoped → her HTTP request'te taze DbContext alır.
// DbContext Scoped olduğu için bu servis de Scoped OLMAK ZORUNDA.
public class EfKitapServisi : IKitapServisi, IKitapSorguServisi, IKitapBatchServisi
{
    // ═════════════════════════════════════════════════════════════════════
    // GÜN 31: Compiled Query — uygulama ömrü boyunca tek kez derlenir.
    //
    // static readonly: sınıf yüklendiğinde bir kez oluşturulur, tüm
    // instance'lar aynı derlenmiş sorguyu paylaşır.
    // static yapmasaydık her EfKitapServisi instance'ı (her request!)
    // ayrı derleme yapardı → compiled query'nin tek avantajı sıfırlanırdı.
    //
    // EF.CompileAsyncQuery: expression tree'yi derleme anında SQL'e çevirir.
    // Çağrı anında sadece parametreler bağlanır (translation yok).
    // ═════════════════════════════════════════════════════════════════════
    private static readonly Func<KitabeviDbContext, int, Task<KitapDetayViewModel?>>
        _hizliDetayQuery = EF.CompileAsyncQuery(
            (KitabeviDbContext ctx, int id) =>
                ctx.Kitaplar
                   .AsNoTracking()              // sadece okuma: Change Tracker'a kaydetme
                   .Where(k => k.Id == id)      // PK filtresi
                   .Select(k => new KitapDetayViewModel
                   {
                       Id           = k.Id,
                       Baslik       = k.Baslik,
                       Yazar        = k.Yazar,
                       Fiyat        = k.Fiyat,
                       Kategori     = k.Kategori,
                       StokAdedi    = k.StokAdedi,
                       EklemeTarihi = k.EklemeTarihi,
                       // YazarNavigation burada yok — Include compiled query'de desteklenmez.
                       // YazarAdi null kalır; controller'da gerekirse Explicit Loading yapılır.
                       // YazarNavigation gerekliyse DetayYazarlaGetirAsync kullan.
                       YazarAdi     = k.Yazar    // fallback: entity'deki string alan
                   })
                   .FirstOrDefault());
    //              ↑ FirstOrDefaultAsync değil — compile API senkron lambda ister,
    //              async/await içinde çağrılınca IAsyncEnumerable veya Task<T> döner.

    private readonly KitabeviDbContext _context;
    private readonly ILogger<EfKitapServisi> _logger;

    public EfKitapServisi(KitabeviDbContext context, ILogger<EfKitapServisi> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────
    // HepsiniGetirAsync — AsNoTracking + Projection
    //
    // AsNoTracking(): dönen nesneler Change Tracker'a eklenmez.
    //   Neden? Sadece okuyoruz, güncelleme yok.
    //   Faydası: %10-30 daha hızlı, daha az bellek.
    //
    // Select(k => new KitapListeViewModel(...)): Projection.
    //   EF Core bunu SQL'e çevirir → sadece gerekli kolonları seçer.
    //   SELECT Id, Baslik, Yazar, Fiyat, Kategori, StokAdedi FROM Kitaplar
    //   (tüm kolonları değil — bant genişliği kazancı)
    // ─────────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<KitapListeViewModel>> HepsiniGetirAsync()
    {
        return await _context.Kitaplar
            // ── Gün 33: TagWith ──────────────────────────────────────────────
            .TagWith("EfKitapServisi.HepsiniGetirAsync - stokta olan tüm kitaplar")
            // bunu yazmasaydık production log'unda bu SQL'in nereden geldiği belli olmazdı
            // DBA veya monitoring aracı (Application Insights, Datadog) kaynağı göremezdi
            // ─────────────────────────────────────────────────────────────────
            .AsNoTracking()
            .Where(k => k.StokAdedi > 0) // stokta olmayan kitapları listede gösterme
            .OrderBy(k => k.Baslik)
            .Select(k => new KitapListeViewModel(
                k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
            .ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────
    // KategoriyeGoreGetirAsync — parametreli filtre
    //
    // WHERE filtresi server-side (SQL'de) çalışır:
    //   SELECT ... FROM Kitaplar WHERE Kategori = @kategori
    //
    // Dikkat: EF Core string karşılaştırmasını DB collation'a göre yapar.
    // Büyük/küçük harf duyarlılığı DB ayarına bağlı; case-insensitive
    // istiyorsan EF.Functions.Like veya ToLower() kullan.
    // ─────────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<KitapListeViewModel>> KategoriyeGoreGetirAsync(string kategori)
    {
        return await _context.Kitaplar
            .TagWith($"EfKitapServisi.KategoriyeGoreGetirAsync - Kategori:{kategori}")
            // dinamik tag: production'da hangi kategori parametresiyle çağrıldığı log'da görünür
            // bunu yazmasaydık yavaş kategoriler profilerda tespit edilemezdi
            .AsNoTracking()
            .Where(k => k.Kategori == kategori)
            .OrderBy(k => k.Baslik)
            .Select(k => new KitapListeViewModel(
                k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
            .ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────
    // BulByIdAsync — tracking açık (güncelleme senaryosunu destekler)
    //
    // FindAsync: PRIMARY KEY araması — EF Core önce Change Tracker'a bakar,
    // yoksa DB'ye gider. SingleOrDefaultAsync'ten daha verimli PK sorgularında.
    //
    // Tracking AÇIK bırakıldı: Detay sayfasından düzenleme sayfasına geçilebilir.
    // Sadece görüntüleyeceksen AsNoTracking eklenebilir — ama bunu bilmiyoruz.
    // ─────────────────────────────────────────────────────────────────────
    public async Task<KitapFormViewModel?> BulByIdAsync(int id)
    {
        // FindAsync → PK araması, önce 1. seviye cache (Change Tracker)
        var kitap = await _context.Kitaplar.FindAsync(id);
        if (kitap is null) return null;

        return new KitapFormViewModel
        {
            Id        = kitap.Id,
            Baslik    = kitap.Baslik,
            Yazar     = kitap.Yazar,
            Fiyat     = kitap.Fiyat,
            Kategori  = kitap.Kategori,
            StokAdedi = kitap.StokAdedi,
            // ── Gün 33: RowVersion — concurrency için view'a taşınır ──────
            RowVersion = kitap.RowVersion
            // bunu yazmasaydık: Duzenle view'da RowVersion null olurdu
            // GuncelleAsync'te concurrency kontrolü hiç çalışmazdı (null = skip)
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // EkleAsync — Change Tracker: Added → SaveChanges → INSERT
    //
    // context.Kitaplar.Add(kitap):
    //   Entity State: Added
    //   SaveChangesAsync(): INSERT INTO Kitaplar (...) VALUES (...)
    //   SaveChanges sonrası: DB'nin atadığı Id, kitap.Id'ye otomatik yansır.
    // ─────────────────────────────────────────────────────────────────────
    public async Task<int> EkleAsync(KitapFormViewModel model)
    {
        var kitap = new Kitap
        {
            Baslik    = model.Baslik,
            Yazar     = model.Yazar,
            Fiyat     = model.Fiyat,
            Kategori  = model.Kategori,
            StokAdedi = model.StokAdedi,
            EklemeTarihi = DateTime.UtcNow
        };

        _context.Kitaplar.Add(kitap);        // State: Added
        await _context.SaveChangesAsync();    // INSERT — kitap.Id artık dolu

        _logger.LogInformation("EF: Kitap eklendi. Id={Id}, Baslik={Baslik}",
            kitap.Id, kitap.Baslik);

        return kitap.Id;
    }

    // ─────────────────────────────────────────────────────────────────────
    // GuncelleAsync — Change Tracker: Unchanged → Modified → UPDATE
    //
    // Yaklaşım: entity'yi DB'den çek (Unchanged), property'leri değiştir
    // (Modified), SaveChanges() yap (UPDATE).
    //
    // Alternatif: context.Update(detachedEntity) → tüm kolonları günceller.
    // Bu yaklaşım daha verimli: sadece değişen kolonlar UPDATE edilir.
    // ─────────────────────────────────────────────────────────────────────
    // ─────────────────────────────────────────────────────────────────────
    // Gün 33: GuncelleAsync — Optimistic Concurrency korumalı.
    //
    // model.RowVersion: formdan gelen orijinal değer (kullanıcı sayfayı açtığında).
    // "Bu sürümü okudum; değişmediyse güncelle" anlamına gelir.
    //
    // SQL Server'da üretilen SQL:
    //   UPDATE Kitaplar SET Baslik=@p0, ... WHERE Id=@p5 AND RowVersion=@originalRV
    //
    // Eğer başka biri bu arada güncelledi → RowVersion değişti → WHERE 0 satır → exception.
    // DbUpdateConcurrencyException: controller'da yakalanır, kullanıcıya bildirilir.
    //
    // InMemory provider: RowVersion görmezden gelir → exception hiç fırlatmaz.
    // ─────────────────────────────────────────────────────────────────────
    public async Task<bool> GuncelleAsync(KitapFormViewModel model)
    {
        var kitap = await _context.Kitaplar.FindAsync(model.Id);
        if (kitap is null) return false;

        // State: Unchanged → bu satırdan sonra EF Core değişiklikleri izler
        kitap.Baslik    = model.Baslik;
        kitap.Yazar     = model.Yazar;
        kitap.Fiyat     = model.Fiyat;
        kitap.Kategori  = model.Kategori;
        kitap.StokAdedi = model.StokAdedi;
        // State: Modified (EF Core farkı tespit etti)

        // ── Gün 33: Concurrency token — OriginalValue olarak set et ──────
        if (model.RowVersion is not null)
        {
            _context.Entry(kitap)
                    .Property(k => k.RowVersion)
                    .OriginalValue = model.RowVersion;
            // bunu yazmasaydık: EF Core mevcut DB'deki RowVersion'ı OriginalValue alır
            // → her zaman eşleşir → çakışma hiç yakalanmaz → "lost update" problemi
        }
        // model.RowVersion null ise (Ekle senaryosu veya eski istemci):
        // concurrency kontrolü atlanır — bu bilerek yapılmış bir trade-off
        // ─────────────────────────────────────────────────────────────────

        await _context.SaveChangesAsync();
        // SQL Server: UPDATE Kitaplar SET ... WHERE Id=@id AND RowVersion=@originalRV
        // Eşleşmezse → DbUpdateConcurrencyException (controller'da yakalanır)
        // InMemory:   UPDATE Kitaplar SET ... WHERE Id=@id (RowVersion kontrolü yok)

        _logger.LogInformation("EF: Kitap güncellendi. Id={Id}", model.Id);
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────
    // SilAsync — Change Tracker: Unchanged → Deleted → DELETE
    // ─────────────────────────────────────────────────────────────────────
    public async Task<bool> SilAsync(int id)
    {
        var kitap = await _context.Kitaplar.FindAsync(id);
        if (kitap is null) return false;

        _context.Kitaplar.Remove(kitap); // State: Deleted
        await _context.SaveChangesAsync(); // DELETE FROM Kitaplar WHERE Id=@id

        _logger.LogInformation("EF: Kitap silindi. Id={Id}", id);
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────
    // BaslikVarMiAsync — veri bütünlüğü kontrolü, her zaman DB'ye gider.
    //
    // AnyAsync: SQL'de EXISTS kullanır — tüm satırları çekmez, çok verimli.
    //   SELECT CASE WHEN EXISTS (SELECT 1 FROM Kitaplar WHERE ...) THEN 1 ELSE 0
    //
    // haricId: güncelleme senaryosunda kendi ID'si hariç tutulur.
    // ─────────────────────────────────────────────────────────────────────
    public async Task<bool> BaslikVarMiAsync(string baslik, int haricId = 0)
    {
        return await _context.Kitaplar
            .AnyAsync(k => k.Baslik == baslik && k.Id != haricId);
    }

    // ═════════════════════════════════════════════════════════════════════
    // GÜN 30: IKitapSorguServisi implementasyonu
    // ═════════════════════════════════════════════════════════════════════

    // ─────────────────────────────────────────────────────────────────────
    // DetayYazarlaGetirAsync — Include + Projection birlikte
    //
    // Soru: Include ile Projection aynı sorguda kullanılabilir mi?
    // Cevap: Evet — Include önce, Select sonra gelir.
    //
    // SQL üretimi:
    //   SELECT k.Id, k.Baslik, k.Fiyat, k.Kategori, k.StokAdedi,
    //          k.Yazar, k.EklemeTarihi, y.Ad, y.Soyad
    //   FROM Kitaplar k
    //   LEFT JOIN Yazarlar y ON k.YazarId = y.Id
    //   WHERE k.Id = @id
    //
    // Projection içinde navigation property'ye erişilebilir:
    //   k.YazarNavigation?.Ad → SQL'de y.Ad
    // ─────────────────────────────────────────────────────────────────────
    public async Task<KitapDetayViewModel?> DetayYazarlaGetirAsync(int id)
    {
        // ─────────────────────────────────────────────────────────────
        // NOT: Expression tree (Select içi) koleksiyon ifadesi [] desteklemiyor.
        // Çözüm: KategoriOneriler'ı projection dışında, sonuç nesnesi üzerinde set et.
        // Bu pattern "projection + post-mapping" olarak bilinir.
        // ─────────────────────────────────────────────────────────────
        var detay = await _context.Kitaplar
            .AsNoTracking()
            .Include(k => k.YazarNavigation)   // Yazarlar tablosunu JOIN et
            .Where(k => k.Id == id)
            .Select(k => new KitapDetayViewModel
            {
                Id           = k.Id,
                Baslik       = k.Baslik,
                Yazar        = k.Yazar,
                Fiyat        = k.Fiyat,
                Kategori     = k.Kategori,
                StokAdedi    = k.StokAdedi,
                EklemeTarihi = k.EklemeTarihi,

                // Navigation property projection:
                // YazarNavigation null ise (YazarId set edilmemiş) eski string alana düş
                YazarAdi = k.YazarNavigation != null
                    ? k.YazarNavigation.Ad + " " + k.YazarNavigation.Soyad
                    : k.Yazar
            })
            .FirstOrDefaultAsync();
        // FirstOrDefaultAsync → LIMIT 1 (veya TOP 1 SQL Server'da)
        // Sonuç yoksa null döner — controller 404 döndürür
        return detay;
    }

    // ─────────────────────────────────────────────────────────────────────
    // AyniKategoridekilerAsync — IQueryable dinamik filtre zinciri
    //
    // Kullanım: Detay sayfasında "Bu kategoriden daha fazlası" bölümü.
    // Göstermek istediğimiz: IQueryable zinciri terminal metoda kadar SQL'e dönüşmez.
    //
    // Üretilen SQL:
    //   SELECT TOP(@limit) Id, Baslik, Yazar, Fiyat, Kategori, StokAdedi
    //   FROM Kitaplar
    //   WHERE Kategori = @kategori AND Id != @haricId AND StokAdedi > 0
    //   ORDER BY StokAdedi DESC, Fiyat ASC
    // ─────────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<KitapListeViewModel>> AyniKategoridekilerAsync(
        string kategori, int haricId, int limit = 5)
    {
        // Adım 1: base sorgu — IQueryable, SQL henüz yok
        var sorgu = _context.Kitaplar
            .AsNoTracking()
            .Where(k => k.Kategori == kategori
                     && k.Id != haricId
                     && k.StokAdedi > 0);

        // Adım 2: sıralama — stokta bol olan önce, eşitse ucuz olan önce
        // Her satır bir LINQ metodu — hepsi tek SQL'de birleşir
        sorgu = sorgu
            .OrderByDescending(k => k.StokAdedi)
            .ThenBy(k => k.Fiyat);

        // Adım 3: limit + projection + çalıştır
        return await sorgu
            .Take(limit)   // SQL: TOP(@limit)
            .Select(k => new KitapListeViewModel(
                k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
            .ToListAsync(); // ← burada SQL üretilir ve DB'ye gönderilir
    }

    // ─────────────────────────────────────────────────────────────────────
    // FiyatAraligiGetirAsync — optional parametreli dinamik IQueryable
    //
    // Her parametre opsiyonel: yalnızca verilenleri WHERE'e ekle.
    // Bu pattern "specification" veya "criteria" olarak bilinir.
    //
    // Kötü alternatif: her kombinasyon için ayrı metod yazmak
    //   GetirMinFiyatla(), GetirMaxFiyatla(), GetirFiyatAraligiyla()...
    // ─────────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<KitapListeViewModel>> FiyatAraligiGetirAsync(
        decimal? minFiyat, decimal? maxFiyat, string? kategori = null)
    {
        // AsQueryable() → açıkça IQueryable olduğunu belirtir
        var sorgu = _context.Kitaplar.AsNoTracking().AsQueryable();

        // Her koşul bağımsız — sadece null olmayanlar SQL'e eklenir
        if (minFiyat.HasValue)
            sorgu = sorgu.Where(k => k.Fiyat >= minFiyat.Value);

        if (maxFiyat.HasValue)
            sorgu = sorgu.Where(k => k.Fiyat <= maxFiyat.Value);

        if (!string.IsNullOrWhiteSpace(kategori))
            sorgu = sorgu.Where(k => k.Kategori == kategori);

        // Tüm filtreler eklendikten sonra tek SQL üretilir:
        // SELECT ... FROM Kitaplar
        // WHERE Fiyat >= @min AND Fiyat <= @max AND Kategori = @kategori
        // ORDER BY Fiyat
        return await sorgu
            .OrderBy(k => k.Fiyat)
            .Select(k => new KitapListeViewModel(
                k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
            .ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────
    // AraAsync — EF.Functions.Like ile SQL LIKE
    //
    // Neden EF.Functions.Like?
    //   → Contains("metin") → SQL'de LIKE '%metin%' üretir ama
    //     başındaki '%' index kullanımını engeller.
    //   → EF.Functions.Like(k.Baslik, "metin%") → başından arama,
    //     index kullanabilir.
    //   → Bu projede her iki yaklaşımı da gösteriyoruz.
    //
    // NOT: InMemory provider EF.Functions.Like'ı destekler.
    // Production SQL Server'da bu tam LIKE sorgusu üretir.
    // ─────────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<KitapListeViewModel>> AraAsync(string aramaMetni)
    {
        if (string.IsNullOrWhiteSpace(aramaMetni))
            return [];

        // %metin% → başlık veya yazar içinde geçiyorsa eşleş
        // SQL: WHERE Baslik LIKE '%@metin%' OR Yazar LIKE '%@metin%'
        var pattern = $"%{aramaMetni.Trim()}%";

        return await _context.Kitaplar
            .AsNoTracking()
            .Where(k => EF.Functions.Like(k.Baslik, pattern)
                     || EF.Functions.Like(k.Yazar, pattern))
            .OrderBy(k => k.Baslik)
            .Select(k => new KitapListeViewModel(
                k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
            .ToListAsync();
    }

    // ═════════════════════════════════════════════════════════════════════
    // GÜN 31: N+1 Problemi ve Çözümleri
    // ═════════════════════════════════════════════════════════════════════

    // ─────────────────────────────────────────────────────────────────────
    // HizliDetayGetirAsync — Compiled query ile ID araması.
    //
    // Ne zaman DetayYazarlaGetirAsync yerine bu?
    //   → YazarNavigation verisi gerekmiyorsa (yazar adı Yazar string'inden okunuyorsa)
    //   → Çok sık çağrılan hot-path endpoint'lerde (translation overhead önemli)
    //   → Include kullanmak istemiyorsan (JOIN maliyetini kabul etmiyorsan)
    //
    // Ne zaman DetayYazarlaGetirAsync?
    //   → Yazarın Ad/Soyad bilgisi Yazarlar tablosundan JOIN ile gelmeli
    //   → Yazar entity alanları (Biyografi vb.) görüntülenecekse
    // ─────────────────────────────────────────────────────────────────────
    public async Task<KitapDetayViewModel?> HizliDetayGetirAsync(int id)
    {
        // _hizliDetayQuery: statik compiled query — translation yok
        // _context: bu request'e özgü Scoped instance — thread-safe
        // id: SQL parametresine doğrudan bağlanır
        var detay = await _hizliDetayQuery(_context, id);
        // bunu _hizliDetayQuery yerine normal LINQ yazsaydık:
        // her çağrıda expression parse + SQL translation overhead'i olurdu

        if (detay is null) return null;

        // Compiled query'de Include kullanılamaz → Explicit Loading ile
        // sadece ihtiyaç duyulduğunda navigation yüklenir.
        // Bu örnekte: KategoriOneriler ikinci sorgudan dolduruluyor.
        detay.KategoriOneriler = await AyniKategoridekilerAsync(
            detay.Kategori, haricId: id, limit: 4);
        // bunu yazmasaydık liste sayfasında "aynı kategori" bölümü boş kalırdı

        return detay;
    }

    // ═════════════════════════════════════════════════════════════════════
    // GÜN 33: IKitapBatchServisi — ExecuteUpdate / ExecuteDelete
    //
    // Bu metodlar Change Tracker'dan geçmez → tek SQL, sıfır bellek yükü.
    // SaveChanges() GEREKMEZ — doğrudan DB'ye gider.
    // Dönüş değeri: etkilenen satır sayısı (int).
    // ═════════════════════════════════════════════════════════════════════

    // ─────────────────────────────────────────────────────────────────────
    // KategoriStokSifirlaAsync — toplu stok sıfırlama.
    //
    // ESKİ YOL (EF Core 6): ToListAsync → foreach StokAdedi=0 → SaveChanges
    //   100 kitap → 1 SELECT + 100 UPDATE = 101 DB roundtrip + 100 EntityEntry
    //
    // YENİ YOL (EF Core 7+): ExecuteUpdateAsync
    //   100 kitap → 1 UPDATE SQL, 0 EntityEntry, 0 Change Tracker yükü
    // ─────────────────────────────────────────────────────────────────────
    public async Task<int> KategoriStokSifirlaAsync(string kategori)
    {
        var etkilenen = await _context.Kitaplar
            .TagWith($"EfKitapServisi.KategoriStokSifirlaAsync - Kategori:{kategori}")
            // log'da bu batch işlemin hangi kategori için tetiklendiği görünür
            .Where(k => k.Kategori == kategori)
            // bunu yazmasaydık tüm Kitaplar tablosunun stoğu sıfırlanırdı
            .ExecuteUpdateAsync(s =>
                s.SetProperty(k => k.StokAdedi, 0));
                // SetProperty(kolon, yeniDeger): tek SET ifadesi üretir
                // bunu .SetProperty(k => k.StokAdedi, k => k.StokAdedi) yazsaydık
                // değer değişmezdi (kendine atama)
        // Üretilen SQL:
        // UPDATE Kitaplar SET StokAdedi = 0 WHERE Kategori = N'@kategori'
        // SaveChanges() gerekmez — ExecuteUpdateAsync doğrudan çalışır

        _logger.LogInformation(
            "Batch: '{Kategori}' kategorisinde {Adet} kitabın stoğu sıfırlandı.",
            kategori, etkilenen);

        return etkilenen;
    }

    // ─────────────────────────────────────────────────────────────────────
    // TopluFiyatArttirAsync — hesaplanmış değerle toplu güncelleme.
    //
    // artisOrani: 0.10 → %10 artış. Her kitabın mevcut fiyatı DB'de çarpılır.
    // EF Core bu lambda'yı SQL'e çevirir: SET Fiyat = Fiyat * (1 + @oran)
    // ─────────────────────────────────────────────────────────────────────
    public async Task<int> TopluFiyatArttirAsync(string kategori, decimal artisOrani)
    {
        if (artisOrani is <= 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(artisOrani),
                "Artış oranı 0 ile 5 (=500%) arasında olmalıdır.");
        // validation: %500'den fazla artış muhtemelen hata; DB'ye gönderme

        var etkilenen = await _context.Kitaplar
            .TagWith($"EfKitapServisi.TopluFiyatArttirAsync - {kategori} +%{artisOrani * 100}")
            .Where(k => k.Kategori == kategori && k.StokAdedi > 0)
            // sadece stoktaki kitaplara fiyat artışı — stokta olmayan güncellenmez
            // bunu yazmasaydık sıfır stoklu kitaplara da zam yapılırdı (anlamsız)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(k => k.Fiyat, k => k.Fiyat * (1 + artisOrani)));
                // k => k.Fiyat * (1 + artisOrani): mevcut değeri kullanarak hesapla
                // EF Core bunu SQL'e çevirir: SET Fiyat = Fiyat * @carpan
                // s.SetProperty(k => k.Fiyat, 99m) yazsaydık tüm fiyatlar sabit 99 olurdu
        // Üretilen SQL:
        // UPDATE Kitaplar SET Fiyat = Fiyat * 1.10
        // WHERE Kategori = N'@kategori' AND StokAdedi > 0

        _logger.LogInformation(
            "Batch: '{Kategori}' kategorisinde {Adet} kitaba %{Oran} fiyat artışı uygulandı.",
            kategori, etkilenen, artisOrani * 100);

        return etkilenen;
    }

    // ─────────────────────────────────────────────────────────────────────
    // EskiStoksuzlariSilAsync — koşullu toplu silme.
    //
    // ExecuteDeleteAsync: tek SQL DELETE, Change Tracker yükü sıfır.
    // SaveChanges() GEREKMEZ.
    //
    // DİKKAT: Bu işlem geri alınamaz (DB transaction dışında).
    // Production'da önce dry-run (silmeden sayı dönen sorgu) yapılmalı.
    // ─────────────────────────────────────────────────────────────────────
    public async Task<int> EskiStoksuzlariSilAsync(int yilEsigi = 2)
    {
        var sinirTarihi = DateTime.UtcNow.AddYears(-yilEsigi);
        // sinirTarihi: örn. yilEsigi=2 → 2 yıldan önce eklenen kitaplar silinir
        // bunu hesaplamadan direkt DateTime.UtcNow.AddYears(-2) yazabilirdik ama
        // parametreli yaparak controller'dan kontrol edilebilir kıldık

        // Önce kaç satır etkileneceğini logla (dry-run sayımı)
        var etkilenecekSayi = await _context.Kitaplar
            .CountAsync(k => k.StokAdedi == 0 && k.EklemeTarihi < sinirTarihi);
        // CountAsync: DELETE'den önce kullanıcıya "X kayıt silinecek" bildirimi için

        if (etkilenecekSayi == 0)
        {
            _logger.LogInformation("Batch: Silinecek eski stoksuz kitap bulunamadı.");
            return 0;
        }

        _logger.LogWarning(
            "Batch: {Adet} adet eski stoksuz kitap siliniyor (eklemeTarihi < {Tarih}).",
            etkilenecekSayi, sinirTarihi);

        var silinen = await _context.Kitaplar
            .TagWith($"EfKitapServisi.EskiStoksuzlariSilAsync - sinir:{sinirTarihi:yyyy-MM-dd}")
            .Where(k => k.StokAdedi == 0 && k.EklemeTarihi < sinirTarihi)
            // her iki koşul birlikte zorunlu:
            //   k.StokAdedi == 0 tek başına: stoksuz ama yeni kitaplar da silinir (yanlış)
            //   k.EklemeTarihi < sinirTarihi tek başına: eski ama stoklu kitaplar silinir (yanlış)
            .ExecuteDeleteAsync();
        // Üretilen SQL:
        // DELETE FROM Kitaplar
        // WHERE StokAdedi = 0 AND EklemeTarihi < @sinirTarihi

        _logger.LogInformation("Batch: {Adet} eski stoksuz kitap silindi.", silinen);
        return silinen;
    }

    // ─────────────────────────────────────────────────────────────────────
    // YazarlariyleHepsiniGetirAsync — N+1'den korunan liste.
    //
    // Problem: HepsiniGetirAsync() kitap başına ayrı yazar sorgusu yapar mı?
    //   → HAYIR. Çünkü HepsiniGetirAsync projection kullanıyor (Select + Yazar string).
    //   → Ama YazarNavigation (JOIN) bilgisi gerekiyorsa Include şart.
    //
    // Bu metodun amacı: Yazarlar tablosundan JOIN ile gelen Ad/Soyad'ı
    // KitapListeViewModel.Yazar alanına doldur.
    // Sonuç: tek SQL, N+1 yok.
    //
    // N+1 OLURDU eğer:
    //   foreach (var kitap in await _context.Kitaplar.ToListAsync())
    //       var yazarAdi = kitap.YazarNavigation.Ad;  ← her iterasyonda ayrı SQL
    // ─────────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<KitapListeViewModel>> YazarlariyleHepsiniGetirAsync()
    {
        return await _context.Kitaplar
            .AsNoTracking()                            // sadece okuma — Change Tracker yükü yok
            .Include(k => k.YazarNavigation)           // Yazarlar tablosunu JOIN et
                                                       // bunu yazmasaydık YazarNavigation null gelirdi,
                                                       // aşağıdaki null-coalescing hep k.Yazar'a düşerdi
            .Where(k => k.StokAdedi > 0)              // stokta olmayan kitapları gösterme
            .OrderBy(k => k.Baslik)                    // alfabetik sırala
            .Select(k => new KitapListeViewModel(
                k.Id,
                k.Baslik,
                k.YazarNavigation != null              // JOIN'den yazar geldi mi?
                    ? k.YazarNavigation.Ad + " " + k.YazarNavigation.Soyad  // evet → tam ad
                    : k.Yazar,                         // hayır → entity'deki string alan (fallback)
                                                       // bu fallback olmasaydı YazarId set edilmemiş
                                                       // kitaplarda yazar adı boş görünürdü
                k.Fiyat,
                k.Kategori,
                k.StokAdedi))
            .ToListAsync();
        // Üretilen SQL (SQL Server):
        // SELECT k.Id, k.Baslik, y.Ad, y.Soyad, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi
        // FROM Kitaplar k
        // LEFT JOIN Yazarlar y ON k.YazarId = y.Id
        // WHERE k.StokAdedi > 0
        // ORDER BY k.Baslik
    }
}
