using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Authorization;
using ServiceOps.Api.Data;
using ServiceOps.Api.DTOs.Operations;
using ServiceOps.Api.Models;
using ServiceOps.Api.Services.Interfaces;
using ServiceOps.Api.Services.Operations;
namespace ServiceOps.Api.Controllers;

[ApiController, Route("api"), Authorize(Roles = "Administrator,Service Manager,Customer"), TypeFilter(typeof(AccountApiExceptionFilter))]
public class CatalogController(AppDbContext db, OperationsAccess access, IUserService users) : ControllerBase
{
    [HttpPost("customers/onboard"), Authorize(Roles = "Administrator,Service Manager")]
    public async Task<IActionResult> OnboardCustomer(CustomerOnboardingRequest input)
    {
        if (string.IsNullOrWhiteSpace(input.FullName) || string.IsNullOrWhiteSpace(input.CustomerName))
            return BadRequest(new { message = "Account and customer names are required." });
        var roleId = await db.Roles.Where(r => r.RoleName == ServiceOpsRoles.Customer).Select(r => r.RoleId).SingleAsync();
        ServiceOps.Api.DTOs.Users.UserResponse account;
        try
        {
            account = await users.CreateUserAsync(new ServiceOps.Api.DTOs.Users.CreateUserRequest
            {
                FullName = input.FullName, Email = input.Email, Password = input.Password, ConfirmPassword = input.Password,
                CustomerName = input.CustomerName, PhoneNumber = input.PhoneNumber, RoleId = roleId
            });
        }
        catch (ArgumentException) { return BadRequest(new { message = "Invalid customer account information." }); }
        catch (InvalidOperationException) { return Conflict(new { message = "A user with this email already exists." }); }
        var customer = await db.Customers.Where(c => c.UserId == account.UserId)
            .Select(c => new { c.CustomerId, c.UserId, c.CustomerName }).SingleAsync();
        return Created($"/api/customers/{customer.CustomerId}", customer);
    }
    [HttpGet("customers")]
    public async Task<IActionResult> Customers() => Ok(await access.Customers.AsNoTracking().OrderBy(c => c.CustomerId)
        .Select(c => new { c.CustomerId, c.UserId, c.CustomerName, c.AddressLine, c.City, c.Province, c.PostalCode, c.Notes, c.User.Email, c.User.PhoneNumber }).ToListAsync());

