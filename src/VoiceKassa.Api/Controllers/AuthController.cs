using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VoiceKassa.Api;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

[ApiController]
[AllowAnonymous]
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
        if (!SetupKeyValidator.IsAuthorized(
                _setupKeys.FirstSuperAdminKey,
                Request.Headers["X-Setup-Key"].FirstOrDefault(),
                _environment.IsDevelopment()))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Birinchi Super Adminni yaratish uchun X-Setup-Key sozlanishi kerak." });

        var (success, error, result) = await _authService.CreateFirstSuperAdminAsync(request, ct);
        return success ? Ok(result) : BadRequest(new { error });
    }

    [HttpPost("super-admin/login")]
    public async Task<IActionResult> SuperAdminLogin([FromBody] LoginRequest request, CancellationToken ct)
    {
        var ip = ClientIp;
        if (!_authLimiter.TryEnter(ip))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "Juda ko'p urinish. Birozdan keyin qayta urinib ko'ring." });

        var (success, error, result) = await _authService.SuperAdminLoginAsync(request, ct);
        if (success) _authLimiter.RegisterSuccess(ip);
        else _authLimiter.RegisterFailure(ip);
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
        if (success) _authLimiter.RegisterSuccess(ip);
        else _authLimiter.RegisterFailure(ip);
        return success ? Ok(result) : BadRequest(new { error });
    }
}
