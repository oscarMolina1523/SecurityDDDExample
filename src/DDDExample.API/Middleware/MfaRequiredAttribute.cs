using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using DDDExample.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DDDExample.API.Middleware;

public class MfaRequiredAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (!user.Identity.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var appUser = await userManager.FindByIdAsync(userId);

        if (!appUser.MfaEnabled)
        {
            context.Result = new ForbidResult();
            return;
        }

        // Verificar si MFA fue verificado en esta sesión
        var mfaVerified = context.HttpContext.Items["MfaVerified"] as bool?;
        if (!mfaVerified.HasValue || !mfaVerified.Value)
        {
            context.Result = new StatusCodeResult(428); // Precondition Required
            return;
        }

        await Task.CompletedTask;
    }
}