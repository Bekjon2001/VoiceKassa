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

        // Bir xil nomli FAOL restoran bo'lsa yaratish taqiqlanadi. Eski
        // restoran passiv qilingan bo'lsa - shu nomni qayta ishlatish mumkin.
        var normalizedName = request.RestaurantName.Trim().ToLowerInvariant();
        var existingBusinesses = await _repo.GetAllBusinessesAsync(ct);
        if (existingBusinesses.Any(b => b.IsActive &&
                b.Name.Trim().ToLowerInvariant() == normalizedName))
            return (false, "Bu nomdagi faol restoran allaqachon mavjud. Boshqa nom tanlang yoki eski restoranni faollashtiring.", null);

        return await CreateBusinessWithOwnerCoreAsync(
            name: request.RestaurantName, address: request.Address, phone: request.RestaurantPhoneNumber,
            type: BusinessType.Restaurant, ownerFullName: request.OwnerFullName,
            ownerPhoneNumber: request.OwnerPhoneNumber, subscriptionAmount: request.SubscriptionAmount,
            paymentPaidAt: request.PaymentPaidAt, subscriptionMonths: request.SubscriptionMonths,
            login: request.Login, password: request.Password, ct);
    }

    /// <summary>
    /// Supermarket + egasini yaratish (Restoran bilan bir xil umumiy oqim,
    /// farq faqat Business turida — Market). Backend bitta funksiya bilan
    /// ikkala turga xizmat qiladi.
    /// </summary>
    public async Task<(bool Success, string? Error, OwnerLoginResponse? Owner)> CreateMarketWithOwnerAsync(
        CreateMarketWithOwnerRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.MarketName) || string.IsNullOrWhiteSpace(request.OwnerFullName) ||
            string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
            return (false, "Supermarket, xo'jayin, login va parol majburiy.", null);
        if (request.SubscriptionMonths < 1 || request.SubscriptionAmount < 0 || request.PaymentPaidAt == default)
            return (false, "Obuna oy va to'lov qiymati noto'g'ri.", null);
        if (await _repo.GetOwnerByLoginAsync(request.Login.Trim(), ct) is not null)
            return (false, "Bu login allaqachon band.", null);

        // Bir xil nomli FAOL biznes bo'lsa yaratish taqiqlanadi. Eski
        // biznes passiv qilingan bo'lsa - shu nomni qayta ishlatish mumkin.
        var normalizedName = request.MarketName.Trim().ToLowerInvariant();
        var existingBusinesses = await _repo.GetAllBusinessesAsync(ct);
        if (existingBusinesses.Any(b => b.IsActive &&
                b.Name.Trim().ToLowerInvariant() == normalizedName))
            return (false, "Bu nomdagi faol supermarket allaqachon mavjud. Boshqa nom tanlang yoki eski supermarketni faollashtiring.", null);

        return await CreateBusinessWithOwnerCoreAsync(
            name: request.MarketName, address: request.Address, phone: request.MarketPhoneNumber,
            type: BusinessType.Market, ownerFullName: request.OwnerFullName, ownerPhoneNumber: request.OwnerPhoneNumber,
            subscriptionAmount: request.SubscriptionAmount, paymentPaidAt: request.PaymentPaidAt,
            subscriptionMonths: request.SubscriptionMonths, login: request.Login, password: request.Password, ct);
    }

    /// <summary>
    /// Umumiy yordamchi: Restoran yoki Supermarket + ega yaratish. Bitta
    /// funksiya ikkala biznes turiga xizmat qiladi (Restaurant | Market).
    /// </summary>
    private async Task<(bool Success, string? Error, OwnerLoginResponse? Owner)> CreateBusinessWithOwnerCoreAsync(
        string name, string? address, string? phone, BusinessType type,
        string ownerFullName, string ownerPhoneNumber, decimal subscriptionAmount,
        DateTime paymentPaidAt, int subscriptionMonths, string login, string password,
        CancellationToken ct = default)
    {
        var business = await _repo.CreateBusinessAsync(new Business
        {
            Name = name.Trim(), Type = type,
            Address = address, PhoneNumber = phone,
        }, ct);
        var owner = await _repo.CreateRestaurantOwnerAsync(new RestaurantOwner
        {
            BusinessId = business.Id, FullName = ownerFullName.Trim(),
            PhoneNumber = ownerPhoneNumber.Trim(), Login = login.Trim(),
            PasswordHash = HashPassword(password), SubscriptionAmount = subscriptionAmount,
            PaymentPaidAt = paymentPaidAt.ToUniversalTime(),
            SubscriptionMonths = subscriptionMonths,
            SubscriptionEndsAt = DateTime.UtcNow.AddMonths(subscriptionMonths),
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
        // Passiv egani ham ko'rsatamiz — Super Admin "Faollashtirish" qila oladi.
        var owner = await _repo.GetOwnerByBusinessIdAnyStateAsync(businessId, ct);
        var business = await _repo.GetBusinessByIdAsync(businessId, ct);
        if (owner is null || business is null) return null;
        return new OwnerAdminResponse
        {
            BusinessId = business.Id, RestaurantName = business.Name, OwnerFullName = owner.FullName,
            OwnerPhoneNumber = owner.PhoneNumber, Login = owner.Login,
            SubscriptionAmount = owner.SubscriptionAmount, PaymentPaidAt = owner.PaymentPaidAt,
            SubscriptionMonths = owner.SubscriptionMonths, SubscriptionEndsAt = owner.SubscriptionEndsAt,
            IsActive = owner.IsActive,
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

    /// <summary>
    /// Super Admin eganing login va/yoki parolini restoranni qayta
    /// yaratmasdan tiklaydi. Ikkala maydon ham bo'sh bo'lsa xato qaytadi.
    /// Login o'zgarsa boshqa egalar bilan to'qnashuvi tekshiriladi.
    /// </summary>
    public async Task<(bool Success, string? Error, OwnerAdminResponse? Owner)> ResetOwnerCredentialsAsync(
        ResetOwnerCredentialsRequest request, CancellationToken ct = default)
    {
        var owner = await _repo.GetOwnerByBusinessIdAnyStateAsync(request.BusinessId, ct);
        if (owner is null) return (false, "Bu biznes uchun egalar topilmadi.", null);

        var newLogin = request.NewLogin?.Trim();
        var hasNewLogin = !string.IsNullOrWhiteSpace(newLogin) &&
                          !string.Equals(newLogin, owner.Login, StringComparison.OrdinalIgnoreCase);
        var hasNewPassword = !string.IsNullOrWhiteSpace(request.NewPassword);

        if (!hasNewLogin && !hasNewPassword)
            return (false, "Yangi login yoki yangi parol kiritilishi kerak.", null);

        if (hasNewLogin && await _repo.IsOwnerLoginTakenAsync(newLogin!, owner.BusinessId, ct))
            return (false, "Bu login boshqa restoran egasida band.", null);

        var newHash = hasNewPassword ? HashPassword(request.NewPassword!.Trim()) : null;
        if (!await _repo.UpdateOwnerCredentialsAsync(owner.Id, hasNewLogin ? newLogin : null, newHash, ct))
            return (false, "Ma'lumotni saqlab bo'lmadi.", null);

        return (true, null, await GetOwnerAdminAsync(request.BusinessId, ct));
    }

    /// <summary>
    /// Restoran (egasi) akkountini passivlashtirish/faollashtirish. Passiv
    /// ega tizimga kira olmaydi, lekin obuna ma'lumotlari saqlanib qoladi.
    /// </summary>
    public async Task<(bool Success, string? Error, OwnerAdminResponse? Owner)> SetOwnerActiveAsync(
        UpdateOwnerStatusRequest request, CancellationToken ct = default)
    {
        var owner = await _repo.GetOwnerByBusinessIdAnyStateAsync(request.BusinessId, ct);
        if (owner is null) return (false, "Bu biznes uchun egalar topilmadi.", null);

        if (owner.IsActive != request.IsActive)
        {
            if (!await _repo.UpdateOwnerActiveAsync(owner.Id, request.IsActive, ct))
                return (false, "Holatni o'zgartirib bo'lmadi.", null);
        }

        return (true, null, await GetOwnerAdminAsync(request.BusinessId, ct));
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

        var firstName = string.IsNullOrWhiteSpace(request.FirstName) ? "" : request.FirstName.Trim();
        var lastName = string.IsNullOrWhiteSpace(request.LastName) ? "" : request.LastName.Trim();
        var fullName = string.IsNullOrWhiteSpace(request.FullName)
            ? string.Join(" ", new[] { firstName, lastName }.Where(x => x != "")).Trim()
            : request.FullName.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            return (false, "Xodim ismi bo'sh bo'lishi mumkin emas.", null);
        if (request.MonthlySalary < 0)
            return (false, "Oylik manfiy bo'lishi mumkin emas.", null);

        var staff = new Staff
        {
            BusinessId = request.BusinessId,
            FullName = fullName,
            FirstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName,
            LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName,
            PhoneNumber = request.PhoneNumber,
            Role = request.Role,
            Age = request.Age,
            MonthlySalary = request.MonthlySalary,
            HireDate = request.HireDate,
            IsActive = true,
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

    /// <summary>Oylikni yangilaydi va tarixga yozuv qo'shadi.</summary>
    public async Task<(bool Success, string? Error, StaffResponse? Staff)> UpdateStaffSalaryAsync(
        long staffId, UpdateStaffSalaryRequest request, CancellationToken ct = default)
    {
        if (request.NewSalary < 0) return (false, "Yangi oylik manfiy bo'lishi mumkin emas.", null);
        var staff = await _repo.UpdateStaffSalaryAsync(staffId, request.NewSalary, request.Reason, ct);
        if (staff is null) return (false, "Xodim topilmadi.", null);
        return (true, null, ToStaffResponse(staff));
    }

    /// <summary>Ishdan bo'shatish / faollashtirish (FiredAt avtomatik boshqariladi).</summary>
    public async Task<(bool Success, string? Error, StaffResponse? Staff)> SetStaffActiveAsync(
        long staffId, bool isActive, CancellationToken ct = default)
    {
        var staff = await _repo.UpdateStaffActiveAsync(staffId, isActive, null, ct);
        if (staff is null) return (false, "Xodim topilmadi.", null);
        return (true, null, ToStaffResponse(staff));
    }

    /// <summary>Xodimning biznesi owner tokenga tegishli ekanini tekshiradi.</summary>
    public async Task<(bool Success, string? Error)> AuthorizeStaffAsync(
        long staffId, string? token, CancellationToken ct = default)
    {
        var staff = await _repo.GetStaffByIdAsync(staffId, ct);
        if (staff is null) return (false, "Xodim topilmadi.");
        var owner = await AuthorizeOwnerAsync(staff.BusinessId, token, ct);
        return (owner.Success, owner.Error);
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
        Id = s.Id, BusinessId = s.BusinessId, FullName = s.FullName ?? "",
        FirstName = s.FirstName, LastName = s.LastName,
        PhoneNumber = s.PhoneNumber, Role = s.Role, IsActive = s.IsActive,
        Age = s.Age, MonthlySalary = s.MonthlySalary,
        HireDate = s.HireDate, FiredAt = s.FiredAt,
        SalaryHistory = (s.SalaryHistory ?? new List<SalaryHistory>())
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new SalaryHistoryResponse
            {
                Id = h.Id, ChangedAt = h.ChangedAt,
                OldSalary = h.OldSalary, NewSalary = h.NewSalary, Reason = h.Reason,
            })
            .ToList(),
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
