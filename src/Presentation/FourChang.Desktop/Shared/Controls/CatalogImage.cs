using FourChang.Desktop.Shared.Theme;
namespace FourChang.Desktop.Shared.Controls;

/// <summary>Ảnh menu theo đường dẫn API; lỗi ảnh để trống nhẹ nhàng, không gán nhầm ảnh món khác.</summary>
internal static class CatalogImage
{
    private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
    /// <summary>Tái sử dụng sprite thương hiệu; ảnh khác tải bất đồng bộ, có giới hạn và hủy khi rời trang.</summary>
    internal static Control Create(string? path, string label, int height)
    {
        if (path?.StartsWith("sprite:", StringComparison.Ordinal) == true && int.TryParse(path[7..], out var index) && index is >= 0 and < 6)
            return new ProductImage { ImageIndex = index, Dock = DockStyle.Top, Height = height, AccessibleName = label };
        var box = new PictureBox { Dock = DockStyle.Top, Height = height, SizeMode = PictureBoxSizeMode.Zoom, BackColor = CafeTheme.Soft, AccessibleName = label };
        var lifetime = new CancellationTokenSource(); var token = lifetime.Token; bool started = false, disposed = false;
        box.HandleCreated += async (_, _) =>
        {
            if (started) return; started = true;
            try
            {
                byte[] bytes;
                if (path?.StartsWith("/assets/", StringComparison.Ordinal) == true && !path.Contains("..") && !path.Contains('\\'))
                    bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Assets", Path.GetFileName(path)), token);
                else if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme == "https")
                {
                    using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength > 6 * 1024 * 1024) return;
                    await using var source = await response.Content.ReadAsStreamAsync(token); using var output = new MemoryStream(); var buffer = new byte[8192]; int n;
                    while ((n = await source.ReadAsync(buffer, token)) > 0) { if (output.Length + n > 6 * 1024 * 1024) return; output.Write(buffer, 0, n); }
                    bytes = output.ToArray();
                }
                else return;
                using var memory = new MemoryStream(bytes); using var decoded = Image.FromStream(memory); var image = new Bitmap(decoded);
                if (box.IsDisposed) image.Dispose(); else box.Image = image;
            }
            catch (Exception ex) when (ex is IOException or HttpRequestException or OperationCanceledException or ArgumentException or OutOfMemoryException) { }
        };
        box.Disposed += (_, _) => { if (disposed) return; disposed = true; lifetime.Cancel(); lifetime.Dispose(); box.Image?.Dispose(); }; return box;
    }
}
