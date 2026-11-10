using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using VoiceKassa.Application.Interfaces;

namespace VoiceKassa.Api;

public sealed class JwtTokenService : IAccessTokenIssuer
{
    public const string RoleClaim = "Role";
    public const string UserIdClaim = "UserId";
    public const string RestaurantIdClaim = "RestaurantId";
    public const string SessionIdClaim = "SessionId";

    private readonly SigningCredentials _credentials;

    public JwtTokenService(byte[] signingKey)
    {
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(signingKey),
            SecurityAlgorithms.HmacSha256);
    }

    public string CreateSuperAdminToken(long userId, string sessionId) =>
        CreateToken(userId, "super-admin", 0, sessionId);

    public string CreateRestaurantAdminToken(long userId, long restaurantId, string sessionId) =>
        CreateToken(userId, "restaurant-admin", restaurantId, sessionId);

    private string CreateToken(long userId, string role, long restaurantId, string sessionId)
    {
        var claims = new[]
        {
            new Claim(UserIdClaim, userId.ToString()),
            new Claim(RoleClaim, role),
            new Claim(RestaurantIdClaim, restaurantId.ToString()),
            new Claim(SessionIdClaim, sessionId),
        };
        var token = new JwtSecurityToken(
            issuer: "VoiceKassa",
            audience: "VoiceKassa",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: _credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
