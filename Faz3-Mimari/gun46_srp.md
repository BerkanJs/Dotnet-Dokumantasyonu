# Gün 46 — Single Responsibility Principle (SRP)

---

## 1. Günlük Hayat Analojisi

Bir restoranda tek bir garson hem sipariş alıyor, hem mutfakta yemek yapıyor, hem kasada ödeme alıyor, hem temizlik yapıyor.

Ne olur?
- Sipariş alırken mutfaktan çağrılıyor → iş yarım kalıyor
- Birisi hastalanınca her şey duruyor
- "Yemeğin tuzu az" dersen kimi azarlarsın?

Yazılımdaki karşılığı: **God Class.** `UserService` içinde hem kullanıcı kaydı, hem e-posta, hem şifre hash'leme. Bir şey değişince her şeye dokunmak zorunda kalıyorsun.

---

## 2. SRP'nin Gerçek Tanımı

> "A class should have only one reason to change." — Robert C. Martin

"One class, one job" denir ama bu yanlış basitleştirme.

Doğru yorum: **Bir class'ı değiştirmeni gerektiren tek bir sebep/aktör olmalı.**

```
Pazarlama "e-posta şablonu değişsin" → UserService değişmemeli
Güvenlik  "şifre algoritması değişsin" → UserService değişmemeli
Ürün ekibi "kayıt zorunlu alanlar değişsin" → UserService değişebilir ✓
```

---

## 3. Faz2'de Böyle Yaptık

`Faz2-ASPNET-MVC/KitabeviMVC/Services/EfKitapServisi.cs` içinde şunlar bir aradaydı:

```csharp
public class EfKitapServisi : IKitapServisi
{
    public async Task<List<Kitap>> TumKitaplariGetirAsync() { ... }  // veri erişimi
    public async Task KitapEkleAsync(Kitap kitap) { ... }           // veri yazma
    public async Task StokGuncelleAsync(int id, int miktar) { ... } // iş kuralı
    // + validation + loglama hepsi burada
}
```

**500 kullanıcı için:** Çalışır. Proje küçük, tek geliştirici, hız önemli.

**50k kullanıcı / büyük proje için sorun:**
- Stok kuralı değişince (`EfKitapServisi` açılıyor) → validasyon ve veri erişimi kodu aynı dosyada dikkat dağılıyor
- Farklı geliştiriciler aynı dosyaya dokunuyor → merge conflict
- Servisi unit test etmek için DbContext + 3 başka dependency mock'lamak gerekiyor → çünkü içinde 4 farklı sorumluluk var

---

## 4. Büyük Projede Böyle Yapmalısın

Her sorumluluk kendi class'ında. Değişme sebebi = tek aktör.

```csharp
// SifreServisi.cs
// Sorumluluk: SADECE şifre işlemleri
// Kim değiştirirse? → Güvenlik ekibi algoritma değiştirmek isteyince

public class SifreServisi
{
    public string Hash(string duzMetin)
        => BCrypt.Net.BCrypt.HashPassword(duzMetin);
    // bunu yazmasaydık → her yerde BCrypt tekrarlanırdı
    // yarın Argon2'ye geçince SADECE bu dosya değişir

    public bool Dogrula(string duzMetin, string hash)
        => BCrypt.Net.BCrypt.Verify(duzMetin, hash);
    // bunu yazmasaydık → login kodu BCrypt'e direkt bağımlı olurdu
}
```

```csharp
// KullaniciValidator.cs
// Sorumluluk: SADECE validasyon kuralları
// Kim değiştirirse? → Ürün ekibi "şifre 12 karakter olsun" derse

public class KullaniciValidator
{
    public void KayitDogrula(string email, string sifre)
    {
        if (string.IsNullOrEmpty(email))
            throw new ArgumentException("Email boş olamaz");
        // bunu yazmasaydık → geçersiz email DB'ye girerdi

        if (!email.Contains('@'))
            throw new ArgumentException("Geçerli bir email girin");
        // bunu yazmasaydık → "berkangmail.com" kabul edilirdi

        if (sifre.Length < 8)
            throw new ArgumentException("Şifre en az 8 karakter olmalı");
        // kuralı 12'ye çıkarmak için SADECE bu dosya değişir
    }
}
```

