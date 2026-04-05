// Gün 10 — Delegates, Events: Kod Demoları

using System;
using System.Collections.Generic;
using System.Linq;

// ============================================================
// BÖLÜM 1: Action, Func, Predicate
// ============================================================
Console.WriteLine("=== Action / Func / Predicate ===");

Action<string> yazdir = mesaj => Console.WriteLine($"  > {mesaj}");
yazdir("Kitabevi sistemi başladı");

Func<decimal, decimal> kdvEkle = fiyat => fiyat * 1.20m;
Console.WriteLine($"  100₺'ye KDV'li: {kdvEkle(100m):C}");

Predicate<Kitap> ucuzMu = k => k.Fiyat < 50m;
var kitap1 = new Kitap(1, "Clean Code", 45m);
Console.WriteLine($"  {kitap1.Baslik} ucuz mu: {ucuzMu(kitap1)}");

Console.WriteLine();

// ============================================================
// BÖLÜM 2: Metodu parametre olarak geçmek
// ============================================================
Console.WriteLine("=== Metodu Parametre Olarak Geçmek ===");

var kitaplar = new List<Kitap>
{
    new(1, "Clean Code",         45m),
    new(2, "DDD",                85m),
    new(3, "SICP",               30m),
    new(4, "Refactoring",        55m),
    new(5, "The Pragmatic Prog", 50m),
};

// Farklı Func'lar geçerek farklı davranış
var ucuzlar   = Filtrele(kitaplar, k => k.Fiyat < 50);
var pahalılar = Filtrele(kitaplar, k => k.Fiyat > 70);

Console.WriteLine("  Ucuzlar:");
ucuzlar.ForEach(k => Console.WriteLine($"    {k.Baslik} — {k.Fiyat:C}"));

Console.WriteLine("  Pahalılar:");
pahalılar.ForEach(k => Console.WriteLine($"    {k.Baslik} — {k.Fiyat:C}"));

Console.WriteLine();

// ============================================================
// BÖLÜM 3: Closure — dış değişkeni yakalama
// ============================================================
Console.WriteLine("=== Closure ===");

decimal sinir = 50m;
Func<Kitap, bool> filtre = k => k.Fiyat < sinir;  // sinir yakalandı

Console.WriteLine($"  sinir={sinir} → Clean Code (45) filtreden geçer mi: {filtre(kitap1)}");

sinir = 40m;  // sinir değişti
Console.WriteLine($"  sinir={sinir} → Clean Code (45) filtreden geçer mi: {filtre(kitap1)}");
// Lambda sinir'e referans tutuyor, kopyasını değil

Console.WriteLine();

// ============================================================
// BÖLÜM 4: Multicast delegate
// ============================================================
Console.WriteLine("=== Multicast Delegate ===");

Action<Kitap> kitapIslemleri = null!;

kitapIslemleri += k => Console.WriteLine($"  [Log] Kitap eklendi: {k.Baslik}");
kitapIslemleri += k => Console.WriteLine($"  [Cache] Temizlendi: {k.Id}");
kitapIslemleri += k => Console.WriteLine($"  [Bildirim] {k.Baslik} eklendi");

kitapIslemleri(kitap1);  // üçü de sırayla çalışır

Console.WriteLine();

// ============================================================
// BÖLÜM 5: Event — publisher/subscriber
// ============================================================
Console.WriteLine("=== Event: Observer Pattern ===");

var kitapServisi = new KitapServisi();
var logServisi   = new LogServisi(kitapServisi);
var stokServisi  = new StokServisi(kitapServisi);

kitapServisi.KitapEkle(new Kitap(10, "Yeni Kitap", 60m));
kitapServisi.KitapEkle(new Kitap(11, "Bir Kitap Daha", 35m));

Console.WriteLine();

// ============================================================
// BÖLÜM 6: IDisposable ile event aboneliği yönetimi
// ============================================================
Console.WriteLine("=== Event Memory Leak Önlemi ===");

var servis = new KitapServisi();

using (var geciciAbone = new GeciciAbone(servis))
{
    servis.KitapEkle(new Kitap(20, "Test", 10m));
}
// Dispose çağrıldı → -= yapıldı

servis.KitapEkle(new Kitap(21, "Sonraki", 10m));
// geciciAbone artık çağrılmıyor

Console.WriteLine();

// ============================================================
// BÖLÜM 7: Func zinciri — pipeline pattern
// ============================================================
Console.WriteLine("=== Func Zinciri (Mini Pipeline) ===");

// Her adım bir Func — girdi alır, çıktı döner
Func<decimal, decimal> kdvEkle2    = f => f * 1.20m;
Func<decimal, decimal> kargoEkle   = f => f + 15m;
Func<decimal, decimal> yuvarla     = f => Math.Round(f, 2);

// Zincir
Func<decimal, decimal> toplamFiyat = f => yuvarla(kargoEkle(kdvEkle2(f)));

Console.WriteLine($"  100₺ → KDV + Kargo + Yuvarlama: {toplamFiyat(100m):C}");

// ============================================================
// Local fonksiyonlar
// ============================================================

static List<Kitap> Filtrele(List<Kitap> liste, Func<Kitap, bool> kosul)
    => liste.Where(kosul).ToList();

// ============================================================
// Tip tanımları
// ============================================================

record Kitap(int Id, string Baslik, decimal Fiyat);

class KitapServisi
{
    public event Action<Kitap>? KitapEklendi;

    public void KitapEkle(Kitap kitap)
    {
        Console.WriteLine($"  [KitapServisi] {kitap.Baslik} eklendi");
        KitapEklendi?.Invoke(kitap);
    }
}

class LogServisi
{
    public LogServisi(KitapServisi servis)
        => servis.KitapEklendi += k => Console.WriteLine($"  [Log] Id={k.Id} kaydedildi");
}

class StokServisi
{
    public StokServisi(KitapServisi servis)
        => servis.KitapEklendi += k => Console.WriteLine($"  [Stok] {k.Baslik} stok güncellendi");
}

class GeciciAbone : IDisposable
{
    private readonly KitapServisi _servis;

    public GeciciAbone(KitapServisi servis)
    {
        _servis = servis;
        _servis.KitapEklendi += Dinle;
        Console.WriteLine("  [GeciciAbone] Abone oldu");
    }

    void Dinle(Kitap k) => Console.WriteLine($"  [GeciciAbone] {k.Baslik} duyuldu");

    public void Dispose()
    {
        _servis.KitapEklendi -= Dinle;
        Console.WriteLine("  [GeciciAbone] Abonelikten çıktı (Dispose)");
    }
}
