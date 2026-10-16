using Microsoft.AspNetCore.Mvc;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

[Route("[controller]/[action]")]
[ApiController]
public class OrderController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly ReservationService _reservationService;
    private readonly BusinessService _businessService;

    public OrderController(OrderService orderService, ReservationService reservationService, BusinessService businessService)
    {
        _orderService = orderService;
        _reservationService = reservationService;
        _businessService = businessService;
    }

    /// <summary>
    /// Xodim gapirgan (yoki yozgan) matnni qabul qiladi, Gemini orqali
    /// mahsulot/taom, miqdor va to'lov turini ajratib, buyurtma yaratadi
    /// yoki mavjud (stoldagi ochiq) buyurtmaga qo'shadi.
    /// Restoran oqimida (TableId bo'lsa) X-Table-Token talab qilinadi —
    /// mijoz faqat o'z stoliga buyurtma bera oladi.
    /// </summary>
    [HttpPost("voice")]
    public async Task<IActionResult> CreateFromVoice([FromBody] CreateOrderFromTextRequest request, CancellationToken ct)
    {
        if (request.TableId.HasValue)
        {
            // Header ustuvor; body'dagi SessionToken fallback sifatida qoldirildi.
            var headerToken = Request.Headers["X-Table-Token"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerToken)) request.SessionToken = headerToken.Trim();

            var (allowed, authError) = await AuthorizeTableAccessAsync(request.TableId.Value, request.BusinessId, request.SessionToken, ct);
            if (!allowed) return authError!;
        }

        var (success, error, order) = await _orderService.CreateFromTextAsync(request, ct);
        return success ? Ok(order) : BadRequest(new { error });
    }

    /// <summary>
    /// Buyurtmani yopish (to'lov qabul qilindi, stol bo'shatiladi).
    /// X-Table-Token (mijoz — o'z stoli) YOKI X-Owner-Token (admin/oshxona).
    /// </summary>
    [HttpPost("{orderId:long}/close")]
    public async Task<IActionResult> Close(long orderId, [FromBody] CloseOrderRequest request, CancellationToken ct)
    {
        var order = await _orderService.GetOrderAsync(orderId, ct);
        if (order is null) return NotFound(new { error = "Buyurtma topilmadi." });

        var (allowed, authError) = await AuthorizeOrderAccessAsync(order, ct);
        if (!allowed) return authError!;

        var (success, error, result) = await _orderService.CloseOrderAsync(orderId, request, ct);
        return success ? Ok(result) : BadRequest(new { error });
    }

    /// <summary>
    /// Oshxona uchun: biznesdagi ochiq (Open/InProgress) buyurtmalar
    /// — tarkibi va stol nomi bilan. FAQAT X-Owner-Token bilan (mijoz uchun
    /// ochiq emas — u Order/Mine'dan o'z buyurtmasini oladi).
    /// </summary>
    [HttpGet("open/{businessId:long}")]
    public async Task<IActionResult> GetOpen(long businessId, CancellationToken ct)
    {
        var access = await _businessService.AuthorizeOwnerAsync(businessId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        if (!access.Success) return Unauthorized(new { error = access.Error ?? "Ruxsat yo'q." });

        var orders = await _orderService.GetOpenOrdersAsync(businessId, ct);
        return Ok(orders);
    }

    /// <summary>
    /// Mijoz uchun: X-Table-Token orqali o'z ochiq buyurtmasini qaytaradi
    /// (menyu sahifasini tiklash, to'lov uchun). Boshqa stolning buyurtmasini
    /// ko'rsatmaydi.
    /// </summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var token = Request.Headers["X-Table-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized(new { error = "X-Table-Token kerak (bron kodi bilan kiring)." });

        var order = await _orderService.GetMineAsync(token, ct);
        // Ochiq buyurtma yo'q — 404: mijoz menyuni ochib yangi buyurtma beradi.
        return order is null ? NotFound(new { error = "Sizning stolingizda ochiq buyurtma yo'q." }) : Ok(order);
    }

    /// <summary>Oshxona buyurtmani oldi: Open -> InProgress (X-Owner-Token).</summary>
    [HttpPost("{orderId:long}/start")]
    public async Task<IActionResult> Start(long orderId, CancellationToken ct)
    {
        var order = await _orderService.GetOrderAsync(orderId, ct);
        if (order is null) return NotFound(new { error = "Buyurtma topilmadi." });

        var (allowed, authError) = await AuthorizeOrderAccessAsync(order, ct);
        if (!allowed) return authError!;

        var (success, error, result) = await _orderService.StartOrderAsync(orderId, ct);
        return success ? Ok(result) : BadRequest(new { error });
    }

    /// <summary>
    /// Butun ochiq buyurtmani bekor qilish: X-Table-Token (mijoz — o'z stoli)
    /// YOKI X-Owner-Token (oshxona). Body ixtiyoriy: { "reason": "..." }.
    /// </summary>
    [HttpPost("{orderId:long}/cancel")]
    public async Task<IActionResult> Cancel(long orderId, [FromBody] CancelOrderRequest? request, CancellationToken ct)
    {
        var order = await _orderService.GetOrderAsync(orderId, ct);
        if (order is null) return NotFound(new { error = "Buyurtma topilmadi." });

        var (allowed, authError) = await AuthorizeOrderAccessAsync(order, ct);
        if (!allowed) return authError!;

        var (success, error, result) = await _orderService.CancelOrderAsync(orderId, request, ct);
        return success ? Ok(result) : BadRequest(new { error });
    }

    /// <summary>
    /// Buyurtmadagi bitta qatorni o'chiradi (oshxona): masalan bitta taom
    /// tugagan bo'lsa — qolganlari oshxonada qoladi. X-Owner-Token yoki X-Table-Token.
    /// </summary>
    [HttpDelete("{orderId:long}/items/{orderItemId:long}")]
    public async Task<IActionResult> RemoveItem(long orderId, long orderItemId, CancellationToken ct)
    {
        var order = await _orderService.GetOrderAsync(orderId, ct);
        if (order is null) return NotFound(new { error = "Buyurtma topilmadi." });

        var (allowed, authError) = await AuthorizeOrderAccessAsync(order, ct);
        if (!allowed) return authError!;

        var (success, error, result) = await _orderService.RemoveOrderItemAsync(orderId, orderItemId, ct);
        return success ? Ok(result) : BadRequest(new { error });
    }

    /// <summary>
    /// Buyurtmani ID bo'yicha olish: X-Table-Token (mijoz — faqat o'z stoli
    /// buyurtmasi; boshqa stolga so'rasa 403) YOKI X-Owner-Token (oshxona).
    /// </summary>
    [HttpGet("{orderId:long}")]
    public async Task<IActionResult> GetById(long orderId, CancellationToken ct)
    {
        var order = await _orderService.GetOrderAsync(orderId, ct);
        if (order is null) return NotFound();

        var (allowed, authError) = await AuthorizeOrderAccessAsync(order, ct);
        if (!allowed) return authError!;

        return Ok(order);
    }

    // ---------- Xavfsizlik yordamchilari ----------

    /// <summary>
    /// Buyurtmaga kirish: X-Table-Token (sessiya bronning stoliga mos bo'lishi
    /// shart — mos bo'lmasa 403, yaroqsiz bo'lsa 401) YOKI X-Owner-Token
    /// (admin/oshxona paneli — o'z biznesidagi buyurtmalar).
    /// </summary>
    private async Task<(bool Allowed, IActionResult? Error)> AuthorizeOrderAccessAsync(OrderResponse order, CancellationToken ct)
    {
        var tableToken = Request.Headers["X-Table-Token"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(tableToken))
        {
            var reservation = await _reservationService.GetBySessionTokenAsync(tableToken.Trim(), ct);
            if (reservation is null)
                return (false, Unauthorized(new { error = "Sessiya yaroqsiz — bron kodi bilan qayta kiring." }));
            if (order.TableId is null || reservation.TableId != order.TableId.Value)
                return (false, StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Bu buyurtma boshqa stolga tegishli — o'z stolingizning buyurtmasini ko'ring." }));
            return (true, null);
        }

        var access = await _businessService.AuthorizeOwnerAsync(order.BusinessId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        return access.Success
            ? (true, null)
            : (false, Unauthorized(new { error = "X-Table-Token yoki X-Owner-Token kerak." }));
    }

    /// <summary>Voice buyurtma uchun stolga ulanish: sessiya shu stolga tegishli bo'lishi shart.</summary>
    private async Task<(bool Allowed, IActionResult? Error)> AuthorizeTableAccessAsync(
        long tableId, long businessId, string? sessionToken, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            var reservation = await _reservationService.GetBySessionTokenAsync(sessionToken.Trim(), ct);
            if (reservation is null)
                return (false, Unauthorized(new { error = "Sessiya yaroqsiz — bron kodi bilan qayta kiring." }));
            if (reservation.TableId != tableId || reservation.BusinessId != businessId)
                return (false, StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Bu sessiya boshqa stolga tegishli." }));
            if (reservation.Status is Domain.Enums.ReservationStatus.Cancelled or Domain.Enums.ReservationStatus.Closed)
                return (false, StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Bron yopilgan yoki bekor qilingan." }));
            return (true, null);
        }

        // Sessiyasiz — faqat egasi/staff (owner token) restoran stoliga buyurtma bera oladi.
        var access = await _businessService.AuthorizeOwnerAsync(businessId, Request.Headers["X-Owner-Token"].FirstOrDefault(), ct);
        return access.Success
            ? (true, null)
            : (false, Unauthorized(new { error = "X-Table-Token bilan stolingizga ulaning yoki X-Owner-Token kiriting." }));
    }
}
