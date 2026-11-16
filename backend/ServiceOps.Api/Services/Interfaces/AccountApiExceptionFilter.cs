using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ServiceOps.Api.Services.Interfaces;

// Scoped to account controllers so failures never expose database or credential details.
public sealed class AccountApiExceptionFilter(ILogger<AccountApiExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var status = context.Exception switch
        {
            DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "40P01" or "23505" or "23503" } } => 409,
            PostgresException { SqlState: "40001" or "40P01" or "23505" or "23503" } => 409,
            _ => 500
        };
        // Avoid exception text: provider diagnostics can contain personal data.
        logger.LogError("Account API failed with exception type {ExceptionType}.", context.Exception.GetType().Name);
        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = status == 409 ? "Request conflicts with an existing record or concurrent update." : "Unable to process the account request."
        }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
