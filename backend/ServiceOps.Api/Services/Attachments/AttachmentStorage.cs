namespace ServiceOps.Api.Services.Attachments;

public sealed class AttachmentStorage(IWebHostEnvironment environment, IConfiguration configuration)
{
    public const long MaximumBytes = 8 * 1024 * 1024;
    public string Root { get; } = Path.GetFullPath(configuration["Attachments:Root"] ?? Path.Combine(environment.ContentRootPath, "App_Data", "attachments"));
    public string Resolve(string key)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(key, "^[a-f0-9]{32}\\.(png|jpg|pdf)$"))
            throw new InvalidOperationException("Invalid stored attachment identifier.");
        return Path.Combine(Root, key);
    }
    private bool UsesBlob => string.Equals(configuration["Attachments:Provider"], "VercelBlob", StringComparison.OrdinalIgnoreCase);
    public async Task WriteAsync(string key, byte[] bytes, string contentType, CancellationToken ct)
    {
        var path = Resolve(key); // Apply identical strict key validation for both providers.
        if (UsesBlob) { await new BlobAttachmentStore(configuration).WriteAsync(key, bytes, contentType, ct); return; }
        Directory.CreateDirectory(Root);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await output.WriteAsync(bytes, ct);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
    public async Task<byte[]?> ReadAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        if (UsesBlob) return await new BlobAttachmentStore(configuration).ReadAsync(key, ct);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
    }
    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        if (UsesBlob) { await new BlobAttachmentStore(configuration).DeleteAsync(key, ct); return; }
        if (File.Exists(path)) File.Delete(path);
    }
    public static string? Extension(string filename, string mime, ReadOnlySpan<byte> bytes)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        if (ext == ".png" && mime == "image/png" && bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return ".png";
        if ((ext == ".jpg" || ext == ".jpeg") && mime == "image/jpeg" && bytes.StartsWith(new byte[] { 255, 216, 255 })) return ".jpg";
        if (ext == ".pdf" && mime == "application/pdf" && bytes.StartsWith("%PDF-"u8)) return ".pdf";
        return null;
    }
}
