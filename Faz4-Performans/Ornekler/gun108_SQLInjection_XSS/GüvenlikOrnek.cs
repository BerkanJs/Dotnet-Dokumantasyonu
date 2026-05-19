// GÜN 108 — SQL Injection, XSS ve Güvenlik Header'ları

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Ornekler.gun108;

// --- 1. SQL Injection ---
public class SqlInjectionOrnek
{
    private readonly string _connectionString = "...";

    // YANLIŞ: string concat → SQL Injection
    public async Task<string> YanlisKullaniciBul(string kullaniciAdi)
    {
        // Saldırgan gönderir: admin' OR '1'='1
        // SQL olur: SELECT * FROM Users WHERE KullaniciAdi = 'admin' OR '1'='1'
        // → tüm kullanıcılar döner

        // ne yapar: ham SQL çalıştırır
        // SORUN: kullaniciAdi parametresi doğrudan SQL'e eklenir → injection
        var sql = $"SELECT * FROM Users WHERE KullaniciAdi = '{kullaniciAdi}'";
        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(sql, conn);
        // ... tehlikeli!
        return "";
    }

    // DOĞRU: parametreli sorgu
    public async Task<string> DogruKullaniciBul(string kullaniciAdi)
    {
        // ne yapar: kullaniciAdi parametresi ayrı gönderilir — SQL'e gömülmez
        // bunu yazmasaydık: tüm DB manipüle edilebilirdi (DROP TABLE dahil)
        var sql = "SELECT * FROM Users WHERE KullaniciAdi = @kullaniciAdi";
        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@kullaniciAdi", kullaniciAdi);
        // → SQL Server parametreyi string literal olarak işler, parse etmez
        return "";
    }
}

// DAHA DOĞRU: EF Core — parametreler otomatik
public class EfCoreSqlOrnek
{
    private readonly AppDbContext _db;

    public EfCoreSqlOrnek(AppDbContext db) => _db = db;

    public async Task<List<Kullanici>> EfCoreIleKullaniciBul(string isim)
    {
        // ne yapar: EF Core LINQ'ı parametreli SQL'e çevirir — injection imkansız
        // bunu yazmasaydık: FromSqlRaw ile string concat yapmak zorunda kalırdık
        return await _db.Kullanicilar
            .Where(k => k.Ad == isim)
            .ToListAsync();
        // Üretilen SQL: SELECT ... WHERE Ad = @__isim_0  → parametreli
    }

    // Ham SQL gerekirse: interpolated string kullan — EF Core parametreleştirir
    public async Task<List<Kullanici>> HamSqlGüvenli(string isim)
    {
        // ne yapar: $"" string interpolation — EF Core bunu SqlParameter'a çevirir
        // bunu yazmasaydık: FromSqlRaw + string concat → injection açığı
        return await _db.Kullanicilar
            .FromSql($"SELECT * FROM Kullanicilar WHERE Ad = {isim}")
            .ToListAsync();
    }
}

// --- 2. XSS: Cross-Site Scripting ---
public class XssOrnek
{
    // YANLIŞ: kullanıcı girdisini HTML'e doğrudan yaz
    // <div>Yorum: {kullanici_girdisi}</div>
    // Saldırgan gönderir: <script>document.cookie → saldırgan sunucusuna gönder</script>

    // DOĞRU: HTML encode
    public string HtmlEncode(string girdi)
    {
        // ne yapar: <, >, & gibi karakterleri HTML entity'ye çevirir
        // bunu yazmasaydık: kullanıcı girdisi HTML olarak execute edilirdi
        return System.Net.WebUtility.HtmlEncode(girdi);
        // < → &lt;   > → &gt;   & → &amp;   " → &quot;
    }

    // ASP.NET Core Razor otomatik encode eder:
    // @Model.Yorum  → encode edilir (güvenli)
    // @Html.Raw(Model.Yorum) → ENCODE EDİLMEZ (tehlikeli, sadece güvenilir içerik için)
}

// --- 3. Güvenlik Header'ları ---
public static class GüvenlikHeaderlari
{
    public static void Konfigur(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            // ne yapar: sitenin başka sitelerde iframe içinde açılmasını engeller
            // bunu yazmasaydık: clickjacking saldırısı mümkün olurdu
            context.Response.Headers.Append("X-Frame-Options", "DENY");

            // ne yapar: tarayıcının MIME type sniffing yapmasını engeller
            // bunu yazmasaydık: JS olarak upload edilen resim dosyası execute edilebilirdi
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

            // ne yapar: sadece HTTPS'den kaynak yükle, inline script/style yasak
            // bunu yazmasaydık: XSS saldırısı script inject edip çalıştırabilirdi
            context.Response.Headers.Append(
                "Content-Security-Policy",
                "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:;");

            // ne yapar: tarayıcıyı HTTPS kullanmaya zorlar (2 yıl)
            // bunu yazmasaydık: HTTP üzerinden de erişilebilir → man-in-the-middle
            context.Response.Headers.Append(
                "Strict-Transport-Security",
                "max-age=63072000; includeSubDomains; preload");

            // ne yapar: Referer header'da sadece origin gönder, tam URL değil
            // bunu yazmasaydık: başka siteye geçişte URL (query string dahil) paylaşılırdı
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

            await next();
        });
    }
}

public class AppDbContext : DbContext
{
    public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();
}

public class Kullanici
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;
}
