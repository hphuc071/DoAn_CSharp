using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Admin;

/// <summary>ADM-13 và điểm nối POS-01 chạy trên ca/thiết bị/SQL thật trong database cô lập.</summary>
public sealed class WorkSessionAdministrationTests
{
    /// <summary>Kiểm tra NULL khác 0, số tiền nguồn, khóa POS, receipt, chốt Admin, hồi phục và rollback audit.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-13"), Trait("Layer", "API")]
    public async Task GuiDoiChieu_ChotCa_Quyen_VaRollback()
    {
        await using var api = await TestApi.Start("UC-ADM-13-Close");
        var admin = await api.AdminLogin(); var employee = await api.Employee(admin);
        var device = await api.NewDevice(admin);
        var session = (await api.Send("POST", "/api/pos/shifts", api.OpenBody(employee, device), device.Headers)).Expect(200, "Mở ca").Json;
        var staff = TestApi.Bearer(session); foreach (var pair in device.Headers) staff[pair.Key] = pair.Value;
        var id = session.Number("shiftId"); var url = "/api/admin/work-sessions/" + id;
        (await api.Send("GET", url)).Expect(401, "Ẩn danh");
        (await api.Send("GET", url, headers: staff)).Expect(403, "Staff không phê duyệt");
        var ca = (await api.Send("GET", url, headers: admin)).Expect(200, "Chi tiết").Json["ca"]!;
        Assert.Null(ca["tienMatThucDem"]);
        (await api.Send("POST", url + "/close", TestApi.Action(ca), admin)).Expect(400, "Chưa thực đếm không tự lấy 0");
        (await api.Send("GET", "/api/admin/work-sessions?from=2026-12-01&to=2026-01-01", headers: admin)).Expect(400, "Ngày ngược");
        var source = await api.Sql($"INSERT dbo.DonHang(MaDonHienThi,MaCaLamViec,KenhDatHang,HinhThucNhanMon,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai) VALUES('SHIFT-CASH',{id},'POS','TAKEAWAY',0,0,'COMPLETED'); SELECT SCOPE_IDENTITY();");
        await api.Sql($"INSERT dbo.ThanhToan(MaThanhToanHienThi,MaDonHang,MaCaLamViec,PhuongThuc,SoTienYeuCau,SoTienDaXacMinh,TrangThai,MaThaoTac) VALUES('SHIFT-CASH-P',{source},{id},'CASH',100000,100000,'VERIFIED',NEWID()),('SHIFT-BANK-P',{source},{id},'BANK_TRANSFER',200000,200000,'VERIFIED',NEWID());");
        await api.Sql($"DECLARE @payment bigint=(SELECT MaThanhToan FROM dbo.ThanhToan WHERE MaThanhToanHienThi='SHIFT-CASH-P'); INSERT dbo.HoSoHoanTien(MaHoanTienHienThi,MaDonHang,MaThanhToan,LyDoHoan,SoTienYeuCau,SoTienDuocDuyet,PhuongThucHoan,TrangThai,MaQuanLyYeuCau) VALUES('SHIFT-REFUND',{source},@payment,N'Fixture',20000,20000,'CASH','PROCESSING',1); DECLARE @refund bigint=SCOPE_IDENTITY(); INSERT dbo.LanChiHoanTien(MaHoanTien,SoThuTuLanChi,MaChongChiLap,SoTien,PhuongThuc,TrangThai,BatDauLuc,MaCaLamViec) VALUES(@refund,1,NEWID(),10000,'CASH','SUCCEEDED',SYSUTCDATETIME(),{id}),(@refund,2,NEWID(),3000,'BANK_TRANSFER','SUCCEEDED',SYSUTCDATETIME(),{id}),(@refund,3,NEWID(),5000,'CASH','FAILED',SYSUTCDATETIME(),{id});");
        var detail = (await api.Send("GET", url, headers: admin)).Expect(200, "Nguồn thu thật").Json;
        Assert.Equal(590000m, detail["tienMatDuKienHienTai"]!.GetValue<decimal>());
        Assert.Equal(5, detail["giaoDich"]!.AsArray().Count);
        var submit = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", ca.Text("phienBanDuLieu")), ("TienMatThucDem", 589000));
        (await api.Send("POST", "/api/pos/shifts/current/reconciliation", submit, staff)).Expect(400, "Thiếu lý do chênh lệch");
        submit["LyDoChenhLech"] = "Kiểm đếm thiếu 1000";
        (await api.Send("POST", "/api/pos/shifts/current/reconciliation", submit, staff)).Expect(200, "Gửi và khóa ca");
        (await api.Send("POST", "/api/pos/shifts/current/reconciliation", submit, staff)).Expect(200, "Gửi lặp sau LOCKED");
        (await api.Send("GET", "/api/pos/data/DonHang", headers: staff)).Expect(401, "Ca khóa không bán");
        (await api.Send("GET", "/api/pos/shifts/current/reconciliation/operations/" + submit.Text("MaThaoTac"), headers: staff)).Expect(200, "Receipt ca khóa");
        detail = (await api.Send("GET", url, headers: admin)).Expect(200, "Đối chiếu Admin").Json;
        ca = detail["ca"]!; Assert.Equal(-1000m, detail["chenhLech"]!.GetValue<decimal>());
        var staleMoney = TestApi.Action(ca, ("TienMatDuKienDaXem", 123));
        (await api.Send("POST", url + "/close", staleMoney, admin)).Expect(409, "Nguồn tiền đã xem khác nguồn hiện tại");
        var close = TestApi.Action(ca, ("TienMatDuKienDaXem", detail["tienMatDuKienHienTai"]));
        await api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try { Check((await api.Send("POST", url + "/close", close, admin)).Status >= 400, "Audit lỗi rollback chốt"); }
        finally { await api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app"); }
        Assert.Equal("LOCKED", await api.Sql($"SELECT TrangThai FROM dbo.CaLamViec WHERE MaCaLamViec={id}"));
        ca = (await api.Send("GET", url, headers: admin)).Expect(200, "Đọc lại sau rollback").Json["ca"]!;
        close = TestApi.Action(ca, ("TienMatDuKienDaXem", detail["tienMatDuKienHienTai"]));
        var done = (await api.Send("POST", url + "/close", close, admin)).Expect(200, "Admin chốt").Json;
        Assert.Equal("CLOSED", done.Text("trangThai"));
        (await api.Send("POST", url + "/close", close, admin)).Expect(200, "Cùng mã không chốt hai lần");
        (await api.Send("POST", url + "/close", TestApi.Action(ca), admin)).Expect(409, "Mã mới không chốt ca đã kết thúc");
        (await api.Send("POST", url + "/resume", TestApi.Action(done), admin)).Expect(409, "Không mở lại CLOSED");
        Assert.Equal("2", await api.Sql($"SELECT COUNT(*) FROM dbo.ThanhToan WHERE MaCaLamViec={id}"));
        (await api.Send("GET", "/api/admin/work-sessions/operations/" + close.Text("MaThaoTac"), headers: admin)).Expect(200, "Receipt Admin");
    }

    /// <summary>Kiểm tra bàn giao đúng nguồn, ca đích xung đột, khoản chưa rõ, nhận ca POS và giữ lịch sử tài chính/nhân viên.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-13"), Trait("Layer", "API")]
    public async Task BanGiaoCa_NhanTaiPOS_GiuNguon_VaTiepTucCaKhoa()
    {
        await using var api = await TestApi.Start("UC-ADM-13-Handover");
        var admin = await api.AdminLogin(); var first = await api.Employee(admin, "Nhân viên nguồn"); var receiver = await api.Employee(admin, "Nhân viên nhận");
        var device = await api.NewDevice(admin); var target = await api.NewDevice(admin);
        var session = (await api.Send("POST", "/api/pos/shifts", api.OpenBody(first, device), device.Headers)).Expect(200, "Ca nguồn").Json;
        var id = session.Number("shiftId"); var url = "/api/admin/work-sessions/" + id;
        var staff = TestApi.Bearer(session); foreach (var pair in device.Headers) staff[pair.Key] = pair.Value;
        var order = await api.Sql($"INSERT dbo.DonHang(MaDonHienThi,MaCaLamViec,KenhDatHang,HinhThucNhanMon,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai) VALUES('HANDOVER-ORDER',{id},'POS','TAKEAWAY',0,0,'PREPARING'); SELECT SCOPE_IDENTITY();");
        await api.Sql($"INSERT dbo.ThanhToan(MaThanhToanHienThi,MaDonHang,MaCaLamViec,PhuongThuc,SoTienYeuCau,SoTienDaXacMinh,TrangThai,MaThaoTac) VALUES('HANDOVER-P',{order},{id},'BANK_TRANSFER',100000,0,'RECONCILING',NEWID());");
        var detail = (await api.Send("GET", url, headers: admin)).Expect(200, "Đơn dở và khoản chưa rõ").Json; var ca = detail["ca"]!;
        var submit = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", ca.Text("phienBanDuLieu")), ("TienMatThucDem", 500000));
        (await api.Send("POST", "/api/pos/shifts/current/reconciliation", submit, staff)).Expect(200, "Đếm để bàn giao");
        ca = (await api.Send("GET", url, headers: admin)).Expect(200, "Ca đã khóa").Json["ca"]!;
        (await api.Send("POST", url + "/close", TestApi.Action(ca, ("TienMatDuKienDaXem", detail["tienMatDuKienHienTai"])), admin)).Expect(409, "Chặn chốt khi còn nguồn");
        // A4: Admin cho tiếp tục trên máy cũ, số đếm cũ không tự tái sử dụng.
        var resumed = (await api.Send("POST", url + "/resume", TestApi.Action(ca), admin)).Expect(200, "Tiếp tục LOCKED").Json;
        Assert.Null(resumed["tienMatThucDem"]);
        (await api.Send("GET", "/api/pos/data/DonHang", headers: staff)).Expect(200, "Token chỉ bán khi ca mở lại hợp lệ");
        submit["MaThaoTac"] = NewId(); submit["PhienBanDuLieu"] = resumed.Text("phienBanDuLieu");
        (await api.Send("POST", "/api/pos/shifts/current/reconciliation", submit, staff)).Expect(200, "Gửi lại đếm mới");
        detail = (await api.Send("GET", url, headers: admin)).Expect(200, "Đối chiếu trước bàn giao").Json; ca = detail["ca"]!;
        var handover = TestApi.Action(ca, ("MaNhanVienNhan", receiver.Number("maNhanVien")), ("MaMayNhan", target.Input.Text("MaMay")),
            ("TienMatDuKienDaXem", detail["tienMatDuKienHienTai"]), ("MaDonBanGiao", new long[] { long.Parse(order) }), ("KhoanChuaRo", detail["khoanChuaRo"]));
        var inactive = await api.Employee(admin, "Người nhận đã ngừng");
        (await api.Send("POST", $"/api/admin/employees/{inactive.Number("maNhanVien")}/status", TestApi.Action(inactive, ("TrangThai", "INACTIVE")), admin)).Expect(200, "Ngừng người nhận fixture");
        var badReceiver = handover.Copy(); badReceiver["MaNhanVienNhan"] = inactive.Number("maNhanVien");
        (await api.Send("POST", url + "/handover", badReceiver, admin)).Expect(400, "Không nhận vào hồ sơ ngừng làm");
        var badMachine = handover.Copy(); badMachine["MaMayNhan"] = "MAY-KHONG-DUOC-CAP";
        (await api.Send("POST", url + "/handover", badMachine, admin)).Expect(400, "Không nhận vào máy không được cấp");
        var bad = handover.Copy(); bad["MaDonBanGiao"] = new JsonArray();
        (await api.Send("POST", url + "/handover", bad, admin)).Expect(409, "Không bỏ sót đơn");
        var destination = (await api.Send("POST", "/api/pos/shifts", api.OpenBody(receiver, target), target.Headers)).Expect(200, "Ca đích đang mở").Json;
        (await api.Send("POST", url + "/handover", handover, admin)).Expect(409, "Xung đột ca đích");
        (await api.Send("POST", "/api/pos/shifts/current/close", Body(("TienMatThucDem", 500000)), TestApi.Bearer(destination))).Expect(200, "Đóng ca đích fixture");
        await api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try { Check((await api.Send("POST", url + "/handover", handover, admin)).Status >= 400, "Audit lỗi rollback bàn giao"); }
        finally { await api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app"); }
        ca = (await api.Send("GET", url, headers: admin)).Expect(200, "Ca nguồn bảo toàn").Json["ca"]!;
        Assert.Equal("LOCKED", ca.Text("trangThai"));
        handover["PhienBanDuLieu"] = ca.Text("phienBanDuLieu");
        (await api.Send("POST", url + "/handover", handover, admin)).Expect(200, "Bàn giao có biên bản");
        (await api.Send("POST", url + "/handover", handover, admin)).Expect(200, "Receipt bàn giao");
        Assert.Equal(id.ToString(), await api.Sql($"SELECT MaCaLamViec FROM dbo.ThanhToan WHERE MaThanhToanHienThi='HANDOVER-P'"));
        Assert.Equal(first.Number("maNhanVien").ToString(), await api.Sql($"SELECT MaNhanVien FROM dbo.CaLamViec WHERE MaCaLamViec={id}"));
        var stopReceiver = TestApi.Action(receiver, ("TrangThai", "INACTIVE"));
        (await api.Send("POST", $"/api/admin/employees/{receiver.Number("maNhanVien")}/status", stopReceiver, admin)).Expect(409, "Người nhận chưa mở ca vẫn phải giữ hồ sơ");
        var received = (await api.Send("POST", "/api/pos/shifts", api.OpenBody(receiver, target), target.Headers)).Expect(200, "POS nhận ca theo phân công").Json;
        Assert.Equal(id.ToString(), await api.Sql($"SELECT MaCaBanGiaoTruoc FROM dbo.CaLamViec WHERE MaCaLamViec={received.Number("shiftId")}"));
        var receivedDetail = (await api.Send("GET", "/api/admin/work-sessions/" + received.Number("shiftId"), headers: admin)).Expect(200, "Truy vết phần bàn giao").Json;
        Assert.Single(receivedDetail["donDangXuLy"]!.AsArray()); Assert.Single(receivedDetail["khoanChuaRo"]!.AsArray());
        Assert.Equal(500000m, receivedDetail["tienMatDuKienHienTai"]!.GetValue<decimal>());
    }
}
