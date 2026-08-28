using VoiceKassa.Domain.Entities;

namespace VoiceKassa.Application.Interfaces;

/// <summary>
/// Super Admin va Restoran Egasi akkauntlari bilan ishlash. UserAccount -
/// platforma darajasidagi akkauntlar (hozircha faqat Super Admin uchun).
/// RestaurantOwner - har bir restoranning o'z egasi, obuna/to'lov
/// ma'lumotlari bilan birga saqlanadi.
/// </summary>
public interface IAuthRepository
{
    Task<bool> AnySuperAdminExistsAsync(CancellationToken ct = default);
    Task<UserAccount?> GetUserAccountByLoginAsync(string login, CancellationToken ct = default);
    Task<UserAccount?> GetUserAccountByTokenAsync(string token, CancellationToken ct = default);
    Task<UserAccount> CreateUserAccountAsync(UserAccount account, CancellationToken ct = default);
    Task<bool> UpdateUserAccessTokenAsync(long userId, string token, CancellationToken ct = default);

    Task<RestaurantOwner?> GetOwnerByLoginAsync(string login, CancellationToken ct = default);
    Task<RestaurantOwner?> GetOwnerByTokenAsync(string token, CancellationToken ct = default);
    Task<RestaurantOwner?> GetOwnerByIdAsync(long ownerId, CancellationToken ct = default);
    Task<RestaurantOwner> CreateOwnerAsync(RestaurantOwner owner, CancellationToken ct = default);
    Task<bool> UpdateOwnerAccessTokenAsync(long ownerId, string token, CancellationToken ct = default);

    /// <summary>Super Admin panelidagi "Restoranlar" ro'yxati uchun - egasi + biznes birga.</summary>
    Task<List<(RestaurantOwner Owner, Business Business)>> GetAllOwnersWithBusinessAsync(CancellationToken ct = default);
}
