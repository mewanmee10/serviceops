using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ServiceOps.Api.Data;
using ServiceOps.Api.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceOps.Api.DTOs.Auth;
using ServiceOps.Api.Services.Interfaces;

namespace ServiceOps.Api.Controllers;

[ApiController]
[TypeFilter(typeof(AccountApiExceptionFilter))]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger=logger;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("account-security")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Email and password are required."
            });
        }

        var result = await _authService.LoginAsync(request);

        if (result == null)
        {
            _logger.LogWarning("ServiceOps login rejected: invalid credentials or inactive account.");
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("account-security")]
    public async Task<IActionResult> Refresh(
        RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new
            {
                message = "Refresh token is required."
            });
        }

        var result=await _authService.RefreshTokenAsync(request);
        return result==null?Unauthorized(new {message="Refresh session is invalid. Sign in again."}):Ok(result);
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> Profile([FromServices] AppDbContext db)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var profile = await db.Users.AsNoTracking().Where(u => u.UserId == id)
            .Select(u => new { u.FullName, u.Email, u.PhoneNumber }).SingleOrDefaultAsync();
        return profile == null ? NotFound() : Ok(profile);
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest input, [FromServices] AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(input.FullName)) return BadRequest(new { message = "Name is required." });
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Users\", \"RefreshSessions\" IN SHARE ROW EXCLUSIVE MODE");
        var user = await db.Users.SingleOrDefaultAsync(u => u.UserId == id);
        if (user == null) return NotFound();
        user.FullName = input.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
        var sessions = await db.RefreshSessions.Where(s => s.UserId == id && s.RevokedAt == null).ToListAsync();
        foreach (var session in sessions) session.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me"), AllowInitialPasswordChange]
    public async Task<IActionResult> GetCurrentUser([FromServices] AppDbContext db)
    {
        var userId =
            User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier
            )?.Value;

        var name =
            User.FindFirst(
                System.Security.Claims.ClaimTypes.Name
            )?.Value;

        var role =
            User.FindFirst(
                System.Security.Claims.ClaimTypes.Role
            )?.Value;

        var email =
            User.FindFirst(
                System.Security.Claims.ClaimTypes.Email
            )?.Value;

        return Ok(new
        {
            userId,
            name,
            email,
            role,
            mustChangePassword = await db.Users.Where(u => u.UserId == int.Parse(userId!)).Select(u => u.MustChangePassword).SingleAsync(),
            permissions = await db.Users.AsNoTracking().Where(u => u.UserId == int.Parse(userId!))
                .SelectMany(u => u.Role.RolePermissions.Select(p => p.Permission.PermissionName))
                .OrderBy(p => p).ToListAsync()
        });
    }
}
