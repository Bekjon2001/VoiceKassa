namespace VoiceKassa.Application.DTOs;

// ---------- Shop ----------

public class CreateShopRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
}

public class ShopResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ---------- Cashier ----------

public class CreateCashierRequest
{
    public Guid ShopId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}

public class CashierResponse
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
}

// ---------- Product ----------

public class CreateProductRequest
{
    public Guid ShopId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string>? Aliases { get; set; }
    public string Unit { get; set; } = "dona";
    public decimal? DefaultPrice { get; set; }
    public decimal StockQuantity { get; set; }
    public decimal LowStockThreshold { get; set; }
}

public class UpdateStockRequest
{
    public decimal NewQuantity { get; set; }
}

public class ProductResponse
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = new();
    public string Unit { get; set; } = string.Empty;
    public decimal? DefaultPrice { get; set; }
    public decimal StockQuantity { get; set; }
    public decimal LowStockThreshold { get; set; }
    public bool IsLowStock => StockQuantity <= LowStockThreshold;
}
