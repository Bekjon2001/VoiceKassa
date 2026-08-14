using Microsoft.EntityFrameworkCore;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.DataLayer;
using VoiceKassa.Domain.Entities;

namespace VoiceKassa.DataLayer.Repository;

public class ShopRepository : IShopRepository
{
    private readonly AppDbContext _db;

    public ShopRepository(AppDbContext db) => _db = db;

    public async Task<Shop> CreateShopAsync(Shop shop, CancellationToken ct = default)
    {
        _db.Shops.Add(shop);
        await _db.SaveChangesAsync(ct);
        return shop;
    }

    public Task<Shop?> GetShopByIdAsync(Guid shopId, CancellationToken ct = default) =>
        _db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, ct);

    public Task<List<Shop>> GetAllShopsAsync(CancellationToken ct = default) =>
        _db.Shops.OrderBy(s => s.Name).ToListAsync(ct);

    public async Task<Cashier> CreateCashierAsync(Cashier cashier, CancellationToken ct = default)
    {
        _db.Cashiers.Add(cashier);
        await _db.SaveChangesAsync(ct);
        return cashier;
    }

    public Task<List<Cashier>> GetCashiersByShopAsync(Guid shopId, CancellationToken ct = default) =>
        _db.Cashiers.Where(c => c.ShopId == shopId).OrderBy(c => c.FullName).ToListAsync(ct);

    public async Task<Product> CreateProductAsync(Product product, CancellationToken ct = default)
    {
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        return product;
    }

    public Task<Product?> GetProductByIdAsync(Guid productId, CancellationToken ct = default) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);

    public Task<List<Product>> GetProductsByShopAsync(Guid shopId, CancellationToken ct = default) =>
        _db.Products.Where(p => p.ShopId == shopId).OrderBy(p => p.Name).ToListAsync(ct);

    public Task<List<Product>> GetLowStockProductsAsync(Guid shopId, CancellationToken ct = default) =>
        _db.Products
            .Where(p => p.ShopId == shopId && p.StockQuantity <= p.LowStockThreshold)
            .OrderBy(p => p.StockQuantity)
            .ToListAsync(ct);

    public async Task<bool> UpdateStockAsync(Guid productId, decimal newQuantity, CancellationToken ct = default)
    {
        var product = await _db.Products.FindAsync(new object[] { productId }, ct);
        if (product is null) return false;

        product.StockQuantity = newQuantity;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
