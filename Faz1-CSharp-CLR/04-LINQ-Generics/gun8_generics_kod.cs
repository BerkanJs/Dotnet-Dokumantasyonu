// Gün 8 — Generics: Kod Demoları

using System;
using System.Collections.Generic;
using System.Linq;

// ============================================================
// BÖLÜM 1: Generics olmadan vs ile — fark
// ============================================================
Console.WriteLine("=== Generic Metot ===");

// Generic: bir metot, her tip
Console.WriteLine(Maksimum(3, 7));          // int
Console.WriteLine(Maksimum(3.14, 2.71));    // double
Console.WriteLine(Maksimum("abc", "ab"));   // string (uzunluğa göre)

Console.WriteLine();

// ============================================================
// BÖLÜM 2: C# generics runtime'da tip bilgisini korur
// ============================================================
Console.WriteLine("=== Reified Generics: Runtime Tip Bilgisi ===");

var intListesi = new List<int> { 1, 2, 3 };
var stringListesi = new List<string> { "a", "b" };

// Runtime'da tip bilgisi hâlâ var — Java'da bu çalışmaz
Console.WriteLine($"List<int> tipi   : {intListesi.GetType().Name}");
Console.WriteLine($"Generic argüman  : {intListesi.GetType().GetGenericArguments()[0]}");
Console.WriteLine($"List<string> tipi: {stringListesi.GetType().Name}");
Console.WriteLine($"Generic argüman  : {stringListesi.GetType().GetGenericArguments()[0]}");

Console.WriteLine();

// ============================================================
// BÖLÜM 3: Generic constraints — where T
// ============================================================
Console.WriteLine("=== Generic Constraints ===");

// where T : new() — nesne oluşturabiliyoruz
var kitap = Olustur<Kitap>();
Console.WriteLine($"Oluşturuldu: {kitap.GetType().Name}");

// where T : IEntity — sadece IEntity implement edenleri kabul et
var repo = new Repository<Kitap>();
repo.Ekle(new Kitap { Id = 1, Baslik = "Clean Code" });
repo.Ekle(new Kitap { Id = 2, Baslik = "DDD" });
Console.WriteLine($"Repository'deki kayıt sayısı: {repo.Hepsi().Count}");

Console.WriteLine();

// ============================================================
// BÖLÜM 4: Kovaryans — IEnumerable<out T>
// ============================================================
Console.WriteLine("=== Kovaryans: IEnumerable<out T> ===");

List<Kedi> kediler = new() { new Kedi("Boncuk"), new Kedi("Pamuk") };

// List<Kedi> → List<Hayvan> ÇALIŞMAZ
// List<Hayvan> hayvanListesi = kediler;  // derleme hatası

// IEnumerable<Kedi> → IEnumerable<Hayvan> ÇALIŞIR (kovaryans)
IEnumerable<Hayvan> hayvanlar = kediler;
foreach (var h in hayvanlar)
    Console.WriteLine($"  Hayvan: {h.Ad}");

Console.WriteLine();

// ============================================================
// BÖLÜM 5: Kontravaryans — Action<in T>
// ============================================================
Console.WriteLine("=== Kontravaryans: Action<in T> ===");

// Hayvan işleyen bir Action
Action<Hayvan> hayvaniTanit = h => Console.WriteLine($"  Ben bir {h.GetType().Name}: {h.Ad}");

// Action<Hayvan> → Action<Kedi>'ye atanabilir (kontravaryans)
Action<Kedi> kediTanit = hayvaniTanit;

kediTanit(new Kedi("Minnoş"));  // Kedi bir Hayvan, hayvaniTanit onu işleyebilir

Console.WriteLine();

// ============================================================
// BÖLÜM 6: Generic Repository pattern
// ============================================================
Console.WriteLine("=== Generic Repository Pattern ===");

var kitapRepo = new Repository<Kitap>();
var yazarRepo = new Repository<Yazar>();

kitapRepo.Ekle(new Kitap { Id = 1, Baslik = "Clean Code" });
kitapRepo.Ekle(new Kitap { Id = 2, Baslik = "DDD" });
yazarRepo.Ekle(new Yazar { Id = 1, Ad = "Robert Martin" });

Console.WriteLine($"Kitaplar ({kitapRepo.Hepsi().Count}):");
foreach (var k in kitapRepo.Hepsi())
    Console.WriteLine($"  [{k.Id}] {k.Baslik}");

Console.WriteLine($"Yazarlar ({yazarRepo.Hepsi().Count}):");
foreach (var y in yazarRepo.Hepsi())
    Console.WriteLine($"  [{y.Id}] {y.Ad}");

// ============================================================
// Local fonksiyonlar
// ============================================================

static T Maksimum<T>(T a, T b) where T : IComparable<T>
    => a.CompareTo(b) >= 0 ? a : b;

static T Olustur<T>() where T : new()
    => new T();

// ============================================================
// Tip tanımları
// ============================================================

interface IEntity
{
    int Id { get; set; }
}

class Kitap : IEntity
{
    public int Id { get; set; }
    public string Baslik { get; set; } = "";
}

class Yazar : IEntity
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
}

// Generic repository — T IEntity implement etmeli
class Repository<T> where T : class, IEntity
{
    private readonly List<T> _store = new();

    public void Ekle(T entity) => _store.Add(entity);
    public T? GetById(int id) => _store.FirstOrDefault(e => e.Id == id);
    public List<T> Hepsi() => _store.ToList();
}

// Kovaryans/Kontravaryans için hiyerarşi
class Hayvan
{
    public string Ad { get; }
    public Hayvan(string ad) { Ad = ad; }
}

class Kedi : Hayvan
{
    public Kedi(string ad) : base(ad) { }
}
