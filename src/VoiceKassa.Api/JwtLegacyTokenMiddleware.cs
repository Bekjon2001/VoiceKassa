using System.Security.Claims;
using VoiceKassa.Application.Interfaces;

namespace VoiceKassa.Api;

public sealed class JwtLegacyTokenMiddleware
{
    private readonly RequestDelegate _next;

    public JwtLegacyTokenMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        IAuthRepository authRepository,
        IBusinessRepository businessRepository)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdValue = context.User.FindFirst(JwtTokenService.UserIdClaim)?.Value;
            var role = context.User.FindFirst(JwtTokenService.RoleClaim)?.Value;
            var restaurantIdValue = context.User.FindFirst(JwtTokenService.RestaurantIdClaim)?.Value;
            var sessionId = context.User.FindFirst(JwtTokenService.SessionIdClaim)?.Value;
            if (!long.TryParse(userIdValue, out var userId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if (role == "super-admin")
            {
                var user = await authRepository.GetUserAccountByIdAsync(userId, context.RequestAborted);
                if (user is null ||
                    !user.IsActive ||
                    !user.IsSuperAdmin ||
                    !string.Equals(user.AccessToken, sessionId, StringComparison.Ordinal))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                context.Request.Headers["X-Super-Admin-Token"] = user.AccessToken;
                context.Request.Headers["X-Access-Token"] = user.AccessToken;
            }
            else if (role == "restaurant-admin")
            {
                var owner = await businessRepository.GetOwnerByIdAsync(userId, context.RequestAborted);
                var business = owner is null
                    ? null
                    : await businessRepository.GetBusinessByIdAsync(owner.BusinessId, context.RequestAborted);
                if (owner is null ||
                    !owner.IsActive ||
                    owner.SubscriptionEndsAt <= DateTime.UtcNow ||
                    business is null ||
                    !business.IsActive ||
                    !string.Equals(owner.AccessToken, sessionId, StringComparison.Ordinal) ||
                    !long.TryParse(restaurantIdValue, out var restaurantId) ||
                    restaurantId != owner.BusinessId)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                context.Request.Headers["X-Owner-Token"] = owner.AccessToken;
            }
            else if (role is "manager" or "cashier" or "waiter" or "cook")
            {
                var staff = await businessRepository.GetStaffByIdAsync(userId, context.RequestAborted);
                var business = staff is null
                    ? null
                    : await businessRepository.GetBusinessByIdAsync(staff.BusinessId, context.RequestAborted);
                var owner = staff is null
                    ? null
                    : await businessRepository.GetOwnerByBusinessIdAsync(staff.BusinessId, context.RequestAborted);
                if (staff is null ||
                    !staff.IsActive ||
                    staff.Role.ToString().ToLowerInvariant() != role ||
                    business is null ||
                    !business.IsActive ||
                    owner is null ||
                    owner.SubscriptionEndsAt <= DateTime.UtcNow ||
                    !string.Equals(staff.AccessToken, sessionId, StringComparison.Ordinal) ||
                    !long.TryParse(restaurantIdValue, out var restaurantId) ||
                    restaurantId != staff.BusinessId)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                context.Request.Headers["X-Owner-Token"] = staff.AccessToken;
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await _next(context);
    }
}
