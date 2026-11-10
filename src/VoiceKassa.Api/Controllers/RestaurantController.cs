using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

/// <summary>
/// Super Admin panelidagi "Restoranlar" bo'limi: yangi restoran+egasi
/// yaratish, ro'yxatini ko'rish, obuna holatini kuzatish.
///
/// JWT orqali Super Admin rolini tekshiradi; JWT sessiyasining faol holati
/// middleware tomonidan bazadagi sessiya ID bilan solishtiriladi.
/// </summary>
[ApiController]
[Authorize(Roles = "super-admin")]
[Route("api/restaurants")]
public class RestaurantController : ApiControllerBase
{
    private readonly AuthService _authService;

    public RestaurantController(AuthService authService) => _authService = authService;

    [HttpPost]
    [SuperAdminReadOnly]
    public async Task<IActionResult> Create([FromBody] CreateRestaurantRequest request, CancellationToken ct)
    {
        var (success, error, result) = await _authService.CreateRestaurantAsync(request, ct);
        return success ? Ok(result) : BadRequest(new { error });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var restaurants = await _authService.GetAllRestaurantsAsync(ct);
        return Ok(restaurants);
    }
}
