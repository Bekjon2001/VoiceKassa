using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.Domain.Entities;
using VoiceKassa.Domain.Enums;

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
