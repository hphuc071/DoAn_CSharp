using System.Text.Json;
using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Configuration;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Api.Features.Admin.Reports;

public sealed record StoredReport(long Owner, ReportSnapshot Snapshot, bool Audit = false);
public sealed record StoredReportJob(long Owner, Guid Operation, ReportExportJob Job);

/// <summary>Snapshot/job bền vững trên thư mục riêng theo database; không thêm bảng, không đưa tệp báo cáo vào source/Git.</summary>
public sealed class ReportSnapshotStore(IConfiguration configuration)
{
    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    public string Root => Path.Combine(configuration["Reports:StoragePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "reports"), DatabaseSettings.DatabaseName);
    /// <summary>Lưu payload đã đọc nhất quán cùng người yêu cầu để xuất lại không truy vấn số mới.</summary>
    public Task Save(ReportSnapshot snapshot, long owner, bool audit = false) => Write(snapshot.MaSnapshot.ToString("N") + ".snapshot.json", new StoredReport(owner, snapshot, audit));
    /// <summary>Ràng buộc snapshot với Admin và database hiện hành, không tin owner do client gửi.</summary>
    public async Task<StoredReport> Read(Guid id, long owner)
    {
        var result = await Load<StoredReport>(id.ToString("N") + ".snapshot.json") ?? throw new AdminRule(404, "Snapshot không còn sẵn sàng, hãy tải lại báo cáo.");
        if (result.Owner != owner) throw new AdminRule(404, "Không tìm thấy snapshot.");
        return result;
    }
    /// <summary>Ghi trạng thái bằng temp + rename; không trả file JSON mới ghi một phần khi UI polling.</summary>
    public async Task Write<T>(string name, T value)
    {
        Directory.CreateDirectory(Root); var target = Path.Combine(Root, name); var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(value, json)); File.Move(temp, target, true);
    }
    /// <summary>Đọc metadata nội bộ; mọi tên nhận từ API phải được dựng từ Guid ở service, không nhận đường dẫn tùy ý.</summary>
    public async Task<T?> Load<T>(string name)
    {
        var file = Path.Combine(Root, name); return File.Exists(file) ? JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(file), json) : default;
    }
    /// <summary>Tên tệp hoàn chỉnh chỉ dùng Guid và định dạng đã được máy chủ kiểm tra.</summary>
    public string Output(Guid id, string format) => Path.Combine(Root, id.ToString("N") + (format == "PDF" ? ".pdf" : ".xlsx"));
}
