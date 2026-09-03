using Microsoft.AspNetCore.Mvc;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

[Route("[controller]/[action]")]
[ApiController]
public class BusinessController : ControllerBase
{
    private readonly BusinessService _businessService;
    private readonly AuthService _authService;

    public BusinessController(BusinessService businessService, AuthService authService)
    {
        _businessService = businessService;
        _authService = authService;
    }

    [HttpPost("super-admin/first")]
    public async Task<IActionResult> CreateFirstSuperAdmin([FromBody] CreateSuperAdminRequest request, CancellationToken ct)
    {
        var (success, error, account) = await _authService.CreateFirstSuperAdminAsync(request, ct);
        return success ? Ok(account) : BadRequest(new { error });
    }

    [HttpPost("super-admin/login")]
    public async Task<IActionResult> LoginSuperAdmin([FromBody] LoginRequest request, CancellationToken ct)
    {
        var (success, error, account) = await _authService.SuperAdminLoginAsync(request, ct);
        return success ? Ok(account) : Unauthorized(new { error });
    }

    [HttpGet("super-admin/session")]
    public async Task<IActionResult> CheckSuperAdmin(CancellationToken ct)
    {
        var token = Request.Headers["X-Super-Admin-Token"].FirstOrDefault();
        return await _businessService.IsSuperAdminTokenAsync(token, ct) ? Ok() : Unauthorized();
    }

    [HttpPost("restaurant")]
    public async Task<IActionResult> CreateRestaurant([FromBody] CreateRestaurantWithOwnerRequest request, CancellationToken ct)
    {
        if (await _businessService.HasSuperAdminAsync(ct) && !await IsSuperAdmin(ct)) return Unauthorized();
        var (success, error, owner) = await _businessService.CreateRestaurantWithOwnerAsync(request, ct);
        return success ? Ok(owner) : BadRequest(new { error });
    }

    /// <summary>Supermarket + egasini yaratadi (Super Admin). Backend bitta umumiy oqimdan
    /// foydalanadi — farq faqat Business turida (Market).</summary>
    [HttpPost("market")]
    public async Task<IActionResult> CreateMarket([FromBody] CreateMarketWithOwnerRequest request, CancellationToken ct)
    {
        if (await _businessService.HasSuperAdminAsync(ct) && !await IsSuperAdmin(ct)) return Unauthorized();
        var (success, error, owner) = await _businessService.CreateMarketWithOwnerAsync(request, ct);
        return success ? Ok(owner) : BadRequest(new { error });
    }

    [HttpPost("owner/login")]
    public async Task<IActionResult> LoginOwner([FromBody] OwnerLoginRequest request, CancellationToken ct)
    {
        var (success, error, owner) = await _businessService.LoginOwnerAsync(request, ct);
        return success ? Ok(owner) : Unauthorized(new { error });
    }

