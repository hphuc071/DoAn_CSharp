using FourChang.Tests.Shared;
using Microsoft.Playwright;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests;

/// <summary>Đầu-cuối ba hình thức dùng Web/API/SQL thật trong fixture riêng; QR chỉ mô phỏng.</summary>
public sealed class SimulatedFulfillmentBrowserTests
{
    /// <summary>Chọn bàn/mang đi/giao hàng, tạo đơn đúng phí và thông tin, trả tiền, rồi đặt tiếp không cần reset thủ công.</summary>
    [Fact, Trait("Feature", "SimulatedFulfillment"), Trait("Layer", "Browser")]
    public async Task BaHinhThuc_BaoGia_TaoDon_ThanhToan_VaGiuSnapshot()
    {
        await using var f = await BrowserFixture.Start("Simulated-Fulfillment", new()
        {
            ["Orders__FulfillmentSimulationEnabled"] = "true", ["Payments__SimulationEnabled"] = "true",
            ["Orders__CustomerCreationEnabled"] = "true", ["Checkout__OrderCreationEnabled"] = "true",
            ["Orders__PendingPaymentTimeoutMinutes"] = "15"
        });
        var customer = await f.Api.NewCustomer("fulfillment");
        var auth = await f.Api.Login(customer);
        await f.Login(customer.Input.Text("TenDangNhap"),customer.Input.Text("MatKhau"));
        var product = (await f.Api.Send("GET","/api/menu")).Expect(200,"Menu").Json.AsArray().First(p=>p!.Flag("available"))!;
        var variant = (await f.Api.Send("GET",$"/api/menu/{product.Number("maMon")}/variants")).Expect(200,"Cấu hình").Json.AsArray().First(v=>v!.Flag("available"))!;
        var pair = variant["tuyChonPhaChe"]!.AsArray().First()!;
        foreach(var mode in new[]{"takeaway","dinein","delivery"})
        {
            await f.Page.GotoAsync(f.WebUrl + "/");
            await f.Page.Locator($"[data-product-id='{product.Number("maMon")}']").ClickAsync();
            await CartEditorActions.Variant(f.Page,variant.Number("maCauHinhMon").ToString());
            await CartEditorActions.Brew(f.Page,$"[{pair.Number("phanTramDuong")},{pair.Number("phanTramDa")}]");
            await f.Page.Locator("[data-editor-checkout]").ClickAsync();
            await f.Page.WaitForURLAsync("**/Checkout");
            await f.Page.Locator($"[data-tab='{mode}']").ClickAsync();
            await f.Page.Locator("#takeaway-name").FillAsync("Nguyễn Minh Anh");
            await f.Page.Locator("#takeaway-phone").FillAsync("0912345678");
            if(mode=="dinein")
            {
                await f.Page.Locator("#checkout-table option").Nth(1).WaitForAsync(new(){State=WaitForSelectorState.Attached});
                await f.Page.Locator("#checkout-table").SelectOptionAsync(new SelectOptionValue{Index=1});
            }
            if(mode=="delivery")
            {
                await f.Page.Locator("#checkout-address").FillAsync("123 Nguyễn Trãi, Phường Bến Thành, TP.HCM");
                await f.Page.Locator("#checkout-delivery-note").FillAsync("Gọi khi đến cổng");
            }
            var fee = mode=="delivery" ? 15000 : 0;
            await f.Page.WaitForFunctionAsync("fee => window.fourchangCheckoutQuote?.price?.phiGiaoHang === fee",fee);
            await f.Page.Locator("#checkout-confirmed").CheckAsync();
            await Assertions.Expect(f.Page.Locator("#checkout-proceed")).ToBeEnabledAsync();
            await f.Page.SetViewportSizeAsync(390,900);
            Assert.False(await f.Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 1"));
            await f.Screenshot("checkout-"+mode+"-390");
            await f.Page.Locator("#checkout-proceed").ClickAsync();
            await Assertions.Expect(f.Page.Locator("#checkout-receipt")).ToBeVisibleAsync();
            var id = await f.Page.EvaluateAsync<long>("JSON.parse(localStorage.getItem('fourchang.checkout-intent.v1')).receipt.maDonHang");
            var detail = (await f.Api.Send("GET",$"/api/customer/orders/{id}",headers:auth)).Expect(200,"Đọc đơn đã tạo").Json;
            Assert.Equal(fee,detail.Number("shippingFee"));
            Assert.Equal(mode=="dinein" ? "1" : "0",await f.Api.Sql($"SELECT COUNT(*) FROM dbo.DonHang WHERE MaDonHang={id} AND MaBan IS NOT NULL"));
            if(mode=="delivery")
            {
                Assert.Equal("123 Nguyễn Trãi, Phường Bến Thành, TP.HCM",await f.Api.Sql($"SELECT DiaChiGiaoDaChot FROM dbo.DonHang WHERE MaDonHang={id}"));
                Assert.Equal("SIMULATION",await f.Api.Sql($"SELECT JSON_VALUE(ChinhSachGiaoHangDaChotJson,'$.mode') FROM dbo.DonHang WHERE MaDonHang={id}"));
            }
            await f.Page.Locator("#receipt-payment").ClickAsync();
            var qr = (await f.Api.Send("GET",$"/api/customer/orders/{id}/simulation",headers:auth)).Expect(200,"QR mô phỏng").Json;
            (await f.Api.Send("POST",new Uri(qr.Text("url")).AbsolutePath)).Expect(200,"Thanh toán mô phỏng");
            await f.Page.ReloadAsync();
            await Assertions.Expect(f.Page.GetByRole(AriaRole.Heading,new(){Name="Thanh toán thành công",Exact=true})).ToBeVisibleAsync();
            await f.Page.EvaluateAsync("FourChangCheckoutSession.check()");
            Assert.Null(await f.Page.EvaluateAsync<string?>("localStorage.getItem('fourchang.checkout-intent.v1')"));
        }
        Assert.Equal("3",await f.Api.Sql($"SELECT COUNT(*) FROM dbo.DonHang WHERE MaKhachHang={customer.Id}"));
    }
}
