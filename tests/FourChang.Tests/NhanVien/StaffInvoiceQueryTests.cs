using System.Globalization;
using FourChang.Tests.Admin;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Staff;

/// <summary>Tra hóa đơn qua HTTP/SQL thật: lọc trước phân trang, không giới hạn vào 100 dòng đã tải.</summary>
[Collection("AdminFeatures"),Trait("Feature","StaffInvoiceSearch")]
public sealed class StaffInvoiceQueryTests(AdminFeatureFixture fixture)
{
    /// <summary>Tìm mã đơn/tên, kiểm cận ngày loại trừ và ba trang; endpoint chỉ dành cho Staff đang có ca.</summary>
    [Fact]
    public async Task TimKiem_LocNgay_PhanTrang_VaPhanQuyen()
    {
        var prefix="INVQ-"+Guid.NewGuid().ToString("N")[..10];
        var start=DateTime.Today.AddDays(-2).ToUniversalTime();var end=DateTime.Today.AddDays(-1).ToUniversalTime();
        var startSql=start.ToString("yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture);
        var endSql=end.ToString("yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture);
        var scenario=await OperationsScenarioSeed.Create(fixture);
        await fixture.Api.Sql($"""
            SET XACT_ABORT ON;
            DECLARE @i int=1,@id bigint,@issued datetime2;
            WHILE @i<=105 BEGIN
              SET @issued=CASE WHEN @i=105 THEN CONVERT(datetime2,'{endSql}') ELSE DATEADD(minute,@i,CONVERT(datetime2,'{startSql}')) END;
              INSERT dbo.DonHang(MaDonHienThi,MaCaLamViec,KenhDatHang,HinhThucNhanMon,TenNguoiNhan,DienThoaiNguoiNhan,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai)
              VALUES(CONCAT('{prefix}-',@i),{scenario.Shift},'POS','TAKEAWAY',N'{prefix} Khách kiểm thử','0901234567',0,0,'ACCEPTED');
              SET @id=SCOPE_IDENTITY();
              INSERT dbo.HoaDon(SoHoaDon,MaDonHang,LoaiHoaDon,SoTien,NoiDungHoaDonDaChotJson,TrangThai,PhatHanhLuc,MaQuanLyPhatHanh)
              VALUES(CONCAT('{prefix}-',@i),@id,'ORIGINAL',10000,N'[]','ISSUED',@issued,1);
              SET @i=@i+1;
            END;
            """);
        var employee=await fixture.Api.Employee(fixture.Admin,"Trần Hoàng Phúc");
        var session=(await fixture.Api.Send("POST","/api/pos/shifts",Body(("MaNhanVien",employee.Number("maNhanVien")),("HoTen",employee.Text("hoTen")),("TenCa","Sáng"),("MaMay",prefix),("TienMatDauCa",0),("MaThaoTac",NewId())))).Expect(200,"Mở ca").Json;
        var staff=TestApi.Bearer(session);
        (await fixture.Api.Send("GET","/api/pos/invoices")).Expect(401,"Ẩn danh không xem hóa đơn");
        var customer=await fixture.Api.NewCustomer("invoice");
        (await fixture.Api.Send("GET","/api/pos/invoices",headers:await fixture.Api.Login(customer))).Expect(403,"Khách không dùng API POS");
        var range=$"search={prefix}&from={Uri.EscapeDataString(start.ToString("O"))}&to={Uri.EscapeDataString(end.ToString("O"))}";
        for(var page=1;page<=3;page++)
        {
            var data=(await fixture.Api.Send("GET",$"/api/pos/invoices?{range}&page={page}",headers:staff)).Expect(200,"Lọc trước phân trang").Json;
            Assert.Equal(104,data.Number("tongSo"));Assert.Equal(page==3?4:50,data["duLieu"]!.AsArray().Count);
        }
        var single=(await fixture.Api.Send("GET",$"/api/pos/invoices?search={prefix}-104",headers:staff)).Expect(200,"Tìm ngoài 100 bản ghi đầu").Json;
        var invoice=Assert.Single(single["duLieu"]!.AsArray())!;var code=invoice.Text("maDon");Assert.StartsWith("POS-",code);
        var byOrder=(await fixture.Api.Send("GET",$"/api/pos/invoices?search={code}",headers:staff)).Expect(200,"Tìm mã đơn ngắn").Json;
        Assert.Equal(1,byOrder.Number("tongSo"));
        var byName=(await fixture.Api.Send("GET",$"/api/pos/invoices?search={Uri.EscapeDataString(prefix+" Khách kiểm thử")}",headers:staff)).Expect(200,"Tìm tên người nhận").Json;
        Assert.Equal(105,byName.Number("tongSo"));
        (await fixture.Api.Send("GET",$"/api/pos/invoices?from={Uri.EscapeDataString(end.ToString("O"))}&to={Uri.EscapeDataString(start.ToString("O"))}",headers:staff)).Expect(400,"Khoảng ngày đảo ngược");
        var detail=(await fixture.Api.Send("GET",$"/api/pos/invoices/{invoice.Number("maHoaDon")}",headers:staff)).Expect(200,"Đọc đúng hóa đơn").Json;
        Assert.Null(detail["lichSu"]);Assert.NotNull(detail["snapshotJson"]);
    }
}
