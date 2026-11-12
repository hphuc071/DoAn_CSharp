using FourChang.Tests.Admin;
namespace FourChang.Tests.Staff;
/// <summary>Kiểm tra POS thật trên STA, dùng cùng API fixture và csproj của cả nhóm.</summary>
[Collection("AdminFeatures"), Trait("UseCase","UC-POS-02"), Trait("Layer","WinForms")]
public sealed class PosSalesUiTests(AdminFeatureFixture fixture)
{
    /// <summary>Chọn hai món bằng dialog, cấu hình/số lượng và thu CASH; các dòng chỉ xóa sau server đã xác nhận.</summary>
    [Fact]
    public Task ChonNhieuMon_CauHinh_ThuTienBangControl() => AdminFeaturesUiTests.RunRoute(fixture,"UC-POS-02","pos-sale");
}
