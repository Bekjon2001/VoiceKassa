using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.Domain.Entities;
using VoiceKassa.Domain.Enums;
using System.Security.Cryptography;
using System.Text;

namespace VoiceKassa.Application.Services;

/// <summary>
/// Business, Staff, Category, Product, Table bo'yicha use case'lar.
/// OrderService'dan alohida - bu yerda AI ishtirok etmaydi, faqat
/// oddiy CRUD orkestratsiyasi.
/// </summary>
public class BusinessService
{
    private readonly IBusinessRepository _repo;

    public BusinessService(IBusinessRepository repo) => _repo = repo;

    public async Task<(bool Success, string? Error, SuperAdminLoginResponse? Account)> CreateFirstSuperAdminAsync(
        CreateSuperAdminRequest request, CancellationToken ct = default)
    {
        if (await _repo.HasSuperAdminAsync(ct)) return (false, "Super Admin allaqachon mavjud.", null);
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
            return (false, "F.I.SH, login va parol majburiy.", null);
        if (await _repo.GetUserByLoginAsync(request.Login.Trim(), ct) is not null)
            return (false, "Bu login allaqachon band.", null);

        var account = await _repo.CreateUserAccountAsync(new UserAccount
        {
            FullName = request.FullName.Trim(), PhoneNumber = request.PhoneNumber?.Trim(),
            Login = request.Login.Trim(), PasswordHash = HashPassword(request.Password),
            IsActive = true, IsSuperAdmin = true,
            AccessToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        }, ct);
        return (true, null, new SuperAdminLoginResponse { FullName = account.FullName, AccessToken = account.AccessToken, IsSuperAdmin = true });
    }

    public async Task<(bool Success, string? Error, SuperAdminLoginResponse? Account)> LoginSuperAdminAsync(
        SuperAdminLoginRequest request, CancellationToken ct = default)
    {
        var account = await _repo.GetUserByLoginAsync(request.Login.Trim(), ct);
        if (account is null || !account.IsSuperAdmin || !VerifyPassword(request.Password, account.PasswordHash))
            return (false, "Super Admin login yoki paroli noto'g'ri.", null);
        return (true, null, new SuperAdminLoginResponse { FullName = account.FullName, AccessToken = account.AccessToken, IsSuperAdmin = true });
    }

    public async Task<bool> IsSuperAdminTokenAsync(string? token, CancellationToken ct = default) =>
        !string.IsNullOrWhiteSpace(token) && await _repo.GetSuperAdminByTokenAsync(token, ct) is not null;

    public Task<bool> HasSuperAdminAsync(CancellationToken ct = default) => _repo.HasSuperAdminAsync(ct);

    public async Task<(bool Success, string? Error, OwnerLoginResponse? Owner)> CreateRestaurantWithOwnerAsync(
        CreateRestaurantWithOwnerRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RestaurantName) || string.IsNullOrWhiteSpace(request.OwnerFullName) ||
            string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
            return (false, "Restoran, xo'jayin, login va parol majburiy.", null);
        if (request.SubscriptionMonths < 1 || request.SubscriptionAmount < 0 || request.PaymentPaidAt == default)
            return (false, "Obuna oy va to'lov qiymati noto'g'ri.", null);
        if (await _repo.GetOwnerByLoginAsync(request.Login.Trim(), ct) is not null)
            return (false, "Bu login allaqachon band.", null);

