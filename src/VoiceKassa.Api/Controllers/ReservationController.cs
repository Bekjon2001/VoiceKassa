using Microsoft.AspNetCore.Mvc;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

/// <summary>
/// Bron tizimi. Egasi uchun: yaratish/royxat/bekor qilish/kodni qayta berish
/// (X-Owner-Token). Mijoz uchun: kod bilan kirish (enter) — ochiq, lekin
/// urinishlar soni cheklangan. Joyida bron (walkin) — egasi/stol uchun.
/// </summary>
[Route("[controller]/[action]")]
[ApiController]
public class ReservationController : ControllerBase
{
    private readonly ReservationService _reservationService;
    private readonly BusinessService _businessService;
    private readonly ReservationEnterLimiter _enterLimiter;

    public ReservationController(
        ReservationService reservationService,
        BusinessService businessService,
        ReservationEnterLimiter enterLimiter)
    {
        _reservationService = reservationService;
        _businessService = businessService;
        _enterLimiter = enterLimiter;
    }

    /// <summary>Yangi bron yaratish (X-Owner-Token).</summary>
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateReservationRequest request, CancellationToken ct)
    {
        var access = await _businessService.AuthorizeOwnerAsync(request.BusinessId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        if (!access.Success) return Unauthorized(new { error = access.Error ?? "Ruxsat yo'q." });

        var (success, error, reservation) = await _reservationService.CreateAsync(request, ct);
        return success ? Ok(reservation) : BadRequest(new { error });
    }

    /// <summary>Bronlar ro'yxati (X-Owner-Token). ?bookingDate=yyyy-MM-dd ixtiyoriy filtr.</summary>
    [HttpGet("list/{businessId:long}")]
    public async Task<IActionResult> GetList(long businessId, [FromQuery] DateTime? bookingDate, CancellationToken ct)
    {
        var access = await _businessService.AuthorizeOwnerAsync(businessId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        if (!access.Success) return Unauthorized(new { error = access.Error ?? "Ruxsat yo'q." });

        var list = await _reservationService.GetListAsync(businessId, bookingDate, ct);
        return Ok(list);
    }

    /// <summary>
    /// Kod bilan kirish (enter) — ochiq endpoint, lekin urinishlar soni
    /// cheklangan (har IP uchun 10 daqiqada 5 urinish). Muvaffaqiyatdan
    /// keyin SESSION_TOKEN qaytariladi — u X-Table-Token sifatida ishlatiladi.
    /// </summary>
    [HttpPost("enter")]
    public async Task<IActionResult> Enter([FromBody] EnterReservationRequest request, CancellationToken ct)
    {
        var clientKey = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!_enterLimiter.TryEnter(clientKey))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "Juda ko'p urinish. Birozdan keyin qayta urinib ko'ring." });

        var (success, error, result) = await _reservationService.EnterByCodeAsync(request, ct);
        if (success)
        {
            _enterLimiter.Reset(clientKey);
            return Ok(result);
        }
        return BadRequest(new { error });
    }

    /// <summary>Joyida bron (walkin) — mijoz hozir stolga o'tirdi (X-Owner-Token).</summary>
    [HttpPost("walkin")]
    public async Task<IActionResult> WalkIn([FromBody] WalkInReservationRequest request, CancellationToken ct)
    {
        var access = await _businessService.AuthorizeOwnerAsync(request.BusinessId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        if (!access.Success) return Unauthorized(new { error = access.Error ?? "Ruxsat yo'q." });

        var (success, error, result) = await _reservationService.WalkInAsync(request, ct);
        return success ? Ok(result) : BadRequest(new { error });
    }

    /// <summary>Bronni bekor qilish (X-Owner-Token). Sessiya tokeni o'chiriladi.</summary>
    [HttpPost("cancel/{reservationId:long}")]
    public async Task<IActionResult> Cancel(long reservationId, [FromQuery] long businessId, CancellationToken ct)
    {
        var access = await _businessService.AuthorizeOwnerAsync(businessId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        if (!access.Success) return Unauthorized(new { error = access.Error ?? "Ruxsat yo'q." });

        var (success, error, reservation) = await _reservationService.CancelAsync(reservationId, ct);
        return success ? Ok(reservation) : BadRequest(new { error });
    }

    /// <summary>Bron kodini qayta berish (yangi yagona kod) — X-Owner-Token.</summary>
    [HttpPost("regenerate-code/{reservationId:long}")]
    public async Task<IActionResult> RegenerateCode(long reservationId, [FromQuery] long businessId, CancellationToken ct)
    {
        var access = await _businessService.AuthorizeOwnerAsync(businessId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        if (!access.Success) return Unauthorized(new { error = access.Error ?? "Ruxsat yo'q." });

        var (success, error, reservation) = await _reservationService.RegenerateCodeAsync(reservationId, ct);
        return success ? Ok(reservation) : BadRequest(new { error });
    }

    /// <summary>
    /// Mijoz uchun: berilgan sanada bronda bo'lmagan va sig'imi yetishadigan stollar.
    /// (Token shart emas — QR/skaner yoki menyudan stol tanlash uchun.)
    /// </summary>
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable(
        [FromQuery] long businessId, [FromQuery] DateTime date, [FromQuery] int guests = 1, CancellationToken ct = default)
    {
        var list = await _reservationService.GetAvailableTablesAsync(businessId, date, guests, ct);
        return Ok(list);
    }
}