using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace ServiceOps.Api.Services.Attachments;

/// <summary>Private Vercel Blob storage. URLs and credentials never leave the API.</summary>
internal sealed class BlobAttachmentStore(IConfiguration configuration)
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(30) };
    private string StoreId
    {
        get
        {
            var id = configuration["BLOB_STORE_ID"] ?? throw new InvalidOperationException("Private attachment store is not configured.");
            if (id.StartsWith("store_", StringComparison.Ordinal)) id = id[6..];
            if (!Regex.IsMatch(id, "^[a-zA-Z0-9]{8,64}$")) throw new InvalidOperationException("Invalid private attachment store configuration.");
            return id;
        }
    }
    private HttpRequestMessage Request(HttpMethod method, string url)
    {
        var token = configuration["BLOB_READ_WRITE_TOKEN"] ?? configuration["VERCEL_OIDC_TOKEN"];
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Private attachment credentials are not configured.");
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("x-api-version", "12");
        request.Headers.Add("x-vercel-blob-store-id", StoreId);
        return request;
    }
    private string Url(string key) => $"https://{StoreId.ToLowerInvariant()}.private.blob.vercel-storage.com/{key}";
    public async Task WriteAsync(string key, byte[] data, string contentType, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Put, "https://vercel.com/api/blob/?pathname=" + Uri.EscapeDataString(key));
        request.Headers.Add("x-vercel-blob-access", "private");
        request.Headers.Add("x-add-random-suffix", "0");
        request.Headers.Add("x-allow-overwrite", "0");
        request.Headers.Add("x-content-type", contentType);
        request.Headers.Add("x-content-length", data.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Content = new ByteArrayContent(data);
        using var response = await Client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new IOException($"Private attachment storage could not accept the file (HTTP {(int)response.StatusCode}).");
    }
    public async Task<byte[]?> ReadAsync(string key, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Get, Url(key));
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) throw new IOException("Private attachment storage is unavailable.");
        if (response.Content.Headers.ContentLength > AttachmentStorage.MaximumBytes) throw new IOException("Stored attachment exceeds the allowed size.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0) break;
            if (output.Length + read > AttachmentStorage.MaximumBytes) throw new IOException("Stored attachment exceeds the allowed size.");
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return output.ToArray();
    }
    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Post, "https://vercel.com/api/blob/delete");
        request.Content = JsonContent.Create(new { urls = new[] { Url(key) } });
        using var response = await Client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            throw new IOException($"Private attachment cleanup is unavailable (HTTP {(int)response.StatusCode}).");
    }
}
