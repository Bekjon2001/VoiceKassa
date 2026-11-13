using Microsoft.AspNetCore.Mvc;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Api;

public abstract class ApiControllerBase : ControllerBase
{
    protected bool IsSuperAdminRole => User.IsInRole("super-admin");

    protected bool IsRestaurantAdminRole => User.IsInRole("restaurant-admin");

    protected bool IsManagerRole => User.IsInRole("manager");

    protected bool IsCashierRole => User.IsInRole("cashier");

    protected bool IsWaiterRole => User.IsInRole("waiter");

    protected bool IsCookRole => User.IsInRole("cook");

    protected StaffRole? TokenStaffRole =>
        Enum.TryParse<StaffRole>(User.FindFirst(JwtTokenService.RoleClaim)?.Value, true, out var role)
            ? role
            : null;

    protected long? TokenRestaurantId =>
        long.TryParse(User.FindFirst(JwtTokenService.RestaurantIdClaim)?.Value, out var id) ? id : null;

    protected IActionResult? EnsureBusinessReadAccess(long businessId, params StaffRole[] allowedStaffRoles)
    {
        if (User.Identity?.IsAuthenticated == true && IsSuperAdminRole)
            return null;

        if (User.Identity?.IsAuthenticated == true && TokenRestaurantId == businessId)
        {
            if (IsRestaurantAdminRole || IsManagerRole || (TokenStaffRole is StaffRole role && allowedStaffRoles.Contains(role)))
                return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, new { error = "Бу ресторан маълумотларини кўришга рухсат йўқ." });
    }
}
