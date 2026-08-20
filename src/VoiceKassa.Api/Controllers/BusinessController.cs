using Microsoft.AspNetCore.Mvc;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

[ApiController]
[Route("api/businesses")]
public class BusinessController : ControllerBase
{
    private readonly BusinessService _businessService;

    public BusinessController(BusinessService businessService) => _businessService = businessService;

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
        var businesses = await _businessService.GetAllBusinessesAsync(ct);
        return Ok(businesses);
    }

    [HttpGet("{businessId:long}")]
    public async Task<IActionResult> GetById(long businessId, CancellationToken ct)
    {
        var business = await _businessService.GetBusinessAsync(businessId, ct);
        return business is null ? NotFound() : Ok(business);
    }

    // ---- Staff ----

    [HttpPost("staff")]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequest request, CancellationToken ct)
    {
        var (success, error, staff) = await _businessService.CreateStaffAsync(request, ct);
        return success ? Ok(staff) : BadRequest(new { error });
    }

    [HttpGet("{businessId:long}/staff")]
    public async Task<IActionResult> GetStaff(long businessId, CancellationToken ct)
    {
        var staff = await _businessService.GetStaffAsync(businessId, ct);
        return Ok(staff);
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
}
