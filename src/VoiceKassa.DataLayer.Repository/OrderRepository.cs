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

    /// <summary>
    /// Buyurtma (yoki mavjud ochiq buyurtmaga qo'shilgan yangi qatorlar) + ombor
    /// chiqimi + stol holati BITTA tranzaksiyada saqlanadi — yo hammasi, yo hech
    /// narsa. Shu tufayli "chek saqlandi, lekin ombor kamaymadi" kabi yarim
    /// holatlar yuzaga kelmaydi.
    /// </summary>
    public async Task<Order> SaveOrderWithStockAsync(
        Order order,
        IReadOnlyList<(long ProductId, decimal Quantity)> stockDecrements,
        IReadOnlyList<InventoryTransaction> inventory,
        long? occupyTableId,
        CancellationToken ct = default)
    {
        // Repository va service bitta scoped AppDbContext'ni bo'lishadi: ochiq
        // buyurtma GetOpenOrderByTableAsync'da allaqachon kuzatilyapti. Shu sababli
        // entity faqat yangi (Id == 0) yoki hali kuzatilmayotgan (detached) holatda
        // qo'shiladi — aks holda EF takroriy INSERT (yoki ID bilan INSERT) qilishga urinardi.
        if (order.Id == 0)
        {
            _db.Orders.Add(order);
        }
        else if (_db.Entry(order).State == EntityState.Detached)
        {
            _db.Orders.Attach(order);
            _db.Entry(order).State = EntityState.Modified;

            foreach (var item in order.Items)
            {
                var itemEntry = _db.Entry(item);
                if (itemEntry.State == EntityState.Detached)
                    itemEntry.State = item.Id == 0 ? EntityState.Added : EntityState.Modified;
            }
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Ombor qoldig'ini kamaytirish (restoran oqimida ro'yxat bo'sh keladi).
            foreach (var (productId, quantity) in stockDecrements)
            {
                var product = await _db.Products.FindAsync(new object[] { productId }, ct);
                if (product?.StockQuantity is null) continue;

                product.StockQuantity -= quantity;
                product.UpdatedAt = DateTime.UtcNow;
            }

            if (inventory.Count > 0)
                _db.InventoryTransactions.AddRange(inventory);

            // Restoran oqimi: stol band qilinadi.
            if (occupyTableId.HasValue)
            {
                var table = await _db.Tables.FindAsync(new object[] { occupyTableId.Value }, ct);
                if (table is not null) table.Status = TableStatus.Occupied;
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return order;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Faqat Open/InProgress buyurtmani yopadi (to'lov qabul qilindi) va stolni
    /// bo'shatadi — hammasi bitta tranzaksiyada. Allaqachon yopilgan buyurtma
    /// qayta yopilmaydi (ikki marta pul qabul qilinmasligi uchun).
    /// </summary>
    public async Task<(bool Success, string? Error, Order? Order)> CloseAtomicallyAsync(
        long orderId, PaymentType paymentType, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var order = await _db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);

            if (order is null)
            {
                await tx.RollbackAsync(ct);
                return (false, "Buyurtma topilmadi.", null);
            }

            if (order.Status is OrderStatus.Completed or OrderStatus.Cancelled)
            {
                await tx.RollbackAsync(ct);
                return (false, "Buyurtma allaqachon yopilgan.", null);
            }

            order.Status = OrderStatus.Completed;
            order.ClosedAt = DateTime.UtcNow;
            order.PaymentType = paymentType;

            if (order.TableId.HasValue)
            {
                var table = await _db.Tables.FindAsync(new object[] { order.TableId.Value }, ct);
                if (table is not null) table.Status = TableStatus.Free;
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (true, null, order);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
