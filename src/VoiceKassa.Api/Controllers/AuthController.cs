using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VoiceKassa.Api;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly AuthLimiter _authLimiter;
    private readonly SetupKeyOptions _setupKeys;
    private readonly IWebHostEnvironment _environment;

    public AuthController(
        AuthService authService,
        AuthLimiter authLimiter,
        IOptions<SetupKeyOptions> setupKeys,
        IWebHostEnvironment environment)
    {
        _authService = authService;
        _authLimiter = authLimiter;
        _setupKeys = setupKeys.Value;
        _environment = environment;
    }

    private string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>Frontend shu orqali "login" yoki "birinchi admin yaratish" ekranini tanlaydi.</summary>
    [HttpGet("super-admin/exists")]
    public async Task<IActionResult> SuperAdminExists(CancellationToken ct)
    {
        var result = await _authService.CheckSuperAdminExistsAsync(ct);
        return Ok(result);
    }

    [HttpPost("super-admin/create-first")]
    public async Task<IActionResult> CreateFirstSuperAdmin([FromBody] CreateSuperAdminRequest request, CancellationToken ct)
    {
        // Faqat sozlamadagi Setup:FirstSuperAdminKey bilan bo‘lishi mumkin
        if (!string.IsNullOrEmpty(_setupKeys.FirstSuperAdminKey) &&
            request.Login == _setupKeys.FirstSuperAdminKey)
        {
            // Kalit to‘g‘ri bo‘lsa — oddiy yaratish folyati
            var (success, error, result) = await _authService.CreateFirstSuperAdminAsync(request, ct);
            return success ? Ok(result) : BadRequest(new { error });
        }
        // Key emas yoki noto‘g‘ri — faqat Development muhitida ochiq
        if (_environment.IsDevelopment())
        {
            var (success, error, result) = await _authService.CreateFirstSuperAdminAsync(request, ct);
            return success ? Ok(result) : BadRequest(new { error });
        }
        return StatusCode(StatusCodes.Status403Forbidden,
            new { error = "Ikkinchi urinish cheklangan. Sozlamada Setup:FirstSuperAdminKey kiritilishi shart." });
    }

    [HttpPost("super-admin/login")]
    public async Task<IActionResult> SuperAdminLogin([FromBody] LoginRequest request, CancellationToken ct)
    {
        var ip = ClientIp;
        if (!_authLimiter.TryEnter(ip))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "Juda ko'p urinish. Birozdan keyin qayta urinib ko'ring." });

        var (success, error, result) = await _authService.SuperAdminLoginAsync(request, ct);
        return success ? Ok(result) : BadRequest(new { error });
    }

    // ---------- Restoran egasi ----------

    [HttpPost("owner/login")]
    public async Task<IActionResult> OwnerLogin([FromBody] LoginRequest request, CancellationToken ct)
    {
        var ip = ClientIp;
        if (!_authLimiter.TryEnter(ip))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "Juda ko'p urinish. Birozdan keyin qayta urinib ko'ring." });

        var (success, error, result) = await _authService.OwnerLoginAsync(request, ct);
        return success ? Ok(result) : BadRequest(new { error });
    }
}
