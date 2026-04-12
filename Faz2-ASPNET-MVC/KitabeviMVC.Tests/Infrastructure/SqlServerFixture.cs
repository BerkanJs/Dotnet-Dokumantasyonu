using Testcontainers.MsSql;

namespace KitabeviMVC.Tests.Infrastructure;

// ─────────────────────────────────────────────────────────────────────────────
// SqlServerFixture — TestContainers ile gerçek SQL Server container yönetimi.
//
// Gün 42: TestContainers — InMemory'nin yetersiz kaldığı durumlar için.
//   InMemory: UNIQUE constraint, RowVersion concurrency, stored procedure yok.
//   Gerçek SQL Server: tüm bu özellikler tam çalışır.
//
// IAsyncLifetime: xUnit'in async setup/teardown interface'i.
//   InitializeAsync: test sınıfı çalışmadan önce container başlatılır.
//   DisposeAsync: tüm testler bittikten sonra container durdurulup silinir.
//
// Kullanım:
//   IClassFixture<SqlServerFixture>  → her test sınıfı için ayrı container
//   ICollectionFixture<SqlServerFixture> → birden fazla sınıf tek container paylaşır
// ─────────────────────────────────────────────────────────────────────────────
public class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        // WithImage: hangi SQL Server Docker image kullanılacak.
        // 2022-latest: SQL Server 2022 — JSON column, ledger tables gibi yeni özellikler.
        // 2019-latest: daha eski ama lighter (CI'da tercih edilebilir).
        .WithPassword("TestSifre123!")
        // WithPassword: SQL Server SA şifresi — güçlü olmalı (büyük+küçük+rakam+özel).
        // Test ortamı dışında KESİNLİKLE kullanılmamalı.
        // Bunu appsettings.Testing.json'a taşımak: secret yönetimi için daha iyi.
        .Build();
        // Build(): MsSqlContainer instance oluşturur — henüz Docker çalıştırmıyor.
        // StartAsync() gerçek başlatmayı yapar.

    /// <summary>
    /// Gerçek SQL Server'a bağlanmak için connection string.
    /// Format: "Server=localhost,PORT;Database=master;User=sa;Password=..."
    /// PORT: Docker'ın host'a atadığı rastgele port — her çalışmada farklı.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();
    // GetConnectionString(): container başlatıldıktan sonra çağrılabilir.
    // InitializeAsync öncesi çağrılırsa: port henüz atanmadığından hata olabilir.

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        // StartAsync: Docker container başlatır.
        // İlk çalışmada: image pull (~500MB) + container start = ~30-60 saniye.
        // Sonraki çalışmalar: image cache'te → sadece container start = ~5-10 saniye.
        // Docker Desktop çalışmıyorsa: DockerClientException fırlatır.
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        // DisposeAsync: container durdurur ve siler.
        // Container kalmaması: disk + port temizlenir.
        // Bunu çağırmazsak: container çalışmaya devam eder (zombie container).
    }
}