    /// <summary>Yangi biznes ro'yxatdan o'tkazish (restoran, do'kon, market, ombor).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBusinessRequest request, CancellationToken ct)
    {
        var business = await _businessService.CreateBusinessAsync(request, ct);
        return Ok(business);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct)) return Unauthorized();
        var businesses = await _businessService.GetAllBusinessesAsync(ct);
        return Ok(businesses);
    }

    [HttpGet("{businessId:long}")]
    public async Task<IActionResult> GetById(long businessId, CancellationToken ct)
    {
        var business = await _businessService.GetBusinessAsync(businessId, ct);
        return business is null ? NotFound() : Ok(business);
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
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequest request, CancellationToken ct)
    {
        var ownerAccess = await _businessService.AuthorizeOwnerAsync(request.BusinessId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        if (!ownerAccess.Success) return Unauthorized(new { error = ownerAccess.Error });
        var (success, error, staff) = await _businessService.CreateStaffAsync(request, ct);
        return success ? Ok(staff) : BadRequest(new { error });
    }

    [HttpGet("{businessId:long}/staff")]
    public async Task<IActionResult> GetStaff(long businessId, CancellationToken ct)
    {
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
    public async Task<IActionResult> UpdateStaffSalary(long staffId, [FromBody] UpdateStaffSalaryRequest request, CancellationToken ct)
    {
        var ownerAccess = await _businessService.AuthorizeStaffAsync(staffId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        if (!ownerAccess.Success) return Unauthorized(new { error = ownerAccess.Error });
        var (success, error, staff) = await _businessService.UpdateStaffSalaryAsync(staffId, request, ct);
        return success ? Ok(staff) : BadRequest(new { error });
    }

    /// <summary>Xodimni ishdan bo'shatish / faollashtirish (owner).</summary>
    [HttpPut("staff/{staffId:long}/active")]
    public async Task<IActionResult> SetStaffActive(long staffId, [FromBody] UpdateStaffStatusRequest request, CancellationToken ct)
    {
        var ownerAccess = await _businessService.AuthorizeStaffAsync(staffId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        if (!ownerAccess.Success) return Unauthorized(new { error = ownerAccess.Error });
        var (success, error, staff) = await _businessService.SetStaffActiveAsync(staffId, request.IsActive, ct);
        return success ? Ok(staff) : BadRequest(new { error });
    }

    [HttpPut("staff/{staffId:long}/status")]
    public async Task<IActionResult> UpdateStaffStatus(long staffId, [FromBody] UpdateStaffStatusRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct)) return Unauthorized();
        var (success, error) = await _businessService.UpdateStaffStatusAsync(staffId, request, ct);
        return success ? NoContent() : NotFound(new { error });
    }

    // ---- Category ----

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        var (success, error, category) = await _businessService.CreateCategoryAsync(request, ct);
        return success ? Ok(category) : BadRequest(new { error });
    }

    [HttpGet("{businessId:long}/categories")]
    public async Task<IActionResult> GetCategories(long businessId, CancellationToken ct)
    {
        var categories = await _businessService.GetCategoriesAsync(businessId, ct);
        return Ok(categories);
    }

    // ---- Table (restoran uchun) ----

    [HttpPost("tables")]
    public async Task<IActionResult> CreateTable([FromBody] CreateTableRequest request, CancellationToken ct)
    {
        var access = await _businessService.AuthorizeOwnerAsync(request.BusinessId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        if (!access.Success) return Unauthorized(new { error = access.Error });
        var (success, error, table) = await _businessService.CreateTableAsync(request, ct);
        return success ? Ok(table) : BadRequest(new { error });
    }

    [HttpGet("{businessId:long}/tables")]
    public async Task<IActionResult> GetTables(long businessId, CancellationToken ct)
    {
        var tables = await _businessService.GetTablesAsync(businessId, ct);
        return Ok(tables);
    }

    [HttpPut("tables/{tableId:long}/status")]
    public async Task<IActionResult> UpdateTableStatus(long tableId, [FromBody] UpdateTableStatusRequest request, CancellationToken ct)
    {
        var (success, error) = await _businessService.UpdateTableStatusAsync(tableId, request, ct);
        return success ? NoContent() : NotFound(new { error });
    }

    /// <summary>Ega login/parolini restoranni qayta yaratmasdan tiklash (faqat Super Admin).</summary>
    [HttpPut]
    public async Task<IActionResult> ResetOwnerCredentials([FromBody] ResetOwnerCredentialsRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct)) return Unauthorized(new { error = "Super Admin sifatida kiring." });
        var (success, error, owner) = await _businessService.ResetOwnerCredentialsAsync(request, ct);
        return success ? Ok(owner) : BadRequest(new { error });
    }

    /// <summary>Restoran (egasi) akkountini passivlashtirish/faollashtirish (faqat Super Admin).</summary>
    [HttpPut]
    public async Task<IActionResult> UpdateOwnerStatus([FromBody] UpdateOwnerStatusRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct)) return Unauthorized(new { error = "Super Admin sifatida kiring." });
        var (success, error, owner) = await _businessService.SetOwnerActiveAsync(request, ct);
        return success ? Ok(owner) : BadRequest(new { error });
    }

    private Task<bool> IsSuperAdmin(CancellationToken ct) =>
        _businessService.IsSuperAdminTokenAsync(Request.Headers["X-Super-Admin-Token"].FirstOrDefault(), ct);
}
