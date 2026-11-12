namespace FourChang.Tests.Admin;

/// <summary>ADM-12 kiểm tra giao diện Admin/POS thật, mã một lần và quyền chỉ đối chiếu.</summary>
[Collection("AdminFeatures"), Trait("UseCase", "UC-ADM-12"), Trait("Layer", "WinForms"), Trait("Actor", "QuanLy")]
public sealed class WorkSessionUiTests(AdminFeatureFixture fixture)
{
    /// <summary>Từ danh sách ca đến cấp/đổi mã, nhập thực đếm và tải trạng thái đã chốt, có ảnh thực tế.</summary>
    [Fact]
    public async Task DoiChieu_PhucHoiMotLan_POS_VaTrangThaiChot()
    {
        await WorkforceScenarios.Open(fixture);
        await AdminFeaturesUiTests.RunRoute(fixture, "UC-ADM-12", "work-sessions");
    }
}
