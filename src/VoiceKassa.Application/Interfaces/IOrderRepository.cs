using VoiceKassa.Domain.Entities;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Application.Interfaces;

/// <summary>Buyurtma/savdo oqimi: Order, OrderItem, InventoryTransaction.</summary>
public interface IOrderRepository
{
    Task<Order> AddAsync(Order order, CancellationToken ct = default);
    Task<Order?> GetByIdAsync(long orderId, CancellationToken ct = default);

    /// <summary>Restoran oqimi: shu stolda hozir ochiq (yopilmagan) buyurtma bormi.</summary>
    Task<Order?> GetOpenOrderByTableAsync(long tableId, CancellationToken ct = default);

    Task<List<Order>> GetByBusinessAndRangeAsync(
        long businessId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    Task<bool> UpdateOrderStatusAsync(long orderId, OrderStatus status, DateTime? closedAt, PaymentType? paymentType, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Ovozdan tanilgan mahsulot nomini bazadagi Product/Aliases bilan moslashtiradi.</summary>
    Task<Product?> FindProductByNameAsync(long businessId, string spokenName, CancellationToken ct = default);

    Task DecrementStockAsync(long productId, decimal quantity, CancellationToken ct = default);

    Task AddInventoryTransactionAsync(InventoryTransaction transaction, CancellationToken ct = default);
}
