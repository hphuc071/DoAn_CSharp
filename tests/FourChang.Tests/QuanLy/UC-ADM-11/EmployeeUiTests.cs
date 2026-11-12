using FourChang.Tests.Shared;
namespace FourChang.Tests.Admin;

/// <summary>ADM-11 dùng WinForms/API thật, kiểm tra form, tìm kiếm, địa chỉ và liên kết lịch sử.</summary>
[Collection("AdminFeatures"), Trait("UseCase", "UC-ADM-11"), Trait("Layer", "WinForms"), Trait("Actor", "QuanLy")]
public sealed class EmployeeUiTests(AdminFeatureFixture fixture)
{
    /// <summary>Nhân viên mẫu chỉ tồn tại trong database fixture; không thêm hồ sơ vào quán.</summary>
    [Fact]
    public async Task HoSo_TimKiem_DiaChi_LichSu_ThaoTacBangControl()
    {
        await fixture.Send("POST", "/api/admin/employees", WorkforceScenarios.EmployeeInput());
        await fixture.Send("POST", "/api/admin/employees", WorkforceScenarios.EmployeeInput());
        await AdminFeaturesUiTests.RunRoute(fixture, "UC-ADM-11", "employees");
    }
}
