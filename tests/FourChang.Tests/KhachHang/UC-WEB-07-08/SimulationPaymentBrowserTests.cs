using FourChang.Tests.Shared;
using Microsoft.Playwright;
using System.Text.Json.Nodes;
using static FourChang.Tests.Shared.TestData;
namespace FourChang.Tests;
/// <summary>Web/API/SQL thật: mở QR chỉ đọc, xác nhận trên trang mô phỏng rồi trang thanh toán tự cập nhật.</summary>
public sealed class SimulationPaymentBrowserTests
{
    /// <summary>Không có nút tự giả PAID trên trang khách; việc xác nhận thử nghiệm được lưu một lần tại server.</summary>
    [Fact, Trait("UseCase","UC-WEB-07")]
    public async Task QrMoPhong_XacNhan_TrangKhachTuCapNhat()
    {
        await using var f=await BrowserFixture.Start("Web-Simulation",new(){["Payments__SimulationEnabled"]="true",["Orders__CustomerCreationEnabled"]="true",["Orders__PendingPaymentTimeoutMinutes"]="15",["Checkout__OrderCreationEnabled"]="true"});
        var customer=await f.Api.NewCustomer();var headers=await f.Api.Login(customer);
        var menu=(await f.Api.Send("GET","/api/menu")).Expect(200,"Menu").Json.AsArray().First(x=>x!.Flag("available"))!;
        var variant=(await f.Api.Send("GET",$"/api/menu/{menu.Number("maMon")}/variants")).Expect(200,"Cấu hình").Json.AsArray().First(x=>x!.Flag("available"))!;
        var pair=variant["tuyChonPhaChe"]!.AsArray()[0]!;
        var input=Body(("HinhThucNhanMon","TAKEAWAY"),("TenNguoiNhan","Nguyễn Tuấn Kiệt"),("DienThoaiNguoiNhan","0912345678"),
            ("Items",new JsonArray(Body(("MaCauHinhMon",variant.Number("maCauHinhMon")),("SoLuong",1),("PhanTramDuong",pair.Number("phanTramDuong")),("PhanTramDa",pair.Number("phanTramDa")),("Toppings",new JsonArray())))));
        headers["Idempotency-Key"]=Guid.NewGuid().ToString();headers["X-Checkout-Accepted-Total"]=variant["giaBan"]!.ToString();
        var order=(await f.Api.Send("POST","/api/customer/orders",input,headers)).Expect(201,"Tạo đơn Web").Json;
        var id=order.Number("maDonHang");
        await f.Login(customer.Input.Text("TenDangNhap"),customer.Input.Text("MatKhau"));
        await f.Page.GotoAsync(f.WebUrl+"/Checkout/Payment?orderId="+id);
        await Assertions.Expect(f.Page.Locator("[data-simulation-payment]")).ToBeVisibleAsync();
        var link=f.Page.GetByRole(AriaRole.Link,new(){Name="Mở trang xác nhận mô phỏng",Exact=true});
        var popup=await f.Page.RunAndWaitForPopupAsync(()=>link.ClickAsync());
        await Assertions.Expect(popup.GetByRole(AriaRole.Button,new(){Name="Xác nhận thanh toán mô phỏng"})).ToBeVisibleAsync();
        Assert.Equal("0",await f.Api.Sql($"SELECT SoTienDaXacMinh FROM dbo.ThanhToan WHERE MaDonHang={id}"));
        await popup.GetByRole(AriaRole.Button,new(){Name="Xác nhận thanh toán mô phỏng"}).ClickAsync();
        await Assertions.Expect(popup.GetByRole(AriaRole.Heading,new(){Name="Thanh toán mô phỏng thành công"})).ToBeVisibleAsync();
        await Assertions.Expect(f.Page.Locator("[data-simulation-payment]")).ToHaveCountAsync(0,new(){Timeout=15000});
        await Assertions.Expect(f.Page.GetByRole(AriaRole.Heading,new(){Name="Thanh toán thành công",Exact=true})).ToBeVisibleAsync();
        Assert.Equal("PENDING_CONFIRMATION",await f.Api.Sql($"SELECT TrangThai FROM dbo.DonHang WHERE MaDonHang={id}"));
        Assert.Equal("1",await f.Api.Sql($"SELECT COUNT(*) FROM dbo.HoaDon WHERE MaDonHang={id}"));
        await popup.CloseAsync();await f.Screenshot("web-payment-confirmed");
    }
}
