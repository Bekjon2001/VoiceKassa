using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.Domain.Entities;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Application.Services;

/// <summary>
/// Super Admin va Restoran Egasi autentifikatsiyasi, shuningdek Super
/// Admin tomonidan yangi restoran+egasi yaratish oqimi.
///
/// Token boshqaruvi: to'liq JWT o'rniga oddiy tasodifiy token ishlatiladi
/// (Guid asosida), bazada saqlanadi va har so'rovda tekshiriladi. MVP
/// bosqichi uchun yetarli - keyinchalik JWT'ga almashtirish oson (faqat
/// shu servis ichida, tashqi controllerlar o'zgarmaydi).
/// </summary>
public class AuthService
{
    private readonly IAuthRepository _authRepo;
    private readonly IBusinessRepository _businessRepo;

    public AuthService(IAuthRepository authRepo, IBusinessRepository businessRepo)
    {
        _authRepo = authRepo;
        _businessRepo = businessRepo;
    }

    // ---------- Super Admin ----------

    public async Task<SuperAdminExistsResponse> CheckSuperAdminExistsAsync(CancellationToken ct = default)
    {
        var exists = await _authRepo.AnySuperAdminExistsAsync(ct);
        return new SuperAdminExistsResponse { Exists = exists };
    }

    /// <summary>Faqat tizimda hali hech qanday Super Admin bo'lmagandagina ishlaydi.</summary>
    public async Task<(bool Success, string? Error, SuperAdminLoginResponse? Result)> CreateFirstSuperAdminAsync(
        CreateSuperAdminRequest request, CancellationToken ct = default)
    {
        if (await _authRepo.AnySuperAdminExistsAsync(ct))
            return (false, "Super Admin allaqachon mavjud. Iltimos, kirish (login) qiling.", null);

        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
            return (false, "Login va parol bo'sh bo'lishi mumkin emas.", null);

        var existing = await _authRepo.GetUserAccountByLoginAsync(request.Login, ct);
        if (existing is not null)
            return (false, "Bu login band.", null);

        var token = GenerateToken();
        var account = new UserAccount
        {
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Login = request.Login,
            PasswordHash = PasswordHasher.Hash(request.Password),
            IsSuperAdmin = true,
            IsActive = true,
            AccessToken = token,
        };

        await _authRepo.CreateUserAccountAsync(account, ct);
        return (true, null, new SuperAdminLoginResponse
        {
            AccessToken = token,
            FullName = account.FullName,
            IsSuperAdmin = true,
        });
    }

    public async Task<(bool Success, string? Error, SuperAdminLoginResponse? Result)> SuperAdminLoginAsync(
        LoginRequest request, CancellationToken ct = default)
    {
        var account = await _authRepo.GetUserAccountByLoginAsync(request.Login, ct);
        if (account is null || !account.IsSuperAdmin || !account.IsActive)
            return (false, "Login yoki parol noto'g'ri.", null);

        if (!PasswordHasher.Verify(request.Password, account.PasswordHash))
            return (false, "Login yoki parol noto'g'ri.", null);

        var token = GenerateToken();
        await _authRepo.UpdateUserAccessTokenAsync(account.Id, token, ct);

        return (true, null, new SuperAdminLoginResponse { AccessToken = token, FullName = account.FullName, IsSuperAdmin = true });
    }

    public async Task<UserAccount?> ValidateSuperAdminTokenAsync(string token, CancellationToken ct = default)
    {
        var account = await _authRepo.GetUserAccountByTokenAsync(token, ct);
        return (account is not null && account.IsSuperAdmin && account.IsActive) ? account : null;
    }

    // ---------- Restoran + Egasi yaratish (Super Admin amali) ----------

