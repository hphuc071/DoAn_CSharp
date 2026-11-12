using System.Text.Json;
using FourChang.Api.Shared.Documents;
using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Invoices;

/// <summary>Dựng tài liệu từ hóa đơn đã phát hành, không tạo invoice/thu/hoàn khi người dùng xem hoặc xuất.</summary>
public sealed class InvoicePdfService(CafeDbContext db, IConfiguration configuration)
{
    /// <summary>Chỉ đọc snapshot bất biến, thêm định danh chứng từ và nguồn gốc vào bản PDF có font nhúng.</summary>
    public async Task<byte[]> Render(long id)
    {
        var invoice = await db.HoaDon.AsNoTracking().SingleOrDefaultAsync(x => x.MaHoaDon == id && x.TrangThai == "ISSUED")
            ?? throw new AdminRule(404, "Chưa có hóa đơn đã phát hành.");
        var rows = new List<IReadOnlyList<string?>> { new string?[] { "Số hóa đơn", invoice.SoHoaDon }, new string?[] { "Loại", invoice.LoaiHoaDon },
            new string?[] { "Đơn", invoice.MaDonHang.ToString() }, new string?[] { "Hóa đơn gốc", invoice.MaHoaDonGoc?.ToString() },
            new string?[] { "Người mua", invoice.TenNguoiMua }, new string?[] { "Giá trị chứng từ (đ)", invoice.SoTien.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            new string?[] { "Phát hành UTC", invoice.PhatHanhLuc?.ToString("O") } };
        using var json = JsonDocument.Parse(invoice.NoiDungHoaDonDaChotJson); Flatten(json.RootElement, "Nội dung đã chốt", rows);
        var instant = invoice.PhatHanhLuc!.Value;
        var report = new ReportSnapshot(Guid.Empty, instant, new(instant, instant.AddTicks(1)), "Asia/Ho_Chi_Minh (UTC+07)",
            "Tài liệu bán hàng nội bộ từ snapshot đã phát hành; không phải phát hành lại hoặc tích hợp hóa đơn điện tử.",
            new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null), [new("Hóa đơn " + invoice.SoHoaDon, [new("Thông tin"), new("Nội dung")], rows)], "INVOICE");
        return ReportDocumentWriter.Pdf(report, configuration["Reports:FontPath"]);
    }
    /// <summary>Đọc mọi trường snapshot cũ theo cấu trúc gốc; không suy giá/số lượng bằng menu hiện hành.</summary>
    private static void Flatten(JsonElement value, string path, List<IReadOnlyList<string?>> rows)
    {
        if (value.ValueKind == JsonValueKind.Object) { foreach (var field in value.EnumerateObject()) Flatten(field.Value, path + " / " + field.Name, rows); return; }
        if (value.ValueKind == JsonValueKind.Array) { var index = 0; foreach (var item in value.EnumerateArray()) Flatten(item, path + " [" + (++index) + "]", rows); return; }
        rows.Add(new string?[] { path, value.ToString() });
    }
}
