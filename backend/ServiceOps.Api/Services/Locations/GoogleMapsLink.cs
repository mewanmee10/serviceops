namespace ServiceOps.Api.Services.Locations;

/// <summary>Validates an external Maps link only. Never resolves, fetches or geocodes it.</summary>
public static class GoogleMapsLink
{
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        value = value.Trim();
        if (value.Length > 2048 || value.Any(char.IsControl) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort) return false;
        var host = uri.IdnHost.ToLowerInvariant();
        var path = uri.AbsolutePath;
        return host switch
        {
            "google.com" or "www.google.com" => path == "/maps" || path.StartsWith("/maps/", StringComparison.Ordinal),
            "maps.google.com" => true,
            "maps.app.goo.gl" => path.Length > 1,
            "goo.gl" => path.StartsWith("/maps/", StringComparison.Ordinal),
            _ => false
        };
    }
}
