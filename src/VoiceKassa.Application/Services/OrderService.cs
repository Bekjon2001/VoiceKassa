using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.Domain.Entities;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Application.Services;

/// <summary>
/// Ovozli (matnli) buyurtma qabul qilish va yopish oqimi.
/// Narx faqat bazadagi Product.Price; jami faqat qatorlar yig'indisi.
/// </summary>
public class OrderService
{
    private readonly IOrderRepository _orderRepo;
    private readonly IBusinessRepository _businessRepo;
    private readonly IAiExtractionService _aiExtraction;

    public OrderService(IOrderRepository orderRepo, IBusinessRepository businessRepo, IAiExtractionService aiExtraction)
    {
        _orderRepo = orderRepo;
        _businessRepo = businessRepo;
        _aiExtraction = aiExtraction;
    }

    public async Task<(bool Success, string? Error, OrderResponse? Order)> CreateFromTextAsync(
        CreateOrderFromTextRequest request, CancellationToken ct = default)
    {
        var hasItems = request.Items is { Count: > 0 };
        if (!hasItems && string.IsNullOrWhiteSpace(request.TranscriptText))
            return (false, "Matn bo'sh bo'lishi mumkin emas.", null);

        var business = await _businessRepo.GetBusinessByIdAsync(request.BusinessId, ct);
        if (business is null || !business.IsActive)
            return (false, "Biznes topilmadi yoki faol emas.", null);

        var isRestaurantFlow = request.TableId.HasValue;
        if (isRestaurantFlow)
        {
            var table = await _businessRepo.GetTableByIdAsync(request.TableId!.Value, ct);
            if (table is null || table.BusinessId != request.BusinessId)
                return (false, "Stol shu biznesga tegishli emas.", null);
        }

        // Qo'shimcha matn bo'lsa — AI orqali qo'shimcha itemlar ajratiladi
        // (faqat menyudan tanlanmagan, qo'lda yozilgan qismlar uchun).
        var extraction = new OrderExtractionResult { Success = true };
        if (!string.IsNullOrWhiteSpace(request.TranscriptText))
        {
            extraction = await _aiExtraction.ExtractOrderAsync(request.TranscriptText, ct);
            if (!extraction.Success && !hasItems)
                return (false, extraction.ErrorMessage ?? "Buyurtmadan mahsulot topilmadi.", null);
        }

        // Yagona ro'yxat: menyudan tanlangan (strukturali) + matndan ajratilgan.
        var wanted = new List<(long? ProductId, string Name, decimal Quantity, string Unit)>();
        foreach (var item in request.Items ?? new List<SelectedOrderItem>())
        {
            var name = item.Name?.Trim() ?? string.Empty;
            if (item.ProductId is null && string.IsNullOrEmpty(name)) continue;
            wanted.Add((item.ProductId, name, item.Quantity <= 0 ? 1 : item.Quantity, "dona"));
        }
        if (extraction.Success)
        {
            foreach (var item in extraction.Items)
                wanted.Add((null, item.Name?.Trim() ?? string.Empty, item.Quantity,
                    string.IsNullOrWhiteSpace(item.Unit) ? "dona" : item.Unit));
        }
        if (wanted.Count == 0)
            return (false, "Buyurtmadan mahsulot topilmadi.", null);

        Order order;
        if (isRestaurantFlow)
        {
            order = await _orderRepo.GetOpenOrderByTableAsync(request.TableId!.Value, ct)
                    ?? new Order
                    {
                        BusinessId = request.BusinessId,
                        TableId = request.TableId,
                        StaffId = request.StaffId,
                        Status = OrderStatus.Open,
                    };
        }
        else
        {
            order = new Order
            {
                BusinessId = request.BusinessId,
                StaffId = request.StaffId,
                Status = OrderStatus.Completed,
                ClosedAt = DateTime.UtcNow,
                PaymentType = ParsePaymentType(extraction.PaymentTypeRaw),
            };
        }

        order.TranscriptText = request.TranscriptText?.Trim() ?? string.Empty;

        var stockMoves = new List<(long ProductId, decimal Quantity)>();
        var inventory = new List<InventoryTransaction>();
        var unmatched = new List<string>();

        foreach (var (productId, name, quantity, unit) in wanted)
        {
            if (quantity <= 0)
                return (false, $"'{(string.IsNullOrEmpty(name) ? productId?.ToString() : name)}' miqdori noto'g'ri.", null);

            var product = productId.HasValue
                ? await _orderRepo.GetProductByIdAsync(productId.Value, ct)
                : await _orderRepo.FindProductByNameAsync(request.BusinessId, name, ct);

            if (product is null || product.BusinessId != request.BusinessId || !product.IsAvailable)
            {
                unmatched.Add(string.IsNullOrEmpty(name) ? "?" : name);
                continue;
            }

            var unitPrice = product.Price;
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductNameSpoken = string.IsNullOrEmpty(name) ? product.Name : name,
                Quantity = quantity,
                Unit = string.IsNullOrWhiteSpace(unit) ? product.Unit : unit,
                LineTotal = unitPrice * quantity,
            });

