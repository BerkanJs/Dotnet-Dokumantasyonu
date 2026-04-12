namespace BehavioralDemo2.Mediator;

// Mediator Pattern: nesneler birbirine direkt bağlı değil, merkezi arabulucu üzerinden haberleşir
// MediatR kütüphanesi bu pattern'in .NET uygulaması — Faz3 CQRS bölümünde tam kullanacağız

// Request/Response sözleşmeleri (MediatR'daki IRequest<T> karşılığı)
public record KitapEkleKomut(string Baslik, decimal Fiyat);
public record KitapListesorgu();

// Handler'lar (MediatR'daki IRequestHandler<T> karşılığı)
public class KitapEkleHandler
{
    private readonly KitapDepo _depo;
    public KitapEkleHandler(KitapDepo depo) => _depo = depo;
    public int Handle(KitapEkleKomut komut)
    {
        Console.WriteLine($"[HANDLER] '{komut.Baslik}' ekleniyor");
        return _depo.Ekle(komut.Baslik, komut.Fiyat);
    }
}

public class KitapListeHandler
{
    private readonly KitapDepo _depo;
    public KitapListeHandler(KitapDepo depo) => _depo = depo;
    public List<string> Handle(KitapListesorgu _) => _depo.HepsiniGetir();
}

// Mediator: hangi handler'ın çağrılacağını biliyor — çağıranlar birbirini bilmiyor
// bunu yazmasaydık → Controller direkt Handler'ları inject edip çağırmak zorunda kalırdı
// Handler sayısı arttıkça Controller'da inject sayısı patlar
public class KitapMediator
{
    private readonly KitapEkleHandler _ekleHandler;
    private readonly KitapListeHandler _listeHandler;

    public KitapMediator(KitapDepo depo)
    {
        _ekleHandler  = new KitapEkleHandler(depo);
        _listeHandler = new KitapListeHandler(depo);
        // gerçek MediatR: DI container handler'ları otomatik bulur
        // bunu yazmasaydık → Controller tüm handler'ları bilmek zorunda kalırdı
    }

    public int Gonder(KitapEkleKomut komut) => _ekleHandler.Handle(komut);
    public List<string> Gonder(KitapListesorgu sorgu) => _listeHandler.Handle(sorgu);
}

public class KitapDepo
{
    private readonly List<string> _db = [];
    private int _id = 0;
    public int Ekle(string baslik, decimal fiyat) { _db.Add(baslik); return ++_id; }
    public List<string> HepsiniGetir() => _db;
}
