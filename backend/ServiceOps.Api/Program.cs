using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using ServiceOps.Api.Services.Reporting;
using PdfSharp.Fonts;
using ServiceOps.Api.Services.Attachments;
using ServiceOps.Api.Services.Auditing;
using ServiceOps.Api.Services.Operations;
using System.Security.Claims;
using ServiceOps.Api.Authorization;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ServiceOps.Api.Data;
using ServiceOps.Api.Services.Auth;
using ServiceOps.Api.Services.Interfaces;
using ServiceOps.Api.Services.Users;

var builder = WebApplication.CreateBuilder(args);
ServiceOps.Api.Services.Configuration.PostgresConfiguration.Configure(builder.Configuration);

// ======================================================
// DATABASE
// ======================================================

builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddDbContext<AppDbContext>((services, options) =>
    options.AddInterceptors(services.GetRequiredService<AuditInterceptor>()).UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);


// ======================================================
// CONTROLLERS
// ======================================================

builder.Services.AddControllers();
builder.Services.AddRateLimiter(options=>
{
    options.RejectionStatusCode=StatusCodes.Status429TooManyRequests;
    options.AddPolicy("contact-inquiries", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 10, Window = TimeSpan.FromMinutes(10), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("account-security",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions {PermitLimit=30,Window=TimeSpan.FromMinutes(1),QueueLimit=0,AutoReplenishment=true}));
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddHostedService<ServiceOps.Api.Services.Notifications.OverdueNotificationService>();
builder.Services.AddScoped<OperationsAccess>();
builder.Services.AddSingleton<AttachmentStorage>();
builder.Services.AddSingleton<ServiceReportPdf>();
GlobalFontSettings.FontResolver = new ServiceOpsFontResolver();


// ======================================================
// OPENAPI
// ======================================================

builder.Services.AddOpenApi();


// ======================================================
// SERVICE REGISTRATION
// ======================================================

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();

// ======================================================
// CORS
// Allows the Next.js frontend running on localhost:3000
// to communicate with the ASP.NET Core API.
// ======================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("ServiceOpsFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


// ======================================================
// JWT AUTHENTICATION
// ======================================================

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT key is not configured."
    );

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT issuer is not configured."
    );

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT audience is not configured."
    );

if (Encoding.UTF8.GetByteCount(jwtKey) < 32 || string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
    throw new InvalidOperationException("JWT configuration requires a signing key of at least 32 bytes and nonempty issuer and audience.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal!;
                if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                {
                    context.Fail("Invalid account identifier.");
                    return;
                }
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var user = await db.Users.AsNoTracking().Include(u => u.Role)
                    .SingleOrDefaultAsync(u => u.UserId == id, context.HttpContext.RequestAborted);
                var sessionValid=Guid.TryParse(principal.FindFirstValue("session_id"),out var sessionId)&&await db.RefreshSessions.AnyAsync(s=>s.RefreshSessionId==sessionId&&s.UserId==id&&s.RevokedAt==null&&s.ExpiresAt>DateTime.UtcNow,context.HttpContext.RequestAborted);
                if (!sessionValid || user == null || user.AccountStatus != "Active" ||
                    !ServiceOpsRoles.All.Contains(user.Role.RoleName) ||
                    principal.FindFirstValue(ClaimTypes.Role) != user.Role.RoleName ||
                    principal.FindFirstValue(ClaimTypes.Email) != user.Email ||
                    principal.FindFirstValue(ClaimTypes.Name) != user.FullName ||
                    !PasswordStamp.Valid(principal.FindFirstValue("password_version"), user.PasswordHash, jwtKey))
                    context.Fail("Account is inactive or its details have changed. Sign in again.");
                else if (principal.Identity is ClaimsIdentity identity)
                {
                    foreach (var claim in identity.FindAll("must_change_password").ToList()) identity.RemoveClaim(claim);
                    identity.AddClaim(new Claim("must_change_password", user.MustChangePassword ? "true" : "false"));
                }
            }
        };
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)
                    ),

                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.Zero
            };
    });


// ======================================================
// AUTHORIZATION
// ======================================================

builder.Services.AddAuthorization(options =>
{
    foreach (var name in PermissionCatalog.Defaults.Keys)
        options.AddPolicy("permission:" + name, policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(name)));
    foreach (var role in ServiceOpsRoles.All)
        options.AddPolicy(role, policy => policy.RequireAuthenticatedUser().RequireRole(role));
});


// ======================================================
// BUILD APPLICATION
// ======================================================

var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next(context);
});

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();

    var dbContext =
        scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

    await DevDataSeeder.SeedAsync(
        dbContext,
        app.Configuration,
        app.Environment,
        scope.ServiceProvider.GetRequiredService<IUserService>()
    );
}


using (var permissionScope = app.Services.CreateScope())
    await PermissionCatalog.InitializeAsync(permissionScope.ServiceProvider.GetRequiredService<AppDbContext>());

// ======================================================
// DEVELOPMENT OPENAPI
// ======================================================

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ServiceOps API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "ServiceOps API";
        options.ConfigObject.AdditionalItems["persistAuthorization"] = false;
    });
}


// ======================================================
// MIDDLEWARE PIPELINE
// ======================================================

app.UseCors("ServiceOpsFrontend");

app.UseAuthentication();
app.UseMiddleware<InitialPasswordChangeMiddleware>();

app.UseRateLimiter();

app.UseAuthorization();


// ======================================================
// CONTROLLER ENDPOINTS
// ======================================================

app.MapControllers();


// ======================================================
// SIMPLE HEALTH CHECK
// ======================================================

app.MapGet("/", () => new
{
    application = "ServiceOps API",
    status = "Running",
    timestamp = DateTime.UtcNow
});


// ======================================================
// RUN APPLICATION
// ======================================================

app.Run();
