namespace SrpDemo;

// ✅ Sorumluluk: SADECE kayıt kurallarını doğrula
// Kim değiştirirse? → Ürün ekibi "şifre 12 karakter olsun" veya "telefon zorunlu" derse
public class KullaniciValidator
{
    public void KayitDogrula(string email, string sifre)
    {
        if (string.IsNullOrEmpty(email))
            throw new ArgumentException("Email boş olamaz");
        // bunu yazmasaydık → geçersiz email DB'ye girerdi

        if (!email.Contains('@'))
            throw new ArgumentException("Geçerli bir email adresi girin");
        // bunu yazmasaydık → "berkangmail.com" gibi adresler kabul edilirdi

        if (sifre.Length < 8)
            throw new ArgumentException("Şifre en az 8 karakter olmalı");
        // kuralı 12'ye çıkarmak için sadece bu dosyaya dokunuyorsun
        // KullaniciServisi'ne hiç dokunman gerekmez
    }
}
