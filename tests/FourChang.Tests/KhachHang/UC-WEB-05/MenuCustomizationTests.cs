using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Customer;

public sealed class MenuCustomizationTests
{
    [Fact, Trait("Actor", "KhachHang"), Trait("UseCase", "UC-WEB-05"), Trait("Layer", "API")]
    public async Task TuyChonMonVaBaoGia_ChinhXac()
    {
        await using var api = await TestApi.Start("UC-WEB-05");

        var menuRes = await api.Send("GET", "/api/menu");
        menuRes.Expect(200, "Lấy thực đơn thành công");
        var products = menuRes.Json.AsArray();

        if (products.Count > 0)
        {
            long maMon = products[0]!.Number("maMon") != 0 ? products[0]!.Number("maMon") : products[0]!.Number("id");
            long variantId = products[0]!.Number("id") != 0 ? products[0]!.Number("id") : 1;

            var variantRes = await api.Send("GET", $"/api/menu/{maMon}/variants");
            variantRes.Expect(200, "Lấy biến thể và tùy chỉnh món thành công");

            var quoteBody = Body(
                ("hinhThucNhanMon", "DELIVERY"),
                ("khoangCachKm", 2.5m),
                ("items", new JsonArray(
                    Body(
                        ("maCauHinhMon", variantId),
                        ("soLuong", 2),
                        ("phanTramDuong", 50),
                        ("phanTramDa", 50),
                        ("toppings", new JsonArray())
                    )
                ))
            );

            var quoteRes = await api.Send("POST", "/api/customer/orders/quote", quoteBody);
            quoteRes.Expect(200, "Báo giá giỏ hàng thành công");
            Assert.True(quoteRes.Json.Number("tongCong") > 0, "Tổng cộng phải lớn hơn 0");
        }
    }
}