    public async Task<(bool Success, string? Error, RestaurantOwnerSummaryResponse? Result)> CreateRestaurantAsync(
        CreateRestaurantRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RestaurantName))
            return (false, "Restoran nomi bo'sh bo'lishi mumkin emas.", null);

        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
            return (false, "Login va parol bo'sh bo'lishi mumkin emas.", null);

        var existingOwner = await _authRepo.GetOwnerByLoginAsync(request.Login, ct);
        if (existingOwner is not null)
            return (false, "Bu login band.", null);

        var business = await _businessRepo.CreateBusinessAsync(new Business
        {
            Name = request.RestaurantName,
            Type = BusinessType.Restaurant,
        }, ct);

        var subscriptionEndsAt = request.PaymentPaidAt.AddMonths(request.SubscriptionMonths);

        var owner = await _authRepo.CreateOwnerAsync(new RestaurantOwner
        {
            BusinessId = business.Id,
            FullName = request.OwnerFullName,
            PhoneNumber = request.OwnerPhoneNumber,
            Login = request.Login,
            PasswordHash = PasswordHasher.Hash(request.Password),
            SubscriptionAmount = request.SubscriptionAmount,
            PaymentPaidAt = request.PaymentPaidAt,
            SubscriptionMonths = request.SubscriptionMonths,
            SubscriptionEndsAt = subscriptionEndsAt,
            IsActive = true,
        }, ct);

        return (true, null, ToSummary(owner, business));
    }

    public async Task<List<RestaurantOwnerSummaryResponse>> GetAllRestaurantsAsync(CancellationToken ct = default)
    {
        var pairs = await _authRepo.GetAllOwnersWithBusinessAsync(ct);
        return pairs.Select(p => ToSummary(p.Owner, p.Business)).ToList();
    }

    // ---------- Restoran egasi login ----------

    public async Task<(bool Success, string? Error, OwnerLoginResponse? Result)> OwnerLoginAsync(
        LoginRequest request, CancellationToken ct = default)
    {
        var owner = await _authRepo.GetOwnerByLoginAsync(request.Login, ct);
        if (owner is null || !owner.IsActive)
            return (false, "Login yoki parol noto'g'ri.", null);

        if (!PasswordHasher.Verify(request.Password, owner.PasswordHash))
            return (false, "Login yoki parol noto'g'ri.", null);

        var business = await _businessRepo.GetBusinessByIdAsync(owner.BusinessId, ct);
        if (business is null)
            return (false, "Restoran topilmadi. Administrator bilan bog'laning.", null);

        var isSubscriptionActive = owner.SubscriptionEndsAt > DateTime.UtcNow;
        if (!isSubscriptionActive)
            return (false, "Obuna muddati tugagan. Administrator bilan bog'laning.", null);

        var token = GenerateToken();
        await _authRepo.UpdateOwnerAccessTokenAsync(owner.Id, token, ct);

        return (true, null, new OwnerLoginResponse
        {
            AccessToken = token,
            BusinessId = business.Id,
            RestaurantName = business.Name,
            OwnerFullName = owner.FullName,
            IsSubscriptionActive = isSubscriptionActive,
            SubscriptionEndsAt = owner.SubscriptionEndsAt,
        });
    }

    public async Task<RestaurantOwner?> ValidateOwnerTokenAsync(string token, CancellationToken ct = default)
    {
        var owner = await _authRepo.GetOwnerByTokenAsync(token, ct);
        return (owner is not null && owner.IsActive && owner.SubscriptionEndsAt > DateTime.UtcNow) ? owner : null;
    }

    private static string GenerateToken() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    private static RestaurantOwnerSummaryResponse ToSummary(RestaurantOwner o, Business b) => new()
    {
        OwnerId = o.Id,
        BusinessId = b.Id,
        RestaurantName = b.Name,
        OwnerFullName = o.FullName,
        PhoneNumber = o.PhoneNumber,
        Login = o.Login,
        SubscriptionAmount = o.SubscriptionAmount,
        PaymentPaidAt = o.PaymentPaidAt,
        SubscriptionMonths = o.SubscriptionMonths,
        SubscriptionEndsAt = o.SubscriptionEndsAt,
        IsActive = o.IsActive,
    };
}
