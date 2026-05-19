// GÜN 73 — Program.cs
using BenchmarkDotNet.Running;

// ne yapar: belirtilen benchmark sınıfını çalıştırır
// bunu yazmasaydık: BenchmarkDotNet neyi ölçeceğini bilemezdi
BenchmarkRunner.Run<StringBirlesirmeBenchmark>();

// Tüm benchmark'ları komut satırından seçmek için:
// BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
