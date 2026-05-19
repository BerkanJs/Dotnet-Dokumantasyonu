using KitabeviMediatr.Application.UseCases.SiparisOlustur;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace KitabeviMediatr.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SiparisController : ControllerBase
{
    private readonly IMediator _mediator;
    //               ↑ tek bağımlılık — Gün59'da SiparisOlusturHandler vardı

    public SiparisController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Olustur(
        [FromBody] SiparisOlusturCommand cmd,
        //                               ↑ Gün59'da ayrı SiparisOlusturRequest DTO vardı
        //                                 Gün60'da: Command zaten IRequest — doğrudan bind edilebilir
        CancellationToken ct)
    {
        // ValidationBehavior zaten çalıştı — geldiyse geçerli demek
        var sonuc = await _mediator.Send(cmd, ct);
        //                          ↑ MediatR: SiparisOlusturCommand → SiparisOlusturHandler
        return CreatedAtAction(nameof(Olustur), new { id = sonuc.SiparisId }, sonuc);

        // try/catch yok — GlobalExceptionHandler halleder
        // Gün59'da: catch (DomainException ex) { return BadRequest(...) }
        // Gün60'da: exception middleware → DomainException → 400, ValidationException → 422
    }
}
