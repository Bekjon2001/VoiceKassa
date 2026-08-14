using VoiceKassa.Domain.Entities;

namespace VoiceKassa.Application.Interfaces;

/// <summary>
/// Do'kon, kassir va mahsulotlar uchun asosiy CRUD shartnomasi.
/// ISaleRepository'dan alohida ushlanadi, chunki u faqat savdo (Sale)
/// oqimiga tegishli - bu esa katalog/master-data tomoni.
/// </summary>
public interface IShopRepository
{
    Task<Shop> CreateShopAsync(Shop shop, CancellationToken ct = default);
    Task<Shop?> GetShopByIdAsync(Guid shopId, CancellationToken ct = default);
    Task<List<Shop>> GetAllShopsAsync(CancellationToken ct = default);

    Task<Cashier> CreateCashierAsync(Cashier cashier, CancellationToken ct = default);
    Task<List<Cashier>> GetCashiersByShopAsync(Guid shopId, CancellationToken ct = default);

    Task<Product> CreateProductAsync(Product product, CancellationToken ct = default);
    Task<Product?> GetProductByIdAsync(Guid productId, CancellationToken ct = default);
    Task<List<Product>> GetProductsByShopAsync(Guid shopId, CancellationToken ct = default);
    Task<List<Product>> GetLowStockProductsAsync(Guid shopId, CancellationToken ct = default);
    Task<bool> UpdateStockAsync(Guid productId, decimal newQuantity, CancellationToken ct = default);
}
