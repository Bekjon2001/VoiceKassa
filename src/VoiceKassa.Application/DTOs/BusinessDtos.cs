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

public class CreateRestaurantWithOwnerRequest
{
    public string RestaurantName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? RestaurantPhoneNumber { get; set; }
    public string OwnerFullName { get; set; } = string.Empty;
    public string OwnerPhoneNumber { get; set; } = string.Empty;
    public decimal SubscriptionAmount { get; set; }
    public DateTime PaymentPaidAt { get; set; }
    public int SubscriptionMonths { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Supermarket va uning egasini yaratish so'rovi.
/// Backend umumiy oqimdan foydalanadi — Restoran yol'q,
/// faqat hosil bo'lgan Business turi Market bo'ladi.
/// </summary>
public class CreateMarketWithOwnerRequest
{
    public string MarketName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? MarketPhoneNumber { get; set; }
    public string OwnerFullName { get; set; } = string.Empty;
    public string OwnerPhoneNumber { get; set; } = string.Empty;
    public decimal SubscriptionAmount { get; set; }
    public DateTime PaymentPaidAt { get; set; }
    public int SubscriptionMonths { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class OwnerLoginRequest
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class SuperAdminLoginRequest
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// Qadrdonlar: OwnerLoginRequest va SuperAdminLoginRequest shu faylda qoladi.
// Boshqalar (CreateSuperAdminRequest, SuperAdminLoginResponse, OwnerLoginResponse)
// faqat AuthDtos.cs'da joylashgan — ikki marta takrorlanganda CS0101 bilan
// build buzilgandi.

public class OwnerAdminResponse
{
    public long BusinessId { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public string OwnerFullName { get; set; } = string.Empty;
    public string OwnerPhoneNumber { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public decimal SubscriptionAmount { get; set; }
    public DateTime PaymentPaidAt { get; set; }
    public int SubscriptionMonths { get; set; }
    public DateTime SubscriptionEndsAt { get; set; }

    // Super Admin panelda "Passiv/Faol" pill ko'rsatish uchun.
    public bool IsActive { get; set; } = true;
}

// ---------- Super Admin: egani boshqarish ----------

/// <summary>Restoran qayta yaratilmasdan login va/yoki parolni tiklash.</summary>
public class ResetOwnerCredentialsRequest
{
    public long BusinessId { get; set; }

    /// <summary>null/bo'sh bo'lsa login o'zgarmaydi.</summary>
    public string? NewLogin { get; set; }

    /// <summary>null/bo'sh bo'lsa parol o'zgarmaydi.</summary>
    public string? NewPassword { get; set; }
}

/// <summary>Restoran (egasi) akkountini passivlashtirish/faollashtirish.</summary>
public class UpdateOwnerStatusRequest
{
    public long BusinessId { get; set; }
    public bool IsActive { get; set; }
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
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public StaffRole Role { get; set; } = StaffRole.Cashier;
    public int? Age { get; set; }
    public decimal MonthlySalary { get; set; }
    public DateTime? HireDate { get; set; }
}

public class StaffResponse
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public StaffRole Role { get; set; }
    public bool IsActive { get; set; }
    public int? Age { get; set; }
    public decimal MonthlySalary { get; set; }
    public DateTime? HireDate { get; set; }
    public DateTime? FiredAt { get; set; }
    public List<SalaryHistoryResponse> SalaryHistory { get; set; } = new();
}

/// <summary>Maosh tarixi yozuvi.</summary>
public class SalaryHistoryResponse
{
    public long Id { get; set; }
    public DateTime ChangedAt { get; set; }
    public decimal OldSalary { get; set; }
    public decimal NewSalary { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Xodim oyligini o'zgartirish (tarixga avtomatik qo'shiladi).</summary>
public class UpdateStaffSalaryRequest
{
    public decimal NewSalary { get; set; }
    public string? Reason { get; set; }
}

public class UpdateStaffStatusRequest
{
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
