
using Microsoft.AspNetCore.Identity;
using ServiceOps.Api.Authorization;
using ServiceOps.Api.Services.Auth;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Data;
using ServiceOps.Api.DTOs.Users;
using ServiceOps.Api.Models;
using ServiceOps.Api.Services.Interfaces;

namespace ServiceOps.Api.Services.Users;

public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly PasswordHasher<User> _passwordHasher;

    public UserService(AppDbContext context)
    {
        _context = context;
        _passwordHasher = new PasswordHasher<User>();
    }

    // Get all registered users.
    public async Task<IEnumerable<UserResponse>> GetAllUsersAsync()
{
    var users = await _context.Users
        .AsNoTracking()
        .Include(u => u.Role)
        .OrderBy(u => u.UserId)
        .ToListAsync();

    return users.Select(MapUser).ToList();
}

    // Get a single user by ID.
    public async Task<UserResponse?> GetUserByIdAsync(int id)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == id);

        return user == null ? null : MapUser(user);
    }

    // Create a new user account.
    public async Task<UserResponse> CreateUserAsync(
        CreateUserRequest request)
    {
        if (PasswordPolicy.Error(request.Password) is string error) throw new ArgumentException(error);
        if (request.Password != request.ConfirmPassword) throw new ArgumentException("Passwords must match.");
        using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.Database.ExecuteSqlRawAsync("LOCK TABLE \"Users\" IN SHARE ROW EXCLUSIVE MODE");

        var email = request.Email.Trim().ToLowerInvariant();

        var emailExists = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == email);

        if (emailExists)
        {
            throw new InvalidOperationException(
                "A user with this email already exists.");
        }

        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.RoleId == request.RoleId);

        if (role == null || !ServiceOpsRoles.All.Contains(role.RoleName))
        {
            throw new ArgumentException(
                "The selected role does not exist.");
        }

        if (role.RoleName == ServiceOpsRoles.Customer && string.IsNullOrWhiteSpace(request.CustomerName))
            throw new ArgumentException("CustomerName is required for Customer onboarding.");
        if (role.RoleName == ServiceOpsRoles.Technician && string.IsNullOrWhiteSpace(request.TechnicianSkill))
            throw new ArgumentException("TechnicianSkill is required for Technician onboarding.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber,
            RoleId = role.RoleId,
            AccountStatus = "Active",
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            request.Password);

        if (role.RoleName == ServiceOpsRoles.Customer)
            user.Customer = new Customer { CustomerName = request.CustomerName!.Trim() };
        if (role.RoleName == ServiceOpsRoles.Technician)
            user.Technician = new Technician { Skill = request.TechnicianSkill!.Trim(), Availability = true };
        _context.Users.Add(user);

        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        { throw new InvalidOperationException("A user with this email already exists."); }
        await transaction.CommitAsync();

        user.Role = role;

        return MapUser(user);
    }

    // Update an existing user's profile.
    public async Task<bool> UpdateUserAsync(
        int id,
        UpdateUserRequest request, int actorUserId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.Database.ExecuteSqlRawAsync("LOCK TABLE \"Users\" IN SHARE ROW EXCLUSIVE MODE");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
        {
            return false;
        }

        var email = request.Email.Trim().ToLowerInvariant();

        var emailExists = await _context.Users
            .AnyAsync(u =>
                u.Email.ToLower() == email &&
                u.UserId != id);

        if (emailExists)
        {
            throw new InvalidOperationException(
                "Another user already uses this email.");
        }

        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.RoleId == request.RoleId);

        if (role == null || !ServiceOpsRoles.All.Contains(role.RoleName))
        {
            throw new ArgumentException(
                "The selected role does not exist.");
        }

        if (id == actorUserId && user.RoleId != request.RoleId)
            throw new InvalidOperationException("You cannot change your own role.");

        var hasCustomer = await _context.Customers.AnyAsync(c => c.UserId == id);
        var hasTechnician = await _context.Technicians.AnyAsync(t => t.UserId == id);
        if ((role.RoleName == ServiceOpsRoles.Customer && !hasCustomer) ||
            (role.RoleName == ServiceOpsRoles.Technician && !hasTechnician) ||
            (hasCustomer && role.RoleName != ServiceOpsRoles.Customer) ||
            (hasTechnician && role.RoleName != ServiceOpsRoles.Technician))
            throw new InvalidOperationException("This role change requires coordinated profile onboarding or reassignment.");

        // Prevent removal of the last active administrator.
        if (user.RoleId != request.RoleId &&
            user.AccountStatus == "Active")
        {
            var currentRole = await _context.Roles
                .FirstAsync(r => r.RoleId == user.RoleId);

            if (currentRole.RoleName == "Administrator")
            {
                var activeAdmins = await _context.Users
                    .CountAsync(u =>
                        u.RoleId == user.RoleId &&
                        u.AccountStatus == "Active");

                if (activeAdmins <= 1)
                {
                    throw new InvalidOperationException(
                        "Cannot change the last active administrator's role.");
                }
            }
        }

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.PhoneNumber = request.PhoneNumber;
        user.RoleId = request.RoleId;
        await _context.RefreshSessions.Where(s=>s.UserId==id&&s.RevokedAt==null).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.RevokedAt,DateTime.UtcNow));

        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        { throw new InvalidOperationException("A user with this email already exists."); }
        await transaction.CommitAsync();

        return true;
    }

    // Activate or deactivate a user account.
    public async Task<bool> UpdateUserStatusAsync(
        int id,
        UpdateUserStatusRequest request, int actorUserId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.Database.ExecuteSqlRawAsync("LOCK TABLE \"Users\" IN SHARE ROW EXCLUSIVE MODE");

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
        {
            return false;
        }

        if (id == actorUserId && request.Status == "Inactive")
            throw new InvalidOperationException("You cannot deactivate your own account.");

        if (user.Role.RoleName == "Administrator" &&
            user.AccountStatus == "Active" &&
            request.Status == "Inactive")
        {
            var activeAdmins = await _context.Users
                .CountAsync(u =>
                    u.RoleId == user.RoleId &&
                    u.AccountStatus == "Active");

            if (activeAdmins <= 1)
            {
                throw new InvalidOperationException(
                    "Cannot deactivate the last active administrator.");
            }
        }

        user.AccountStatus = request.Status;
        if(request.Status=="Inactive")await _context.RefreshSessions.Where(s=>s.UserId==id&&s.RevokedAt==null).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.RevokedAt,DateTime.UtcNow));

        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        { throw new InvalidOperationException("A user with this email already exists."); }
        await transaction.CommitAsync();

        return true;
    }

    public async Task<IEnumerable<RoleResponse>> GetRolesAsync() =>
        await _context.Roles.AsNoTracking()
            .Where(r => ServiceOpsRoles.All.Contains(r.RoleName))
            .OrderBy(r => r.RoleId)
            .Select(r => new RoleResponse { RoleId = r.RoleId, RoleName = r.RoleName })
            .ToListAsync();

    // Convert a database user into a safe API response.
    private static UserResponse MapUser(User user)
    {
        return new UserResponse
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            RoleId = user.RoleId,
            RoleName = user.Role.RoleName,
            AccountStatus = user.AccountStatus,
            MustChangePassword = user.MustChangePassword,
            PasswordChangedAt = user.PasswordChangedAt,
            CreatedAt = user.CreatedAt
        };
    }
}
