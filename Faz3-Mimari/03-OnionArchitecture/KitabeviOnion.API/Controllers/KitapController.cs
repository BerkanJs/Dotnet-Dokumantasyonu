using KitabeviOnion.Application.UseCases.KitapListele;
using KitabeviOnion.Application.UseCases.KitapSil;
using KitabeviOnion.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace KitabeviOnion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KitapController : ControllerBase
{
    private readonly KitapListeleHandler _listeleHandler;
    //               ↑ Handler inject edildi — Controller DB'yi, EF Core'u bilmiyor
    //                 bunu yazmasaydık → Controller DbContext görürdü, katman ayrımı bozulurdu
    private readonly KitapSilHandler _silHandler;

    public KitapController(KitapListeleHandler listeleHandler, KitapSilHandler silHandler)
    {
        _listeleHandler = listeleHandler;
        _silHandler = silHandler;
    }

    [HttpGet]
    public async Task<IActionResult> Listele(CancellationToken ct)
    {
        var sonuc = await _listeleHandler.Handle(new KitapListeleQuery(), ct);
        //                                        ↑ query nesnesi oluştur — handler'a teslim et
        return Ok(sonuc);
        //         ↑ DTO listesi — domain entity değil, API kontratı korunuyor
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Sil(int id, CancellationToken ct)
    {
        try
        {
            await _silHandler.Handle(new KitapSilCommand(id), ct);
            //                        ↑ command nesnesi oluştur — handler'a teslim et
            return NoContent();
            //      ↑ 204: silme başarılı, döndürülecek içerik yok
        }
        catch (DomainException ex)
        {
            return NotFound(new { hata = ex.Message });
            //      ↑ kitap bulunamadı → 404
            //        bunu yazmasaydık → DomainException 500 dönerdi
        }
    }
}
