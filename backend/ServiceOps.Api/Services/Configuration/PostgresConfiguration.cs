using Npgsql;

namespace ServiceOps.Api.Services.Configuration;

/// <summary>Accept hosted PostgreSQL URLs without changing local connection settings.</summary>
public static class PostgresConfiguration
{
    public static void Configure(IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection"))) return;
        var value = configuration["DATABASE_URL"];
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("postgres" or "postgresql") || string.IsNullOrWhiteSpace(uri.Host))
            throw new InvalidOperationException("DATABASE_URL must be a PostgreSQL connection URL.");
        var credentials = uri.UserInfo.Split(':', 2);
        if (credentials.Length != 2 || uri.AbsolutePath.Length < 2)
            throw new InvalidOperationException("DATABASE_URL requires credentials and a database name.");
        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1]),
            SslMode = SslMode.VerifyFull,
            ApplicationName = "ServiceOps.Api"
        };
        configuration["ConnectionStrings:DefaultConnection"] = connection.ConnectionString;
    }
}
