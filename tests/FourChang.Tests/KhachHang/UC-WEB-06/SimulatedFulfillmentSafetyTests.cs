using FourChang.Api.Features.Customer.Orders;
using FourChang.Tests.Shared;
using Microsoft.Extensions.Configuration;
using System.Text.Json.Nodes;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests;

public sealed class SimulatedFulfillmentSafetyTests
{
    /// <summary>Mô phỏng phải có cờ riêng và môi trường Development; Production luôn đóng.</summary>
    [Theory, Trait("Feature", "SimulatedFulfillment")]
    [InlineData("Development", "true", true)]
    [InlineData("Development", "false", false)]
    [InlineData("Production", "true", false)]
    public void GateRequiresDevelopmentAndExplicitFlag(string environment, string flag, bool expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["ASPNETCORE_ENVIRONMENT"] = environment, ["Orders:FulfillmentSimulationEnabled"] = flag }).Build();
        Assert.Equal(expected, SimulatedFulfillment.Enabled(configuration));
    }

    /// <summary>Không nhận phí/khoảng cách tùy ý; bàn phải còn hoạt động khi tạo đơn, mọi nhánh lỗi không ghi đơn.</summary>
    [Fact, Trait("Feature", "SimulatedFulfillment"), Trait("Layer", "API")]
    public async Task PhiDoServerTinh_VaKiemTraLaiBanTrongGiaoDich()
    {
        await using var api = await TestApi.Start("Fulfillment-Safety",runtime:new()
        { ["Orders__FulfillmentSimulationEnabled"]="true", ["Orders__CustomerCreationEnabled"]="true", ["Orders__PendingPaymentTimeoutMinutes"]="15" });
        var customer = await api.NewCustomer("deliverysafety"); var auth = await api.Login(customer);
        var product = (await api.Send("GET","/api/menu")).Expect(200,"Menu").Json.AsArray().First(p=>p!.Flag("available"))!;
        var v = (await api.Send("GET",$"/api/menu/{product.Number("maMon")}/variants")).Expect(200,"Biến thể").Json.AsArray().First(x=>x!.Flag("available"))!;
        var pair = v["tuyChonPhaChe"]!.AsArray().First()!;
        var request = Body(("HinhThucNhanMon","DELIVERY"),("TenNguoiNhan","Khách mô phỏng"),("DienThoaiNguoiNhan","0912345678"),
            ("DiaChiGiao","123 Nguyễn Trãi, TP.HCM"),("Items",new JsonArray(Body(("MaCauHinhMon",v.Number("maCauHinhMon")),("SoLuong",1),
                ("PhanTramDuong",pair.Number("phanTramDuong")),("PhanTramDa",pair.Number("phanTramDa")),("Toppings",new JsonArray())))));
        var quote = (await api.Send("POST","/api/customer/orders/quote",request)).Expect(200,"Báo giá giao hàng").Json;
        Assert.Equal(15000,quote.Number("phiGiaoHang"));
        auth["Idempotency-Key"] = Guid.NewGuid().ToString();
        auth["X-Checkout-Accepted-Total"] = v.Number("giaBan").ToString();
        (await api.Send("POST","/api/customer/orders",request,auth)).Expect(409,"Không được bỏ phí giao");
        request["KhoangCachKm"] = 1;
        (await api.Send("POST","/api/customer/orders/quote",request)).Expect(400,"Không dùng khoảng cách client trong mô phỏng");
        request["KhoangCachKm"] = null; request["DiaChiGiao"] = null;
        (await api.Send("POST","/api/customer/orders/quote",request)).Expect(400,"Giao hàng bắt buộc địa chỉ");
        var tables = (await api.Send("GET","/api/menu/tables")).Expect(200,"Danh sách bàn").Json.AsArray();
        Assert.DoesNotContain("maBamQR",tables.ToJsonString(),StringComparison.OrdinalIgnoreCase);
        request["HinhThucNhanMon"]="DINE_IN"; request["MaBanHienThi"]=tables.First()!.Text("maBanHienThi");
        (await api.Send("POST","/api/customer/orders/quote",request)).Expect(200,"Bàn đang dùng");
        await api.Sql("UPDATE dbo.Ban SET DangSuDung=0");
        (await api.Send("POST","/api/customer/orders",request,auth)).Expect(400,"Bàn đã dừng sau khi báo giá");
        Assert.Equal("0",await api.Sql("SELECT COUNT(*) FROM dbo.DonHang"));
    }
}
