using Microsoft.EntityFrameworkCore;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.DataLayer;
using VoiceKassa.Domain.Entities;

namespace VoiceKassa.DataLayer.Repository;

public class AuthRepository : IAuthRepository
{
    private readonly AppDbContext _db;

    public AuthRepository(AppDbContext db) => _db = db;

    public Task<bool> AnySuperAdminExistsAsync(CancellationToken ct = default) =>
        _db.UserAccounts.AnyAsync(u => u.IsSuperAdmin, ct);

    public Task<UserAccount?> GetUserAccountByLoginAsync(string login, CancellationToken ct = default) =>
        _db.UserAccounts.FirstOrDefaultAsync(u => u.Login == login, ct);

    public Task<UserAccount?> GetUserAccountByTokenAsync(string token, CancellationToken ct = default) =>
        _db.UserAccounts.FirstOrDefaultAsync(u => u.AccessToken == token, ct);

    public async Task<UserAccount> CreateUserAccountAsync(UserAccount account, CancellationToken ct = default)
    {
        _db.UserAccounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return account;
    }

    public async Task<bool> UpdateUserAccessTokenAsync(long userId, string token, CancellationToken ct = default)
    {
        var user = await _db.UserAccounts.FindAsync(new object[] { userId }, ct);
        if (user is null) return false;
        user.AccessToken = token;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task<RestaurantOwner?> GetOwnerByLoginAsync(string login, CancellationToken ct = default) =>
        _db.RestaurantOwners.FirstOrDefaultAsync(o => o.Login == login, ct);

    public Task<RestaurantOwner?> GetOwnerByTokenAsync(string token, CancellationToken ct = default) =>
        _db.RestaurantOwners.FirstOrDefaultAsync(o => o.AccessToken == token, ct);

    public Task<RestaurantOwner?> GetOwnerByIdAsync(long ownerId, CancellationToken ct = default) =>
        _db.RestaurantOwners.FirstOrDefaultAsync(o => o.Id == ownerId, ct);

    public async Task<RestaurantOwner> CreateOwnerAsync(RestaurantOwner owner, CancellationToken ct = default)
    {
        _db.RestaurantOwners.Add(owner);
        await _db.SaveChangesAsync(ct);
        return owner;
    }

    public async Task<bool> UpdateOwnerAccessTokenAsync(long ownerId, string token, CancellationToken ct = default)
    {
        var owner = await _db.RestaurantOwners.FindAsync(new object[] { ownerId }, ct);
        if (owner is null) return false;
        owner.AccessToken = token;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<(RestaurantOwner Owner, Business Business)>> GetAllOwnersWithBusinessAsync(CancellationToken ct = default)
    {
        var owners = await _db.RestaurantOwners.OrderByDescending(o => o.CreatedAt).ToListAsync(ct);
        var businessIds = owners.Select(o => o.BusinessId).ToList();
        var businesses = await _db.Businesses.Where(b => businessIds.Contains(b.Id)).ToListAsync(ct);

        return owners
            .Select(o => (o, businesses.First(b => b.Id == o.BusinessId)))
            .ToList();
    }
}
