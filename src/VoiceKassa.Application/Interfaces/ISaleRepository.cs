using VoiceKassa.Domain.Entities;

namespace VoiceKassa.Application.Interfaces;

public interface ISaleRepository
{
    Task<Sale> AddAsync(Sale sale, CancellationToken ct = default);

    Task<List<Sale>> GetByShopAndRangeAsync(
        Guid shopId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>Best-effort match of a spoken product name against known Products/Aliases.</summary>
    Task<Product?> FindProductByNameAsync(Guid shopId, string spokenName, CancellationToken ct = default);

    Task DecrementStockAsync(Guid productId, decimal quantity, CancellationToken ct = default);
}
