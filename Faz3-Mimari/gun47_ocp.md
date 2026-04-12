# Gün 47 — Open/Closed Principle (OCP)

---

## 1. Günlük Hayat Analojisi

Bir telefon kılıfı üreticisi düşün. Her yeni telefon modeli çıkınca fabrikayı baştan inşa etmiyor — yeni bir kalıp ekliyor. Fabrika açık (yeni kalıp eklenebilir), ama mevcut kalıplara dokunulmuyor (değişime kapalı).

Yazılımda: **yeni davranış = yeni class. Mevcut kod dokunulmaz.**

---

## 2. Tanım

> "Software entities should be open for extension, but closed for modification."
> — Bertrand Meyer

```
Açık   → yeni davranış eklenebilir
Kapalı → mevcut çalışan kod değiştirilmez
```

**OCP'nin en yaygın sinyali:** Projeye yeni bir özellik eklemek için var olan bir class'ı açıp `if` veya `switch` bloğu ekliyorsun.

---

## 3. Faz2'de Böyle Yaptık

`Faz2-ASPNET-MVC/KitabeviMVC/Services/CachedKitapServisi.cs` — Faz2'de OCP'yi zaten uyguladık, farkında olmadan:

```csharp
// Faz2: Caching eklemek için EfKitapServisi'ni AÇMADIK
// Yeni class yazdık — mevcut kod dokunulmadı

public class CachedKitapServisi : IKitapServisi
{
    private readonly IKitapServisi _gercekServis;  // EfKitapServisi içeride
    // ...
}
```

```
Program.cs'de kayıt:
  builder.Services.AddScoped<IKitapServisi, CachedKitapServisi>()
```

`EfKitapServisi` caching'den tamamen habersiz kaldı. Caching davranışını eklemek için onu açmadık — yeni bir class yazdık. **Bu OCP.**

Şimdi aynı prensibin daha klasik kullanımına bakalım: **indirim hesaplama.**

---

## 4. OCP İhlali — Faz2'ye İndirim Eklenseydik Ne Olurdu?

Eğer `EfKitapServisi`'ne indirim hesaplama eklenseydik muhtemelen şöyle yazardık:

```csharp
// ❌ Her yeni indirim tipi bu metodu açıp if bloğu ekletir
public decimal IndirimliFiyatHesapla(decimal fiyat, string indirimTipi)
{
    if (indirimTipi == "ogrenci")
        return fiyat * 0.80m;
    else if (indirimTipi == "yaz")
        return fiyat * 0.70m;
    else if (indirimTipi == "kupon")
        return fiyat * 0.85m;
    // Yeni indirim gelince → BU DOSYA AÇILIYOR
    // Mevcut çalışan if blokları bozulma riski taşıyor
    // Test: her yeni if için tüm dalları tekrar test etmek gerekiyor
    return fiyat;
}
```

**Problem:** Pazarlama "doğum günü indirimi ekle" deyince bu metodu açıyorsun → OCP ihlali.

---

## 5. Büyük Projede Böyle Yapmalısın

Her indirim tipi kendi class'ında. `FiyatServisi` indirim tipini bilmiyor — sadece interface'i biliyor.

```csharp
// IIndirimStrategy.cs
// Yeni indirim tipi = yeni class → FiyatServisi'ne hiç dokunulmaz
public interface IIndirimStrategy
{
    decimal Hesapla(decimal fiyat);
    string Ad { get; }
}
```

```csharp
// Indirimler.cs
public class OgrenciIndirimi : IIndirimStrategy
{
    public string Ad => "Öğrenci İndirimi";
    public decimal Hesapla(decimal fiyat) => fiyat * 0.80m;
    // oran değişince sadece bu dosyaya dokunulur
}

public class YazMevsimIndirimi : IIndirimStrategy
{
    public string Ad => "Yaz Mevsimi İndirimi";
    public decimal Hesapla(decimal fiyat) => fiyat * 0.70m;
    // FiyatServisi açılmadı, OgrenciIndirimi açılmadı
}

public class KuponIndirimi : IIndirimStrategy
{
    private readonly decimal _kuponOrani;
    public string Ad => $"Kupon İndirimi (%{(1 - _kuponOrani) * 100:0})";

    public KuponIndirimi(decimal kuponOrani) => _kuponOrani = kuponOrani;
    // farklı oranlı kuponlar için aynı class, farklı constructor parametresi
    // bunu yazmasaydık → her oran için ayrı class veya if dalı gerekirdi

    public decimal Hesapla(decimal fiyat) => fiyat * _kuponOrani;
}
```

```csharp
// FiyatServisi.cs
// Bu class bir daha açılmayacak — yeni indirim tipi gelse de
public class FiyatServisi
{
    public decimal IndirimliFiyatHesapla(decimal fiyat, IIndirimStrategy indirim)
    {
        var sonuc = indirim.Hesapla(fiyat);
        // indirim tipini bilmiyoruz — polymorphism hallediyor
        // bunu yazmasaydık → hangi indirim uygulandığını bilemezdik

        Console.WriteLine($"{indirim.Ad}: {fiyat:C2} → {sonuc:C2}");
        return sonuc;
    }
}
```

Yarın "doğum günü indirimi" gelirse:

```csharp
// YENİ DOSYA — başka hiçbir şeye dokunulmadı
public class DogumGunuIndirimi : IIndirimStrategy
{
    public string Ad => "Doğum Günü İndirimi";
    public decimal Hesapla(decimal fiyat) => fiyat * 0.75m;
}
```

---

## 6. Faz2 ile Bağlantı — CachedKitapServisi Tekrar

Faz2'deki `CachedKitapServisi` aslında **Decorator pattern** — ve OCP'nin en temiz örneği:

```
Eklenen davranış: Caching
Değiştirilen class: YOK
Açılan class: YOK
Yazılan yeni class: CachedKitapServisi
```

Faz3'te bu pattern'i Onion Architecture içinde tekrar göreceğiz: `LoggingBehavior`, `ValidationBehavior` gibi MediatR pipeline behavior'ları aynı mantıkla çalışır.

---

## 7. 500 vs 50k Kullanıcı

| | 500 kullanıcı/ay | 50k kullanıcı/ay |
|---|---|---|
| **if/switch bırak?** | 2-3 sabit tip varsa → çalışır | ❌ Her sprint'te yeni tip geliyorsa bakımı imkansız |
| **Strategy pattern?** | Tipler sık değişiyorsa uygula | ✅ Şart — her yeni tip izole, test edilebilir |
| **Overengineering sinyali** | 2 tip var ve hiç değişmeyecek → interface gereksiz | — |

**Pratik kural:** "Bu `if` bloğuna ne sıklıkla yeni dal ekliyorum?" sorusunu sor.
- Hiç → if bırak
- Her birkaç ayda bir → interface + Strategy düşün
- Her sprint → kesinlikle OCP uygula

---

## Sorular

1. Faz2'deki `CachedKitapServisi` neden OCP örneği? `EfKitapServisi`'ne ne zaman dokundun?
2. `KuponIndirimi` class'ı constructor parametresi alıyor. ASP.NET Core DI container'ına bunu nasıl kayıt edersin?
3. Birden fazla indirim aynı anda uygulanacaksa (`OgrenciIndirimi` + `KuponIndirimi`) yapıyı nasıl değiştirirdin?
