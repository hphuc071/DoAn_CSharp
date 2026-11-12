using FourChang.Tests.Shared;

namespace FourChang.Tests.Admin;

/// <summary>Kiểm tra điều hướng từ sáu tab báo cáo bằng control/event WinForms thật và API fixture.</summary>
[Collection("AdminFeatures"), Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-10"), Trait("Layer", "WinForms")]
public sealed class ReportNavigationUiTests(AdminFeatureFixture fixture)
{
    /// <summary>Nút đổi tên theo tab và mở đúng trang, kể cả báo cáo không có dòng để chọn.</summary>
    [Fact]
    public Task SauTab_ChuyenDungTrang_KhongCanChonDong() =>
        AdminFeaturesUiTests.RunRoute(fixture, "UC-ADM-10-Navigation", "reports");
}
