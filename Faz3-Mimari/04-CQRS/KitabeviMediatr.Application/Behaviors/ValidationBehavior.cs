using FluentValidation;
using MediatR;

namespace KitabeviMediatr.Application.Behaviors;

public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    //               ↑ FluentValidation validator'ları inject — birden fazla olabilir
    //                 bunu yazmasaydık → ValidationBehavior IValidator inject edemez, boş liste alırdı

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!_validators.Any())
            return await next();
        //                ↑ bu request için validator yoksa direkt geç
        //                  bunu yazmasaydık → boş validator listesinde gereksiz context oluşturulurdu

        var context = new ValidationContext<TRequest>(request);

        var hatalar = _validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(e => e is not null)
            .ToList();

        if (hatalar.Any())
            throw new ValidationException(hatalar);
        //  ↑ handler'a ulaşmadan validation hatası fırlar
        //    GlobalExceptionHandler bu exception'ı 422 olarak yakalar
        //    bunu yazmasaydık → her handler başında if-if-if yazardık

        return await next();
    }
}
