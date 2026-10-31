namespace FourChang.Infrastructure.Configuration;

// Chỉ backend/bootstrap đọc .env. Không gửi mật khẩu SQL tới Web browser hoặc WinForms.
public static class LocalEnvironment
{
    public static void Load(string? explicitPath = null)
    {
        var path = explicitPath;
        if (path is null)
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FourChang.sln"))) dir = dir.Parent;
            if (dir is not null) path = Path.Combine(dir.FullName, ".env");
        }
        if (path is null || !File.Exists(path)) return;
        foreach (var line in File.ReadAllLines(path))
        {
            var value = line.Trim();
            if (value.Length == 0 || value.StartsWith('#')) continue;
            var separator = value.IndexOf('=');
            if (separator <= 0) continue;
            var name = value[..separator].Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$")) continue;
            if (Environment.GetEnvironmentVariable(name) is not null) continue;
            Environment.SetEnvironmentVariable(name, value[(separator + 1)..].Trim().Trim('"', '\''));
        }
    }
}
