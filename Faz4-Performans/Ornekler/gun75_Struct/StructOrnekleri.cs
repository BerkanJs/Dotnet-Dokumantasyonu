// GÜN 75 — Struct, readonly struct, ref struct
// Class: heap allocation, reference semantics
// Struct: stack allocation (küçük ve kısa ömürlüyse), value semantics

namespace Ornekler.gun75;

// --- 1. Sıradan struct: para birimi gibi küçük değer nesnesi ---
public struct Para
{
    public decimal Miktar { get; }
    public string Para_Birimi { get; }

    public Para(decimal miktar, string paraBirimi)
    {
        Miktar = miktar;
        Para_Birimi = paraBirimi;
    }

    // ne yapar: iki Para değerini toplar, yeni Para döner
    // bunu yazmasaydık: her toplama işlemi için class olsaydı heap allocation olurdu
    public Para Topla(Para diger)
    {
        if (Para_Birimi != diger.Para_Birimi)
            throw new InvalidOperationException("Para birimleri farklı");
        return new Para(Miktar + diger.Miktar, Para_Birimi);
    }
}

// --- 2. readonly struct: değişmez, defensive copy önler ---
// Compiler: readonly struct'ın metodları orijinal değeri değiştiremez
// → her metod çağrısında defensive copy oluşturmak ZORUNDA DEĞİL → performans kazancı
public readonly struct Koordinat
{
    public double X { get; }   // ne yapar: readonly struct'ta tüm field'lar otomatik readonly
    public double Y { get; }   // bunu yazmasaydık: struct mutation'a açık olurdu

    public Koordinat(double x, double y) => (X, Y) = (x, y);

    // ne yapar: iki nokta arasındaki mesafeyi hesaplar
    // bunu yazmasaydık: her kullanıcı kendi mesafe formülünü yazardı
    public double MesafeHesapla(Koordinat diger)
    {
        double dx = X - diger.X;
        double dy = Y - diger.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

// --- 3. ref struct: sadece stack'te yaşayabilir (Span<T> gibi) ---
// Heap'e kaçamaz → async metod içinde KULLANAMASSIZ → boxing yok → GC baskısı sıfır
public ref struct StackOnlyParser
{
    private ReadOnlySpan<char> _veri;
    private int _konum;

    // ne yapar: string'i kopyalamadan parse etmek için stack'te parser oluşturur
    // bunu yazmasaydık: her parse işlemi için heap'te nesne oluştururduk
    public StackOnlyParser(ReadOnlySpan<char> veri)
    {
        _veri = veri;
        _konum = 0;
    }

    public ReadOnlySpan<char> SonrakiKelimeOku()
    {
        int baslangic = _konum;
        // ne yapar: boşluk bulana kadar ilerler
        // bunu yazmasaydık: string.Split() → yeni string[] + N string nesnesi
        while (_konum < _veri.Length && _veri[_konum] != ' ')
            _konum++;

        var kelime = _veri.Slice(baslangic, _konum - baslangic);
        _konum++; // boşluğu atla
        return kelime;
    }
}

// --- Ne zaman struct, ne zaman class? ---
// Struct kullan: ≤16 byte, immutable, value semantics (Para, Koordinat, RGB, Rect)
// ref struct kullan: Span<T> wrapperleri, stack-only parser
// Class kullan: geri kalan her şey

// --- Struct kopyalama tuzağı ---
public class KopyalamaTuzagi
{
    public static void Goster()
    {
        var p1 = new Para(100, "TRY");

        // ne yapar: p1'in TAM KOPYASINI oluşturur — aynı nesne DEĞİL
        // bunu yazmasaydık (class olsaydı): p2 = p1 aynı heap nesnesine referans olurdu
        var p2 = p1;

        Console.WriteLine(p1.Miktar); // 100 — struct kopyalandı, bağımsız
    }
}
