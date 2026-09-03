using Microsoft.AspNetCore.Mvc;
using VoiceKassa.AiServices;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

[Route("[controller]/[action]")]
[ApiController]
public class QueryController : ControllerBase
{
    private readonly QueryService _queryService;
    private readonly BusinessService _businessService;
    private readonly EdgeTtsService _ttsService;

    public QueryController(QueryService queryService, BusinessService businessService, EdgeTtsService ttsService)
    {
        _queryService = queryService;
        _businessService = businessService;
        _ttsService = ttsService;
    }

    /// <summary>
    /// Biznes egasi tabiiy tilda savol beradi: "bugun qancha savdo bo'ldi?",
    /// "eng ko'p nima sotildi?" - javob faqat haqiqiy bazadagi ma'lumotlar
    /// asosida beriladi (AI raqam o'ylab topmaydi).
    /// </summary>
    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AskQuestionRequest request, CancellationToken ct)
    {
        var response = await _queryService.AskAsync(request, ct);
        return Ok(response);
    }

    /// <summary>
    /// Super Admin platforma darajasidagi savol beradi: "qancha restoran bor?",
    /// "muddati tugagan obunalar nechta?" - javob faqat haqiqiy bazadagi
    /// barcha bizneslar/obunalar asosida beriladi.
    /// </summary>
    [HttpPost("ask-super")]
    public async Task<IActionResult> AskSuperAdmin([FromBody] AskSuperAdminRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
            return Unauthorized(new { error = "Super Admin sifatida kiring." });

        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { error = "Savol bo'sh bo'lishi mumkin emas." });

        var response = await _queryService.AskSuperAdminAsync(request.Question.Trim(), ct);
        return Ok(response);
    }

    /// <summary>
    /// Super Admin AI yordamchi javobini o'zbekcha tabiiy ovoz (TTS) bilan o'qish.
    /// Microsoft Edge read-aloud (Bing) nervli ovozi ishlatiladi — bepul, kalitsiz:
    ///   uz-UZ-MadinaNeural (ayol), uz-UZ-SardorNeural (erkak).
    /// Brauzerning standart speechSynthesis ovozi bilan solishtirganda ancha sifatli.
    /// </summary>
    [HttpGet("speak-super")]
    public async Task<IActionResult> SpeakSuper([FromQuery] string text, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
            return Unauthorized(new { error = "Super Admin sifatida kiring." });

        if (string.IsNullOrWhiteSpace(text))
            return BadRequest(new { error = "Matn bo'sh bo'lishi mumkin emas." });

        try
        {
            var bytes = await _ttsService.SynthesizeAsync(text.Trim(), ct: ct);
            return File(bytes, "audio/mpeg");
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, new { error = "Ovozlash vaqti tugadi." });
        }
        catch (Exception)
        {
            return StatusCode(502, new { error = "Ovoz xizmati hozircha ishlamayapti." });
        }
    }

    /// <summary>Berilgan sana oralig'i uchun tayyor raqamli hisobot.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] long businessId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var fromUtc = (fromDate ?? DateTime.UtcNow.Date).ToUniversalTime();
        var toUtc = (toDate ?? DateTime.UtcNow.Date.AddDays(1)).ToUniversalTime();

        var summary = await _queryService.GetSummaryAsync(businessId, fromUtc, toUtc, ct);
        return Ok(summary);
    }

    private Task<bool> IsSuperAdmin(CancellationToken ct) =>
        _businessService.IsSuperAdminTokenAsync(Request.Headers["X-Super-Admin-Token"].FirstOrDefault(), ct);
}
