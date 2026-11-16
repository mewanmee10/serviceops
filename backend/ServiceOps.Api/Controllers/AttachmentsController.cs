using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Data;
using ServiceOps.Api.Models;
using ServiceOps.Api.Services.Attachments;
using ServiceOps.Api.Services.Interfaces;
using ServiceOps.Api.Services.Operations;
namespace ServiceOps.Api.Controllers;

[ApiController, Route("api"), Authorize(Roles = "Administrator,Service Manager,Technician,Customer"), TypeFilter(typeof(AccountApiExceptionFilter))]
public sealed class AttachmentsController(AppDbContext db, OperationsAccess access, AttachmentStorage storage) : ControllerBase
{
    [HttpGet("requests/{id:int}/attachments")]
    public Task<IActionResult> RequestAttachments(int id) => List(id, null);
    [HttpGet("work-orders/{id:int}/attachments")]
    public Task<IActionResult> JobAttachments(int id) => List(null, id);
    [Authorize(Policy = "permission:evidence.upload")]
    [HttpPost("requests/{id:int}/attachments"), RequestSizeLimit(9 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 9 * 1024 * 1024)]
    public Task<IActionResult> UploadRequest(int id, IFormFile file, CancellationToken ct) => Upload(id, null, file, ct);
    [Authorize(Policy = "permission:evidence.upload")]
    [HttpPost("work-orders/{id:int}/attachments"), RequestSizeLimit(9 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 9 * 1024 * 1024)]
    public Task<IActionResult> UploadJob(int id, IFormFile file, CancellationToken ct) => Upload(null, id, file, ct);

    private async Task<bool> Allowed(int? requestId, int? jobId) => requestId != null
        ? await access.Requests.AnyAsync(r => r.ServiceRequestId == requestId)
        : await access.Jobs.AnyAsync(w => w.WorkOrderId == jobId);
    private async Task<IActionResult> List(int? requestId, int? jobId)
    {
        if (!await Allowed(requestId, jobId)) return NotFound();
        return Ok(await db.Attachments.AsNoTracking().Where(a => requestId != null ? a.ServiceRequestId == requestId : a.WorkOrderId == jobId)
            .OrderByDescending(a => a.UploadedAt).Select(a => new { a.AttachmentId, a.FileName, a.FileType, a.UploadedAt, a.ServiceRequestId, a.WorkOrderId }).ToListAsync());
    }
    private async Task<IActionResult> Upload(int? requestId, int? jobId, IFormFile file, CancellationToken ct)
    {
        if (!await Allowed(requestId, jobId)) return NotFound();
        if (file == null || file.Length < 1 || file.Length > AttachmentStorage.MaximumBytes) return BadRequest(new { message = "Choose a file between 1 byte and 8 MiB." });
        var name = file.FileName;
        if (name.Length > 200 || name.Any(char.IsControl) || name.Contains('/') || name.Contains('\\') || name != Path.GetFileName(name)) return BadRequest(new { message = "Invalid file name." });
        using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Attachments\" IN SHARE ROW EXCLUSIVE MODE", ct);
        if (await db.Attachments.CountAsync(a => requestId != null ? a.ServiceRequestId == requestId : a.WorkOrderId == jobId) >= 50) return Conflict(new { message = "A maximum of 50 files may be attached to one record." });
        using var memory = new MemoryStream(); await file.CopyToAsync(memory, ct);
        if (memory.Length > AttachmentStorage.MaximumBytes) return BadRequest(new { message = "File exceeds 8 MiB." });
        var data = memory.ToArray(); var extension = AttachmentStorage.Extension(name, file.ContentType, data);
        if (extension == null) return BadRequest(new { message = "Only PNG, JPEG and PDF with matching content, extension and MIME type are permitted." });
        var key = Guid.NewGuid().ToString("N") + extension;
        var stored = false;
        try
        {
            await storage.WriteAsync(key, data, file.ContentType, ct);
            stored = true;
            var attachment = new Attachment { ServiceRequestId = requestId, WorkOrderId = jobId, FileName = name, FileType = file.ContentType, FilePath = key };
            db.Attachments.Add(attachment);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Created($"/api/attachments/{attachment.AttachmentId}", new { attachment.AttachmentId, attachment.FileName, attachment.FileType, attachment.UploadedAt });
        }
        catch (Exception error)
        {
            if (stored)
            {
                try { await storage.DeleteAsync(key, CancellationToken.None); }
                catch { /* Preserve the original failure; no uncommitted file is publicly accessible. */ }
            }
            if (error is IOException or HttpRequestException or TaskCanceledException)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Attachment storage is temporarily unavailable. Please retry." });
            throw;
        }
    }
    [HttpGet("attachments/{id:int}")]
    public async Task<IActionResult> Download(int id, bool preview = false)
    {
        var attachment = await db.Attachments.AsNoTracking().SingleOrDefaultAsync(a => a.AttachmentId == id);
        if (attachment == null || !await Allowed(attachment.ServiceRequestId, attachment.WorkOrderId)) return NotFound();
        byte[]? bytes;
        try { bytes = await storage.ReadAsync(attachment.FilePath, HttpContext.RequestAborted); }
        catch (Exception error) when (error is IOException or HttpRequestException or TaskCanceledException)
        { return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Attachment storage is temporarily unavailable. Please retry." }); }
        if (bytes == null) return NotFound(new { message = "Stored evidence is unavailable; contact the administrator." });
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox; frame-ancestors 'self'";
        Response.Headers.XFrameOptions = "SAMEORIGIN";
        if (preview && (attachment.FileType.StartsWith("image/", StringComparison.Ordinal) || attachment.FileType == "application/pdf")) return File(bytes, attachment.FileType);
        return File(bytes, attachment.FileType, attachment.FileName);
    }
}
