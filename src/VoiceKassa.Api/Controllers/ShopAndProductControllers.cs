using Microsoft.AspNetCore.Mvc;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

[ApiController]
[Route("api/shops")]
public class ShopController : ControllerBase
{
    private readonly ShopService _shopService;

    public ShopController(ShopService shopService)
    {
        _shopService = shopService;
    }

    /// <summary>Yangi do'kon ro'yxatdan o'tkazish.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ShopResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateShopRequest request, CancellationToken ct)
    {
        var shop = await _shopService.CreateShopAsync(request, ct);
        return Ok(shop);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<ShopResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var shops = await _shopService.GetAllShopsAsync(ct);
        return Ok(shops);
    }

    [HttpGet("{shopId:guid}")]
    [ProducesResponseType(typeof(ShopResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid shopId, CancellationToken ct)
    {
        var shop = await _shopService.GetShopAsync(shopId, ct);
        return shop is null ? NotFound() : Ok(shop);
    }

    /// <summary>Do'konga yangi kassir qo'shish.</summary>
    [HttpPost("cashiers")]
    [ProducesResponseType(typeof(CashierResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCashier([FromBody] CreateCashierRequest request, CancellationToken ct)
    {
        var (success, error, cashier) = await _shopService.CreateCashierAsync(request, ct);
        return success ? Ok(cashier) : BadRequest(new { error });
    }

    [HttpGet("{shopId:guid}/cashiers")]
    [ProducesResponseType(typeof(List<CashierResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCashiers(Guid shopId, CancellationToken ct)
    {
        var cashiers = await _shopService.GetCashiersAsync(shopId, ct);
        return Ok(cashiers);
    }
}

[ApiController]
[Route("api/products")]
public class ProductController : ControllerBase
{
    private readonly ShopService _shopService;

    public ProductController(ShopService shopService)
    {
        _shopService = shopService;
    }

    /// <summary>Do'konga yangi mahsulot qo'shish (nomi, narxi, boshlang'ich qoldig'i).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var (success, error, product) = await _shopService.CreateProductAsync(request, ct);
        return success ? Ok(product) : BadRequest(new { error });
    }

    /// <summary>Berilgan do'kondagi barcha mahsulotlar ro'yxati (qoldiq bilan).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByShop([FromQuery] Guid shopId, CancellationToken ct)
    {
        var products = await _shopService.GetProductsAsync(shopId, ct);
        return Ok(products);
    }

    /// <summary>"Qaysi mahsulot tugayapti?" - qoldig'i chegaradan past mahsulotlar.</summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(List<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLowStock([FromQuery] Guid shopId, CancellationToken ct)
    {
        var products = await _shopService.GetLowStockProductsAsync(shopId, ct);
        return Ok(products);
    }

    /// <summary>Mahsulot qoldig'ini qo'lda to'g'rilash (masalan, tovar kirimi).</summary>
    [HttpPut("{productId:guid}/stock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStock(
        Guid productId, [FromBody] UpdateStockRequest request, CancellationToken ct)
    {
        var (success, error) = await _shopService.UpdateStockAsync(productId, request, ct);
        return success ? NoContent() : NotFound(new { error });
    }
}
