namespace CreationalDemo.Builder;

// Builder: karmaşık nesne inşasını adım adım yap
// Fluent API — her metot this döner, zincirleme çağrı mümkün
// Faz2'de IQueryable zinciri aynı fikir: .Where().OrderBy().Take()
public class KitapSorgu
{
    public string? Kategori { get; private set; }
    public decimal? MinFiyat { get; private set; }
    public decimal? MaxFiyat { get; private set; }
    public int Limit { get; private set; } = 20;
    public bool SadeceStoktakiler { get; private set; }

    // Constructor private — dışarıdan direkt oluşturulamaz
    // bunu yazmasaydık → 5 parametreli constructor, hangi sırayla ne verileceği belirsiz
    private KitapSorgu() { }

    public static KitapSorguBuilder Olustur() => new();

    public override string ToString() =>
        $"Kategori={Kategori ?? "hepsi"} | Fiyat={MinFiyat}-{MaxFiyat} | Limit={Limit} | Stok={SadeceStoktakiler}";

    // Builder iç sınıf olarak — KitapSorgu'nun private alanlarına erişir
    public class KitapSorguBuilder
    {
        private readonly KitapSorgu _sorgu = new();

        public KitapSorguBuilder Kategori(string kategori)
        {
            _sorgu.Kategori = kategori;
            return this;                // this döner → zincirleme için
            // bunu yazmasaydık → her adım ayrı değişkene atamak gerekir
        }

        public KitapSorguBuilder FiyatAraligi(decimal min, decimal max)
        {
            _sorgu.MinFiyat = min;
            _sorgu.MaxFiyat = max;
            return this;
        }

        public KitapSorguBuilder Limit(int limit)
        {
            _sorgu.Limit = limit;
            return this;
        }

        public KitapSorguBuilder SadeceStoktakiler()
        {
            _sorgu.SadeceStoktakiler = true;
            return this;
        }

        public KitapSorgu Bitir() => _sorgu;
        // inşa tamamlandı — artık immutable nesne teslim edildi
        // bunu yazmasaydık → yarım kalmış sorgu nesnesi oluşturulabilirdi
    }
}
