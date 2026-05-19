using KitabeviMediatr.Application.UseCases.KitapEkle;
using KitabeviMediatr.Application.Validators;

namespace KitabeviMediatr.Tests.Application;

public class KitapEkleCommandValidatorTests
{
    private readonly KitapEkleCommandValidator _validator = new();

    [Fact]
    public void Validate_GecerliCommand_HataYok()
    {
        var cmd = new KitapEkleCommand("Clean Code", "9780132350884", 150, "TRY", 5);
        var sonuc = _validator.Validate(cmd);
        Assert.True(sonuc.IsValid);
    }

    [Theory]
    [InlineData("", "9780132350884", 150, "TRY", 5)]      // boş başlık
    [InlineData("Clean Code", "", 150, "TRY", 5)]          // boş ISBN
    [InlineData("Clean Code", "9780132350884", 0, "TRY", 5)]   // sıfır fiyat
    [InlineData("Clean Code", "9780132350884", 150, "TRY", -1)] // negatif stok
    [InlineData("Clean Code", "9780132350884", 150, "US", 5)]   // 2 harfli para birimi
    public void Validate_GecersizCommand_HataVar(
        string baslik, string isbn, decimal fiyat, string para, int stok)
    {
        var cmd = new KitapEkleCommand(baslik, isbn, fiyat, para, stok);
        var sonuc = _validator.Validate(cmd);
        Assert.False(sonuc.IsValid);
        //  ↑ her geçersiz kombinasyon en az bir hata vermeli
        //    Theory: aynı test 5 farklı girdiyle çalışır
        //    bunu yazmasaydık → her case için ayrı test metodu yazardık
    }
}
