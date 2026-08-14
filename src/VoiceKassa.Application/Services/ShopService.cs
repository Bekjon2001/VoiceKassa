using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.Domain.Entities;

namespace VoiceKassa.Application.Services;

/// <summary>
/// Do'kon, kassir va mahsulot (katalog / master-data) bo'yicha use case'lar.
/// SaleService/QueryService'dan alohida - bu yerda AI ishtirok etmaydi,
/// faqat oddiy CRUD orkestratsiyasi.
/// </summary>
public class ShopService
{
    private readonly IShopRepository _repo;

    public ShopService(IShopRepository repo)
    {
        _repo = repo;
    }

    public async Task<ShopResponse> CreateShopAsync(CreateShopRequest request, CancellationToken ct = default)
    {
        var shop = new Shop
        {
            Name = request.Name,
            Address = request.Address,
        };

        var saved = await _repo.CreateShopAsync(shop, ct);
        return ToShopResponse(saved);
    }

    public async Task<ShopResponse?> GetShopAsync(Guid shopId, CancellationToken ct = default)
    {
        var shop = await _repo.GetShopByIdAsync(shopId, ct);
        return shop is null ? null : ToShopResponse(shop);
    }

    public async Task<List<ShopResponse>> GetAllShopsAsync(CancellationToken ct = default)
    {
        var shops = await _repo.GetAllShopsAsync(ct);
        return shops.Select(ToShopResponse).ToList();
    }

    public async Task<(bool Success, string? Error, CashierResponse? Cashier)> CreateCashierAsync(
        CreateCashierRequest request, CancellationToken ct = default)
    {
        var shop = await _repo.GetShopByIdAsync(request.ShopId, ct);
        if (shop is null)
            return (false, "Bunday do'kon topilmadi.", null);

        if (string.IsNullOrWhiteSpace(request.FullName))
            return (false, "Kassir ismi bo'sh bo'lishi mumkin emas.", null);

        var cashier = new Cashier
        {
            ShopId = request.ShopId,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
        };

        var saved = await _repo.CreateCashierAsync(cashier, ct);
        return (true, null, ToCashierResponse(saved));
    }

    public async Task<List<CashierResponse>> GetCashiersAsync(Guid shopId, CancellationToken ct = default)
    {
        var cashiers = await _repo.GetCashiersByShopAsync(shopId, ct);
        return cashiers.Select(ToCashierResponse).ToList();
    }

    public async Task<(bool Success, string? Error, ProductResponse? Product)> CreateProductAsync(
        CreateProductRequest request, CancellationToken ct = default)
    {
        var shop = await _repo.GetShopByIdAsync(request.ShopId, ct);
        if (shop is null)
            return (false, "Bunday do'kon topilmadi.", null);

        if (string.IsNullOrWhiteSpace(request.Name))
            return (false, "Mahsulot nomi bo'sh bo'lishi mumkin emas.", null);

        var product = new Product
        {
            ShopId = request.ShopId,
            Name = request.Name,
            Aliases = request.Aliases ?? new List<string>(),
            Unit = request.Unit,
            DefaultPrice = request.DefaultPrice,
            StockQuantity = request.StockQuantity,
            LowStockThreshold = request.LowStockThreshold,
        };

        var saved = await _repo.CreateProductAsync(product, ct);
        return (true, null, ToProductResponse(saved));
    }

    public async Task<List<ProductResponse>> GetProductsAsync(Guid shopId, CancellationToken ct = default)
    {
        var products = await _repo.GetProductsByShopAsync(shopId, ct);
        return products.Select(ToProductResponse).ToList();
    }

    /// <summary>Kassir/do'kon egasi "qaysi mahsulot tugayapti?" deb so'raganda ishlatiladi.</summary>
    public async Task<List<ProductResponse>> GetLowStockProductsAsync(Guid shopId, CancellationToken ct = default)
    {
        var products = await _repo.GetLowStockProductsAsync(shopId, ct);
        return products.Select(ToProductResponse).ToList();
    }

    public async Task<(bool Success, string? Error)> UpdateStockAsync(
        Guid productId, UpdateStockRequest request, CancellationToken ct = default)
    {
        var updated = await _repo.UpdateStockAsync(productId, request.NewQuantity, ct);
        return updated ? (true, null) : (false, "Bunday mahsulot topilmadi.");
    }

    private static ShopResponse ToShopResponse(Shop shop) => new()
    {
        Id = shop.Id,
        Name = shop.Name,
        Address = shop.Address,
        CreatedAt = shop.CreatedAt,
    };

    private static CashierResponse ToCashierResponse(Cashier cashier) => new()
    {
        Id = cashier.Id,
        ShopId = cashier.ShopId,
        FullName = cashier.FullName,
        PhoneNumber = cashier.PhoneNumber,
        IsActive = cashier.IsActive,
    };

    private static ProductResponse ToProductResponse(Product product) => new()
    {
        Id = product.Id,
        ShopId = product.ShopId,
        Name = product.Name,
        Aliases = product.Aliases,
        Unit = product.Unit,
        DefaultPrice = product.DefaultPrice,
        StockQuantity = product.StockQuantity,
        LowStockThreshold = product.LowStockThreshold,
    };
}
