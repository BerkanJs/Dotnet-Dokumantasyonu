namespace KitabeviMVC.Tests.Infrastructure;

// ─────────────────────────────────────────────────────────────────────────────
// SqlServerCollection — Container Paylaşımı
//
// Gün 42: CollectionFixture ile SqlServerFixture'ı birden fazla test sınıfına paylaştır.
//
// Neden paylaşım?
//   Container başlatma maliyetli (~10 saniye).
//   Her test sınıfı için ayrı container: 5 sınıf → 5 × 10s = 50 saniye overhead.
//   Tek container tüm sınıflara: 1 × 10s = 10 saniye.
//
// IClassFixture vs ICollectionFixture:
//   IClassFixture<T>: fixture bir test sınıfı ömrünce yaşar (bir sınıfla paylaşım).
//   ICollectionFixture<T>: fixture aynı collection adındaki TÜM sınıflarla paylaşılır.
//
// Kullanım: test sınıfına [Collection("SqlServer")] ekle.
// ─────────────────────────────────────────────────────────────────────────────

[CollectionDefinition("SqlServer")]
// CollectionDefinition: "SqlServer" adıyla bir collection tanımlar.
// [Collection("SqlServer")] kullanan tüm test sınıfları bu fixture'ı paylaşır.
// Bunu yazmadan [Collection] kullansak: xUnit fixture'ı bulamaz → runtime hata.
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
// ICollectionFixture<T>: SqlServerFixture'ı collection ömrünce manage et.
// Body boş: sadece marker interface — xUnit bu sınıfı bulup fixture'ı register eder.
{
    // Body yok — yalnızca koleksiyon kaydı için bu sınıf gerekli.
    // Gerçek fixture mantığı SqlServerFixture.cs'te.
}
