using FourChang.Tests.Admin;
namespace FourChang.Tests.Staff;
/// <summary>Danh sách/chi tiết đơn và hóa đơn nhân viên chạy với API/SQL thật trên WinForms.</summary>
[Collection("AdminFeatures"), Trait("Actor", "NhanVien"), Trait("Feature","CommercePresentation"), Trait("Layer","WinForms")]
public sealed class StaffDocumentsUiTests(AdminFeatureFixture fixture)
{
    /// <summary>Kiểm điều hướng, tìm kiếm, tải lại và mở dialog, không tạo nút nghiệp vụ giả.</summary>
    [Fact(DisplayName = "staff-orders tra cứu đơn"), Trait("UseCase", "UC-POS-03")]
    public Task DonHang_LayoutVaTraCuu() =>
        AdminFeaturesUiTests.RunRoute(fixture, "UC-POS-03", "staff-orders");

    [Fact(DisplayName = "staff-invoices tra cứu hóa đơn"), Trait("UseCase", "UC-POS-05")]
    public Task HoaDon_LayoutVaTraCuu() =>
        AdminFeaturesUiTests.RunRoute(fixture, "UC-POS-05", "staff-invoices");
}
