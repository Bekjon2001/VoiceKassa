using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

[Route("[controller]/[action]")]
[ApiController]
[Authorize]
public class BusinessController : ApiControllerBase
{
    private readonly BusinessService _businessService;
    private readonly AuthService _authService;
    private readonly AuthLimiter _authLimiter;
    private readonly SetupKeyOptions _setupKeys;
    private readonly IWebHostEnvironment _environment;

    public BusinessController(
        BusinessService businessService,
        AuthService authService,
        AuthLimiter authLimiter,
        IOptions<SetupKeyOptions> setupKeys,
        IWebHostEnvironment environment)
    {
        _businessService = businessService;
        _authService = authService;
        _authLimiter = authLimiter;
        _setupKeys = setupKeys.Value;
        _environment = environment;
    }

    private string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    [HttpPost("super-admin/first")]
    [AllowAnonymous]
    [SuperAdminReadOnly]
    public async Task<IActionResult> CreateFirstSuperAdmin([FromBody] CreateSuperAdminRequest request, CancellationToken ct)
    {
        if (!SetupKeyValidator.IsAuthorized(
                _setupKeys.FirstSuperAdminKey,
                Request.Headers["X-Setup-Key"].FirstOrDefault(),
                _environment.IsDevelopment()))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Birinchi Super Adminni yaratish uchun X-Setup-Key sozlanishi kerak." });

        var (success, error, account) = await _authService.CreateFirstSuperAdminAsync(request, ct);
        return success ? Ok(account) : BadRequest(new { error });
    }

    [HttpPost("super-admin/login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginSuperAdmin([FromBody] LoginRequest request, CancellationToken ct)
    {
        var ip = ClientIp;
        if (!_authLimiter.TryEnter(ip))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "Juda ko'p urinish. Birozdan keyin qayta urinib ko'ring." });

        var (success, error, account) = await _authService.SuperAdminLoginAsync(request, ct);
        if (success) _authLimiter.RegisterSuccess(ip);
        else _authLimiter.RegisterFailure(ip);
        return success ? Ok(account) : Unauthorized(new { error });
    }

    [HttpGet("super-admin/session")]
    public async Task<IActionResult> CheckSuperAdmin(CancellationToken ct)
    {
        var token = Request.Headers["X-Super-Admin-Token"].FirstOrDefault();
        return await _businessService.IsSuperAdminTokenAsync(token, ct) ? Ok() : Unauthorized();
    }

    [HttpPost("restaurant")]
    [SuperAdminReadOnly]
    public async Task<IActionResult> CreateRestaurant([FromBody] CreateRestaurantWithOwnerRequest request, CancellationToken ct)
    {
        if (await _businessService.HasSuperAdminAsync(ct) && !await IsSuperAdmin(ct)) return Unauthorized();
        var (success, error, owner) = await _businessService.CreateRestaurantWithOwnerAsync(request, ct);
        return success ? Ok(owner) : BadRequest(new { error });
    }

    /// <summary>Supermarket + egasini yaratadi (Super Admin). Backend bitta umumiy oqimdan
    /// foydalanadi — farq faqat Business turida (Market).</summary>
    [HttpPost("market")]
    [SuperAdminReadOnly]
    public async Task<IActionResult> CreateMarket([FromBody] CreateMarketWithOwnerRequest request, CancellationToken ct)
    {
        if (await _businessService.HasSuperAdminAsync(ct) && !await IsSuperAdmin(ct)) return Unauthorized();
        var (success, error, owner) = await _businessService.CreateMarketWithOwnerAsync(request, ct);
        return success ? Ok(owner) : BadRequest(new { error });
    }

    [HttpPost("owner/login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginOwner([FromBody] OwnerLoginRequest request, CancellationToken ct)
    {
        var ip = ClientIp;
        if (!_authLimiter.TryEnter(ip))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "Juda ko'p urinish. Birozdan keyin qayta urinib ko'ring." });

        var (isSuperAdmin, _, superAdmin) = await _authService.SuperAdminLoginAsync(
            new LoginRequest { Login = request.Login, Password = request.Password }, ct);
        if (isSuperAdmin)
        {
            _authLimiter.RegisterSuccess(ip);
            return Ok(superAdmin);
        }

        var (success, error, owner) = await _businessService.LoginOwnerAsync(request, ct);
        if (success) _authLimiter.RegisterSuccess(ip);
        else _authLimiter.RegisterFailure(ip);
        return success ? Ok(owner) : Unauthorized(new { error });
    }

    [HttpPost("staff/login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginStaff([FromBody] LoginRequest request, CancellationToken ct)
    {
        var ip = ClientIp;
        if (!_authLimiter.TryEnter(ip))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "Juda ko'p urinish. Birozdan keyin qayta urinib ko'ring." });

        var (success, error, staff) = await _businessService.LoginStaffAsync(request, ct);
        if (success) _authLimiter.RegisterSuccess(ip);
        else _authLimiter.RegisterFailure(ip);
        return success ? Ok(staff) : Unauthorized(new { error });
    }

