using FourChang.Tests.Admin;
namespace FourChang.Tests.Staff;
/// <summary>Danh sách/chi tiết đơn và hóa đơn nhân viên chạy với API/SQL thật trên WinForms.</summary>
[Collection("AdminFeatures"), Trait("Feature","CommercePresentation"), Trait("Layer","WinForms")]
public sealed class StaffDocumentsUiTests(AdminFeatureFixture fixture)
{
    /// <summary>Kiểm điều hướng, tìm kiếm, tải lại và mở dialog, không tạo nút nghiệp vụ giả.</summary>
    [Theory]
    [InlineData("staff-orders")]
    [InlineData("staff-invoices")]
    public Task DonVaHoaDon_LayoutVaTraCuu(string route)=>AdminFeaturesUiTests.RunRoute(fixture,"Commerce-"+route,route);
}
