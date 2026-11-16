using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ServiceOps.Api.Authorization;

/// <summary>Only account state, own password change and logout may bypass the initial-reset gate.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowInitialPasswordChangeAttribute : Attribute { }

public sealed class InitialPasswordChangeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.FindFirstValue("must_change_password") == "true" &&
            endpoint?.Metadata.GetMetadata<IAllowAnonymous>() == null &&
            endpoint?.Metadata.GetMetadata<AllowInitialPasswordChangeAttribute>() == null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "password_change_required",
                message = "Replace your initial password before accessing ServiceOps."
            }, context.RequestAborted);
            return;
        }
        await next(context);
    }
}
