namespace FourChang.Tests.Admin;

/// <summary>Test cấp lại mật khẩu theo yêu cầu khách nằm đúng nhóm UC-ADM-01; worker STA được dùng chung.</summary>
[Collection("AdminFeatures"), Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-01"), Trait("Layer", "WinForms")]
public sealed class CustomerPasswordResetUiTests(AdminFeatureFixture fixture)
{
    /// <summary>Form thật kiểm tra xác nhận, lưu qua API, thu hồi phiên và cho khách đổi mật khẩu tạm trên cùng dữ liệu.</summary>
    [Fact]
    public Task CapLaiMatKhau_FormDenPhienKhachHang() => AdminFeaturesUiTests.RunRoute(fixture, "UC-ADM-01-RESET", "customer-reset");
}
