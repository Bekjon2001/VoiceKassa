using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.Domain.Entities;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Application.Services;

/// <summary>
/// Ovozli (matnli) buyurtma qabul qilish va yopish oqimi.
///
/// Restoran oqimi (TableId berilganda):
///   - Shu stolning ochiq buyurtmasi bo'lsa - yangi qatorlar shunga qo'shiladi.
///   - Bo'lmasa - yangi Order (Status=Open) yaratiladi, stol Occupied qilinadi.
///   - Ombor bu bosqichda KAMAYTIRILMAYDI (taom hali tayyorlanmagan bo'lishi
///     mumkin) - faqat CloseOrderAsync chaqirilganda hisoblanadi.
///
/// Do'kon oqimi (TableId berilmaganda):
///   - Bitta gap = bitta yakunlangan sotuv. Order to'g'ridan-to'g'ri
///     Status=Completed qilib yaratiladi, ombor darhol kamaytiriladi.
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

        var extraction = await _aiExtraction.ExtractOrderAsync(request.TranscriptText, ct);
        if (!extraction.Success || extraction.Items.Count == 0)
            return (false, extraction.ErrorMessage ?? "Buyurtmadan mahsulot topilmadi.", null);

        var isRestaurantFlow = request.TableId.HasValue;

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

        order.TranscriptText = request.TranscriptText;

        foreach (var item in extraction.Items)
        {
            var product = await _orderRepo.FindProductByNameAsync(request.BusinessId, item.Name, ct);
            var unitPrice = item.Price ?? product?.Price ?? 0;

            order.Items.Add(new OrderItem
            {
                ProductId = product?.Id,
                ProductNameSpoken = item.Name,
                Quantity = item.Quantity,
                Unit = string.IsNullOrWhiteSpace(item.Unit) ? "dona" : item.Unit,
                LineTotal = unitPrice * item.Quantity,
            });

            // Do'kon oqimida ombor darhol kamayadi. Restoran oqimida
            // buyurtma yopilganda (CloseOrderAsync) kamayadi.
            if (!isRestaurantFlow && product is not null && product.StockQuantity.HasValue)
            {
                var newQty = product.StockQuantity.Value - item.Quantity;
                await _orderRepo.DecrementStockAsync(product.Id, item.Quantity, ct);
                await _orderRepo.AddInventoryTransactionAsync(new InventoryTransaction
                {
                    BusinessId = request.BusinessId,
                    ProductId = product.Id,
                    Type = InventoryTransactionType.Out,
                    Quantity = item.Quantity,
                    Reason = "Sotuv (ovozli)",
                }, ct);
            }
        }

        order.TotalAmount = extraction.Total ?? order.Items.Sum(i => i.LineTotal);

        if (order.Id == 0)
            await _orderRepo.AddAsync(order, ct);
        else
            await _orderRepo.SaveChangesAsync(ct);

        if (isRestaurantFlow)
            await _businessRepo.UpdateTableStatusAsync(request.TableId!.Value, TableStatus.Occupied, ct);

        return (true, null, ToOrderResponse(order));
    }

    public async Task<(bool Success, string? Error, OrderResponse? Order)> CloseOrderAsync(
        long orderId, CloseOrderRequest request, CancellationToken ct = default)
    {
        var order = await _orderRepo.GetByIdAsync(orderId, ct);
        if (order is null) return (false, "Bunday buyurtma topilmadi.", null);
        if (order.Status == OrderStatus.Completed) return (false, "Buyurtma allaqachon yopilgan.", null);

        // Restoran oqimida ombor faqat shu yerda, yopilganda kamayadi.
        foreach (var item in order.Items.Where(i => i.ProductId.HasValue))
        {
            var product = await _businessRepo.GetProductByIdAsync(item.ProductId!.Value, ct);
            if (product is not null && product.StockQuantity.HasValue)
            {
                await _orderRepo.DecrementStockAsync(product.Id, item.Quantity, ct);
                await _orderRepo.AddInventoryTransactionAsync(new InventoryTransaction
                {
                    BusinessId = order.BusinessId,
                    ProductId = product.Id,
                    Type = InventoryTransactionType.Out,
                    Quantity = item.Quantity,
                    Reason = "Buyurtma yopildi",
                }, ct);
            }
        }

        await _orderRepo.UpdateOrderStatusAsync(orderId, OrderStatus.Completed, DateTime.UtcNow, request.PaymentType, ct);

        if (order.TableId.HasValue)
            await _businessRepo.UpdateTableStatusAsync(order.TableId.Value, TableStatus.Free, ct);

        var refreshed = await _orderRepo.GetByIdAsync(orderId, ct);
        return (true, null, refreshed is null ? null : ToOrderResponse(refreshed));
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
