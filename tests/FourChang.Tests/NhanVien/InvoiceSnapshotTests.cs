using FourChang.Desktop.Shared.Invoices;

namespace FourChang.Tests.Staff;

/// <summary>Kiểm tra bảng hóa đơn từ snapshot đã chốt, không dùng giá menu hoặc số ly sau hủy.</summary>
public sealed class InvoiceSnapshotTests
{
    /// <summary>Topping tính theo số phần mỗi ly; số ly đặt ban đầu được giữ dù đơn đã hủy một phần.</summary>
    [Fact]
    public void DocGiaVaSoLyDaPhatHanh_GomTopping()
    {
        const string json="""
        {"lines":[{"MaChiTietDonHang":5,"TenMonDaChot":"Latte","KichCoDaChot":"M","GiaMonDaChot":45000,"SoLuongDat":2,"SoLuongDaHuy":1}],
         "toppings":[{"MaChiTietDonHang":5,"TenMonThemDaChot":"Trân châu","SoPhanMoiLy":2,"GiaMoiPhanDaChot":10000}]}
        """;
        var line=Assert.Single(InvoiceSnapshotReader.Read(json));
        Assert.Equal("Latte",line.Mon);Assert.Equal("M",line.Size);Assert.Equal(2,line.SoLuong);
        Assert.Equal(65000,line.DonGia);Assert.Equal(130000,line.ThanhTien);Assert.Contains("Trân châu × 2",line.Topping);
    }

    /// <summary>Hóa đơn điều chỉnh/cũ hoặc JSON lỗi không được dựng món và giá giả thay nội dung thiếu.</summary>
    [Theory]
    [InlineData("{\"Amount\":-10000,\"SourceType\":\"CANCELLATION\"}")]
    [InlineData("{\"Mon\":\"Cà phê\",\"SoLy\":2}")]
    [InlineData("không phải JSON")]
    public void ThieuDongMon_KhongTuTaoGia(string snapshot) => Assert.Empty(InvoiceSnapshotReader.Read(snapshot));
}
