namespace SrpDemo;

// ✅ Sorumluluk: SADECE şifre işlemleri
// Kim değiştirirse? → Güvenlik ekibi algoritma değiştirmek isteyince
// Yarın Argon2'ye geçersen SADECE bu dosya değişir
public class SifreServisi
{
    public string Hash(string duzMetin)
        => BCrypt.Net.BCrypt.HashPassword(duzMetin);
    // bunu yazmasaydık → her yerde BCrypt kodu tekrarlanırdı
    // bu metot sayesinde algoritma değişikliği tek noktada

    public bool Dogrula(string duzMetin, string hash)
        => BCrypt.Net.BCrypt.Verify(duzMetin, hash);
    // bunu yazmasaydık → login kodu için BCrypt'e direkt bağımlı olurduk
}
