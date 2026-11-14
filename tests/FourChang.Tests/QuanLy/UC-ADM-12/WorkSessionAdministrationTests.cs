using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Admin;

/// <summary>ADM-12 và tích hợp POS-01: SQL/API thật, không dùng thiết bị để mở phiên ca mới.</summary>
[Collection("AdminFeatures"), Trait("UseCase", "UC-ADM-12"), Trait("Actor", "QuanLy")]
public sealed class WorkSessionAdministrationTests(AdminFeatureFixture fixture)
{
    /// <summary>NULL khác 0, Staff không phê duyệt; gửi đối chiếu khóa bán và chốt Admin chống lặp.</summary>
    [Fact]
    public async Task GuiDoiChieu_ChotCa_Quyen_VaChongLap()
    {
        var s = await WorkforceScenarios.Open(fixture);
        var id = s.Session.Number("shiftId"); var url = "/api/admin/work-sessions/" + id;
        var detail = await WorkforceScenarios.Detail(fixture, id);
        Assert.Null(detail["ca"]!["tienMatThucDem"]);
        (await fixture.Api.Send("GET", url)).Expect(401, "Ẩn danh");
        (await fixture.Api.Send("GET", url, headers: s.Headers)).Expect(403, "Staff không phê duyệt");
        await fixture.Send("POST", url + "/close", WorkforceScenarios.CloseInput(detail), 400);
        await fixture.Send("GET", "/api/admin/work-sessions?from=2026-12-01&to=2026-01-01", expected: 400);
        var missing = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", detail["ca"]!.Text("phienBanDuLieu")));
        (await fixture.Api.Send("POST", "/api/pos/shifts/current/reconciliation", missing, s.Headers)).Expect(400, "Thiếu thực đếm không tự lấy 0");
        (await fixture.Api.Send("POST", "/api/pos/shifts/current/close", missing, s.Headers)).Expect(400, "Thiếu thực đếm không tự chốt 0");
        var input = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", detail["ca"]!.Text("phienBanDuLieu")), ("TienMatThucDem", 499000));
        (await fixture.Api.Send("POST", "/api/pos/shifts/current/reconciliation", input, s.Headers)).Expect(409,"Lệch tiền phải kiểm đếm lại");
        input["TienMatThucDem"]=500000;
        (await fixture.Api.Send("POST", "/api/pos/shifts/current/reconciliation", input, s.Headers)).Expect(200, "Gửi đếm");
        (await fixture.Api.Send("POST", "/api/pos/shifts/current/reconciliation", input, s.Headers)).Expect(200, "Gửi lặp");
        var changed = input.Copy(); changed["TienMatThucDem"] = 499000;
        (await fixture.Api.Send("POST", "/api/pos/shifts/current/reconciliation", changed, s.Headers)).Expect(409, "Không đổi nội dung mã cũ");
        (await fixture.Api.Send("GET", "/api/pos/data/DonHang", headers: s.Headers)).Expect(403, "LOCKED không bán");
        detail = await WorkforceScenarios.Detail(fixture, id);
        Assert.True(detail.Flag("dangChoDoiChieu"));
        Assert.Equal(0m, detail["chenhLech"]!.GetValue<decimal>());
        var close = WorkforceScenarios.CloseInput(detail);
        var closed = await fixture.Send("POST", url + "/close", close);
        Assert.Equal("CLOSED", closed.Text("trangThai"));
        await fixture.Send("POST", url + "/close", close);
        close["MaThaoTac"] = NewId(); await fixture.Send("POST", url + "/close", close, 409);
        Assert.Equal("1", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE HanhDong='SHIFT_ADMIN_CLOSED' AND MaDoiTuong='{id}'"));
    }

    /// <summary>Tổng chỉ lấy đơn hoàn thành và giá đã chốt; không cộng đơn đang xử lý hoặc ca khác.</summary>
    [Fact]
    public async Task TongTienDonHoanThanh_DocGiaDaChot_VaKhongCongDonDo()
    {
        var done=await OperationsScenarioSeed.Create(fixture,"COMPLETED");
        var waiting=await OperationsScenarioSeed.Create(fixture);
        await fixture.Api.Sql($"UPDATE dbo.DonHang SET MaCaLamViec={done.Shift} WHERE MaDonHang={waiting.Order}");
        var detail=await WorkforceScenarios.Detail(fixture,done.Shift);
        Assert.Equal(1,detail.Number("soDonHoanThanh"));Assert.Equal(done.Total,detail["tongTienDonHoanThanh"]!.GetValue<decimal>());
    }

    /// <summary>Đọc nguồn tiền mặt/CK riêng; thay đổi nguồn dù tổng không đổi cũng buộc Admin tải lại.</summary>
    [Fact]
    public async Task NguonTien_ThayDoi_BuocDoiChieuLai()
    {
        var s = await WorkforceScenarios.Open(fixture);
        var id = s.Session.Number("shiftId");
        var code = Guid.NewGuid().ToString("N")[..20];
        var order = await fixture.Api.Sql($"INSERT dbo.DonHang(MaDonHienThi,MaCaLamViec,KenhDatHang,HinhThucNhanMon,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai) VALUES('CA-{code}',{id},'POS','TAKEAWAY',0,0,'COMPLETED'); SELECT SCOPE_IDENTITY();");
        await fixture.Api.Sql($"INSERT dbo.ThanhToan(MaThanhToanHienThi,MaDonHang,MaCaLamViec,PhuongThuc,SoTienYeuCau,SoTienDaXacMinh,TrangThai,MaThaoTac) VALUES('TM-{code}',{order},{id},'CASH',100000,100000,'VERIFIED',NEWID()),('CK-{code}',{order},{id},'BANK_TRANSFER',200000,200000,'VERIFIED',NEWID());");
        await fixture.Api.Sql($"DECLARE @p bigint=(SELECT MaThanhToan FROM dbo.ThanhToan WHERE MaThanhToanHienThi='TM-{code}'); INSERT dbo.HoSoHoanTien(MaHoanTienHienThi,MaDonHang,MaThanhToan,LyDoHoan,SoTienYeuCau,SoTienDuocDuyet,PhuongThucHoan,TrangThai,MaCaYeuCau) VALUES('R-{code}',{order},@p,N'Đối chiếu hoàn CASH',5000,5000,'CASH','REFUNDED',{id}); DECLARE @r bigint=SCOPE_IDENTITY(); INSERT dbo.LanChiHoanTien(MaHoanTien,SoThuTuLanChi,MaChongChiLap,SoTien,PhuongThuc,TrangThai,BatDauLuc,CoKetQuaLuc,MaCaLamViec) VALUES(@r,1,NEWID(),5000,'CASH','SUCCEEDED',SYSUTCDATETIME(),SYSUTCDATETIME(),{id});");
        var detail = await WorkforceScenarios.Submit(fixture, id, s.Headers, 595000);
        Assert.Equal(595000m, detail["tienMatDuKienHienTai"]!.GetValue<decimal>());
        Assert.Equal(3, detail["giaoDich"]!.AsArray().Count);
        Assert.Equal(95000,detail.Number("tienThuDuoc"));Assert.Equal(200000,detail.Number("thuChuyenKhoan"));
        var old = WorkforceScenarios.CloseInput(detail);
        await fixture.Api.Sql($"INSERT dbo.ThanhToan(MaThanhToanHienThi,MaDonHang,MaCaLamViec,PhuongThuc,SoTienYeuCau,SoTienDaXacMinh,TrangThai,MaThaoTac) VALUES('CK2-{code}',{order},{id},'BANK_TRANSFER',1000,1000,'VERIFIED',NEWID());");
        await fixture.Send("POST", $"/api/admin/work-sessions/{id}/close", old, 409);
        detail = await WorkforceScenarios.Detail(fixture, id);
        Assert.Equal(595000m, detail["tienMatDuKienHienTai"]!.GetValue<decimal>());
        await fixture.Send("POST", $"/api/admin/work-sessions/{id}/close", WorkforceScenarios.CloseInput(detail));
    }

    /// <summary>Bàn giao giữ nguyên chủ nguồn và giao dịch, người nhận chưa mở ca không được ngừng/xóa.</summary>
    [Fact]
    public async Task BanGiao_DonDo_KhoanChuaRo_GiuLichSu_VaNhanCa()
    {
        var s = await WorkforceScenarios.Open(fixture);
        var receiver = await fixture.Send("POST", "/api/admin/employees", WorkforceScenarios.EmployeeInput());
        var id = s.Session.Number("shiftId"); var suffix = Guid.NewGuid().ToString("N")[..20];
        var order = await fixture.Api.Sql($"INSERT dbo.DonHang(MaDonHienThi,MaCaLamViec,KenhDatHang,HinhThucNhanMon,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai) VALUES('BG-{suffix}',{id},'POS','TAKEAWAY',0,0,'PREPARING'); SELECT SCOPE_IDENTITY();");
        await fixture.Api.Sql($"INSERT dbo.ThanhToan(MaThanhToanHienThi,MaDonHang,MaCaLamViec,PhuongThuc,SoTienYeuCau,SoTienDaXacMinh,TrangThai,MaThaoTac) VALUES('BG-P-{suffix}',{order},{id},'BANK_TRANSFER',100000,0,'RECONCILING',NEWID());");
        await fixture.Api.Sql($"INSERT dbo.ThanhToan(MaThanhToanHienThi,MaDonHang,MaCaLamViec,PhuongThuc,SoTienYeuCau,SoTienDaXacMinh,TrangThai,MaThaoTac) VALUES('BG-C-{suffix}',{order},{id},'CASH',100000,100000,'VERIFIED',NEWID()); INSERT dbo.DonHang(MaDonHienThi,MaCaLamViec,KenhDatHang,HinhThucNhanMon,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai) VALUES('BG-DONE-{suffix[..10]}',{id},'POS','TAKEAWAY',0,0,'COMPLETED');");
        var detail = await WorkforceScenarios.Submit(fixture, id, s.Headers,600000);
        await fixture.Send("POST", $"/api/admin/work-sessions/{id}/close", WorkforceScenarios.CloseInput(detail), 409);
        var machine = "NHAN-" + suffix[..10];
        var handover = WorkforceScenarios.CloseInput(detail);
        handover["MaNhanVienNhan"] = receiver.Number("maNhanVien"); handover["MaMayNhan"] = machine; handover["LyDo"] = "Đổi người phụ trách ca";
        handover["MaDonBanGiao"] = new JsonArray(long.Parse(order)); handover["KhoanChuaRo"] = detail["khoanChuaRo"]!.DeepClone();
        var bad = handover.Copy(); bad["MaDonBanGiao"] = new JsonArray();
        await fixture.Send("POST", $"/api/admin/work-sessions/{id}/handover", bad, 409);
        await fixture.Send("POST", $"/api/admin/work-sessions/{id}/handover", handover);
        await fixture.Send("POST", $"/api/admin/work-sessions/{id}/handover", handover);
        var receiverId = receiver.Number("maNhanVien");
        await fixture.Send("POST", $"/api/admin/employees/{receiverId}/status", Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", receiver.Text("phienBanDuLieu")), ("TrangThai", "INACTIVE"), ("LyDo", "Xin nghỉ")), 409);
        await fixture.Send("POST", $"/api/admin/employees/{receiverId}/delete", Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", receiver.Text("phienBanDuLieu"))), 409);
        var received = (await fixture.Api.Send("POST", "/api/pos/shifts", Body(("MaNhanVien", receiverId), ("HoTen", receiver.Text("hoTen")), ("TenCa", "Chiều"), ("MaMay", machine), ("TienMatDauCa", 500000)))).Expect(200, "Nhận ca không đăng ký máy").Json;
        var target = await WorkforceScenarios.Detail(fixture, received.Number("shiftId"));
        Assert.Equal(id, target["ca"]!.Number("maCaBanGiaoTruoc"));
        Assert.Single(target["donDangXuLy"]!.AsArray()); Assert.Single(target["khoanChuaRo"]!.AsArray());
        Assert.Equal(0,target.Number("thuTienMat"));Assert.Equal(500000,target.Number("tienMatDuKienHienTai"));
        var receiverStaff=TestApi.Bearer(received);var targetId=received.Number("shiftId");
        var own=await fixture.Api.Sql($"INSERT dbo.DonHang(MaDonHienThi,MaCaLamViec,KenhDatHang,HinhThucNhanMon,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai) VALUES('BG-OWN-{suffix[..10]}',{targetId},'POS','TAKEAWAY',0,0,'ACCEPTED');SELECT SCOPE_IDENTITY();");
        var previous=(await fixture.Api.Send("GET","/api/pos/orders?PhamViCa=PREVIOUS&NhomDon=NORMAL",headers:receiverStaff)).Expect(200,"Đơn nhận từ ca trước").Json;
        var previousRow=Assert.Single(previous["duLieu"]!.AsArray())!;Assert.Equal(long.Parse(order),previousRow.Number("maDonHang"));
        Assert.Equal($"Bàn giao từ ca #{id}",previousRow.Text("nguonCa"));Assert.Equal(1,previous.Number("tongSo"));
        var current=(await fixture.Api.Send("GET","/api/pos/orders?PhamViCa=CURRENT&NhomDon=NORMAL",headers:receiverStaff)).Expect(200,"Đơn ca này").Json;
        var ownRow=Assert.Single(current["duLieu"]!.AsArray())!;Assert.Equal(long.Parse(own),ownRow.Number("maDonHang"));
        Assert.Equal($"Ca này #{targetId}",ownRow.Text("nguonCa"));
        (await fixture.Api.Send("GET","/api/pos/orders?PhamViCa=INVALID",headers:receiverStaff)).Expect(400,"Không chấp nhận nhóm ca giả");

        Assert.Equal(s.Employee.Number("maNhanVien").ToString(), await fixture.Api.Sql($"SELECT MaNhanVien FROM dbo.CaLamViec WHERE MaCaLamViec={id}"));
        Assert.Equal(id.ToString(), await fixture.Api.Sql($"SELECT MaCaLamViec FROM dbo.ThanhToan WHERE MaThanhToanHienThi='BG-P-{suffix}'"));
    }

    /// <summary>Mã phục hồi dùng một lần, không có trong audit/receipt và chỉ cấp quyền gửi đối chiếu.</summary>
    [Fact]
    public async Task MatPhien_MaPhucHoiMotLan_ChiDoiChieu()
    {
        var s = await WorkforceScenarios.Open(fixture);
        var id = s.Session.Number("shiftId");
        var detail = await WorkforceScenarios.Detail(fixture, id);
        var input = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", detail["ca"]!.Text("phienBanDuLieu")), ("LyDo", "Máy mất phiên sau sự cố"));
        var ticket = await fixture.Send("POST", $"/api/admin/work-sessions/{id}/recovery", input);
        Assert.False(string.IsNullOrWhiteSpace(ticket.Text("maPhucHoi")));
        var repeated = await fixture.Send("POST", $"/api/admin/work-sessions/{id}/recovery", input);
        Assert.Null(repeated["maPhucHoi"]);
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE CHARINDEX('{ticket.Text("maPhucHoi")}',COALESCE(DuLieuSauJson,'')+COALESCE(KetQuaDaChotJson,''))>0"));
        var redemption = Body(("MaCaLamViec", id), ("MaNhanVien", s.Employee.Number("maNhanVien")), ("HoTen", s.Employee.Text("hoTen")), ("MaPhucHoi", ticket.Text("maPhucHoi")));
        var recovery = (await fixture.Api.Send("POST", "/api/pos/shifts/recovery", redemption)).Expect(200, "Đổi mã phục hồi").Json;
        (await fixture.Api.Send("POST", "/api/pos/shifts/recovery", redemption)).Expect(409, "Mã chỉ dùng một lần");
        var headers = TestApi.Bearer(recovery);
        (await fixture.Api.Send("GET", "/api/pos/data/DonHang", headers: headers)).Expect(403, "Không cấp quyền bán");
        (await fixture.Api.Send("GET", "/api/admin/work-sessions", headers: headers)).Expect(403, "Không cấp quyền Admin");
        (await fixture.Api.Send("POST", "/api/pos/shifts/current/close", Body(("TienMatThucDem", 500000)), headers)).Expect(403, "Không tự chốt");
        var after = await WorkforceScenarios.Submit(fixture, id, headers);
        await fixture.Send("POST", $"/api/admin/work-sessions/{id}/close", WorkforceScenarios.CloseInput(after));
    }

    /// <summary>Chức năng khóa tạm/tiếp tục đã bỏ; gọi endpoint cũ không đổi trạng thái ca.</summary>
    [Fact]
    public async Task KhongConKhoaTamVaDeNghiTiepTuc()
    {
        var s=await WorkforceScenarios.Open(fixture);var id=s.Session.Number("shiftId");
        var input=Body(("MaThaoTac",NewId()));
        (await fixture.Api.Send("POST","/api/pos/shifts/current/lock",input,s.Headers)).Expect(404,"Đã bỏ khóa tạm");
        (await fixture.Api.Send("POST","/api/pos/shifts/current/resume-request",input,s.Headers)).Expect(404,"Đã bỏ đề nghị tiếp tục");
        (await fixture.Api.Send("POST",$"/api/admin/work-sessions/{id}/resume",input,fixture.Admin)).Expect(404,"Đã bỏ mở khóa tạm");
        Assert.Equal("OPEN",(await WorkforceScenarios.Detail(fixture,id))["ca"]!.Text("trangThai"));
    }

    /// <summary>Không có audit thì ca không đóng một phần; có thể thử lại cùng mã khi quyền ghi được phục hồi.</summary>
    [Fact]
    public async Task ChotCa_AuditHong_Rollback()
    {
        var s = await WorkforceScenarios.Open(fixture);
        var id = s.Session.Number("shiftId");
        var detail = await WorkforceScenarios.Submit(fixture, id, s.Headers);
        var action = WorkforceScenarios.CloseInput(detail);
        await fixture.Api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try { Assert.True((await fixture.Api.Send("POST", $"/api/admin/work-sessions/{id}/close", action, fixture.Admin)).Status >= 400); }
        finally { await fixture.Api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app"); }
        detail = await WorkforceScenarios.Detail(fixture, id);
        Assert.Equal("LOCKED", detail["ca"]!.Text("trangThai"));
        Assert.Null(detail["ca"]!["ketThucLuc"]);
        await fixture.Send("POST", $"/api/admin/work-sessions/{id}/close", action);
    }
}
