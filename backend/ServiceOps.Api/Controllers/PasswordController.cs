using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Data;
using ServiceOps.Api.Models;
using ServiceOps.Api.Services.Interfaces;
using ServiceOps.Api.Services.Auth;
using ServiceOps.Api.Authorization;
namespace ServiceOps.Api.Controllers;

[ApiController, Route("api"), Authorize, TypeFilter(typeof(AccountApiExceptionFilter))]
public sealed class PasswordController(AppDbContext db) : ControllerBase
{
    public sealed class ChangeRequest
    {
        [Required, StringLength(1024)] public string CurrentPassword { get; set; } = string.Empty;
        [Required, PasswordPolicy] public string NewPassword { get; set; } = string.Empty;
        [Required, StringLength(1024), Compare(nameof(NewPassword), ErrorMessage = "Passwords must match.")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
    public sealed class ResetRequest
    {
        [Required, PasswordPolicy] public string NewPassword { get; set; } = string.Empty;
        [Required, StringLength(1024), Compare(nameof(NewPassword), ErrorMessage = "Passwords must match.")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
    [HttpPost("auth/password"), AllowInitialPasswordChange, EnableRateLimiting("account-security")]
    public async Task<IActionResult> Change(ChangeRequest input)
    {
        using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Users\" IN SHARE ROW EXCLUSIVE MODE");
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!); var user = await db.Users.SingleAsync(u => u.UserId == id);
        var hasher = new PasswordHasher<User>();
        try { if (hasher.VerifyHashedPassword(user, user.PasswordHash, input.CurrentPassword) == PasswordVerificationResult.Failed) return Unauthorized(new { message = "Current password is incorrect." }); }
        catch (FormatException) { return Unauthorized(); }
        if (input.NewPassword == input.CurrentPassword || string.IsNullOrWhiteSpace(input.NewPassword)) return BadRequest(new { message = "Choose a different password of at least 12 characters." });
        db.AuditLogs.Add(new AuditLog { UserId = id, EntityType = "User", EntityId = id, Action = "Password changed; existing sessions revoked" });
        user.MustChangePassword = false;
        user.PasswordChangedAt = DateTime.UtcNow;
        user.PasswordHash = hasher.HashPassword(user, input.NewPassword); await db.RefreshSessions.Where(s => s.UserId == user.UserId && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(p => p.RevokedAt, DateTime.UtcNow)); await db.SaveChangesAsync(); await tx.CommitAsync(); return NoContent();
    }
    [HttpPost("users/{id:int}/password"), Authorize(Roles = "Administrator", Policy = "permission:users.manage"), EnableRateLimiting("account-security")]
    public async Task<IActionResult> Reset(int id, ResetRequest input)
    {
        if (id.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier)) return Conflict(new { message = "Use Change password for your own account." });
        if (string.IsNullOrWhiteSpace(input.NewPassword)) return BadRequest();
        using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Users\" IN SHARE ROW EXCLUSIVE MODE");
        var user = await db.Users.SingleOrDefaultAsync(u => u.UserId == id); if (user == null) return NotFound();
        user.MustChangePassword = true;
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, input.NewPassword); await db.RefreshSessions.Where(s => s.UserId == user.UserId && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(p => p.RevokedAt, DateTime.UtcNow)); await db.SaveChangesAsync(); await tx.CommitAsync(); return NoContent();
    }
    [HttpPost("auth/logout"), AllowInitialPasswordChange]
    public async Task<IActionResult> Logout()
    {
        if (!Guid.TryParse(User.FindFirstValue("session_id"), out var id)) return Unauthorized();
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var session = await db.RefreshSessions.SingleOrDefaultAsync(s => s.RefreshSessionId == id && s.UserId == userId); if (session == null) return Unauthorized();
        using var tx = await db.Database.BeginTransactionAsync();
        await db.RefreshSessions.Where(s => s.FamilyId == session.FamilyId && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(p => p.RevokedAt, DateTime.UtcNow));
        db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "Logout; session family revoked", EntityType = "User", EntityId = userId }); await db.SaveChangesAsync(); await tx.CommitAsync(); return NoContent();
    }

}
