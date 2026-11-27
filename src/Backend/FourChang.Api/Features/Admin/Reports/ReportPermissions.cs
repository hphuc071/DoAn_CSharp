// UC: UC-ADM-07
// Chức năng: Kiểm tra quyền xem và xuất các nhóm dữ liệu báo cáo.
// Cần nhớ: Đọc hàm từ bước kiểm tra dữ liệu đến bước trả kết quả; các quyết định nghiệp vụ do Backend kiểm tra.

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
