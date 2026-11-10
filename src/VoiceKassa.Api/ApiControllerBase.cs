using Microsoft.AspNetCore.Mvc;

namespace VoiceKassa.Api;

public abstract class ApiControllerBase : ControllerBase
{
    protected bool IsSuperAdminRole => User.IsInRole("super-admin");

    protected bool IsRestaurantAdminRole => User.IsInRole("restaurant-admin");

    protected long? TokenRestaurantId =>
        long.TryParse(User.FindFirst(JwtTokenService.RestaurantIdClaim)?.Value, out var id) ? id : null;

    protected IActionResult? EnsureBusinessReadAccess(long businessId)
    {
        if (User.Identity?.IsAuthenticated != true ||
            IsSuperAdminRole ||
            (IsRestaurantAdminRole && TokenRestaurantId == businessId))
            return null;

        return StatusCode(StatusCodes.Status403Forbidden, new { error = "Бу ресторан маълумотларини кўришга рухсат йўқ." });
    }
}
