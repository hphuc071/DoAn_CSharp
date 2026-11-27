// UC: UC-ADM-01
// Chức năng: Nhận yêu cầu quản lý khách hàng và chuyển sang lớp xử lý.
// Cần nhớ: Đọc hàm từ bước kiểm tra dữ liệu đến bước trả kết quả; các quyết định nghiệp vụ do Backend kiểm tra.

using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Api.Features.Admin.Customers;

/// <summary>Định tuyến UC-ADM-01 tinh gọn; tất cả thao tác khách hàng đều yêu cầu Admin.</summary>
public static class CustomerEndpoints
{
    /// <summary>Đăng ký tìm/xem/sửa, khóa/mở, reset và tra kết quả; chặn route tạo/xóa đời cũ.</summary>
    public static void MapAdminCustomers(this WebApplication app)
    {
        // BR-08: bảo vệ cả nhóm ở API; ẩn nút WinForms không đủ ngăn khách/POS gọi trực tiếp.
        var group = app.MapGroup("/api/admin/customers").RequireAuthorization("Admin");
        group.MapGet("", (string? search, int? page, int? pageSize, CustomerAccountService service) =>
            service.Search(search, page ?? 1, pageSize ?? 20));
        group.MapGet("/{id:long}", (long id, CustomerAccountService service) => service.Details(id));
        group.MapGet("/operations/{operation:guid}", (Guid operation, HttpContext context, CustomerAccountService service) => service.Operation(operation, context));
        // Giữ route cũ trả 410 sau kiểm tra quyền để client cũ không thể tạo/xóa khách.
        group.MapPost("", () => Results.Problem(statusCode: 410, detail: "Khách hàng tự đăng ký trên Website; Quản lý không tạo tài khoản."));
        group.MapPut("/{id:long}", (long id, CustomerSaveInput input, HttpContext context, CustomerAccountService service) => service.Save(id, input, context));
        group.MapPost("/{id:long}/status", (long id, CustomerActionInput input, HttpContext context, CustomerAccountService service) => service.Act(id, "STATUS", input, context));
        group.MapPost("/{id:long}/reset-password", (long id, CustomerActionInput input, HttpContext context, CustomerAccountService service) => service.Act(id, "RESET_PASSWORD", input, context));
        group.MapDelete("/{id:long}", () => Results.Problem(statusCode: 410, detail: "Không xóa hồ sơ khách hàng. Hãy khóa tài khoản để giữ lịch sử."));
    }
}
