using Microsoft.AspNetCore.Mvc;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

[Route("[controller]/[action]")]
[ApiController]
public class ProductController : ControllerBase
{
    private readonly BusinessService _businessService;

    public ProductController(BusinessService businessService) => _businessService = businessService;

    /// <summary>Yangi mahsulot/taom qo'shish.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var (success, error, product) = await _businessService.CreateProductAsync(request, ct);
        return success ? Ok(product) : BadRequest(new { error });
    }

    [HttpGet]
    public async Task<IActionResult> GetByBusiness([FromQuery] long businessId, CancellationToken ct)
    {
        var products = await _businessService.GetProductsAsync(businessId, ct);
        return Ok(products);
    }

    /// <summary>"Qaysi mahsulot tugayapti?" - qoldig'i chegaradan past mahsulotlar.</summary>
    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock([FromQuery] long businessId, CancellationToken ct)
    {
        var products = await _businessService.GetLowStockProductsAsync(businessId, ct);
        return Ok(products);
    }

    [HttpPut("{productId:long}/stock")]
    public async Task<IActionResult> UpdateStock(long productId, [FromBody] UpdateStockRequest request, CancellationToken ct)
    {
        var (success, error) = await _businessService.UpdateStockAsync(productId, request, ct);
        return success ? NoContent() : NotFound(new { error });
    }
}
