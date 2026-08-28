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

    public Task<bool> HasSuperAdminAsync(CancellationToken ct = default) =>
        _db.UserAccounts.AnyAsync(x => x.IsSuperAdmin && x.IsActive, ct);

    public async Task<UserAccount> CreateUserAccountAsync(UserAccount account, CancellationToken ct = default)
    {
        _db.UserAccounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return account;
    }

    public Task<UserAccount?> GetUserByLoginAsync(string login, CancellationToken ct = default) =>
        _db.UserAccounts.FirstOrDefaultAsync(x => x.Login == login && x.IsActive, ct);

    public Task<UserAccount?> GetSuperAdminByTokenAsync(string token, CancellationToken ct = default) =>
        _db.UserAccounts.FirstOrDefaultAsync(x => x.AccessToken == token && x.IsActive && x.IsSuperAdmin, ct);

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

    public Task<RestaurantOwner?> GetOwnerByBusinessIdAsync(long businessId, CancellationToken ct = default) =>
        _db.RestaurantOwners.FirstOrDefaultAsync(o => o.BusinessId == businessId && o.IsActive, ct);

    public Task<RestaurantOwner?> GetOwnerByBusinessIdAnyStateAsync(long businessId, CancellationToken ct = default) =>
        _db.RestaurantOwners.FirstOrDefaultAsync(o => o.BusinessId == businessId, ct);

    public Task<bool> IsOwnerLoginTakenAsync(string login, long excludeBusinessId, CancellationToken ct = default) =>
        _db.RestaurantOwners.AnyAsync(o => o.Login == login && o.BusinessId != excludeBusinessId, ct);

    public async Task<bool> UpdateOwnerCredentialsAsync(
        long ownerId, string? newLogin, string? newPasswordHash, CancellationToken ct = default)
    {
        var owner = await _db.RestaurantOwners.FindAsync(new object[] { ownerId }, ct);
        if (owner is null) return false;

        if (!string.IsNullOrWhiteSpace(newLogin)) owner.Login = newLogin;
        if (!string.IsNullOrWhiteSpace(newPasswordHash)) owner.PasswordHash = newPasswordHash;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateOwnerActiveAsync(long ownerId, bool isActive, CancellationToken ct = default)
    {
        var owner = await _db.RestaurantOwners.FindAsync(new object[] { ownerId }, ct);
        if (owner is null) return false;

        owner.IsActive = isActive;
        await _db.SaveChangesAsync(ct);
        return true;
    }

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
        _db.StaffMembers
            .Include(s => s.SalaryHistory)
            .Where(s => s.BusinessId == businessId)
            .OrderBy(s => s.FullName)
            .ToListAsync(ct);

    public Task<Staff?> GetStaffByIdAsync(long staffId, CancellationToken ct = default) =>
        _db.StaffMembers
            .Include(s => s.SalaryHistory)
            .FirstOrDefaultAsync(s => s.Id == staffId, ct);

    public async Task<bool> UpdateStaffStatusAsync(long staffId, bool isActive, CancellationToken ct = default)
    {
        var staff = await _db.StaffMembers.FindAsync(new object[] { staffId }, ct);
        if (staff is null) return false;
        staff.IsActive = isActive;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Staff?> UpdateStaffSalaryAsync(long staffId, decimal newSalary, string? reason, CancellationToken ct = default)
    {
        var staff = await _db.StaffMembers
            .Include(s => s.SalaryHistory)
            .FirstOrDefaultAsync(s => s.Id == staffId, ct);
        if (staff is null) return null;

        var oldSalary = staff.MonthlySalary;
        staff.MonthlySalary = newSalary;
        staff.SalaryHistory.Add(new SalaryHistory
        {
            StaffId = staff.Id,
            ChangedAt = DateTime.UtcNow,
            OldSalary = oldSalary,
            NewSalary = newSalary,
            Reason = reason,
        });
        await _db.SaveChangesAsync(ct);
        return staff;
    }

    public async Task<Staff?> UpdateStaffActiveAsync(long staffId, bool isActive, DateTime? firedAt, CancellationToken ct = default)
    {
        var staff = await _db.StaffMembers.FindAsync(new object[] { staffId }, ct);
        if (staff is null) return null;

        staff.IsActive = isActive;
        // Faollashtirishda FiredAt tozalanadi, bo'shatishda hozirgi vaqt yoziladi.
        staff.FiredAt = isActive ? null : (firedAt ?? DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return staff;
    }

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
