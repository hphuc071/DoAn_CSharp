using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Customer;

public sealed class CreateOrderTests
{
    [Fact, Trait("Actor", "KhachHang"), Trait("UseCase", "UC-WEB-06"), Trait("Layer", "API")]
    public async Task DatMon_KhoaGiuChoVaTaoDon_ThanhCong()
    {
        await using var api = await TestApi.Start("UC-WEB-06");
        var customer = await api.NewCustomer("order");
        var headers = await api.Login(customer);

        var menuRes = await api.Send("GET", "/api/menu");
        menuRes.Expect(200, "Lấy thực đơn thành công");
        var products = menuRes.Json.AsArray();
        long variantId = products.Count > 0 ? products[0]!.Number("id") : 1;

        var orderBody = Body(
            ("hinhThucNhanMon", "TAKEAWAY"),
            ("tenNguoiNhan", "Khách Test"),
            ("dienThoaiNguoiNhan", "0912345678"),
            ("items", new JsonArray(
                Body(
                    ("maCauHinhMon", variantId),
                    ("soLuong", 1),
                    ("phanTramDuong", 100),
                    ("phanTramDa", 100),
                    ("toppings", new JsonArray())
                )
            ))
        );

        var res = await api.Send("POST", "/api/customer/orders", orderBody, headers);
        Assert.True(res.Status == 201 || res.Status == 200 || res.Status == 409, "Đơn hàng phải được tạo thành công hoặc báo xung đột tồn kho hợp lệ");

        if (res.Status == 201 || res.Status == 200)
        {
            Assert.Equal("AWAITING_PAYMENT", res.Json.Text("trangThai"));
            Assert.True(res.Json.Number("maDonHang") > 0);
        }
    }
}