            if (!isRestaurantFlow && product.StockQuantity.HasValue)
            {
                if (product.StockQuantity.Value < quantity)
                    return (false, $"'{product.Name}' omborda yetarli emas.", null);

                stockMoves.Add((product.Id, quantity));
                inventory.Add(new InventoryTransaction
                {
                    BusinessId = request.BusinessId,
                    ProductId = product.Id,
                    Type = InventoryTransactionType.Out,
                    Quantity = quantity,
                    Reason = "Sotuv (ovozli)",
                });
            }
        }

        if (unmatched.Count > 0)
            return (false, "Menyuda topilmadi: " + string.Join(", ", unmatched), null);

        if (order.Items.Count == 0)
            return (false, "Buyurtmadan mahsulot topilmadi.", null);

        order.TotalAmount = order.Items.Sum(i => i.LineTotal);

        var saved = await _orderRepo.SaveOrderWithStockAsync(
            order, stockMoves, inventory, isRestaurantFlow ? request.TableId : null, ct);

        return (true, null, ToOrderResponse(saved));
    }

    public async Task<(bool Success, string? Error, OrderResponse? Order)> CloseOrderAsync(
        long orderId, CloseOrderRequest request, CancellationToken ct = default)
    {
        var (success, error, order) = await _orderRepo.CloseAtomicallyAsync(orderId, request.PaymentType, ct);
        return (success, error, order is null ? null : ToOrderResponse(order));
    }

    /// <summary>Oshxona: butun ochiq buyurtmani bekor qiladi (Cancelled).</summary>
    public async Task<(bool Success, string? Error, OrderResponse? Order)> CancelOrderAsync(
        long orderId, CancelOrderRequest? request, CancellationToken ct = default)
    {
        var (success, error, order) = await _orderRepo.CancelAtomicallyAsync(orderId, request?.Reason, ct);
        return (success, error, order is null ? null : ToOrderResponse(order));
    }

    /// <summary>Oshxona: buyurtmadagi bitta qatorni o'chiradi.</summary>
    public async Task<(bool Success, string? Error, OrderResponse? Order)> RemoveOrderItemAsync(
        long orderId, long orderItemId, CancellationToken ct = default)
    {
        var (success, error, order) = await _orderRepo.RemoveItemAtomicallyAsync(orderId, orderItemId, ct);
        return (success, error, order is null ? null : ToOrderResponse(order));
    }

    public async Task<OrderResponse?> GetOrderAsync(long orderId, CancellationToken ct = default)
    {
        var order = await _orderRepo.GetByIdAsync(orderId, ct);
        return order is null ? null : ToOrderResponse(order);
    }

    /// <summary>Oshxona uchun: biznesdagi ochiq (Open/InProgress) buyurtmalar
    /// tarkibi va stol nomi bilan.</summary>
    public async Task<List<OrderResponse>> GetOpenOrdersAsync(long businessId, CancellationToken ct = default)
    {
        var orders = await _orderRepo.GetOpenByBusinessAsync(businessId, ct);
        return orders.Select(ToOrderResponse).ToList();
    }

    /// <summary>Oshxona buyurtmani oldi: Open -> InProgress. Yopilgan/
    /// bekor qilingan buyurtmani olib bo'lmaydi.</summary>
    public async Task<(bool Success, string? Error, OrderResponse? Order)> StartOrderAsync(
        long orderId, CancellationToken ct = default)
    {
        var order = await _orderRepo.GetByIdAsync(orderId, ct);
        if (order is null)
            return (false, "Buyurtma topilmadi.", null);
        if (order.Status is OrderStatus.Completed or OrderStatus.Cancelled)
            return (false, "Bu buyurtma yopilgan.", null);
        if (order.Status == OrderStatus.InProgress)
            return (true, null, ToOrderResponse(order)); // allaqachon oshxonada

        await _orderRepo.UpdateOrderStatusAsync(orderId, OrderStatus.InProgress, null, null, ct);
        order.Status = OrderStatus.InProgress;
        return (true, null, ToOrderResponse(order));
    }

    private static PaymentType ParsePaymentType(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "naqd" => PaymentType.Cash,
        "karta" => PaymentType.Card,
        "onlayn" => PaymentType.Online,
        _ => PaymentType.Unknown,
    };

    private static OrderResponse ToOrderResponse(Order o) => new()
    {
        Id = o.Id, BusinessId = o.BusinessId, TableId = o.TableId, TableName = o.Table?.Name,
        StaffId = o.StaffId,
        Status = o.Status, TotalAmount = o.TotalAmount, PaymentType = o.PaymentType,
        CreatedAt = o.CreatedAt, ClosedAt = o.ClosedAt,
        Items = o.Items.Select(i => new OrderItemResponse
        {
            Id = i.Id, ProductId = i.ProductId, ProductNameSpoken = i.ProductNameSpoken,
            Quantity = i.Quantity, Unit = i.Unit, LineTotal = i.LineTotal,
        }).ToList(),
    };
}
