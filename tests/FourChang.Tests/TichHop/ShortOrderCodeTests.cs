using FourChang.Presentation.Shared.Contracts.Common;
using FourChang.Tests.Admin;
using FourChang.Tests.Shared;

namespace FourChang.Tests.Integration;

/// <summary>Quy ước mã ngắn chung và đọc/tìm đơn cũ không sửa dữ liệu đối soát.</summary>
public sealed class ShortOrderCodeFormatTests
{
    /// <summary>Mã không đệm số 0, giữ kênh và toàn bộ giá trị ID đến giới hạn bigint.</summary>
    [Theory, Trait("Feature", "ShortOrderCode")]
    [InlineData(1, "WEB", "WEB-1")]
    [InlineData(2, "WEB", "WEB-2")]
    [InlineData(125, "WEB", "WEB-125")]
    [InlineData(3, "POS", "POS-3")]
    [InlineData(long.MaxValue, "WEB", "WEB-9223372036854775807")]
    public void FormatKhongDemSo(long id, string channel, string expected) => Assert.Equal(expected, OrderCode.Format(id, channel));

    /// <summary>Không coi GUID cũ, số âm, mã thiếu hoặc vượt bigint là một mã ngắn hợp lệ.</summary>
    [Theory, Trait("Feature", "ShortOrderCode")]
    [InlineData("ORD-20261113-abcdef")]
    [InlineData("WEB-0")]
    [InlineData("POS--1")]
    [InlineData("WEB-9223372036854775808")]
    [InlineData("ABC-125")]
    [InlineData("WEB-1x")]
    public void ParseTuChoiMaSai(string value) => Assert.False(OrderCode.TryParse(value, out _, out _));

    /// <summary>Tìm mã không phân biệt chữ hoa/thường, giữ đúng kênh và số nội bộ.</summary>
    [Fact, Trait("Feature", "ShortOrderCode")]
    public void ParseDungKenhVaId()
    {
        Assert.True(OrderCode.TryParse(" web-125 ", out var channel, out var id)); Assert.Equal("WEB", channel); Assert.Equal(125, id);
    }
}

/// <summary>Đơn lưu mã cũ vẫn đọc được mã mới trên API, trong khi nội dung ngân hàng/chứng từ cũ không bị rewrite.</summary>
[Collection("AdminFeatures")]
public sealed class ShortOrderCodeIntegrationTests(AdminFeatureFixture fixture)
{
    /// <summary>Đối chiếu mã giữa Admin, Web, bảng Staff, tìm theo alias cũ và tìm theo mã WEB-ID mới.</summary>
    [Fact, Trait("Feature", "ShortOrderCode")]
    public async Task DonCu_HienThiMaNgan_TimDuocMaCuVaMoi_KhongSuaDatabase()
    {
        var data = await OperationsScenarioSeed.Create(fixture);
        var oldCode = await fixture.Api.Sql($"SELECT MaDonHienThi FROM dbo.DonHang WHERE MaDonHang={data.Order}");
        var customer = await fixture.Api.NewCustomer(); var owner = await fixture.Api.Login(customer);
        await fixture.Api.Sql($"UPDATE dbo.DonHang SET KenhDatHang='WEB',MaKhachHang={customer.Id} WHERE MaDonHang={data.Order}");
        var expected = "WEB-" + data.Order;
        var admin = (await fixture.Send("GET", $"/api/admin/orders/{data.Order}"))["don"]!;
        Assert.Equal(expected, admin.Text("maDon"));
        var web = (await fixture.Api.Send("GET", $"/api/customer/orders/{data.Order}", headers: owner)).Expect(200, "Khách đọc mã ngắn").Json;
        Assert.Equal(expected, web["order"]!.Text("code"));
        foreach (var code in new[] { oldCode, expected })
        {
            var found = await fixture.Send("GET", "/api/admin/orders?TimKiem=" + Uri.EscapeDataString(code));
            Assert.Contains(found["duLieu"]!.AsArray(), x => x!.Number("maDonHang") == data.Order && x!.Text("maDon") == expected);
        }
        Assert.Equal(oldCode, await fixture.Api.Sql($"SELECT MaDonHienThi FROM dbo.DonHang WHERE MaDonHang={data.Order}"));
        var stranger = await fixture.Api.Login(await fixture.Api.NewCustomer("other"));
        (await fixture.Api.Send("GET", $"/api/customer/orders/{data.Order}", headers: stranger)).Expect(404, "Mã ngắn không bỏ kiểm tra chủ đơn");
    }
}
