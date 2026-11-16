using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ServiceOps.Api.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ServiceOps.Api.Data;
using ServiceOps.Api.DTOs.Auth;
using ServiceOps.Api.Models;
using ServiceOps.Api.Services.Interfaces;

namespace ServiceOps.Api.Services.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly PasswordHasher<User> _passwordHasher;

    public AuthService(
        AppDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
        _passwordHasher = new PasswordHasher<User>();
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user == null || !ServiceOpsRoles.All.Contains(user.Role.RoleName))
        {
            return null;
        }

        if (!string.Equals(
                user.AccountStatus,
                "Active",
                StringComparison.Ordinal))
        {
            return null;
        }

        PasswordVerificationResult verificationResult;
        try
        {
            verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        }
        catch (FormatException)
        {
            return null;
        }

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            await _context.SaveChangesAsync();
        }

        var raw = NewToken(); var session = NewSession(user, raw, Guid.NewGuid(), DateTime.UtcNow.AddDays(14));
        _context.RefreshSessions.Add(session);
        _context.AuditLogs.Add(new AuditLog { UserId = user.UserId, Action = "Login succeeded", EntityType = "User", EntityId = user.UserId });
        await _context.SaveChangesAsync(); return Response(user, session, raw);
    }
    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    private RefreshSession NewSession(User user, string raw, Guid family, DateTime expiry) => new() { RefreshSessionId = Guid.NewGuid(), UserId = user.UserId, FamilyId = family, TokenHash = Hash(raw), PasswordVersion = PasswordStamp.Create(user.PasswordHash, _configuration["Jwt:Key"]!), ExpiresAt = expiry };
    private LoginResponse Response(User user, RefreshSession session, string raw)
    {
        var expiry = DateTime.UtcNow.AddMinutes(GetJwtExpiryMinutes()); if (expiry > session.ExpiresAt) expiry = session.ExpiresAt;
        return new LoginResponse { AccessToken = GenerateAccessToken(user, expiry, session.RefreshSessionId), RefreshToken = raw, RefreshExpiresAt = session.ExpiresAt, ExpiresAt = expiry, MustChangePassword = user.MustChangePassword, UserId = user.UserId, FullName = user.FullName, Email = user.Email, Role = user.Role.RoleName };
    }
    public async Task<LoginResponse?> RefreshTokenAsync(RefreshTokenRequest request)
    {
        if (request.RefreshToken.Length < 40 || request.RefreshToken.Length > 200) return null;
        using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.Database.ExecuteSqlRawAsync("LOCK TABLE \"RefreshSessions\" IN SHARE ROW EXCLUSIVE MODE");
        var hash = Hash(request.RefreshToken); var session = await _context.RefreshSessions.Include(s => s.User).ThenInclude(u => u.Role).SingleOrDefaultAsync(s => s.TokenHash == hash);
        if (session == null) return null;
        var now = DateTime.UtcNow;
        if (session.RevokedAt != null || session.RotatedAt != null || session.ExpiresAt <= now || session.User.AccountStatus != "Active" || !ServiceOpsRoles.All.Contains(session.User.Role.RoleName) || !PasswordStamp.Valid(session.PasswordVersion, session.User.PasswordHash, _configuration["Jwt:Key"]!))
        {
            await _context.RefreshSessions.Where(s => s.FamilyId == session.FamilyId && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(p => p.RevokedAt, now));
            _context.AuditLogs.Add(new AuditLog { UserId = session.UserId, Action = "Refresh rejected; session family revoked", EntityType = "User", EntityId = session.UserId }); await _context.SaveChangesAsync(); await transaction.CommitAsync(); return null;
        }
        var raw = NewToken(); var replacement = NewSession(session.User, raw, session.FamilyId, session.ExpiresAt);
        session.RotatedAt = now; session.RevokedAt = now; session.ReplacedById = replacement.RefreshSessionId;
        _context.RefreshSessions.Add(replacement); _context.AuditLogs.Add(new AuditLog { UserId = session.UserId, Action = "Refresh session rotated", EntityType = "User", EntityId = session.UserId });
        await _context.SaveChangesAsync(); await transaction.CommitAsync(); return Response(session.User, replacement, raw);
    }

    private string GenerateAccessToken(
        User user,
        DateTime expiresAt, Guid sessionId)
    {
        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT key is not configured."
            );

        var issuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "JWT issuer is not configured."
            );

        var audience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "JWT audience is not configured."
            );

        var claims = new List<Claim>
        {
            new("session_id", sessionId.ToString()),
            new("password_version", PasswordStamp.Create(user.PasswordHash, jwtKey)),
            new(
                JwtRegisteredClaimNames.Sub,
                user.UserId.ToString()
            ),

            new(
                JwtRegisteredClaimNames.Email,
                user.Email
            ),

            new(
                ClaimTypes.NameIdentifier,
                user.UserId.ToString()
            ),

            new(
                ClaimTypes.Name,
                user.FullName
            ),

            new(
                ClaimTypes.Role,
                user.Role.RoleName
            )
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey)
        );

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    private double GetJwtExpiryMinutes()
    {
        var value = _configuration["Jwt:ExpiryMinutes"];

        return double.TryParse(value, out var minutes)
            && double.IsFinite(minutes) && minutes > 0 && minutes <= 1440
            ? minutes
            : throw new InvalidOperationException("Jwt:ExpiryMinutes must be between 0 and 1440.");
    }
}