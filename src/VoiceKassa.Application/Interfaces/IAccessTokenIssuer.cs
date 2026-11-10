namespace VoiceKassa.Application.Interfaces;

public interface IAccessTokenIssuer
{
    string CreateSuperAdminToken(long userId, string sessionId);
    string CreateRestaurantAdminToken(long userId, long restaurantId, string sessionId);
}
