using Microsoft.EntityFrameworkCore;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.DataLayer;
using VoiceKassa.Domain.Entities;

namespace VoiceKassa.DataLayer.Repository;

public class SaleRepository : ISaleRepository
{
    private readonly AppDbContext _db;

    public SaleRepository(AppDbContext db) => _db = db;

    public async Task<Sale> AddAsync(Sale sale, CancellationToken ct = default)
    {
        _db.Sales.Add(sale);
        await _db.SaveChangesAsync(ct);
        return sale;
    }

    public async Task<List<Sale>> GetByShopAndRangeAsync(
        Guid shopId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        return await _db.Sales
            .Include(s => s.Items)
            .Where(s => s.ShopId == shopId && s.CreatedAt >= fromUtc && s.CreatedAt < toUtc)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Product?> FindProductByNameAsync(Guid shopId, string spokenName, CancellationToken ct = default)
    {
        var normalized = spokenName.Trim().ToLowerInvariant();

        // Exact name match first, then alias match. Fuzzy/phonetic matching
        // (Levenshtein, trigram search via pg_trgm) is a good next upgrade
        // once real spoken-name variance data comes in.
        var product = await _db.Products
            .Where(p => p.ShopId == shopId)
            .ToListAsync(ct);

        return product.FirstOrDefault(p =>
            p.Name.Trim().ToLowerInvariant() == normalized ||
            p.Aliases.Any(a => a.Trim().ToLowerInvariant() == normalized));
    }

    public async Task DecrementStockAsync(Guid productId, decimal quantity, CancellationToken ct = default)
    {
        var product = await _db.Products.FindAsync(new object[] { productId }, ct);
        if (product is null) return;

        product.StockQuantity -= quantity;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