    [HttpPost("customers"), Authorize(Roles = "Administrator,Service Manager")]
    public async Task<IActionResult> CreateCustomer(ProfileRequest input)
    {
        var user = await db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == input.UserId);
        if (user?.Role.RoleName != ServiceOpsRoles.Customer) return BadRequest(new { message = "Select a Customer account." });
        if (await db.Customers.AnyAsync(c => c.UserId == input.UserId)) return Conflict(new { message = "Customer profile already exists." });
        if (input.PhoneNumber != null) user.PhoneNumber = input.PhoneNumber;
        var c = new Customer { UserId = input.UserId, CustomerName = input.Name.Trim(), AddressLine = input.AddressLine, City = input.City, Province = input.Province, PostalCode = input.PostalCode, Notes = NormalizeNotes(input.Notes) }; db.Customers.Add(c); await db.SaveChangesAsync();
        return Created($"/api/customers/{c.CustomerId}", new { c.CustomerId, c.UserId, c.CustomerName, c.Notes });
    }
    [HttpGet("customers/{id:int}")]
    public async Task<IActionResult> Customer(int id) { var c = await access.Customers.Where(c => c.CustomerId == id).Select(c => new { c.CustomerId, c.UserId, c.CustomerName, c.City, c.AddressLine, c.Province, c.PostalCode, c.Notes, c.User.Email, c.User.PhoneNumber }).SingleOrDefaultAsync(); return c == null ? NotFound() : Ok(c); }
    [HttpPut("customers/{id:int}")]
    public async Task<IActionResult> UpdateCustomer(int id, ProfileRequest input)
    {
        var customer = await access.Customers.Include(c => c.User)
            .SingleOrDefaultAsync(c => c.CustomerId == id);
        if (customer == null) return NotFound();
        if (input.UserId != customer.UserId)
            return BadRequest(new { message = "Account reassignment is not supported." });

        customer.CustomerName = input.Name.Trim();
        if (input.ReplaceDetails || input.AddressLine != null) customer.AddressLine = input.AddressLine;
        if (input.ReplaceDetails || input.City != null) customer.City = input.City;
        if (input.ReplaceDetails || input.Province != null) customer.Province = input.Province;
        if (input.ReplaceDetails || input.PostalCode != null) customer.PostalCode = input.PostalCode;
        if (input.ReplaceDetails || input.Notes != null) customer.Notes = NormalizeNotes(input.Notes);
        if (input.ReplaceDetails || input.PhoneNumber != null) customer.User.PhoneNumber = input.PhoneNumber;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string? NormalizeNotes(string? notes) => string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    [HttpGet("sites")]
    public async Task<IActionResult> Sites() => Ok(await db.CustomerSites.AsNoTracking().Where(s => access.Customers.Any(c => c.CustomerId == s.CustomerId)).OrderBy(s => s.SiteId)
        .Select(s => new { s.SiteId, s.CustomerId, s.SiteName, s.AddressLine, s.City, s.Province, s.PostalCode, s.LocationDetails, s.MapsUrl, s.Latitude, s.Longitude }).ToListAsync());
    [HttpPost("sites")]
    public async Task<IActionResult> CreateSite(SiteRequest input)
    { if (!await access.Customers.AnyAsync(c => c.CustomerId == input.CustomerId)) return NotFound(); var s = new CustomerSite { CustomerId = input.CustomerId, SiteName = input.SiteName.Trim(), AddressLine = input.AddressLine.Trim(), City = input.City, Province = input.Province, PostalCode = input.PostalCode, LocationDetails = input.LocationDetails, MapsUrl = string.IsNullOrWhiteSpace(input.MapsUrl) ? null : input.MapsUrl.Trim(), Latitude = input.Latitude, Longitude = input.Longitude }; db.CustomerSites.Add(s); await db.SaveChangesAsync(); return Created($"/api/sites/{s.SiteId}", new { s.SiteId, s.CustomerId, s.SiteName, s.AddressLine, s.City, s.Province, s.PostalCode, s.LocationDetails, s.MapsUrl, s.Latitude, s.Longitude }); }
    [HttpGet("sites/{id:int}")]
    public async Task<IActionResult> Site(int id) { var s = await db.CustomerSites.Where(s => s.SiteId == id && access.Customers.Any(c => c.CustomerId == s.CustomerId)).Select(s => new { s.SiteId, s.CustomerId, s.SiteName, s.AddressLine, s.City, s.Province, s.PostalCode, s.LocationDetails, s.MapsUrl, s.Latitude, s.Longitude }).SingleOrDefaultAsync(); return s == null ? NotFound() : Ok(s); }
    [HttpPut("sites/{id:int}")]
    public async Task<IActionResult> UpdateSite(int id, SiteRequest input)
    { var s = await db.CustomerSites.SingleOrDefaultAsync(s => s.SiteId == id && access.Customers.Any(c => c.CustomerId == s.CustomerId)); if (s == null) return NotFound(); if (s.CustomerId != input.CustomerId) return BadRequest(new { message = "Site reassignment is not supported." }); s.SiteName = input.SiteName.Trim(); s.AddressLine = input.AddressLine.Trim(); s.City = input.City; if (input.ReplaceDetails || input.Province != null) s.Province = input.Province; if (input.ReplaceDetails || input.PostalCode != null) s.PostalCode = input.PostalCode; if (input.ReplaceDetails || input.LocationDetails != null) s.LocationDetails = input.LocationDetails; if (input.ReplaceDetails || input.MapsUrl != null) s.MapsUrl = string.IsNullOrWhiteSpace(input.MapsUrl) ? null : input.MapsUrl.Trim(); if (input.Latitude.HasValue || input.Longitude.HasValue) { s.Latitude = input.Latitude; s.Longitude = input.Longitude; } await db.SaveChangesAsync(); return NoContent(); }

    [HttpGet("assets")]
    public async Task<IActionResult> Assets() => Ok(await db.Assets.AsNoTracking().Where(a => access.Customers.Any(c => c.CustomerId == a.CustomerSite.CustomerId))
        .Select(a => new { a.AssetId, a.SiteId, a.AssetName, a.Category, a.SerialNumber, a.InstallationDate, a.WarrantyInformation }).ToListAsync());
    [HttpPost("assets")]
    public async Task<IActionResult> CreateAsset(AssetRequest input)
    { if (input.InstallationDate > DateTimeOffset.UtcNow) return BadRequest(new { message = "Installation date cannot be in the future." }); if (!await db.CustomerSites.AnyAsync(s => s.SiteId == input.SiteId && access.Customers.Any(c => c.CustomerId == s.CustomerId))) return NotFound(); var a = new Asset { SiteId = input.SiteId, AssetName = input.AssetName.Trim(), Category = input.Category, SerialNumber = input.SerialNumber, InstallationDate = input.InstallationDate?.UtcDateTime, WarrantyInformation = input.WarrantyInformation }; db.Assets.Add(a); await db.SaveChangesAsync(); return Created($"/api/assets/{a.AssetId}", new { a.AssetId, a.SiteId, a.AssetName, a.Category, a.SerialNumber, a.InstallationDate, a.WarrantyInformation }); }
    [HttpGet("assets/{id:int}")]
    public async Task<IActionResult> Asset(int id) { var a = await db.Assets.Where(a => a.AssetId == id && access.Customers.Any(c => c.CustomerId == a.CustomerSite.CustomerId)).Select(a => new { a.AssetId, a.SiteId, a.AssetName, a.Category, a.SerialNumber, a.InstallationDate, a.WarrantyInformation }).SingleOrDefaultAsync(); return a == null ? NotFound() : Ok(a); }
    [HttpPut("assets/{id:int}")]
    public async Task<IActionResult> UpdateAsset(int id, AssetRequest input)
    { if (input.InstallationDate > DateTimeOffset.UtcNow) return BadRequest(new { message = "Installation date cannot be in the future." }); var a = await db.Assets.SingleOrDefaultAsync(a => a.AssetId == id && access.Customers.Any(c => c.CustomerId == a.CustomerSite.CustomerId)); if (a == null) return NotFound(); if (a.SiteId != input.SiteId) return BadRequest(new { message = "Asset relocation requires coordination." }); a.AssetName = input.AssetName.Trim(); a.Category = input.Category; a.SerialNumber = input.SerialNumber; if (input.ReplaceDetails || input.InstallationDate != null) a.InstallationDate = input.InstallationDate?.UtcDateTime; if (input.ReplaceDetails || input.WarrantyInformation != null) a.WarrantyInformation = input.WarrantyInformation; await db.SaveChangesAsync(); return NoContent(); }
}
