using FourChang.Api.Shared.Operations;

namespace FourChang.Api.Features.Admin.Reports;

/// <summary>Quyền báo cáo/audit/xuất độc lập qua cấu hình của Admin duy nhất; không thêm tài khoản hoặc bảng quyền.</summary>
public sealed class ReportPermissions(IConfiguration config)
{
    /// <summary>Kiểm tra quyền cụ thể ở từng API. Thiết lập false sẽ chặn dù người gọi có vai trò Admin.</summary>
    public void Demand(string permission)
    {
        if (bool.TryParse(config["AdminPermissions:" + permission], out var allowed) && !allowed)
            throw new AdminRule(403, "Không được cấp quyền " + permission + ".");
    }
}
