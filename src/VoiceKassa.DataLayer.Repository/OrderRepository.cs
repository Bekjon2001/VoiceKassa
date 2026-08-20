using Microsoft.EntityFrameworkCore;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.DataLayer;
using VoiceKassa.Domain.Entities;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.DataLayer.Repository;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;

    public OrderRepository(AppDbContext db) => _db = db;

    public async Task<Order> AddAsync(Order order, CancellationToken ct = default)
    {
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);
        return order;
    }

    public Task<Order?> GetByIdAsync(long orderId, CancellationToken ct = default) =>
        _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, ct);

    public Task<Order?> GetOpenOrderByTableAsync(long tableId, CancellationToken ct = default) =>
        _db.Orders
            .Include(o => o.Items)
            .Where(o => o.TableId == tableId && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<List<Order>> GetByBusinessAndRangeAsync(
        long businessId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        _db.Orders
            .Include(o => o.Items)
            .Where(o => o.BusinessId == businessId && o.CreatedAt >= fromUtc && o.CreatedAt < toUtc)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(ct);

    public async Task<bool> UpdateOrderStatusAsync(
        long orderId, OrderStatus status, DateTime? closedAt, PaymentType? paymentType, CancellationToken ct = default)
    {
        var order = await _db.Orders.FindAsync(new object[] { orderId }, ct);
        if (order is null) return false;

        order.Status = status;
        if (closedAt.HasValue) order.ClosedAt = closedAt;
        if (paymentType.HasValue) order.PaymentType = paymentType.Value;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task<Product?> FindProductByNameAsync(long businessId, string spokenName, CancellationToken ct = default)
    {
        var normalized = spokenName.Trim().ToLowerInvariant();

        // Exact name match first, then alias match. Fuzzy/phonetic matching
        // (Levenshtein, trigram search via pg_trgm) is a good next upgrade
        // once real spoken-name variance data comes in.
        var products = await _db.Products
            .Where(p => p.BusinessId == businessId)
            .ToListAsync(ct);

        return products.FirstOrDefault(p =>
            p.Name.Trim().ToLowerInvariant() == normalized ||
            p.Aliases.Any(a => a.Trim().ToLowerInvariant() == normalized));
    }

    public async Task DecrementStockAsync(long productId, decimal quantity, CancellationToken ct = default)
    {
        var product = await _db.Products.FindAsync(new object[] { productId }, ct);
        if (product is null || !product.StockQuantity.HasValue) return;

        product.StockQuantity -= quantity;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddInventoryTransactionAsync(InventoryTransaction transaction, CancellationToken ct = default)
    {
        _db.InventoryTransactions.Add(transaction);
        await _db.SaveChangesAsync(ct);
    }
}