```csharp
// EmailServisi.cs
// Sorumluluk: SADECE e-posta bildirimleri
// Kim değiştirirse? → Pazarlama şablon değiştirmek isteyince

public class EmailServisi
{
    public void HosgeldinGonder(string email)
    {
        // Gerçek projede SmtpClient inject edilir
        Console.WriteLine($"[EMAIL] Hoş geldiniz! → {email}");
        // şablon değişince SADECE bu dosya değişir
        // KullaniciServisi'ne hiç dokunmaz
    }
}
```

```csharp
// KullaniciServisi.cs
// Sorumluluk: SADECE kayıt akışını orkestra et
// Kim değiştirirse? → Kayıt adımları değişirse (SMS eklendi, onay maili kaldırıldı)
// İş mantığı burada yok — her adımı ilgili servise delege ediyor

public class KullaniciServisi
{
    private readonly SifreServisi _sifreServisi;
    // bunu yazmasaydık → hash mantığı buraya sızardı

    private readonly KullaniciValidator _validator;
    // bunu yazmasaydık → validasyon kuralları buraya sızardı

    private readonly EmailServisi _emailServisi;
    // bunu yazmasaydık → e-posta kodu buraya sızardı

    public KullaniciServisi(
        SifreServisi sifreServisi,
        KullaniciValidator validator,
        EmailServisi emailServisi)
    {
        _sifreServisi = sifreServisi;
        _validator = validator;
        _emailServisi = emailServisi;
        // Constructor injection: bağımlılıklar dışarıdan gelir
        // bunu yazmasaydık → her bağımlılığı burada new'lemek zorunda kalırdık
        // → test ederken gerçek servisleri taklit edemezdik (mock)
    }

    public void KayitOl(string email, string sifre)
    {
        _validator.KayitDogrula(email, sifre);
        // validasyon kuralı değişince bu satıra dokunmuyorsun

        var hash = _sifreServisi.Hash(sifre);
        // algoritma değişince bu satıra dokunmuyorsun

        Console.WriteLine($"[DB] Kaydedildi: {email} | Hash: {hash[..20]}...");

        _emailServisi.HosgeldinGonder(email);
        // şablon değişince bu satıra dokunmuyorsun

        Console.WriteLine($"[LOG] Yeni kullanıcı: {email} @ {DateTime.Now}");
    }
}
```

---

## 5. 500 vs 50k Kullanıcı

| | 500 kullanıcı/ay | 50k kullanıcı/ay |
|---|---|---|
| **God class bırak?** | Kısa vadeli, tek geliştirici → çalışır | ❌ Ekip büyüyünce merge conflict, test edilemez |
| **SRP uygula?** | Uygulayabilirsin ama overhead var | ✅ Şart — her değişiklik izole olmalı |
| **Ne zaman ayrıştır?** | Class 150+ satır geçince | Class'a 2+ farklı sebepten değişiklik gelince |
| **Overengineering sinyali** | 3 satırlık helper için ayrı class açmak | — |

**Pratik kural:** "Bu class'ı değiştirmem için kaç farklı sebep var?" sorusunu sor.
- 1 → SRP uyumlu
- 2+ → bölmeyi düşün
- 5+ → kesinlikle böl

---

## 6. Faz3'te Sonraki Adım

`KitabeviOnion` projesinde `EfKitapServisi`'ni de bu şekilde böleceğiz:
- `KitapValidator` → validasyon
- `KitapRepository` → veri erişimi
- `KitapServisi` → iş kuralları orkestrasyon

---

## Sorular

1. Faz2'deki `EfKitapServisi`'nde kaç farklı "değişme sebebi" var? Listeler misin?
2. `KullaniciServisi` artık SRP'ye uyuyor ama bağımlılık sayısı arttı. Bu dezavantaj mı?
3. `SifreServisi`'ni unit test etmek neden `KullaniciServisi`'ni test etmekten daha kolay?
