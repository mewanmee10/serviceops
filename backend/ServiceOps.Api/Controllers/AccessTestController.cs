using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceOps.Api.Authorization;

namespace ServiceOps.Api.Controllers;

[ApiController]
[Route("api/access-test")]
[Authorize]
public class AccessTestController : ControllerBase, Microsoft.AspNetCore.Mvc.Filters.IActionFilter
{
    public void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        if (!HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            context.Result = NotFound();

    }

    public void OnActionExecuted(Microsoft.AspNetCore.Mvc.Filters.ActionExecutedContext context) { }

    [HttpGet("authenticated")]
    public IActionResult Authenticated()
    {
        return Ok(new { message = "Authenticated user access granted." });
    }

    [HttpGet("admin")]
    [Authorize(Roles = ServiceOpsRoles.Administrator)]
    public IActionResult AdministratorOnly()
    {
        return Ok(new { message = "Administrator access granted." });
    }

    [HttpGet("manager")]
    [Authorize(Roles = ServiceOpsRoles.ServiceManager)]
    public IActionResult ManagerOnly()
    {
        return Ok(new { message = "Service Manager access granted." });
    }
}