        var business = await _repo.CreateBusinessAsync(new Business
        {
            Name = request.RestaurantName.Trim(), Type = BusinessType.Restaurant,
            Address = request.Address, PhoneNumber = request.RestaurantPhoneNumber,
        }, ct);
        var owner = await _repo.CreateRestaurantOwnerAsync(new RestaurantOwner
        {
            BusinessId = business.Id, FullName = request.OwnerFullName.Trim(),
            PhoneNumber = request.OwnerPhoneNumber.Trim(), Login = request.Login.Trim(),
            PasswordHash = HashPassword(request.Password), SubscriptionAmount = request.SubscriptionAmount,
            PaymentPaidAt = request.PaymentPaidAt.ToUniversalTime(),
            SubscriptionMonths = request.SubscriptionMonths,
            SubscriptionEndsAt = DateTime.UtcNow.AddMonths(request.SubscriptionMonths),
            AccessToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        }, ct);
        return (true, null, ToOwnerResponse(owner, business));
    }

    public async Task<(bool Success, string? Error, OwnerLoginResponse? Owner)> LoginOwnerAsync(
        OwnerLoginRequest request, CancellationToken ct = default)
    {
        var owner = await _repo.GetOwnerByLoginAsync(request.Login.Trim(), ct);
        if (owner is null || !VerifyPassword(request.Password, owner.PasswordHash))
            return (false, "Login yoki parol noto'g'ri.", null);
        if (owner.SubscriptionEndsAt <= DateTime.UtcNow)
            return (false, "Obuna muddati tugagan.", null);
        var business = await _repo.GetBusinessByIdAsync(owner.BusinessId, ct);
        return business is null ? (false, "Restoran topilmadi.", null) : (true, null, ToOwnerResponse(owner, business));
    }

    public async Task<OwnerAdminResponse?> GetOwnerAdminAsync(long businessId, CancellationToken ct = default)
    {
        var owner = await _repo.GetOwnerByBusinessIdAsync(businessId, ct);
        var business = await _repo.GetBusinessByIdAsync(businessId, ct);
        if (owner is null || business is null) return null;
        return new OwnerAdminResponse
        {
            BusinessId = business.Id, RestaurantName = business.Name, OwnerFullName = owner.FullName,
            OwnerPhoneNumber = owner.PhoneNumber, Login = owner.Login,
            SubscriptionAmount = owner.SubscriptionAmount, PaymentPaidAt = owner.PaymentPaidAt,
            SubscriptionMonths = owner.SubscriptionMonths, SubscriptionEndsAt = owner.SubscriptionEndsAt,
        };
    }

    public async Task<(bool Success, string? Error, RestaurantOwner? Owner)> AuthorizeOwnerAsync(
        long businessId, string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return (false, "Owner token kerak.", null);
        var owner = await _repo.GetOwnerByTokenAsync(token, ct);
        if (owner is null || owner.BusinessId != businessId) return (false, "Owner huquqi tasdiqlanmadi.", null);
        if (owner.SubscriptionEndsAt <= DateTime.UtcNow) return (false, "Obuna muddati tugagan.", null);
        return (true, null, owner);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 120_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split('.', 2);
        if (parts.Length != 2) return false;
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(parts[0]), 120_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(parts[1]));
    }

    private static OwnerLoginResponse ToOwnerResponse(RestaurantOwner owner, Business business) => new()
    {
        BusinessId = business.Id, RestaurantName = business.Name, OwnerFullName = owner.FullName,
        AccessToken = owner.AccessToken, SubscriptionEndsAt = owner.SubscriptionEndsAt,
        PaymentPaidAt = owner.PaymentPaidAt,
    };

    public async Task<BusinessResponse> CreateBusinessAsync(CreateBusinessRequest request, CancellationToken ct = default)
    {
        var business = new Business
        {
            Name = request.Name,
            Type = request.Type,
            Address = request.Address,
            PhoneNumber = request.PhoneNumber,
        };

        var saved = await _repo.CreateBusinessAsync(business, ct);
        return ToBusinessResponse(saved);
    }

    public async Task<BusinessResponse?> GetBusinessAsync(long businessId, CancellationToken ct = default)
    {
        var business = await _repo.GetBusinessByIdAsync(businessId, ct);
        return business is null ? null : ToBusinessResponse(business);
    }

    public async Task<List<BusinessResponse>> GetAllBusinessesAsync(CancellationToken ct = default)
    {
        var businesses = await _repo.GetAllBusinessesAsync(ct);
        return businesses.Select(ToBusinessResponse).ToList();
    }

    public async Task<(bool Success, string? Error, StaffResponse? Staff)> CreateStaffAsync(
        CreateStaffRequest request, CancellationToken ct = default)
    {
        var business = await _repo.GetBusinessByIdAsync(request.BusinessId, ct);
        if (business is null) return (false, "Bunday biznes topilmadi.", null);
        if (string.IsNullOrWhiteSpace(request.FullName)) return (false, "Xodim ismi bo'sh bo'lishi mumkin emas.", null);

        var staff = new Staff
        {
            BusinessId = request.BusinessId,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Role = request.Role,
        };

        var saved = await _repo.CreateStaffAsync(staff, ct);
        return (true, null, ToStaffResponse(saved));
    }

    public async Task<List<StaffResponse>> GetStaffAsync(long businessId, CancellationToken ct = default)
    {
        var staff = await _repo.GetStaffByBusinessAsync(businessId, ct);
        return staff.Select(ToStaffResponse).ToList();
    }

    public async Task<(bool Success, string? Error)> UpdateStaffStatusAsync(
        long staffId, UpdateStaffStatusRequest request, CancellationToken ct = default)
    {
        var updated = await _repo.UpdateStaffStatusAsync(staffId, request.IsActive, ct);
        return updated ? (true, null) : (false, "Bunday admin topilmadi.");
    }

    public async Task<(bool Success, string? Error, CategoryResponse? Category)> CreateCategoryAsync(
        CreateCategoryRequest request, CancellationToken ct = default)
    {
        var business = await _repo.GetBusinessByIdAsync(request.BusinessId, ct);
        if (business is null) return (false, "Bunday biznes topilmadi.", null);

        var category = new Category
        {
            BusinessId = request.BusinessId,
            Name = request.Name,
            SortOrder = request.SortOrder,
        };

        var saved = await _repo.CreateCategoryAsync(category, ct);
        return (true, null, ToCategoryResponse(saved));
    }

    public async Task<List<CategoryResponse>> GetCategoriesAsync(long businessId, CancellationToken ct = default)
    {
        var categories = await _repo.GetCategoriesByBusinessAsync(businessId, ct);
        return categories.Select(ToCategoryResponse).ToList();
    }

    public async Task<(bool Success, string? Error, ProductResponse? Product)> CreateProductAsync(
        CreateProductRequest request, CancellationToken ct = default)
    {
        var business = await _repo.GetBusinessByIdAsync(request.BusinessId, ct);
        if (business is null) return (false, "Bunday biznes topilmadi.", null);
        if (string.IsNullOrWhiteSpace(request.Name)) return (false, "Mahsulot nomi bo'sh bo'lishi mumkin emas.", null);

        var product = new Product
        {
            BusinessId = request.BusinessId,
            CategoryId = request.CategoryId,
            Name = request.Name,
            Aliases = request.Aliases ?? new List<string>(),
            Unit = request.Unit,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            LowStockThreshold = request.LowStockThreshold,
        };

        var saved = await _repo.CreateProductAsync(product, ct);
        return (true, null, ToProductResponse(saved));
    }

    public async Task<List<ProductResponse>> GetProductsAsync(long businessId, CancellationToken ct = default)
    {
        var products = await _repo.GetProductsByBusinessAsync(businessId, ct);
        return products.Select(ToProductResponse).ToList();
    }

    public async Task<List<ProductResponse>> GetLowStockProductsAsync(long businessId, CancellationToken ct = default)
    {
        var products = await _repo.GetLowStockProductsAsync(businessId, ct);
        return products.Select(ToProductResponse).ToList();
    }

    public async Task<(bool Success, string? Error)> UpdateStockAsync(
        long productId, UpdateStockRequest request, CancellationToken ct = default)
    {
        var updated = await _repo.UpdateStockAsync(productId, request.NewQuantity, ct);
        return updated ? (true, null) : (false, "Bunday mahsulot topilmadi.");
    }

    public async Task<(bool Success, string? Error, TableResponse? Table)> CreateTableAsync(
        CreateTableRequest request, CancellationToken ct = default)
    {
        var business = await _repo.GetBusinessByIdAsync(request.BusinessId, ct);
        if (business is null) return (false, "Bunday biznes topilmadi.", null);

        var table = new Table
        {
            BusinessId = request.BusinessId,
            Name = request.Name,
            Capacity = request.Capacity,
        };

        var saved = await _repo.CreateTableAsync(table, ct);
        return (true, null, ToTableResponse(saved));
    }

    public async Task<List<TableResponse>> GetTablesAsync(long businessId, CancellationToken ct = default)
    {
        var tables = await _repo.GetTablesByBusinessAsync(businessId, ct);
        return tables.Select(ToTableResponse).ToList();
    }

    public async Task<(bool Success, string? Error)> UpdateTableStatusAsync(
        long tableId, UpdateTableStatusRequest request, CancellationToken ct = default)
    {
        var updated = await _repo.UpdateTableStatusAsync(tableId, request.Status, ct);
        return updated ? (true, null) : (false, "Bunday stol topilmadi.");
    }

    private static BusinessResponse ToBusinessResponse(Business b) => new()
    {
        Id = b.Id, Name = b.Name, Type = b.Type, Address = b.Address,
        PhoneNumber = b.PhoneNumber, CreatedAt = b.CreatedAt,
    };

    private static StaffResponse ToStaffResponse(Staff s) => new()
    {
        Id = s.Id, BusinessId = s.BusinessId, FullName = s.FullName,
        PhoneNumber = s.PhoneNumber, Role = s.Role, IsActive = s.IsActive,
    };

    private static CategoryResponse ToCategoryResponse(Category c) => new()
    {
        Id = c.Id, BusinessId = c.BusinessId, Name = c.Name, SortOrder = c.SortOrder,
    };

    private static ProductResponse ToProductResponse(Product p) => new()
    {
        Id = p.Id, BusinessId = p.BusinessId, CategoryId = p.CategoryId, Name = p.Name,
        Aliases = p.Aliases, Unit = p.Unit, Price = p.Price, StockQuantity = p.StockQuantity,
        LowStockThreshold = p.LowStockThreshold, IsAvailable = p.IsAvailable,
    };

    private static TableResponse ToTableResponse(Table t) => new()
    {
        Id = t.Id, BusinessId = t.BusinessId, Name = t.Name, Capacity = t.Capacity, Status = t.Status,
    };
}
