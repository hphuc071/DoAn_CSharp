using Microsoft.Data.SqlClient;
namespace FourChang.Infrastructure.Configuration;

public static class DatabaseSettings
{
    public static string DatabaseName => Environment.GetEnvironmentVariable("FOURCHANG_DATABASE") ?? "FourChangDb";
    public static string ConnectionString(bool administrator = false, string? database = null)
    {
        if (!administrator && Environment.GetEnvironmentVariable("ConnectionStrings__FourChang") is { Length: > 0 } configured) return configured;
        var passwordKey = administrator ? "SQL_SA_PASSWORD" : "SQL_APP_PASSWORD";
        var password = Environment.GetEnvironmentVariable(passwordKey);
        if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException($"Thiếu {passwordKey}. Chạy ChuanBi-Database.ps1.");
        return new SqlConnectionStringBuilder
        {
            DataSource = Environment.GetEnvironmentVariable("FOURCHANG_SQL_SERVER") ?? "127.0.0.1,14330",
            InitialCatalog = database ?? DatabaseName,
            UserID = administrator ? "sa" : "fourchang_app",
            Password = password,
            Encrypt = true,
            TrustServerCertificate = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase) || Environment.GetEnvironmentVariable("FOURCHANG_LOCAL") == "true",
            ConnectTimeout = 15,
            ApplicationName = "FourChang.Api"
        }.ConnectionString;
    }
}
