using KitabeviMediatr.Application.UseCases.KitapEkle;
using KitabeviMediatr.Application.UseCases.KitapListele;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace KitabeviMediatr.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KitapController : ControllerBase
{
    private readonly IMediator _mediator;
    //               ↑ tek bağımlılık — yeni handler eklemek controller'ı değiştirmiyor

    public KitapController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Listele(
        [FromQuery] decimal minFiyat = 0,
        //           ↑ query string: GET /api/kitap?minFiyat=50&maxFiyat=200&sadeceStoktakiler=true
        //             bunu yazmasaydık → filtreler JSON body ile gelirdi, GET isteğinde anormal
        [FromQuery] decimal maxFiyat = decimal.MaxValue,
        [FromQuery] bool sadeceStoktakiler = false,
        CancellationToken ct = default)
        => Ok(await _mediator.Send(
            new KitapListeleQuery(minFiyat, maxFiyat, sadeceStoktakiler), ct));
    //                         ↑ filtreler query'ye iletiliyor — handler specification oluşturacak

    [HttpPost]
    public async Task<IActionResult> Ekle(
        [FromBody] KitapEkleRequest request,
        CancellationToken ct)
    {
        var cmd = new KitapEkleCommand(
            Baslik: request.Baslik,
            Isbn: request.Isbn,
            Fiyat: request.Fiyat,
            ParaBirimi: request.ParaBirimi,
            IlkStok: request.IlkStok);
        //  ↑ API request → Application command dönüşümü
        //    bunu yazmasaydık → Command doğrudan [FromBody] alırdı,
        //    API sözleşmesi Application'a sızardı

        var sonuc = await _mediator.Send(cmd, ct);
        //                           ↑ Pipeline: Logging → Validation → Handler

        return CreatedAtAction(nameof(Listele), new { id = sonuc.KitapId }, sonuc);
        // ↑ 201 Created — GlobalExceptionHandler hataları yakalar, try/catch yok
    }
}

public record KitapEkleRequest(
    string Baslik,
    string Isbn,
    decimal Fiyat,
    string ParaBirimi,
    int IlkStok
);
// ↑ API DTO — Command'den ayrı
//   Gelecekte API'ye "YayinYili" eklenebilir, Command değişmeyebilir
//   ya da Command'e "EkleyenKullaniciId" header'dan eklenebilir
