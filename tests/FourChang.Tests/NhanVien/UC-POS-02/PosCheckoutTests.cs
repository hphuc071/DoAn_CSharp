using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;
namespace FourChang.Tests.Staff;

/// <summary>Chạy HTTP/SQL thật trong database riêng: giỏ nhiều món, thu tiền, giữ kho, replay và QR mô phỏng.</summary>
public sealed class PosCheckoutTests
{
    /// <summary>Thu CASH đủ tiền phát hành một hóa đơn; QR GET không thu, POST mô phỏng một lần và tự đổi trạng thái.</summary>
    [Fact, Trait("UseCase","UC-POS-02"), Trait("UseCase","UC-POS-05")]
    public async Task NhieuMon_ThuTien_Bom_ThanhToanMoPhong_VaPhanQuyen()
    {
        await using var api=await TestApi.Start("POS-Checkout",runtime:new(){["Payments__SimulationEnabled"]="true",["Orders__PendingPaymentTimeoutMinutes"]="15"});
        var admin=await api.AdminLogin();var employee=await api.Employee(admin,"Nguyễn Hoàng Minh");
        var session=(await api.Send("POST","/api/pos/shifts",Body(("MaNhanVien",employee.Number("maNhanVien")),("HoTen",employee.Text("hoTen")),("TenCa","Sáng"),("MaMay","POS-CHECKOUT"),("TienMatDauCa",500000)))).Expect(200,"Mở ca").Json;
        var staff=TestApi.Bearer(session);
        var menu=(await api.Send("GET","/api/menu")).Expect(200,"Menu").Json.AsArray().Where(x=>x!.Flag("available")).Take(2).ToList();
        Assert.Equal(2,menu.Count);var items=new JsonArray();decimal total=0;
        foreach(var dish in menu)
        {
            var variants=(await api.Send("GET",$"/api/menu/{dish!.Number("maMon")}/variants")).Expect(200,"Cấu hình").Json.AsArray();
            var v=variants.First(x=>x!.Flag("available"))!;var pair=v["tuyChonPhaChe"]!.AsArray()[0]!;
            var tops=new JsonArray();var top=v["toppings"]!.AsArray().FirstOrDefault(x=>x!.Flag("available"));
            if(top is not null)tops.Add(Body(("MaMonThem",top.Number("maMonThem")),("SoPhanMoiLy",1)));
            total+=(v["giaBan"]!.GetValue<decimal>()+(top?["giaMoiPhan"]?.GetValue<decimal>()??0))*2;
            items.Add(Body(("MaCauHinhMon",v.Number("maCauHinhMon")),("SoLuong",2),("PhanTramDuong",pair.Number("phanTramDuong")),("PhanTramDa",pair.Number("phanTramDa")),("GhiChu","Pha theo cấu hình"),("Toppings",tops)));
        }
        var input=Body(("MaThaoTac",NewId()),("HinhThuc","TAKEAWAY"),("PhuongThuc","CASH"),("TienKhachDua",total+10000),("TongTienDaChapNhan",total),("Items",items));
        (await api.Send("POST","/api/pos/checkout",input)).Expect(401,"Ẩn danh không thu");
        (await api.Send("POST","/api/pos/checkout",input,admin)).Expect(403,"Admin không giả phiên ca");
        var receipt=(await api.Send("POST","/api/pos/checkout",input,staff)).Expect(200,"Thu CASH").Json;
        Assert.Equal(10000,receipt.Number("tienThua"));var id=receipt.Number("maDonHang"); Assert.Equal("POS-"+id,receipt.Text("maDon")); Assert.Equal("POS-"+id,await api.Sql($"SELECT MaDonHienThi FROM dbo.DonHang WHERE MaDonHang={id}"));
        var replay=(await api.Send("POST","/api/pos/checkout",input,staff)).Expect(200,"Replay không tạo đơn mới").Json;
        Assert.Equal(id,replay.Number("maDonHang"));Assert.Equal("VERIFIED",receipt.Text("trangThaiThanhToan"));
        Assert.Equal("2",await api.Sql($"SELECT COUNT(*) FROM dbo.ChiTietDonHang WHERE MaDonHang={id}"));
        Assert.Equal("1",await api.Sql($"SELECT COUNT(*) FROM dbo.HoaDon WHERE MaDonHang={id}"));
        Assert.Equal("0",await api.Sql($"SELECT COUNT(*) FROM dbo.BienDongKho b JOIN dbo.ChiTietDonHang d ON d.MaChiTietDonHang=b.MaChiTietDonHang WHERE d.MaDonHang={id}"));
        Assert.True(int.Parse(await api.Sql($"SELECT COUNT(*) FROM dbo.GiuChoNguyenLieu g JOIN dbo.ChiTietDonHang d ON d.MaChiTietDonHang=g.MaChiTietDonHang WHERE d.MaDonHang={id}"))>0);
        var invalid=input.DeepClone()!.AsObject();invalid["MaThaoTac"]=NewId();invalid["TienKhachDua"]=total-1;
        (await api.Send("POST","/api/pos/checkout",invalid,staff)).Expect(400,"Thiếu tiền không ghi");
        invalid["TienKhachDua"]=total;invalid["TongTienDaChapNhan"]=total-1;
        (await api.Send("POST","/api/pos/checkout",invalid,staff)).Expect(409,"Giá đổi cần xác nhận");
        var bank=input.DeepClone()!.AsObject();bank["MaThaoTac"]=NewId();bank["PhuongThuc"]="BANK_TRANSFER";bank["TienKhachDua"]=null;
        var pending=(await api.Send("POST","/api/pos/checkout",bank,staff)).Expect(200,"Đơn QR đang chờ").Json;
        var bankId=pending.Number("maDonHang");var qr=(await api.Send("GET",$"/api/pos/checkout/{bankId}/simulation",headers:staff)).Expect(200,"Lấy QR").Json;
        Assert.StartsWith("data:image/png;base64,",qr.Text("pngDataUri"));
        var path=new Uri(qr.Text("url")).AbsolutePath;
        (await api.Send("GET",path)).Expect(200,"Quét chỉ mở trang");
        Assert.Equal("0",await api.Sql($"SELECT SoTienDaXacMinh FROM dbo.ThanhToan WHERE MaDonHang={bankId}"));
        var confirmations=await Task.WhenAll(api.Send("POST",path),api.Send("POST",path));
        foreach(var confirmation in confirmations)confirmation.Expect(200,"Xác nhận đồng thời chỉ ghi một lần");
        var verified=(await api.Send("GET",$"/api/pos/checkout/{bankId}",headers:staff)).Expect(200,"Polling tự đọc thành công").Json;
        Assert.Equal("VERIFIED",verified.Text("trangThaiThanhToan"));
        Assert.Equal("ACCEPTED",await api.Sql($"SELECT TrangThai FROM dbo.DonHang WHERE MaDonHang={bankId}"));
        Assert.Equal("1",await api.Sql($"SELECT COUNT(*) FROM dbo.HoaDon WHERE MaDonHang={bankId}"));
        Assert.Equal("1",await api.Sql($"SELECT COUNT(*) FROM dbo.GiaoDichNganHang WHERE MaThanhToan={verified.Number("maThanhToan")} AND TenNhaCungCap='SIMULATION'"));
        var tampered=path[..^1]+(path[^1]=='A'?'B':'A');
        (await api.Send("POST",tampered)).Expect(400,"QR sửa chữ ký bị chặn");
        var expiryInput=bank.DeepClone()!.AsObject();expiryInput["MaThaoTac"]=NewId();
        var expiryOrder=(await api.Send("POST","/api/pos/checkout",expiryInput,staff)).Expect(200,"Đơn kiểm tra hết hạn").Json.Number("maDonHang");
        var expiryQr=(await api.Send("GET",$"/api/pos/checkout/{expiryOrder}/simulation",headers:staff)).Expect(200,"QR trước hết hạn").Json;
        await api.Sql($"UPDATE dbo.ThanhToan SET HetHanLuc=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE MaDonHang={expiryOrder}");
        (await api.Send("POST",new Uri(expiryQr.Text("url")).AbsolutePath)).Expect(409,"Không xác nhận thanh toán quá hạn");
        Assert.Equal("0",await api.Sql($"SELECT SoTienDaXacMinh FROM dbo.ThanhToan WHERE MaDonHang={expiryOrder}"));
        var before=await api.Sql("SELECT COUNT(*) FROM dbo.DonHang");
        await api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try
        {
            var rollback=input.DeepClone()!.AsObject();rollback["MaThaoTac"]=NewId();
            Assert.True((await api.Send("POST","/api/pos/checkout",rollback,staff)).Status>=400);
        }
        finally{await api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app");}
        Assert.Equal(before,await api.Sql("SELECT COUNT(*) FROM dbo.DonHang"));
        var customer=await api.NewCustomer();var customerSession=await api.Login(customer);
        (await api.Send("GET",$"/api/pos/checkout/{bankId}",headers:customerSession)).Expect(403,"Khách không đọc POS");
        (await api.Send("GET",$"/api/customer/orders/{bankId}/simulation",headers:customerSession)).Expect(404,"Không lấy QR đơn người khác");
    }
}
