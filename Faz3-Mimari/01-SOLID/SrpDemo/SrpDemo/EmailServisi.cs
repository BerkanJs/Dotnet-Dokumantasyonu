namespace SrpDemo;

// ✅ Sorumluluk: SADECE e-posta bildirimleri
// Kim değiştirirse? → Pazarlama "şablon değişsin", IT "SMTP sunucu değişti" derse
public class EmailServisi
{
    public void HosgeldinGonder(string email)
    {
        // Gerçek projede SmtpClient inject edilir
        // Demo'da konsola yazıyoruz
        Console.WriteLine($"[EMAIL] Konu: Hoş geldiniz! → {email}");
        Console.WriteLine($"[EMAIL] Gövde: Merhaba {email}, kaydınız tamamlandı.");
        // bunu yazmasaydık → kullanıcı bildirim alamazdı
        // şablon değişince SADECE bu dosya değişir, KullaniciServisi'ne dokunmaz
    }
}
