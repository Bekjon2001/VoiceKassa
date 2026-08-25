using Microsoft.AspNetCore.Mvc;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

<<<<<<< HEAD
[ApiController]
[Route("api/query")]
=======
[Route("[controller]/[action]")]
[ApiController]
>>>>>>> main
public class QueryController : ControllerBase
{
    private readonly QueryService _queryService;

<<<<<<< HEAD
    public QueryController(QueryService queryService)
    {
        _queryService = queryService;
    }

    /// <summary>
    /// Do'kon egasi tabiiy tilda savol beradi: "bugun qancha savdo bo'ldi?",
=======
    public QueryController(QueryService queryService) => _queryService = queryService;

    /// <summary>
    /// Biznes egasi tabiiy tilda savol beradi: "bugun qancha savdo bo'ldi?",
>>>>>>> main
    /// "eng ko'p nima sotildi?" - javob faqat haqiqiy bazadagi ma'lumotlar
    /// asosida beriladi (AI raqam o'ylab topmaydi).
    /// </summary>
    [HttpPost("ask")]
<<<<<<< HEAD
    [ProducesResponseType(typeof(AskQuestionResponse), StatusCodes.Status200OK)]
=======
>>>>>>> main
    public async Task<IActionResult> Ask([FromBody] AskQuestionRequest request, CancellationToken ct)
    {
        var response = await _queryService.AskAsync(request, ct);
        return Ok(response);
    }

<<<<<<< HEAD
    /// <summary>
    /// Berilgan sana oralig'i uchun tayyor raqamli hisobot: jami, naqd,
    /// karta, onlayn va eng ko'p sotilgan mahsulotlar. Excel eksport
    /// uchun ham asos bo'ladi.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DailySummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] Guid shopId,
=======
    /// <summary>Berilgan sana oralig'i uchun tayyor raqamli hisobot.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] long businessId,
>>>>>>> main
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var fromUtc = (fromDate ?? DateTime.UtcNow.Date).ToUniversalTime();
        var toUtc = (toDate ?? DateTime.UtcNow.Date.AddDays(1)).ToUniversalTime();

<<<<<<< HEAD
        var summary = await _queryService.GetSummaryAsync(shopId, fromUtc, toUtc, ct);
=======
        var summary = await _queryService.GetSummaryAsync(businessId, fromUtc, toUtc, ct);
>>>>>>> main
        return Ok(summary);
    }
}
