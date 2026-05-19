using FluentValidation;
using KitabeviMediatr.Application.UseCases.SiparisOlustur;

namespace KitabeviMediatr.Application.Validators;

public class SiparisOlusturCommandValidator : AbstractValidator<SiparisOlusturCommand>
{
    public SiparisOlusturCommandValidator()
    {
        RuleFor(x => x.KullaniciId)
            .NotEmpty().WithMessage("Kullanıcı Id boş olamaz");
        //  ↑ input validation — DB'ye gitmeden reddet

        RuleFor(x => x.KitapId)
            .GreaterThan(0).WithMessage("Geçerli bir kitap seçilmeli");

        RuleFor(x => x.Adet)
            .InclusiveBetween(1, 10).WithMessage("Adet 1 ile 10 arasında olmalı");
        // ↑ iş kuralı gibi görünse de bu "input validation" — domain kuralı değil
        //   Domain: stok < adet → hata (veri tabanına bakarak)
        //   Validation: adet <= 0 → girmeden önce reddet (DB'ye gitme)
        //   bunu Domain'e koysaydık → her çağrı DB'ye gitmek zorunda kalırdı
    }
}
