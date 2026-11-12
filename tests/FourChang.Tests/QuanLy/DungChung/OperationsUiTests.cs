using System.Text.Json;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Tests.Admin;

/// <summary>Kiểm tra WinForms thật cho các UC mới, có đơn/hóa đơn/hồ sơ thật trong database fixture để chụp giao diện.</summary>
[Collection("AdminFeatures"), Trait("Actor", "QuanLy"), Trait("Feature", "ADM06-10-UI"), Trait("Layer", "WinForms")]
public sealed class OperationsUiTests(AdminFeatureFixture fixture)
{
    /// <summary>Kiểm riêng thanh đơn tối đa3 nút, đúng bước theo trạng thái và menu phụ còn đầy đủ.</summary>
    [Fact, Trait("Feature","CompactOrderToolbar")]
    public Task DonHang_ThanhCongCuGonGan()=>CacTrangMoi_DocDuLieuThat_NutKhongBiCat("UC-ADM-07","manage-orders");

    /// <summary>Mở từng route với API thật, đổi kích thước và kiểm tra dialog chứng từ kho có thể lưu.</summary>
    [Theory]
    [InlineData("UC-ADM-06", "stock")]
    [InlineData("UC-ADM-07", "manage-orders")]
    [InlineData("UC-ADM-08", "invoices")]
    [InlineData("UC-ADM-09", "refunds")]
    [InlineData("UC-ADM-10", "reports")]
    public async Task CacTrangMoi_DocDuLieuThat_NutKhongBiCat(string code, string route)
    {
        var scenario = await OperationsScenarioSeed.Create(fixture, "ACCEPTED", invoice: true);
        var detail = (await fixture.Send("GET", $"/api/admin/orders/{scenario.Order}")).Deserialize<AdminOrderDetail>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        await fixture.Send("POST", $"/api/admin/orders/{scenario.Order}/actions", new OrderActionInput("CANCEL", "Chuẩn bị giao diện có nguồn hủy", detail.Don.PhienBanDuLieu, Guid.NewGuid(), scenario.Line, 1));
        // Dữ liệu riêng trong SQL fixture để số thực thu vượt một triệu: phát hiện KPI bị cắt khi thu nhỏ cửa sổ.
        // Không thêm dữ liệu vào database quán và không thay thuật toán tính tiền của ứng dụng.
        if (route == "reports")
            for (var i = 0; i < 18; i++)
                await OperationsScenarioSeed.Create(fixture, "COMPLETED");
        await AdminFeaturesUiTests.RunRoute(fixture, code, route);
    }
}
