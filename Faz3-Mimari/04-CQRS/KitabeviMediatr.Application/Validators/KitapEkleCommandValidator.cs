using FluentValidation;
using KitabeviMediatr.Application.UseCases.KitapEkle;

namespace KitabeviMediatr.Application.Validators;

public class KitapEkleCommandValidator : AbstractValidator<KitapEkleCommand>
{
    public KitapEkleCommandValidator()
    {
        RuleFor(x => x.Baslik)
            .NotEmpty().WithMessage("Başlık boş olamaz")
            .MaximumLength(200).WithMessage("Başlık 200 karakterden uzun olamaz");
        //  ↑ input validation — DB'ye gitmeden reddet
        //    bunu yazmasaydık → boş başlıklı kitap Domain'e ulaşırdı

        RuleFor(x => x.Isbn)
            .NotEmpty().WithMessage("ISBN boş olamaz")
            .Length(10, 17).WithMessage("ISBN 10 veya 13 haneli olmalı (tire dahil)");
        // ↑ format kontrolü — Isbn Value Object daha detaylı doğrular,
        //   ama 3 karakterlik saçmalığı DB'ye sokmadan reddet

        RuleFor(x => x.Fiyat)
            .GreaterThan(0).WithMessage("Fiyat sıfırdan büyük olmalı");
        // ↑ Fiyat Value Object da kontrol ediyor ama erken reddetmek daha iyi UX

        RuleFor(x => x.ParaBirimi)
            .NotEmpty()
            .Length(3).WithMessage("Para birimi 3 karakter olmalı (TRY, USD, EUR)");

        RuleFor(x => x.IlkStok)
            .GreaterThanOrEqualTo(0).WithMessage("Başlangıç stok negatif olamaz");
    }
}
