using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Customer;

public sealed class CreateOrderTests
{
    [Fact, Trait("Actor", "KhachHang"), Trait("UseCase", "UC-WEB-06"), Trait("Layer", "API")]
    public async Task DatMon_KhoaGiuChoVaTaoDon_ThanhCong()
    {
        await using var api = await TestApi.Start("UC-WEB-06", runtime: new()
        {
            ["Orders__CustomerCreationEnabled"] = "true",
            ["Orders__PendingPaymentTimeoutMinutes"] = "10"
        });
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

        var quote = (await api.Send("POST", "/api/customer/orders/quote", orderBody, headers)).Expect(200, "Báo giá từ máy chủ");
        headers["Idempotency-Key"] = NewId();
        headers["X-Checkout-Accepted-Total"] = quote.Json["tongCong"]!.ToString();
        var res = (await api.Send("POST", "/api/customer/orders", orderBody, headers)).Expect(201, "Tạo đơn từ giỏ hợp lệ");
        Assert.Equal("PENDING_PAYMENT", res.Json.Text("trangThai"));
        Assert.True(res.Json.Number("maDonHang") > 0);
        var replay = (await api.Send("POST", "/api/customer/orders", orderBody, headers)).Expect(200, "Gửi lại cùng mã thao tác");
        Assert.Equal(res.Json.Number("maDonHang"), replay.Json.Number("maDonHang"));
    }
}
