using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Application.DTOs;

// ---------- Business ----------

public class CreateBusinessRequest
{
    public string Name { get; set; } = string.Empty;
    public BusinessType Type { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
}

public class BusinessResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public BusinessType Type { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ---------- Staff ----------

public class CreateStaffRequest
{
    public long BusinessId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public StaffRole Role { get; set; } = StaffRole.Cashier;
}

public class StaffResponse
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public StaffRole Role { get; set; }
    public bool IsActive { get; set; }
}

// ---------- Category ----------

public class CreateCategoryRequest
{
    public long BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class CategoryResponse
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

// ---------- Product ----------

public class CreateProductRequest
{
    public long BusinessId { get; set; }
    public long? CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string>? Aliases { get; set; }
    public string Unit { get; set; } = "dona";
    public decimal Price { get; set; }

    // null qoldiring - restoran menu taomlari uchun (ombor kuzatilmaydi)
    public decimal? StockQuantity { get; set; }
    public decimal LowStockThreshold { get; set; }
}

public class UpdateStockRequest
{
    public decimal NewQuantity { get; set; }
}

public class ProductResponse
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public long? CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = new();
    public string Unit { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? StockQuantity { get; set; }
    public decimal LowStockThreshold { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsLowStock => StockQuantity.HasValue && StockQuantity <= LowStockThreshold;
}

// ---------- Table ----------

public class CreateTableRequest
{
    public long BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; } = 4;
}

public class UpdateTableStatusRequest
{
    public TableStatus Status { get; set; }
}

public class TableResponse
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public TableStatus Status { get; set; }
}
