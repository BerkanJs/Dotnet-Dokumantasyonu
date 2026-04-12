using System.Reflection;

namespace KitabeviMVC.Tests.Architecture;

// ─────────────────────────────────────────────────────────────────────────────
// ArchitectureTestBase — Tüm architecture test sınıflarının base class'ı
//
// Neden base class?
//   Assembly referansı tek yerde tanımlanır.
//   Yeni test class'ı bu sınıfı miras alır → assembly'ye her yerden erişilir.
//   Assembly değişirse (proje yeniden adlandırılırsa) tek yer güncellenir.
// ─────────────────────────────────────────────────────────────────────────────
public abstract class ArchitectureTestBase
{
    /// <summary>
    /// Test edilen assembly — KitabeviMVC ana projesi.
    /// typeof(Program): Program sınıfı ana projede tanımlı → doğru assembly'i döndürür.
    /// typeof(Kitap) de kullanılabilirdi; Program daha açıklayıcı (entry point).
    /// </summary>
    protected static readonly Assembly Assembly = typeof(Program).Assembly;
    // static: instance oluşturmadan erişilebilir — her test için yeniden hesaplanmaz.
    // readonly: bir kez atanır, sonradan değiştirilemez.
    // protected: bu sınıfı miras alan test sınıfları erişebilir, dış dünya göremez.
}
