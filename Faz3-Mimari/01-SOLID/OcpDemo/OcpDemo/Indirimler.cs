namespace OcpDemo;

// Her indirim tipi kendi class'ında — YENİ TİP = YENİ CLASS, ESKİ KOD DOKUNULMAZ

public class OgrenciIndirimi : IIndirimStrategy
{
    public string Ad => "Öğrenci İndirimi";

    public decimal Hesapla(decimal fiyat)
        => fiyat * 0.80m;   // %20 indirim
    // bunu yazmasaydık → indirim oranı FiyatServisi içinde if bloğu olurdu
    // oran değişince sadece bu dosyaya dokunulur
}

public class YazMevsimIndirimi : IIndirimStrategy
{
    public string Ad => "Yaz Mevsimi İndirimi";

    public decimal Hesapla(decimal fiyat)
        => fiyat * 0.70m;   // %30 indirim
    // yeni sezon indirimi eklemek için FiyatServisi'ne hiç dokunulmadı
}

public class KuponIndirimi : IIndirimStrategy
{
    private readonly decimal _kuponOrani;

    public string Ad => $"Kupon İndirimi (%{(1 - _kuponOrani) * 100:0})";

    public KuponIndirimi(decimal kuponOrani) => _kuponOrani = kuponOrani;
    // farklı oranlı kuponlar için aynı class, farklı constructor parametresi

    public decimal Hesapla(decimal fiyat)
        => fiyat * _kuponOrani;
    // bunu yazmasaydık → her kupon oranı için ayrı if dalı gerekirdi
}
