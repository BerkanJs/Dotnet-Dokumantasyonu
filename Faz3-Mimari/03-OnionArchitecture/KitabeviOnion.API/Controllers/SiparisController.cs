using KitabeviOnion.Application.UseCases.SiparisOlustur;
using KitabeviOnion.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace KitabeviOnion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SiparisController : ControllerBase
{
    private readonly SiparisOlusturHandler _siparisHandler;

    public SiparisController(SiparisOlusturHandler siparisHandler)
    {
        _siparisHandler = siparisHandler;
    }

    [HttpPost]
    public async Task<IActionResult> Olustur(
        [FromBody] SiparisOlusturRequest request,
        CancellationToken ct)
    {
        try
        {
            var cmd = new SiparisOlusturCommand(
                KullaniciId: request.KullaniciId,
                KitapId: request.KitapId,
                Adet: request.Adet);

            var sonuc = await _siparisHandler.Handle(cmd, ct);
            //                                ↑ handler tüm iş mantığını yönetiyor
            //                                  Controller sadece HTTP → Command dönüşümü yapıyor

            return CreatedAtAction(nameof(Olustur), new { id = sonuc.SiparisId }, sonuc);
            //     ↑ 201 Created + Location header: yeni kaynağın URL'i
        }
        catch (DomainException ex)
        {
            return BadRequest(new { hata = ex.Message });
            //      ↑ Domain hatası → 400 Bad Request
            //        bunu yazmasaydık → DomainException 500 Internal Server Error dönerdi
        }
    }
}

public record SiparisOlusturRequest(string KullaniciId, int KitapId, int Adet);
// ↑ API request DTO — Command'den ayrı
//   bunu yazmasaydık → Command'i doğrudan FromBody alsaydık, API kontratı Application'a sızardı
