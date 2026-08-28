namespace VoiceKassa.Application.DTOs;

// ---------- Umumiy ----------

public class LoginRequest
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// ---------- Super Admin ----------

public class SuperAdminExistsResponse
{
    public bool Exists { get; set; }
}

public class CreateSuperAdminRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class SuperAdminLoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    // BusinessService ham shu DTO'ni ishlatadi, u yerda belgilanadi.
    public bool IsSuperAdmin { get; set; }
}

// ---------- Restoran + Egasi yaratish (Super Admin amali) ----------

public class CreateRestaurantRequest
{
    public string RestaurantName { get; set; } = string.Empty;
    public string OwnerFullName { get; set; } = string.Empty;
    public string OwnerPhoneNumber { get; set; } = string.Empty;
    public decimal SubscriptionAmount { get; set; }
    public DateTime PaymentPaidAt { get; set; }
    public int SubscriptionMonths { get; set; } = 1;
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RestaurantOwnerSummaryResponse
{
    public long OwnerId { get; set; }
    public long BusinessId { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public string OwnerFullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public decimal SubscriptionAmount { get; set; }
    public DateTime PaymentPaidAt { get; set; }
    public int SubscriptionMonths { get; set; }
    public DateTime SubscriptionEndsAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsSubscriptionActive => IsActive && SubscriptionEndsAt > DateTime.UtcNow;
}

// ---------- Restoran egasi login ----------

public class OwnerLoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public long BusinessId { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public string OwnerFullName { get; set; } = string.Empty;
    public bool IsSubscriptionActive { get; set; }
    public DateTime SubscriptionEndsAt { get; set; }

    // Restoran yaratilganda to'lov sanasini ham ko'rsatish uchun (BusinessService).
    public DateTime PaymentPaidAt { get; set; }
}
