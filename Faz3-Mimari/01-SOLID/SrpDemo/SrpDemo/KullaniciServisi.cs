namespace SrpDemo;

// ✅ Sorumluluk: SADECE kayıt akışını orkestra et
// Kim değiştirirse? → Kayıt adımları değişirse (SMS doğrulama eklendi, onay maili kaldırıldı)
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
        // bunu yazmasaydık → geçersiz veri DB'ye girerdi
        // validasyon kuralı değişince bu satıra dokunmuyorsun

        var hash = _sifreServisi.Hash(sifre);
        // bunu yazmasaydık → düz şifre kaydedilirdi
        // algoritma değişince bu satıra dokunmuyorsun

        Console.WriteLine($"[DB] Kaydedildi: {email} | Hash: {hash[..20]}...");
        // gerçek projede _context.SaveChangesAsync() olur

        _emailServisi.HosgeldinGonder(email);
        // bunu yazmasaydık → bildirim gitmezdi
        // şablon değişince bu satıra dokunmuyorsun

        Console.WriteLine($"[LOG] Yeni kullanıcı: {email} @ {DateTime.Now}");
    }
}
