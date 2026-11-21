namespace FourChang.Tests.Admin;

/// <summary>Test cấp lại mật khẩu theo yêu cầu khách nằm đúng nhóm UC-ADM-01; worker STA được dùng chung.</summary>
[Collection("AdminFeatures"), Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-01"), Trait("Layer", "WinForms")]
public sealed class CustomerPasswordResetUiTests
{
    /// <summary>Form thật kiểm tra xác nhận, lưu qua API, thu hồi phiên và cho khách đổi mật khẩu tạm trên cùng dữ liệu.</summary>
    [Fact]
    public async Task CapLaiMatKhau_FormDenPhienKhachHang()
    {
        // Login/reset assertions need a fresh API quota, independent of other Admin test logins.
        var fixture = new AdminFeatureFixture();
        try
        {
            await fixture.InitializeAsync();
            await AdminFeaturesUiTests.RunRoute(fixture, "UC-ADM-01-RESET", "customer-reset");
        }
        finally { await fixture.DisposeAsync(); }
    }
}