    /// <summary>Yangi biznes ro'yxatdan o'tkazish (restoran, do'kon, market, ombor). Faqat Super Admin.</summary>
    [HttpPost]
    [SuperAdminReadOnly]
    public async Task<IActionResult> Create([FromBody] CreateBusinessRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct)) return Unauthorized(new { error = "Super Admin sifatida kiring." });
        var business = await _businessService.CreateBusinessAsync(request, ct);
        return Ok(business);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!await IsSuperAdmin(ct)) return Unauthorized();
        var businesses = await _businessService.GetAllBusinessesAsync(ct);
        businesses = businesses.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(businesses);
    }

    [HttpGet("{businessId:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(long businessId, CancellationToken ct)
    {
        var accessError = EnsureBusinessReadAccess(businessId);
        if (accessError is not null) return accessError;
        var business = await _businessService.GetBusinessAsync(businessId, ct);
        if (business is null) return NotFound();
        return Ok(new { business.Id, business.Name, business.Type, business.Address, business.PhoneNumber, business.CreatedAt });
    }

    [HttpGet("{businessId:long}/owner")]
    public async Task<IActionResult> GetOwner(long businessId, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct)) return Unauthorized();
        var owner = await _businessService.GetOwnerAdminAsync(businessId, ct);
        return owner is null ? NotFound() : Ok(owner);
    }

    // ---- Staff ----

    [HttpPost("staff")]
    [SuperAdminReadOnly]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequest request, CancellationToken ct)
    {
        var ownerAccess = await _businessService.AuthorizeOwnerAsync(request.BusinessId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        if (!ownerAccess.Success) return Unauthorized(new { error = ownerAccess.Error });
        var (success, error, staff) = await _businessService.CreateStaffAsync(request, ct);
        return success ? Ok(staff) : BadRequest(new { error });
    }

    [HttpPut("staff/{staffId:long}/credentials")]
    [SuperAdminReadOnly]
    public async Task<IActionResult> UpdateStaffCredentials(
        long staffId, [FromBody] UpdateStaffCredentialsRequest request, CancellationToken ct)
    {
        var access = await _businessService.AuthorizeStaffAsync(
            staffId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        if (!access.Success) return Unauthorized(new { error = access.Error });
        var (success, error, staff) = await _businessService.UpdateStaffCredentialsAsync(staffId, request, ct);
        return success ? Ok(staff) : BadRequest(new { error });
    }

    [HttpGet("{businessId:long}/staff")]
    public async Task<IActionResult> GetStaff(long businessId, CancellationToken ct)
    {
        var accessError = EnsureBusinessReadAccess(businessId);
        if (accessError is not null) return accessError;
        // Owner o'z biznesining xodimlarini, Super Admin esa hammasini ko'ra oladi.
        if (!await IsSuperAdmin(ct))
        {
            var ownerAccess = await _businessService.AuthorizeOwnerAsync(businessId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
            if (!ownerAccess.Success) return Unauthorized(new { error = "Owner huquqi talab qilinadi." });
        }
        var staff = await _businessService.GetStaffAsync(businessId, ct);
        return Ok(staff);
    }

    /// <summary>Xodim oyligini yangilash (owner). Tarixga avtomatik yoziladi.</summary>
    [HttpPut("staff/{staffId:long}/salary")]
    [SuperAdminReadOnly]
    public async Task<IActionResult> UpdateStaffSalary(long staffId, [FromBody] UpdateStaffSalaryRequest request, CancellationToken ct)
    {
        var ownerAccess = await _businessService.AuthorizeStaffAsync(staffId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        if (!ownerAccess.Success) return Unauthorized(new { error = ownerAccess.Error });
        var (success, error, staff) = await _businessService.UpdateStaffSalaryAsync(staffId, request, ct);
        return success ? Ok(staff) : BadRequest(new { error });
    }

    /// <summary>Xodimni ishdan bo'shatish / faollashtirish (owner).</summary>
    [HttpPut("staff/{staffId:long}/active")]
    [SuperAdminReadOnly]
    public async Task<IActionResult> SetStaffActive(long staffId, [FromBody] UpdateStaffStatusRequest request, CancellationToken ct)
    {
        var ownerAccess = await _businessService.AuthorizeStaffAsync(staffId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        if (!ownerAccess.Success) return Unauthorized(new { error = ownerAccess.Error });
        var (success, error, staff) = await _businessService.SetStaffActiveAsync(staffId, request.IsActive, ct);
        return success ? Ok(staff) : BadRequest(new { error });
    }

    [HttpPut("staff/{staffId:long}/status")]
    [SuperAdminReadOnly]
    public async Task<IActionResult> UpdateStaffStatus(long staffId, [FromBody] UpdateStaffStatusRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct)) return Unauthorized();
        var (success, error) = await _businessService.UpdateStaffStatusAsync(staffId, request, ct);
        return success ? NoContent() : NotFound(new { error });
    }

    // ---- Category ----

    [HttpPost("categories")]
    [SuperAdminReadOnly]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        var access = await _businessService.AuthorizeOwnerAsync(request.BusinessId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        if (!access.Success) return Unauthorized(new { error = access.Error });
        var (success, error, category) = await _businessService.CreateCategoryAsync(request, ct);
        return success ? Ok(category) : BadRequest(new { error });
    }

    [HttpGet("{businessId:long}/categories")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories(long businessId, CancellationToken ct)
    {
        var accessError = EnsureBusinessReadAccess(businessId);
        if (accessError is not null) return accessError;
        var categories = await _businessService.GetCategoriesAsync(businessId, ct);
        return Ok(categories);
    }

    // ---- Table (restoran uchun) ----

    [HttpPost("tables")]
    [SuperAdminReadOnly]
    public async Task<IActionResult> CreateTable([FromBody] CreateTableRequest request, CancellationToken ct)
    {
        var access = await _businessService.AuthorizeOwnerAsync(request.BusinessId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        if (!access.Success) return Unauthorized(new { error = access.Error });
        var (success, error, table) = await _businessService.CreateTableAsync(request, ct);
        return success ? Ok(table) : BadRequest(new { error });
    }

    [HttpGet("{businessId:long}/tables")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTables(long businessId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var accessError = EnsureBusinessReadAccess(businessId, VoiceKassa.Domain.Enums.StaffRole.Waiter);
        if (accessError is not null) return accessError;
        var tables = await _businessService.GetTablesAsync(businessId, ct);
        tables = tables.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(tables);
    }

    /// <summary>
    /// Mijozlar uchun OCHIQ ro'yxat: faol restoran/marketlarni nom bo'yicha
    /// qidiradi. QR/skaner ishlamagan holatda menyudan restoranni tanlash
    /// uchun — Super Admin tokeni shart emas.
    /// </summary>
    [HttpGet("public/search")]
    [AllowAnonymous]
    public async Task<IActionResult> SearchPublicBusinesses([FromQuery] string? query, CancellationToken ct)
    {
        var businesses = await _businessService.SearchPublicBusinessesAsync(query, ct);
        return Ok(businesses);
    }

    [HttpPut("tables/{tableId:long}/status")]
    [SuperAdminReadOnly]
    public async Task<IActionResult> UpdateTableStatus(long tableId, [FromBody] UpdateTableStatusRequest request, CancellationToken ct)
    {
        var table = await _businessService.GetTableByIdAsync(tableId, ct);
        if (table is null) return NotFound(new { error = "Bunday stol topilmadi." });
        var access = await _businessService.AuthorizeBusinessStaffAsync(
            table.BusinessId,
            Request.Headers["X-Owner-Token"].FirstOrDefault(),
            ct,
            VoiceKassa.Domain.Enums.StaffRole.Waiter);
        if (!access.Success) return Unauthorized(new { error = access.Error });
        if (IsWaiterRole && (request.Status != VoiceKassa.Domain.Enums.TableStatus.Free ||
            table.Status != VoiceKassa.Domain.Enums.TableStatus.NeedsCleaning))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Ofitsiant faqat tozalash kerak bo'lgan stolni bo'sh deb belgilashi mumkin." });
        var (success, error) = await _businessService.UpdateTableStatusAsync(tableId, request, ct);
        if (success) return NoContent();
        // Faol buyurtma borligi sababli bloklangan bo'lsa — 409 (Conflict),
        // stol topilmasa — 404 qaytadi. Frontend ikkalasida ham error matnini ko'rsatadi.
        var statusCode = string.Equals(error, "Bunday stol topilmadi.", StringComparison.Ordinal)
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status409Conflict;
        return StatusCode(statusCode, new { error });
    }

    /// <summary>Ega login/parolini restoranni qayta yaratmasdan tiklash (faqat Super Admin).</summary>
    [HttpPut]
    [SuperAdminReadOnly]
    public async Task<IActionResult> ResetOwnerCredentials([FromBody] ResetOwnerCredentialsRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct)) return Unauthorized(new { error = "Super Admin sifatida kiring." });
        var (success, error, owner) = await _businessService.ResetOwnerCredentialsAsync(request, ct);
        return success ? Ok(owner) : BadRequest(new { error });
    }

    /// <summary>Restoran (egasi) akkountini passivlashtirish/faollashtirish (faqat Super Admin).</summary>
    [HttpPut]
    [SuperAdminReadOnly]
    public async Task<IActionResult> UpdateOwnerStatus([FromBody] UpdateOwnerStatusRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct)) return Unauthorized(new { error = "Super Admin sifatida kiring." });
        var (success, error, owner) = await _businessService.SetOwnerActiveAsync(request, ct);
        return success ? Ok(owner) : BadRequest(new { error });
    }

    private Task<bool> IsSuperAdmin(CancellationToken ct) =>
        _businessService.IsSuperAdminTokenAsync(Request.Headers["X-Super-Admin-Token"].FirstOrDefault(), ct);
}
