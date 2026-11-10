using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace VoiceKassa.Api;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SuperAdminReadOnlyAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.IsInRole("super-admin"))
        {
            context.Result = new ObjectResult(new { error = "Super Admin faqat ko'rish huquqiga ega." })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
            return;
        }

        await next();
    }
}
