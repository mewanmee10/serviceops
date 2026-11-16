using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceOps.Api.Data;
using ServiceOps.Api.Models;
using ServiceOps.Api.Services.Interfaces;

namespace ServiceOps.Api.Controllers;

[ApiController, Route("api/contact-inquiries"), TypeFilter(typeof(AccountApiExceptionFilter))]
public sealed class ContactInquiriesController(AppDbContext db) : ControllerBase
{
    public sealed class InquiryRequest : IValidatableObject
    {
        public Guid SubmissionId { get; set; }
        [Required, StringLength(100)] public string FullName { get; set; } = "";
        [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
        [Phone, StringLength(32)] public string? PhoneNumber { get; set; }
        [Required, StringLength(200)] public string Subject { get; set; } = "";
        [Required, StringLength(4000)] public string Message { get; set; } = "";
        public bool PrivacyAcknowledged { get; set; }
        [StringLength(2048)] public string? Website { get; set; }
        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            if (SubmissionId == Guid.Empty) yield return new ValidationResult("A submission identifier is required.", [nameof(SubmissionId)]);
            if (!PrivacyAcknowledged) yield return new ValidationResult("Acknowledge that your details will be used to respond to this inquiry.", [nameof(PrivacyAcknowledged)]);
            if (!string.IsNullOrEmpty(Website)) yield return new ValidationResult("Submission could not be accepted.");
            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Subject) || string.IsNullOrWhiteSpace(Message))
                yield return new ValidationResult("Name, subject and message cannot be blank.");
        }
    }
    private static object Acknowledgment => new { message = "Your inquiry has been received for review by the ServiceOps administrator. No email delivery is implied." };

    [HttpPost, AllowAnonymous, EnableRateLimiting("contact-inquiries"), RequestSizeLimit(16384)]
    public async Task<IActionResult> Submit(InquiryRequest input)
    {
        var existing = await db.ContactInquiries.AsNoTracking().SingleOrDefaultAsync(i => i.SubmissionId == input.SubmissionId);
        if (existing != null) return Ok(Acknowledgment); // No personal fields are returned for an idempotent retry.
        db.ContactInquiries.Add(new ContactInquiry { SubmissionId = input.SubmissionId, FullName = input.FullName.Trim(),
            Email = input.Email.Trim().ToLowerInvariant(), PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim(),
            Subject = input.Subject.Trim(), Message = input.Message.Trim() });
        var administrators = await db.Users.Where(u => u.Role.RoleName == "Administrator" && u.AccountStatus == "Active").Select(u => u.UserId).ToListAsync();
        foreach (var userId in administrators)
            db.Notifications.Add(new Notification { UserId = userId, Message = "A new contact inquiry is available for administrator review." });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        { return Ok(Acknowledgment); }
        return StatusCode(StatusCodes.Status201Created, Acknowledgment);
    }
    [HttpGet, Authorize(Roles = "Administrator")]
    public async Task<IActionResult> List(int page = 1, bool? reviewed = null, string? search = null)
    {
        if (page < 1 || page > 1000000 || search?.Length > 200) return BadRequest();
        var query = db.ContactInquiries.AsNoTracking().Where(i => reviewed == null || (i.ReviewedAt != null) == reviewed);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(i => i.FullName.Contains(term) || i.Email.Contains(term) || i.Subject.Contains(term) || i.Message.Contains(term)); }
        return Ok(new { Total = await query.CountAsync(), Page = page, PageSize = 25,
            Items = await query.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.ContactInquiryId)
                .Skip((page - 1) * 25).Take(25).Select(i => new { i.ContactInquiryId, i.FullName, i.Email, i.Subject, i.CreatedAt, i.ReviewedAt }).ToListAsync() });
    }
    [HttpGet("{id:int}"), Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Details(int id)
    {
        var inquiry = await db.ContactInquiries.AsNoTracking().Where(i => i.ContactInquiryId == id)
            .Select(i => new { i.ContactInquiryId, i.FullName, i.Email, i.PhoneNumber, i.Subject, i.Message, i.CreatedAt, i.ReviewedAt }).SingleOrDefaultAsync();
        return inquiry == null ? NotFound() : Ok(inquiry);
    }
    [HttpPatch("{id:int}/review"), Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Review(int id)
    {
        var inquiry = await db.ContactInquiries.SingleOrDefaultAsync(i => i.ContactInquiryId == id);
        if (inquiry == null) return NotFound();
        if (inquiry.ReviewedAt == null)
        {
            inquiry.ReviewedAt = DateTime.UtcNow;
            inquiry.ReviewedByUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await db.SaveChangesAsync();
        }
        return NoContent();
    }
}
