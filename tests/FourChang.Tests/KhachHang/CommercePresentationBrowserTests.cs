using FourChang.Tests.Shared;
using Microsoft.Playwright;
using System.Text.Json.Nodes;
using static FourChang.Tests.Shared.TestData;
namespace FourChang.Tests;

/// <summary>QA trực quan Cart/Checkout/Payment/Orders/Invoice trên Web thật, dữ liệu SQL fixture tách riêng.</summary>
public sealed class CommercePresentationBrowserTests
{
    /// <summary>Chạy luồng có món/đơn/hóa đơn thật, kiểm mobile không tràn và giữ đầy đủ ID/action quan trọng.</summary>
    [Fact, Trait("Feature","CommercePresentation"), Trait("Layer","Browser")]
    public async Task Gio_ThanhToan_Don_HoaDon_ResponsiveVaDuLieuThat()
    {
        await using var f=await BrowserFixture.Start("Commerce-UI",new(){["Payments__SimulationEnabled"]="true",["Orders__CustomerCreationEnabled"]="true",["Orders__PendingPaymentTimeoutMinutes"]="15",["Checkout__OrderCreationEnabled"]="true"});
        var customer=await f.Api.NewCustomer("commerce");var headers=await f.Api.Login(customer);
        var menu=(await f.Api.Send("GET","/api/menu")).Expect(200,"Menu").Json.AsArray().Where(x=>x!.Flag("available")).Take(2).ToList();
        await f.Login(customer.Input.Text("TenDangNhap"),customer.Input.Text("MatKhau"));
        await f.Page.EvaluateAsync("localStorage.removeItem('fourchang.cart-draft.v1')");await f.Page.ReloadAsync();
        var items=new JsonArray();decimal total=0;
        foreach(var product in menu)
        {
            var v=(await f.Api.Send("GET",$"/api/menu/{product!.Number("maMon")}/variants")).Expect(200,"Biến thể").Json.AsArray().First(x=>x!.Flag("available"))!;
            var pair=v["tuyChonPhaChe"]!.AsArray()[0]!;
            await f.Page.Locator($"[data-product-id='{product!.Number("maMon")}']").ClickAsync();
            await CartEditorActions.Variant(f.Page, v.Number("maCauHinhMon").ToString());
            await CartEditorActions.Brew(f.Page, $"[{pair.Number("phanTramDuong")},{pair.Number("phanTramDa")}]");
            await f.Page.Locator("[data-editor-save]").ClickAsync();
            await Assertions.Expect(f.Page.Locator("[data-product-added]")).ToBeVisibleAsync();
            total+=v["giaBan"]!.GetValue<decimal>();
            items.Add(Body(("MaCauHinhMon",v.Number("maCauHinhMon")),("SoLuong",1),("PhanTramDuong",pair.Number("phanTramDuong")),("PhanTramDa",pair.Number("phanTramDa")),("Toppings",new JsonArray())));
        }
        var input=Body(("HinhThucNhanMon","TAKEAWAY"),("TenNguoiNhan","Nguyễn Tuấn Kiệt"),("DienThoaiNguoiNhan","0912345678"),("Items",items));
        headers["Idempotency-Key"]=Guid.NewGuid().ToString();headers["X-Checkout-Accepted-Total"]=total.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var order=(await f.Api.Send("POST","/api/customer/orders",input,headers)).Expect(201,"Đơn kiểm giao diện").Json.Number("maDonHang");
        var qr=(await f.Api.Send("GET",$"/api/customer/orders/{order}/simulation",headers:headers)).Expect(200,"QR thử nghiệm").Json;
        await f.Page.GotoAsync(f.WebUrl+"/Checkout/Payment?orderId="+order);
        await Assertions.Expect(f.Page.Locator("[data-simulation-payment]")).ToBeVisibleAsync();
        var qrBox=await f.Page.Locator("[data-simulation-payment] img").BoundingBoxAsync();Assert.NotNull(qrBox);Assert.True(qrBox!.Width<=400);
        // Phiên tạo đơn đã có biên nhận nhưng chưa trả tiền vẫn phải được giữ để chống gửi trùng.
        await f.Page.EvaluateAsync("id => localStorage.setItem('fourchang.checkout-intent.v1', JSON.stringify({version:1,command:{operation:'fixture-operation'},receipt:{maDonHang:id}}))",order);
        await f.Page.EvaluateAsync("FourChangCheckoutSession.check()");
        Assert.True(await f.Page.EvaluateAsync<bool>("localStorage.getItem('fourchang.checkout-intent.v1') !== null"));
        // Chụp cả QR đang chờ, không chỉ trạng thái đã thanh toán.
        foreach (var paymentWidth in new[] {1440,390})
        {
            await f.Page.SetViewportSizeAsync(paymentWidth,900);
            Assert.False(await f.Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 1"));
            await f.Screenshot($"commerce-qr-{paymentWidth}");
        }
        (await f.Api.Send("POST",new Uri(qr.Text("url")).AbsolutePath)).Expect(200,"Xác nhận fixture");
        // Khi API xác minh đã thanh toán, tự kết thúc đúng phiên; giỏ mới vẫn còn hai món.
        await f.Page.EvaluateAsync("FourChangCheckoutSession.check()");
        Assert.True(await f.Page.EvaluateAsync<bool>("localStorage.getItem('fourchang.checkout-intent.v1') === null"));
        Assert.Equal(2,await f.Page.EvaluateAsync<int>("JSON.parse(localStorage.getItem('fourchang.cart-draft.v1')).length"));
        foreach(var width in new[]{1440,390})
        {
            await f.Page.SetViewportSizeAsync(width,900);
            foreach(var route in new[]{"/Cart","/Checkout","/Orders","/Orders/"+order,"/Checkout/Payment?orderId="+order})
            {
                await f.Page.GotoAsync(f.WebUrl+route);
                await f.Page.EvaluateAsync("document.fonts.ready");
                await Assertions.Expect(f.Page.Locator("body")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("commerce-page"));
                if(route=="/Cart" || route=="/Checkout" || route.StartsWith("/Checkout/Payment")) await Assertions.Expect(f.Page.Locator(".checkout-path .current")).ToHaveCountAsync(1);
                await Assertions.Expect(f.Page.Locator("#cart-badge")).ToHaveTextAsync("2");
                if(route=="/Cart")await Assertions.Expect(f.Page.Locator("[data-cart-index]")).ToHaveCountAsync(2);
                if(route.StartsWith("/Orders/"))
                {
                    await Assertions.Expect(f.Page.Locator(".invoice-card")).ToHaveCountAsync(1);
                    Assert.True(await f.Page.EvaluateAsync<bool>("[...document.querySelectorAll('.order-product-card > div:first-child > p')].every(p => getComputedStyle(p).textAlign === 'left')"));
                    Assert.True(await f.Page.Locator(".order-product-card > div:first-child > p").First.EvaluateAsync<bool>("p => parseFloat(getComputedStyle(p).fontSize) >= 14"));
                }
                if(route.StartsWith("/Checkout/Payment"))await Assertions.Expect(f.Page.GetByRole(AriaRole.Heading,new(){Name="Thanh toán thành công",Exact=true})).ToBeVisibleAsync();
                var overflow=await f.Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 1");
                Assert.False(overflow,$"Tràn ngang: {route}, width={width}");
                Assert.Empty(await f.Page.Locator("[role=alert]").AllTextContentsAsync());
                var name=route.StartsWith("/Orders/")?"order-invoice":route.StartsWith("/Checkout/Payment")?"payment":route.Trim('/').ToLowerInvariant();
                await f.Screenshot($"commerce-{name}-{width}");
            }
        }
        // Kiểm tra tăng/giảm vẫn dùng giỏ cũ và không tạo một đơn mới vì đổi layout.
        await f.Page.GotoAsync(f.WebUrl+"/Cart");
        var first=f.Page.Locator("[data-cart-index]").First;
        await first.GetByRole(AriaRole.Button,new(){Name="+",Exact=true}).ClickAsync();
        await Assertions.Expect(f.Page.Locator("#cart-count")).ToContainTextAsync("3 ly");
        // Chỉnh món dùng cùng hộp cấu hình như menu; lưu/đóng không tạo đơn hoặc mất giỏ.
        await first.GetByRole(AriaRole.Button,new(){Name="Sửa món",Exact=true}).ClickAsync();
        var editor=f.Page.Locator("#cart-edit-dialog");
        await Assertions.Expect(editor).ToBeVisibleAsync();
        await Assertions.Expect(editor.Locator("[data-editor-save]")).ToBeEnabledAsync();
        await editor.GetByLabel("Ghi chú",new(){Exact=true}).FillAsync("Ít ngọt, kiểm tra giao diện");
        await f.Page.ScreenshotAsync(new(){Path=Path.Combine(f.Api.Artifacts,"commerce-cart-editor-390.png"),FullPage=false,Animations=ScreenshotAnimations.Disabled});
        await editor.Locator("[data-editor-save]").ClickAsync();
        await Assertions.Expect(editor).ToBeHiddenAsync();
        await Assertions.Expect(f.Page.Locator("#cart-count")).ToContainTextAsync("3 ly");
        await Assertions.Expect(f.Page.Locator("[data-cart-index]").First).ToContainTextAsync("Ít ngọt, kiểm tra giao diện");
        Assert.Equal("1",await f.Api.Sql($"SELECT COUNT(*) FROM dbo.DonHang WHERE MaKhachHang={customer.Id}"));
    }
}
