using VoiceKassa.Domain.Entities;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Application.Interfaces;

/// <summary>
/// Master-data qatlami: Business, Staff, Category, Product, Table.
/// IOrderRepository'dan alohida - bu yerda buyurtma/savdo oqimi emas,
/// katalog/tashkiliy ma'lumotlar boshqariladi.
/// </summary>
public interface IBusinessRepository
{
    Task<bool> HasSuperAdminAsync(CancellationToken ct = default);
    Task<UserAccount> CreateUserAccountAsync(UserAccount account, CancellationToken ct = default);
    Task<UserAccount?> GetUserByLoginAsync(string login, CancellationToken ct = default);
    Task<UserAccount?> GetSuperAdminByTokenAsync(string token, CancellationToken ct = default);
    Task<RestaurantOwner> CreateRestaurantOwnerAsync(RestaurantOwner owner, CancellationToken ct = default);
    Task<RestaurantOwner?> GetOwnerByLoginAsync(string login, CancellationToken ct = default);
    Task<RestaurantOwner?> GetOwnerByTokenAsync(string token, CancellationToken ct = default);
    Task<RestaurantOwner?> GetOwnerByBusinessIdAsync(long businessId, CancellationToken ct = default);
    Task<Business> CreateBusinessAsync(Business business, CancellationToken ct = default);
    Task<Business?> GetBusinessByIdAsync(long businessId, CancellationToken ct = default);
    Task<List<Business>> GetAllBusinessesAsync(CancellationToken ct = default);

    Task<Staff> CreateStaffAsync(Staff staff, CancellationToken ct = default);
    Task<List<Staff>> GetStaffByBusinessAsync(long businessId, CancellationToken ct = default);
    Task<bool> UpdateStaffStatusAsync(long staffId, bool isActive, CancellationToken ct = default);

    Task<Category> CreateCategoryAsync(Category category, CancellationToken ct = default);
    Task<List<Category>> GetCategoriesByBusinessAsync(long businessId, CancellationToken ct = default);

    Task<Product> CreateProductAsync(Product product, CancellationToken ct = default);
    Task<Product?> GetProductByIdAsync(long productId, CancellationToken ct = default);
    Task<List<Product>> GetProductsByBusinessAsync(long businessId, CancellationToken ct = default);
    Task<List<Product>> GetLowStockProductsAsync(long businessId, CancellationToken ct = default);
    Task<bool> UpdateStockAsync(long productId, decimal newQuantity, CancellationToken ct = default);

    Task<Table> CreateTableAsync(Table table, CancellationToken ct = default);
    Task<List<Table>> GetTablesByBusinessAsync(long businessId, CancellationToken ct = default);
    Task<bool> UpdateTableStatusAsync(long tableId, TableStatus status, CancellationToken ct = default);
}
