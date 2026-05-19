// GÜN 106 — OAuth 2.0, JWT ve API Key
// JWT: stateless token — her istekte doğrula, DB'ye gitme
// Refresh token: access token süresi kısıt, refresh token ile yenile

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Ornekler.gun106;

// --- 1. Program.cs: JWT Bearer doğrulama ---
public static class JwtSetup
{
    public static void Kaydet(WebApplicationBuilder builder)
    {
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    // ne yapar: token'ın issuer'ını doğrular
                    // bunu yazmasaydık: başka uygulama için üretilen token geçerli sayılırdı
                    ValidateIssuer = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],

                    // ne yapar: token'ın audience'ını doğrular
                    // bunu yazmasaydık: farklı servise ait token bu API'da da geçerli olurdu
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["Jwt:Audience"],

                    // ne yapar: imzayı doğrular — manipüle edilmiş token geçersiz
                    // bunu yazmasaydık: herkes geçerli token üretebilirdi
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!)),

                    // ne yapar: süre kontrolü — exp claim'i geçmişte olan token geçersiz
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1) // saat farkı toleransı
                };

                // ne yapar: SignalR/WebSocket için token query string'den al
                // bunu yazmasaydık: WebSocket Authorization header desteklemediğinden auth çalışmazdı
                opt.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var accessToken = ctx.Request.Query["access_token"];
                        var path = ctx.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            ctx.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorization();
    }
}

// --- 2. JWT token üretimi ---
public class JwtTokenServisi
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtTokenServisi(string secretKey, string issuer, string audience)
    {
        _secretKey = secretKey;
        _issuer = issuer;
        _audience = audience;
    }

    public string TokenUret(string kullaniciId, string email, IEnumerable<string> roller)
    {
        var claims = new List<Claim>
        {
            // ne yapar: "sub" claim — JWT standardı, kullanıcı kimliği
            // bunu yazmasaydık: token içinde kimlik bilgisi olmaz, User.FindFirst("sub") null dönerdi
            new(JwtRegisteredClaimNames.Sub, kullaniciId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // token ID — blacklist için
        };

        claims.AddRange(roller.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            // ne yapar: token 15 dakika geçerli — kısa süre = güvenlik, refresh token = kullanılabilirlik
            // bunu yazmasaydık: uzun süreli token çalınırsa saatler/günler geçerli olurdu
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Refresh token — kriptografik güvenli rastgele değer
    public string RefreshTokenUret()
    {
        // ne yapar: 64 byte rastgele veri → base64 string
        // bunu yazmasaydık: Guid kullanırdık — entropy daha düşük, tahmin edilebilir
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }
}

// --- 3. API Key middleware ---
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private const string API_KEY_HEADER = "X-API-Key";

    public ApiKeyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IApiKeyValidator validator)
    {
        if (!context.Request.Headers.TryGetValue(API_KEY_HEADER, out var apiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("API key gerekli");
            return;
        }

        // ne yapar: API key'i DB'de doğrula — hash karşılaştır, plain-text değil
        // bunu yazmasaydık: plain-text API key DB'de saklansaydı ihlal = tüm key'ler tehlikede
        if (!await validator.DogrulaAsync(apiKey!))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Geçersiz API key");
            return;
        }

        await _next(context);
    }
}

public interface IApiKeyValidator
{
    Task<bool> DogrulaAsync(string apiKey);
}
