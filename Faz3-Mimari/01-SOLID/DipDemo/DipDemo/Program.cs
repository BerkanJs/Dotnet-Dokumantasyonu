using DipDemo;

// Production: EfKitapRepository inject et
var prodServis = new KitapServisi(new EfKitapRepository());
prodServis.KitapEkle("1984");
Console.WriteLine(string.Join(", ", prodServis.HepsiniGetir()));

Console.WriteLine();

// Test: InMemoryKitapRepository inject et — KitapServisi kodu hiç değişmedi
var testServis = new KitapServisi(new InMemoryKitapRepository());
testServis.KitapEkle("Cesur Yeni Dünya");
Console.WriteLine(string.Join(", ", testServis.HepsiniGetir()));
