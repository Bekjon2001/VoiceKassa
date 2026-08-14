using Microsoft.AspNetCore.Mvc;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

[ApiController]
[Route("api/query")]
public class QueryController : ControllerBase
{
    private readonly QueryService _queryService;

    public QueryController(QueryService queryService)
    {
        _queryService = queryService;
    }

    /// <summary>
    /// Do'kon egasi tabiiy tilda savol beradi: "bugun qancha savdo bo'ldi?",
    /// "eng ko'p nima sotildi?" - javob faqat haqiqiy bazadagi ma'lumotlar
    /// asosida beriladi (AI raqam o'ylab topmaydi).
    /// </summary>
    [HttpPost("ask")]
    [ProducesResponseType(typeof(AskQuestionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Ask([FromBody] AskQuestionRequest request, CancellationToken ct)
    {
        var response = await _queryService.AskAsync(request, ct);
        return Ok(response);
    }

    /// <summary>
    /// Berilgan sana oralig'i uchun tayyor raqamli hisobot: jami, naqd,
    /// karta, onlayn va eng ko'p sotilgan mahsulotlar. Excel eksport
    /// uchun ham asos bo'ladi.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DailySummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] Guid shopId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var fromUtc = (fromDate ?? DateTime.UtcNow.Date).ToUniversalTime();
        var toUtc = (toDate ?? DateTime.UtcNow.Date.AddDays(1)).ToUniversalTime();

        var summary = await _queryService.GetSummaryAsync(shopId, fromUtc, toUtc, ct);
        return Ok(summary);
    }
}
