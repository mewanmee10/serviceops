using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Models;
using ServiceOps.Api.Services.Interfaces;

namespace ServiceOps.Api.Data;

public static class DevDataSeeder
{
    public static async Task SeedAsync(
        AppDbContext context,
        IConfiguration configuration,
        IHostEnvironment environment,
        IUserService userService)
    {
        if (!environment.IsDevelopment()) return;
        // Schema deployment is coordinated with the database engineer; never migrate on startup.

        // -------------------------------------------------
        // 1. Create Administrator role if it does not exist
        // -------------------------------------------------

        var roleNames = new[]
{
    "Administrator",
    "Service Manager",
    "Technician",
    "Inventory Staff",
    "Customer"
};

foreach (var roleName in roleNames)
{
    var roleExists = await context.Roles
        .AnyAsync(r => r.RoleName == roleName);

    if (!roleExists)
    {
        context.Roles.Add(new Role
        {
            RoleName = roleName
        });
    }
}

await context.SaveChangesAsync();

var adminRole = await context.Roles
    .SingleAsync(r => r.RoleName == "Administrator");

        // -------------------------------------------------
        // 2. Read development admin credentials
        // -------------------------------------------------

        var adminEmail =
            configuration["SeedAdmin:Email"];

        var adminPassword =
            configuration["SeedAdmin:Password"];

        var adminName =
            configuration["SeedAdmin:FullName"]
            ?? "ServiceOps Administrator";

        if (configuration.GetValue<bool?>("SeedAdmin:Enabled") == false || string.IsNullOrWhiteSpace(adminEmail) ||
            string.IsNullOrWhiteSpace(adminPassword))
        {
            Console.WriteLine(
                "Development admin seed skipped. " +
                "SeedAdmin credentials are not configured."
            );

            await DemoDataSeeder.SeedAsync(context, configuration, environment, userService);
            return;
        }

        adminEmail = adminEmail
            .Trim()
            .ToLowerInvariant();

        // -------------------------------------------------
        // 3. Do not create duplicate user
        // -------------------------------------------------

        var existingUser = await context.Users
            .FirstOrDefaultAsync(u =>
                u.Email.ToLower() == adminEmail);

        if (existingUser != null)
        {
            Console.WriteLine("Development administrator already exists.");
            await DemoDataSeeder.SeedAsync(context, configuration, environment, userService);
            return;
        }

        // -------------------------------------------------
        // 4. Create administrator user
        // -------------------------------------------------

        if (ServiceOps.Api.Services.Auth.PasswordPolicy.Error(adminPassword) is string error) throw new InvalidOperationException(error);
        var user = new User
        {
            RoleId = adminRole.RoleId,
            FullName = adminName,
            Email = adminEmail,
            PhoneNumber = null,
            AccountStatus = "Active",
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow
        };

        // -------------------------------------------------
        // 5. Hash password
        // -------------------------------------------------

        var passwordHasher =
            new PasswordHasher<User>();

        user.PasswordHash =
            passwordHasher.HashPassword(
                user,
                adminPassword
            );

        context.Users.Add(user);

        await context.SaveChangesAsync();

        Console.WriteLine("Development administrator created successfully.");
        await DemoDataSeeder.SeedAsync(context, configuration, environment, userService);
    }
}
