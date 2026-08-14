using Microsoft.AspNetCore.Mvc;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

[ApiController]
[Route("api/sales")]
public class SalesController : ControllerBase
{
    private readonly SaleService _saleService;

    public SalesController(SaleService saleService)
    {
        _saleService = saleService;
    }

    /// <summary>
    /// Kassir gapirgan (yoki yozgan) matnni qabul qiladi, Gemini orqali
    /// mahsulot/miqdor/summa/to'lov turini ajratib, chek sifatida saqlaydi.
    /// Masalan: "non 2 ta 6 ming, sut 1 litr 12 ming, jami 18 ming naqd"
    /// </summary>
    [HttpPost("voice")]
    [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFromVoice(
        [FromBody] CreateSaleFromTextRequest request, CancellationToken ct)
    {
        var (success, error, sale) = await _saleService.CreateFromTextAsync(request, ct);

        if (!success)
            return BadRequest(new { error });

        return Ok(sale);
    }
}
