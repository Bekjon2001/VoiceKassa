using Microsoft.AspNetCore.Mvc;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

[Route("[controller]/[action]")]
[ApiController]
public class OrderController : ControllerBase
{
    private readonly OrderService _orderService;

    public OrderController(OrderService orderService) => _orderService = orderService;

    /// <summary>
    /// Xodim gapirgan (yoki yozgan) matnni qabul qiladi, Gemini orqali
    /// mahsulot/taom, miqdor va to'lov turini ajratib, buyurtma yaratadi
    /// yoki mavjud (stoldagi ochiq) buyurtmaga qo'shadi.
    /// Masalan: "ikkita lag'mon, bitta cho'chqa go'shti shashlik" (restoran)
    /// yoki "non 2 ta 6 ming naqd" (do'kon).
    /// </summary>
    [HttpPost("voice")]
    public async Task<IActionResult> CreateFromVoice([FromBody] CreateOrderFromTextRequest request, CancellationToken ct)
    {
        var (success, error, order) = await _orderService.CreateFromTextAsync(request, ct);
        return success ? Ok(order) : BadRequest(new { error });
    }

    /// <summary>Buyurtmani yopish (to'lov qabul qilindi, stol bo'shatiladi).</summary>
    [HttpPost("{orderId:long}/close")]
    public async Task<IActionResult> Close(long orderId, [FromBody] CloseOrderRequest request, CancellationToken ct)
    {
        var (success, error, order) = await _orderService.CloseOrderAsync(orderId, request, ct);
        return success ? Ok(order) : BadRequest(new { error });
    }

    /// <summary>Oshxona uchun: biznesdagi ochiq (Open/InProgress) buyurtmalar
    /// — tarkibi va stol nomi bilan. Admin panel "Oshxona" bo'limi 10s da
    /// bir marta so'raydi.</summary>
    [HttpGet("open/{businessId:long}")]
    public async Task<IActionResult> GetOpen(long businessId, CancellationToken ct)
    {
        var orders = await _orderService.GetOpenOrdersAsync(businessId, ct);
        return Ok(orders);
    }

    /// <summary>Oshxona buyurtmani oldi: Open -> InProgress.</summary>
    [HttpPost("{orderId:long}/start")]
    public async Task<IActionResult> Start(long orderId, CancellationToken ct)
    {
        var (success, error, order) = await _orderService.StartOrderAsync(orderId, ct);
        return success ? Ok(order) : BadRequest(new { error });
    }

    [HttpGet("{orderId:long}")]
    public async Task<IActionResult> GetById(long orderId, CancellationToken ct)
    {
        var order = await _orderService.GetOrderAsync(orderId, ct);
        return order is null ? NotFound() : Ok(order);
    }
}
