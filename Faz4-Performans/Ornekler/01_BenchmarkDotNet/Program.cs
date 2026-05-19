// GÜN 73 — BenchmarkDotNet Program Giriş Noktası
// Çalıştırma: dotnet run -c Release
// Release modu ZORUNLU — Debug modda benchmark sonuçları güvenilmez

using BenchmarkDotNet.Running;

// ne yapar: hangi benchmark sınıfını çalıştıracağını belirtir
// bunu yazmasaydık: BenchmarkDotNet neyi ölçeceğini bilemezdi

// Tek benchmark çalıştır:
BenchmarkRunner.Run<StringBirlesirmeBenchmark>();

// Birden fazla:
// BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
// → komut satırından hangi benchmark'ı çalıştıracağını seçebilirsin

// Çıktı: BenchmarkDotNet/results/ klasörüne HTML, CSV, Markdown dosyaları yazar
