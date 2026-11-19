using System.Text.Json;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Tests.Admin;

/// <summary>Kiểm tra WinForms thật cho các UC mới, có đơn/hóa đơn/hồ sơ thật trong database fixture để chụp giao diện.</summary>
[Collection("AdminFeatures"), Trait("Actor", "QuanLy"), Trait("Feature", "ADM06-10-UI"), Trait("Layer", "WinForms")]
public sealed class OperationsUiTests(AdminFeatureFixture fixture)
{
    /// <summary>Kiểm riêng thanh đơn tối đa3 nút, đúng bước theo trạng thái và menu phụ còn đầy đủ.</summary>
    [Fact, Trait("Feature","CompactOrderToolbar")]
    public Task DonHang_ThanhCongCuGonGan()=>CacTrangMoi_DocDuLieuThat_NutKhongBiCat("UC-ADM-05","manage-orders");

    [Fact, Trait("Feature", "ADM05CancellationLayout")]
    public Task YeuCauHuy_BoCucVaLichSu()=>CacTrangMoi_DocDuLieuThat_NutKhongBiCat("UC-ADM-05","cancelled-orders");
    [Theory, InlineData("invoices"), InlineData("refunds"), Trait("Feature", "ADM06Layout")]
    public Task HoaDonHoanTien_GiaoDienTinhGon(string route)=>CacTrangMoi_DocDuLieuThat_NutKhongBiCat("UC-ADM-06",route);
    /// <summary>Bốn trang Admin dùng cùng bố cục; dữ liệu fixture và handler thật được tái sử dụng.</summary>
    [Theory, InlineData("manage-orders"), InlineData("cancelled-orders"), InlineData("refunds"), InlineData("invoices"), Trait("Feature", "UnifiedCommerce")]
    public Task BonTrangGiaoDich_DongBoBoCuc(string route)=>CacTrangMoi_DocDuLieuThat_NutKhongBiCat("COMMERCE-"+route,route);
    [Fact, Trait("Feature", "ADM07Populated")]
    public Task BaoCaoTinhGon_CoDuLieuThatVaNhatKy()=>CacTrangMoi_DocDuLieuThat_NutKhongBiCat("UC-ADM-07","reports");
    /// <summary>Mở từng route với API thật, đổi kích thước và kiểm tra dialog chứng từ kho có thể lưu.</summary>
    [Theory]
    [InlineData("UC-ADM-04", "stock")]
    [InlineData("UC-ADM-05", "manage-orders")]
    [InlineData("UC-ADM-05", "cancelled-orders")]
    [InlineData("UC-ADM-08", "invoices")]
    [InlineData("UC-ADM-09", "refunds")]
    [InlineData("UC-ADM-10", "reports")]
    public async Task CacTrangMoi_DocDuLieuThat_NutKhongBiCat(string code, string route)
    {
        var scenario = await OperationsScenarioSeed.Create(fixture, "ACCEPTED", invoice: true);
        var detail = (await fixture.Send("GET", $"/api/admin/orders/{scenario.Order}")).Deserialize<AdminOrderDetail>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        await OperationsScenarioSeed.RequestAndApprove(fixture, scenario, route != "cancelled-orders");
        // Dữ liệu riêng trong SQL fixture để số thực thu vượt một triệu: phát hiện KPI bị cắt khi thu nhỏ cửa sổ.
        // Không thêm dữ liệu vào database quán và không thay thuật toán tính tiền của ứng dụng.
        if (route == "reports")
            for (var i = 0; i < 18; i++)
                await OperationsScenarioSeed.Create(fixture, "COMPLETED");
        await AdminFeaturesUiTests.RunRoute(fixture, code, route);
    }
}
