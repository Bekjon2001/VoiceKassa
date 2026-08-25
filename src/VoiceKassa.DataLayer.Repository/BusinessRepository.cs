using Microsoft.EntityFrameworkCore;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.DataLayer;
using VoiceKassa.Domain.Entities;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.DataLayer.Repository;

public class BusinessRepository : IBusinessRepository
{
    private readonly AppDbContext _db;

    public BusinessRepository(AppDbContext db) => _db = db;

    public async Task<RestaurantOwner> CreateRestaurantOwnerAsync(RestaurantOwner owner, CancellationToken ct = default)
    {
        _db.RestaurantOwners.Add(owner);
        await _db.SaveChangesAsync(ct);
        return owner;
    }

    public Task<RestaurantOwner?> GetOwnerByLoginAsync(string login, CancellationToken ct = default) =>
        _db.RestaurantOwners.FirstOrDefaultAsync(o => o.Login == login && o.IsActive, ct);

    public Task<RestaurantOwner?> GetOwnerByTokenAsync(string token, CancellationToken ct = default) =>
        _db.RestaurantOwners.FirstOrDefaultAsync(o => o.AccessToken == token && o.IsActive, ct);

    public async Task<Business> CreateBusinessAsync(Business business, CancellationToken ct = default)
    {
        _db.Businesses.Add(business);
        await _db.SaveChangesAsync(ct);
        return business;
    }

    public Task<Business?> GetBusinessByIdAsync(long businessId, CancellationToken ct = default) =>
        _db.Businesses.FirstOrDefaultAsync(b => b.Id == businessId, ct);

    public Task<List<Business>> GetAllBusinessesAsync(CancellationToken ct = default) =>
        _db.Businesses.OrderBy(b => b.Name).ToListAsync(ct);

    public async Task<Staff> CreateStaffAsync(Staff staff, CancellationToken ct = default)
    {
        _db.StaffMembers.Add(staff);
        await _db.SaveChangesAsync(ct);
        return staff;
    }

    public Task<List<Staff>> GetStaffByBusinessAsync(long businessId, CancellationToken ct = default) =>
        _db.StaffMembers.Where(s => s.BusinessId == businessId).OrderBy(s => s.FullName).ToListAsync(ct);

    public async Task<Category> CreateCategoryAsync(Category category, CancellationToken ct = default)
    {
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        return category;
    }

    public Task<List<Category>> GetCategoriesByBusinessAsync(long businessId, CancellationToken ct = default) =>
        _db.Categories.Where(c => c.BusinessId == businessId).OrderBy(c => c.SortOrder).ToListAsync(ct);

    public async Task<Product> CreateProductAsync(Product product, CancellationToken ct = default)
    {
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        return product;
    }

    public Task<Product?> GetProductByIdAsync(long productId, CancellationToken ct = default) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);

    public Task<List<Product>> GetProductsByBusinessAsync(long businessId, CancellationToken ct = default) =>
        _db.Products.Where(p => p.BusinessId == businessId).OrderBy(p => p.Name).ToListAsync(ct);

    public Task<List<Product>> GetLowStockProductsAsync(long businessId, CancellationToken ct = default) =>
        _db.Products
            .Where(p => p.BusinessId == businessId && p.StockQuantity.HasValue && p.StockQuantity <= p.LowStockThreshold)
            .OrderBy(p => p.StockQuantity)
            .ToListAsync(ct);

    public async Task<bool> UpdateStockAsync(long productId, decimal newQuantity, CancellationToken ct = default)
    {
        var product = await _db.Products.FindAsync(new object[] { productId }, ct);
        if (product is null) return false;

        product.StockQuantity = newQuantity;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Table> CreateTableAsync(Table table, CancellationToken ct = default)
    {
        _db.Tables.Add(table);
        await _db.SaveChangesAsync(ct);
        return table;
    }

    public Task<List<Table>> GetTablesByBusinessAsync(long businessId, CancellationToken ct = default) =>
        _db.Tables.Where(t => t.BusinessId == businessId).OrderBy(t => t.Name).ToListAsync(ct);

    public async Task<bool> UpdateTableStatusAsync(long tableId, TableStatus status, CancellationToken ct = default)
    {
        var table = await _db.Tables.FindAsync(new object[] { tableId }, ct);
        if (table is null) return false;

        table.Status = status;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
