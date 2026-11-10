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

    public Task<List<Order>> GetOpenByBusinessAsync(long businessId, CancellationToken ct = default) =>
        _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Table)
            .Where(o => o.BusinessId == businessId
                        && (o.Status == OrderStatus.Open || o.Status == OrderStatus.InProgress))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

    public Task<Product?> GetProductByIdAsync(long productId, CancellationToken ct = default) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);

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
        // Fuzzy matching: "Lag'mon" -> "lagmon 100" kabi boshlanishlarni ham
        // ushlaydi. Avval aniq nom/alias, so'ng boshlanish (startsWith) bo'yicha.
        var products = await _db.Products
            .Where(p => p.BusinessId == businessId)
            .ToListAsync(ct);

        var exact = NormalizeName(spokenName);

        var exactMatch = products.FirstOrDefault(p =>
            NormalizeName(p.Name) == exact ||
            p.Aliases.Any(a => NormalizeName(a) == exact));
        if (exactMatch is not null) return exactMatch;

        // Eng qisqa nomli mahsulotni tanlaymiz ("lagmon" -> "Lag'mon 25",
        // "lagmon 100" emas — eng yaqin moslik).
        return products
            .Where(p => NormalizeName(p.Name).StartsWith(exact) ||
                        p.Aliases.Any(a => NormalizeName(a).StartsWith(exact)))
            .OrderBy(p => p.Name.Length)
            .ThenBy(p => p.Id)
            .FirstOrDefault();
    }

    // "Lag'mon  25 " -> "lagmon 25", "Coca-Cola 100" -> "coca cola 100":
    // kichik harf, apostrof va tire olib tashlanadi, bo'shliqlar siqiladi.
    private static string NormalizeName(string value) =>
        string.Join(' ', value.Trim().ToLowerInvariant()
            .Replace('’', '\'')
            .Replace('-', ' ')
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Replace("'", "")));

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

            await FreeTableIfNoOpenAsync(order.TableId, order.Id, ct);

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

    public async Task<(bool Success, string? Error, Order? Order)> CancelAtomicallyAsync(
        long orderId, string? reason, CancellationToken ct = default)
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

            var logReason = string.IsNullOrWhiteSpace(reason)
                ? "Bekor qilindi (oshxona)"
                : $"Bekor qilindi (oshxona): {reason.Trim()}";
            await RestoreStockAsync(order, logReason, ct);

            order.Status = OrderStatus.Cancelled;
            order.ClosedAt = DateTime.UtcNow;

            await FreeTableIfNoOpenAsync(order.TableId, order.Id, ct);

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

    public async Task<(bool Success, string? Error, Order? Order)> RemoveItemAtomicallyAsync(
        long orderId, long orderItemId, CancellationToken ct = default)
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

            var item = order.Items.FirstOrDefault(i => i.Id == orderItemId);
            if (item is null)
            {
                await tx.RollbackAsync(ct);
                return (false, "Buyurtma qatori topilmadi.", null);
            }

            if (item.ProductId.HasValue)
            {
                var product = await _db.Products.FindAsync(new object[] { item.ProductId.Value }, ct);
                if (product?.StockQuantity is not null)
                {
                    product.StockQuantity += item.Quantity;
                    product.UpdatedAt = DateTime.UtcNow;
                    _db.InventoryTransactions.Add(new InventoryTransaction
                    {
                        BusinessId = order.BusinessId,
                        ProductId = product.Id,
                        Type = InventoryTransactionType.In,
                        Quantity = item.Quantity,
                        Reason = $"Tarkibdan o'chirildi (oshxona): {item.ProductNameSpoken}",
                        CreatedAt = DateTime.UtcNow,
                    });
                }
            }

            _db.OrderItems.Remove(item);
            order.Items.Remove(item);
            order.TotalAmount = order.Items.Sum(i => i.LineTotal);

            if (order.Items.Count == 0)
            {
                order.Status = OrderStatus.Cancelled;
                order.ClosedAt = DateTime.UtcNow;
                await FreeTableIfNoOpenAsync(order.TableId, order.Id, ct);
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

    private async Task RestoreStockAsync(Order order, string reason, CancellationToken ct)
    {
        foreach (var item in order.Items)
        {
            if (!item.ProductId.HasValue) continue;
            var product = await _db.Products.FindAsync(new object[] { item.ProductId.Value }, ct);
            if (product?.StockQuantity is null) continue;

            product.StockQuantity += item.Quantity;
            product.UpdatedAt = DateTime.UtcNow;
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                BusinessId = order.BusinessId,
                ProductId = product.Id,
                Type = InventoryTransactionType.In,
                Quantity = item.Quantity,
                Reason = reason,
                CreatedAt = DateTime.UtcNow,
            });
        }
    }

    private async Task FreeTableIfNoOpenAsync(long? tableId, long excludeOrderId, CancellationToken ct)
    {
        if (!tableId.HasValue) return;
        var hasOtherOpen = await _db.Orders.AnyAsync(o =>
            o.TableId == tableId.Value && o.Id != excludeOrderId &&
            (o.Status == OrderStatus.Open || o.Status == OrderStatus.InProgress), ct);
        if (hasOtherOpen) return;
        var table = await _db.Tables.FindAsync(new object[] { tableId.Value }, ct);
        if (table is not null) table.Status = TableStatus.Free;
    }
}
