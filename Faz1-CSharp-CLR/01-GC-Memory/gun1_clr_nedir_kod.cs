// Gün 1 — CLR Nedir? Kod Demoları
// Çalıştırmak için: dotnet script gun1_clr_nedir_kod.cs
// ya da bir Console projesi oluşturup kopyala.

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

// ============================================================
// BÖLÜM 1: Runtime bilgisi — CLR'yi tanıyalım
// ============================================================
Console.WriteLine("=== CLR & Runtime Bilgisi ===");

// Hangi .NET versiyonu üzerinde koşuyoruz?
Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
// Örnek çıktı: .NET 8.0.1

// İşletim sistemi
Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");

// Mimari (x64, ARM64 vs.)
Console.WriteLine($"Process Architecture: {RuntimeInformation.ProcessArchitecture}");

// CLR'in Environment üzerinden versiyonu
Console.WriteLine($"Environment.Version: {Environment.Version}");

Console.WriteLine();

// ============================================================
// BÖLÜM 2: Assembly ve IL — CLR neyi yükledi?
// ============================================================
Console.WriteLine("=== Assembly Bilgisi ===");

// Çalışan assembly'nin adı ve konumu
var currentAssembly = Assembly.GetExecutingAssembly();
Console.WriteLine($"Assembly: {currentAssembly.FullName}");
Console.WriteLine($"Konum: {currentAssembly.Location}");
// .Location boş dönebilir — Native AOT veya single-file publish'te

// Target framework — bu binary hangi runtime için derlendi?
var targetFramework = currentAssembly
    .GetCustomAttribute<TargetFrameworkAttribute>();
Console.WriteLine($"Target Framework: {targetFramework?.FrameworkName}");

Console.WriteLine();

// ============================================================
// BÖLÜM 3: Managed vs Unmanaged — P/Invoke örneği
// ============================================================
Console.WriteLine("=== Managed vs Unmanaged Kod ===");

// CLR tarafından yönetilen (managed) kod — normal C#
int managedResult = ManagedHesapla(10, 20);
Console.WriteLine($"Managed hesap: {managedResult}");

// Unmanaged kod çağrısı — P/Invoke ile Windows/Linux native API
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    // Windows'a özel: kernel32.dll içindeki native fonksiyon
    uint tickCount = NativeMethods.GetTickCount();
    Console.WriteLine($"Windows native GetTickCount: {tickCount} ms (sistem başladığından beri)");
}
else
{
    // Linux/Mac: libc içindeki getpid
    int pid = NativeMethods.GetPid();
    Console.WriteLine($"Unix native getpid: {pid}");
}

Console.WriteLine();

// ============================================================
// BÖLÜM 4: AssemblyLoadContext — dinamik assembly yükleme
// ============================================================
Console.WriteLine("=== AssemblyLoadContext ===");

// Default context içindeki yüklü assembly'leri listele
// AppDomain.CurrentDomain.GetAssemblies() hâlâ çalışır ama
// AssemblyLoadContext.Default daha doğru yaklaşım
var loadedAssemblies = System.Runtime.Loader.AssemblyLoadContext.Default
    .Assemblies;

int count = 0;
foreach (var asm in loadedAssemblies)
{
    if (count++ < 5) // ilk 5'i göster
        Console.WriteLine($"  Yüklü: {asm.GetName().Name}");
}
Console.WriteLine($"  ... toplam {count} assembly yüklü");

Console.WriteLine();

// ============================================================
// BÖLÜM 5: JIT'in yaptığı işi gözlemle (küçük ısınma demosu)
// ============================================================
Console.WriteLine("=== JIT Warm-up Etkisi ===");

// İlk çağrı: JIT bu metodu derleyecek (cold)
var sw = System.Diagnostics.Stopwatch.StartNew();
long result1 = Hesapla(1_000_000);
sw.Stop();
Console.WriteLine($"İlk çağrı (cold - JIT devrede): {sw.ElapsedMicroseconds} µs, sonuç: {result1}");

// İkinci çağrı: JIT zaten derledi, makine kodu hazır (warm)
sw.Restart();
long result2 = Hesapla(1_000_000);
sw.Stop();
Console.WriteLine($"İkinci çağrı (warm): {sw.ElapsedMicroseconds} µs, sonuç: {result2}");

// Not: Fark küçük görünebilir — küçük metot JIT çabuk derler.
// Büyük metodlarda veya startup'ta bu fark daha belirgindir.
// ASP.NET Core: ilk HTTP isteği genellikle daha yavaş — aynı sebep.

// ============================================================
// Yardımcı metotlar
// ============================================================

static int ManagedHesapla(int a, int b) => a + b;

static long Hesapla(int n)
{
    long toplam = 0;
    for (int i = 0; i < n; i++)
        toplam += i;
    return toplam;
}

// P/Invoke tanımları — CLR'nin unmanaged köprüsü
static class NativeMethods
{
    [DllImport("kernel32.dll")]
    public static extern uint GetTickCount();

    [DllImport("libc", EntryPoint = "getpid")]
    public static extern int GetPid();
}
