# Gün 86 — Immutability ve Functional Patterns

---

## Immutability Neden Önemli?

Gün 82'de thread safety için lock, Interlocked gibi araçlar gördük. Bu araçlar doğru ama karmaşık — bir lock'u unutmak race condition'a yol açar.

Daha radikal ama daha basit bir çözüm var: veriyi hiç değiştirme. Bir nesne oluşturulduktan sonra asla değişmiyorsa, kaç thread erişirse erişsin sorun olmaz — kimse bir şeyi değiştirmediği için yarış yok.

Gerçek dünya benzetmesi: kağıda yazılmış bir fatura. Kimse üzerine yazamaz, düzeltmek istersen yeni fatura kesersin. Orijinal bozulmaz, herkes aynı doğru veriyi görür.

---

## Neden Thread-Safe?

Thread safety sorunlarının hepsi "birden fazla thread aynı anda aynı veriyi değiştiriyor" durumundan kaynaklanır. Veri değiştirilemezse bu durum ortadan kalkar.

```
Mutable:   Thread A → kitap.Fiyat = 50 | Thread B → kitap.Fiyat = 60 → hangisi kazandı?
Immutable: Thread A → yeni kitap1(Fiyat=50) | Thread B → yeni kitap2(Fiyat=60) → ikisi bağımsız
```

---

## Record Types ve with Expression

C# `record` türü immutable veri taşımak için tasarlanmıştır. Değiştirmek istediğinde `with` ile yeni bir kopya oluşturursun.

```csharp
public record Kitap(string Ad, decimal Fiyat, int Stok);

var kitap = new Kitap("Dune", 89.90m, 42);

// Fiyatı değiştirmek istiyorsun — orijinali değiştirmezsin
var indirimli = kitap with { Fiyat = 69.90m };
// kitap hâlâ 89.90 — bozulmadı
// indirimli yeni bir nesne — sadece Fiyat farklı

// with olmasaydık → new Kitap(kitap.Ad, 69.90m, kitap.Stok) yazmak zorunda kalırdın
// 10 alanı olan bir record'da 9'unu elle kopyalamak hatalara davetiye çıkarır
```

`record class` heap'e gider (referans tipi).  
`record struct` stack'te kalır (değer tipi) — küçük immutable veri için idealdir.

```csharp
public readonly record struct Para(decimal Miktar, string BirimKodu);
// readonly + record struct → hem stack hem immutable hem with destekli
```

---

## ImmutableList ve ImmutableDictionary

Normal `List<T>` değiştirilebilir — `Add`, `Remove` listeyi yerinde değiştirir.  
`ImmutableList<T>` ise her değişiklikte yeni bir liste döndürür, orijinal bozulmaz.

```csharp
using System.Collections.Immutable;

var kitaplar = ImmutableList.Create("Dune", "1984", "Fahrenheit 451");

// Add yeni liste döndürür — orijinal değişmez
var yeniListe = kitaplar.Add("Sapiens");
// kitaplar → hâlâ 3 eleman
// yeniListe → 4 eleman

// bunu normal List ile yapsaydık → Add orijinali değiştirir, başka thread okuyorsa race condition
```

```csharp
var config = ImmutableDictionary<string, string>.Empty
    .Add("ConnectionString", "Server=...")
    .Add("MaxRetry", "3");

// Değer güncelleme — yeni dictionary
var yeniConfig = config.SetItem("MaxRetry", "5");
// config["MaxRetry"] hâlâ "3" — orijinal bozulmadı
```

**Maliyet:** Her değişiklik yeni nesne oluşturur — çok sık değişen veriler için performans sorunu olabilir. Az değişen, çok okunan veriler için ideal.

---

## Pure Function — Saf Fonksiyon

Saf fonksiyon: aynı girdiye her zaman aynı çıktıyı verir, hiçbir dış durumu değiştirmez.

