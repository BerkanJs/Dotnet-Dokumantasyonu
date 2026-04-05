// Gün 2 — Stack, Heap ve Bellek Modeli: Kod Demoları
// Bir Console projesi oluşturup bu kodu çalıştırabilirsin.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

// ============================================================
// BÖLÜM 1: Value Type vs Reference Type davranışı
// ============================================================
Console.WriteLine("=== Value Type vs Reference Type ===");

// Value type (struct): kopyalama değeri kopyalar
Nokta2D structA = new Nokta2D { X = 1, Y = 2 };
Nokta2D structB = structA;  // değer kopyalandı
structB.X = 99;
Console.WriteLine($"struct — A.X: {structA.X}, B.X: {structB.X}");
// Çıktı: A.X: 1, B.X: 99  →  bağımsız kopyalar

// Reference type (class): kopyalama adresi kopyalar
NokTa2DClass classA = new NokTa2DClass { X = 1, Y = 2 };
NokTa2DClass classB = classA;  // adres kopyalandı — aynı nesne
classB.X = 99;
Console.WriteLine($"class  — A.X: {classA.X}, B.X: {classB.X}");
// Çıktı: A.X: 99, B.X: 99  →  aynı nesneye bakıyorlar

Console.WriteLine();

// ============================================================
// BÖLÜM 2: "Value type her zaman stack'te değil" demosu
// ============================================================
Console.WriteLine("=== Value Type İçeren Class ===");

// Konteyner class heap'te, içindeki int da heap'te
var konteyner = new Konteyner();
konteyner.Deger = 42;
Console.WriteLine($"Class içindeki int (heap'te): {konteyner.Deger}");

// Yerel int değişken (stack'te)
int yerelSayi = 100;
Console.WriteLine($"Yerel int (stack'te): {yerelSayi}");

Console.WriteLine();

// ============================================================
// BÖLÜM 3: Boxing ve Unboxing — maliyeti gözlemle
// ============================================================
Console.WriteLine("=== Boxing Performans Karşılaştırması ===");

const int ITERASYON = 1_000_000;

// ArrayList: her int box edilir (int → object)
var sw = Stopwatch.StartNew();
var arrayList = new ArrayList();
for (int i = 0; i < ITERASYON; i++)
    arrayList.Add(i);  // boxing burada
sw.Stop();
Console.WriteLine($"ArrayList (boxing var):     {sw.ElapsedMilliseconds} ms");

// List<int>: generic, boxing yok
sw.Restart();
var genericList = new List<int>();
for (int i = 0; i < ITERASYON; i++)
    genericList.Add(i);  // boxing yok
sw.Stop();
Console.WriteLine($"List<int> (boxing yok):     {sw.ElapsedMilliseconds} ms");

Console.WriteLine();

// Boxing adımlarını elle gözlemle
Console.WriteLine("=== Boxing Adımları ===");
int sayi = 42;
object boxed = sayi;       // boxing: heap'te yeni nesne, değer kopyalandı
int unboxed = (int)boxed;  // unboxing: heap'ten değer alındı, stack'e kopyalandı
Console.WriteLine($"Orijinal: {sayi}, Boxed: {boxed}, Unboxed: {unboxed}");

// Boxing sonrası orijinal değişmez
sayi = 100;
Console.WriteLine($"sayi=100 yapıldı — boxed hâlâ: {boxed}");  // 42

Console.WriteLine();

// ============================================================
// BÖLÜM 4: Span<T> — kopyasız pencere
// ============================================================
Console.WriteLine("=== Span<T> Demo ===");

int[] tamDizi = { 10, 20, 30, 40, 50 };

// Klasik yol: yeni dizi oluşturur, heap allocation
int[] kopya = tamDizi[1..3];
Console.WriteLine($"Klasik kopya: [{string.Join(", ", kopya)}]");

// Span yolu: allocation yok, aynı belleğe bakıyor
Span<int> span = tamDizi.AsSpan(1, 2);
Console.WriteLine($"Span penceresi: [{string.Join(", ", span.ToArray())}]");

// Span üzerinden değişiklik orijinal diziyi etkiler (kopya değil)
span[0] = 999;
Console.WriteLine($"Span değiştirince orijinal dizi: [{string.Join(", ", tamDizi)}]");
// tamDizi[1] artık 999

Console.WriteLine();

// ============================================================
// BÖLÜM 5: Stack overflow örneği (çalıştırma — dikkatli!)
// ============================================================
// Aşağıdaki kodu uncommit edersen StackOverflowException alırsın.
// Sonsuz recursive çağrı stack'i doldurur.
// ÇALIŞTIRMA — sadece oku.

// static void SonsuzFonksiyon() => SonsuzFonksiyon();
// SonsuzFonksiyon();

Console.WriteLine("Stack overflow örneği: sonsuz recursive çağrı → stack dolar → uygulama çöker");
Console.WriteLine("(Kod yorum satırında — çalıştırılmadı)");

// ============================================================
// Tip tanımları
// ============================================================

// Value type — struct
struct Nokta2D
{
    public int X;
    public int Y;
}

// Reference type — class
class NokTa2DClass
{
    public int X;
    public int Y;
}

// Value type (int) içeren class — int heap'te yaşar
class Konteyner
{
    public int Deger;  // class heap'te, bu int da heap'te
}
