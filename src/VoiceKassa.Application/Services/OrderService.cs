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
        if (string.IsNullOrWhiteSpace(request.TranscriptText))
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

        var extraction = await _aiExtraction.ExtractOrderAsync(request.TranscriptText, ct);
        if (!extraction.Success || extraction.Items.Count == 0)
            return (false, extraction.ErrorMessage ?? "Buyurtmadan mahsulot topilmadi.", null);

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

        order.TranscriptText = request.TranscriptText.Trim();

        var stockMoves = new List<(long ProductId, decimal Quantity)>();
        var inventory = new List<InventoryTransaction>();
        var unmatched = new List<string>();

        foreach (var item in extraction.Items)
        {
            if (item.Quantity <= 0)
                return (false, $"'{item.Name}' miqdori noto'g'ri.", null);

            var product = await _orderRepo.FindProductByNameAsync(request.BusinessId, item.Name, ct);
            if (product is null || !product.IsAvailable)
            {
                unmatched.Add(string.IsNullOrWhiteSpace(item.Name) ? "?" : item.Name);
                continue;
            }

            var unitPrice = product.Price;
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductNameSpoken = item.Name,
                Quantity = item.Quantity,
                Unit = string.IsNullOrWhiteSpace(item.Unit) ? product.Unit : item.Unit,
                LineTotal = unitPrice * item.Quantity,
            });

            if (!isRestaurantFlow && product.StockQuantity.HasValue)
            {
                if (product.StockQuantity.Value < item.Quantity)
                    return (false, $"'{product.Name}' omborda yetarli emas.", null);

                stockMoves.Add((product.Id, item.Quantity));
                inventory.Add(new InventoryTransaction
                {
                    BusinessId = request.BusinessId,
                    ProductId = product.Id,
                    Type = InventoryTransactionType.Out,
                    Quantity = item.Quantity,
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

    public async Task<OrderResponse?> GetOrderAsync(long orderId, CancellationToken ct = default)
    {
        var order = await _orderRepo.GetByIdAsync(orderId, ct);
        return order is null ? null : ToOrderResponse(order);
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
        Id = o.Id, BusinessId = o.BusinessId, TableId = o.TableId, StaffId = o.StaffId,
        Status = o.Status, TotalAmount = o.TotalAmount, PaymentType = o.PaymentType,
        CreatedAt = o.CreatedAt, ClosedAt = o.ClosedAt,
        Items = o.Items.Select(i => new OrderItemResponse
        {
            Id = i.Id, ProductId = i.ProductId, ProductNameSpoken = i.ProductNameSpoken,
            Quantity = i.Quantity, Unit = i.Unit, LineTotal = i.LineTotal,
        }).ToList(),
    };
}
