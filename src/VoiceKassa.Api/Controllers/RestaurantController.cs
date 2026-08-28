using Microsoft.AspNetCore.Mvc;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

/// <summary>
/// Super Admin panelidagi "Restoranlar" bo'limi: yangi restoran+egasi
/// yaratish, ro'yxatini ko'rish, obuna holatini kuzatish.
///
/// Diqqat: hozircha bu endpoint'lar tokenni "X-Access-Token" header orqali
/// qabul qiladi va AuthService.ValidateSuperAdminTokenAsync bilan qo'lda
/// tekshiradi (MVP). Keyinchalik [Authorize] + JWT middleware'ga
/// almashtirilishi mumkin, controller kodini deyarli o'zgartirmasdan.
/// </summary>
[ApiController]
[Route("api/restaurants")]
public class RestaurantController : ControllerBase
{
    private readonly AuthService _authService;

    public RestaurantController(AuthService authService) => _authService = authService;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRestaurantRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdminAsync(ct)) return Unauthorized(new { error = "Super Admin sifatida kiring." });

        var (success, error, result) = await _authService.CreateRestaurantAsync(request, ct);
        return success ? Ok(result) : BadRequest(new { error });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (!await IsSuperAdminAsync(ct)) return Unauthorized(new { error = "Super Admin sifatida kiring." });

        var restaurants = await _authService.GetAllRestaurantsAsync(ct);
        return Ok(restaurants);
    }

    private async Task<bool> IsSuperAdminAsync(CancellationToken ct)
    {
        var token = Request.Headers["X-Access-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token)) return false;

        var admin = await _authService.ValidateSuperAdminTokenAsync(token, ct);
        return admin is not null;
    }
}