```csharp
// Pure — dış dünyaya dokunmuyor, aynı girdi her zaman aynı sonuç
public decimal IndirimliFiyatHesapla(decimal fiyat, int indirimYuzdesi)
{
    return fiyat - (fiyat * indirimYuzdesi / 100);
    // DB yok, cache yok, static değişken yok — sadece hesaplama
}

// Impure — dış dünyaya bağımlı
public decimal IndirimliFiyatHesapla(Kitap kitap)
{
    var oran = _indirimServisi.GetOranAsync(kitap.Id).Result;   // dış bağımlılık
    _logger.Log($"İndirim hesaplandı: {kitap.Id}");             // yan etki (side effect)
    return kitap.Fiyat - (kitap.Fiyat * oran / 100);
}
```

**Neden önemli?**
- **Test edilebilirlik:** Pure fonksiyonu test etmek için mock, setup, DI gerekmez. Girdi ver, çıktıyı kontrol et.
- **Parallelism:** Dış duruma dokunmayan fonksiyon birden fazla thread'de güvenle çalışır — lock gerekmez.
- **Okunabilirlik:** Fonksiyonun ne yaptığını anlamak için sınıfın state'ini bilmeye gerek yok.

---

## Functional Core, Imperative Shell

Tüm uygulamayı pure yapmak imkansız — DB'ye yazmak, HTTP çağırmak zorunlu. Ama iş mantığını pure tutup, dış dünya etkileşimini dış kabuğa itebilirsin.

```
┌───────────────────────────────┐
│      Imperative Shell         │  DB oku, HTTP çağır, dosya yaz
│  (Controller, Handler, Repo)  │  → impure ama ince
├───────────────────────────────┤
│      Functional Core          │  Hesapla, doğrula, dönüştür
│  (Domain logic, pure funcs)   │  → pure, test edilebilir, thread-safe
└───────────────────────────────┘
```

```csharp
// Functional Core — pure, test edilebilir
public static class FiyatHesaplayici
{
    public static decimal IndirimUygula(decimal fiyat, int oran)
        => fiyat - (fiyat * oran / 100);

    public static bool StokYeterliMi(int mevcut, int talep)
        => mevcut >= talep;
}

// Imperative Shell — impure ama ince
public class SiparisHandler
{
    public async Task<Result> Handle(SiparisCommand cmd, CancellationToken ct)
    {
        var kitap = await _repo.GetAsync(cmd.KitapId, ct);      // impure — DB oku

        if (!FiyatHesaplayici.StokYeterliMi(kitap.Stok, cmd.Adet))
            return Result.Failure("Yetersiz stok");              // pure karar

        var fiyat = FiyatHesaplayici.IndirimUygula(kitap.Fiyat, cmd.Indirim);  // pure hesaplama

        await _repo.SiparisEkleAsync(fiyat, cmd, ct);           // impure — DB yaz
        return Result.Success();
    }
}
```

Handler ince — sadece veriyi getir, pure fonksiyonlara sor, sonucu kaydet.  
İş mantığının testi handler'a hiç bağlı değil — `FiyatHesaplayici` bağımsız test edilir.

---

## 500 vs 50K Kullanıcı

| | 500 | 50K |
|---|---|---|
| Record types | İyi alışkanlık — DTO'larda kullan | Aynı |
| ImmutableList/Dict | Nadiren gerekir | Paylaşılan config, cache için değerli |
| Pure functions | Her zaman iyi — test kolaylığı | Thread safety garantisi kritik |
| Functional Core | Proje küçükse doğal oluyor | Domain karmaşıklaşınca zorunlu hissettiriyor |

---

## Kontrol Soruları

1. Immutable bir nesneye birden fazla thread aynı anda erişirse neden sorun olmaz?
2. `with` expression ne yapar, orijinal nesneye ne olur?
3. Pure fonksiyon ile impure fonksiyon arasındaki fark nedir?
4. "Functional Core, Imperative Shell" yaklaşımında DB erişimi nerede kalır?
