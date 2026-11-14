using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Staff;

/// <summary>Kết thúc ca lưu báo cáo kiểm đếm và đóng ca cùng giao dịch, không chờ nút chốt của Admin.</summary>
public sealed class FinishShiftTests
{
    /// <summary>Không bắt nhập lý do; cuối ca trừ đầu ca được lưu đến Admin, gửi lặp không ghi lại, mở tiếp ca khác.</summary>
    [Fact, Trait("Feature", "FinishShiftWithReport"), Trait("Layer", "API")]
    public async Task GuiBaoCaoVaDongCa_NguyenTu_ChongLap_VaMoCaTiep()
    {
        await using var api = await TestApi.Start("Finish-Shift-With-Report");
        var admin = await api.AdminLogin();
        var employee = await api.Employee(admin,"Nguyễn Minh Anh");
        var open = Body(("MaNhanVien",employee.Number("maNhanVien")),("HoTen",employee.Text("hoTen")),("TenCa","Sáng"),("MaMay","FINISH-SHIFT"),("TienMatDauCa",500000));
        var session = (await api.Send("POST","/api/pos/shifts",open)).Expect(200,"Mở ca").Json;
        var id = session.Number("shiftId"); var staff = TestApi.Bearer(session);
        var detail = (await api.Send("GET",$"/api/admin/work-sessions/{id}",headers:admin)).Expect(200,"Đọc đối chiếu").Json;
        var close = Body(("MaThaoTac",NewId()),("PhienBanDuLieu",detail["ca"]!.Text("phienBanDuLieu")),("TienMatThucDem",499000));
        (await api.Send("POST","/api/pos/shifts/current/close",close,staff)).Expect(409,"Thiếu tiền không được kết ca");
        Assert.Equal("OPEN",await api.Sql($"SELECT TrangThai FROM dbo.CaLamViec WHERE MaCaLamViec={id}"));
        close["TienMatThucDem"]=500000;
        var result = (await api.Send("POST","/api/pos/shifts/current/close",close,staff)).Expect(200,"Gửi kết quả và đóng").Json;
        Assert.Equal("CLOSED",result.Text("trangThai"));
        Assert.Equal(0,result["ketQuaNghiemThu"]!.Number("tienThuDuoc"));
        Assert.Equal(500000,result["ketQuaNghiemThu"]!.Number("tienCuoiCa"));
        Assert.Equal(0,result["ketQuaNghiemThu"]!.Number("chenhLech"));
        Assert.False(string.IsNullOrWhiteSpace(result.Text("ketThucLuc")));
        var replay = (await api.Send("POST","/api/pos/shifts/current/close",close,staff)).Expect(200,"Gửi lại cùng báo cáo").Json;
        Assert.Equal(result.ToJsonString(),replay.ToJsonString());
        Assert.Equal("1",await api.Sql($"SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE MaCaLamViec={id} AND HanhDong='CLOSE_SHIFT'"));
        Assert.Equal("0",await api.Sql($"SELECT JSON_VALUE(KetQuaDaChotJson,'$.ketQuaNghiemThu.chenhLech') FROM dbo.NhatKyHeThong WHERE MaCaLamViec={id} AND HanhDong='CLOSE_SHIFT'"));
        var received = (await api.Send("GET",$"/api/admin/work-sessions/{id}",headers:admin)).Expect(200,"Quản lý nhận kết quả").Json;
        Assert.Equal("CLOSED",received["ca"]!.Text("trangThai"));
        Assert.Equal(500000,received["ca"]!.Number("tienMatThucDem"));
        Assert.Equal(0,received.Number("tienThuDuoc"));
        Assert.False(received.Flag("dangChoDoiChieu"));
        Assert.True((await api.Send("GET","/api/pos/data/DonHang",headers:staff)).Status is 401 or 403);
        open["TenCa"]="Chiều";
        var next = (await api.Send("POST","/api/pos/shifts",open)).Expect(200,"Mở ca khác ngay, không chờ Admin chốt").Json;
        var nextId = next.Number("shiftId"); var nextStaff = TestApi.Bearer(next);

        // Ép lỗi audit chỉ trong database fixture: đóng ca không được thành công một phần.
        await api.Sql("CREATE TRIGGER dbo.TestFailFinishShift ON dbo.NhatKyHeThong AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE HanhDong='CLOSE_SHIFT') THROW 51011, 'Test audit rollback', 1; END");
        var nextClose = Body(("MaThaoTac",NewId()),("TienMatThucDem",500000));
        Assert.True((await api.Send("POST","/api/pos/shifts/current/close",nextClose,nextStaff)).Status>=400);
        Assert.Equal("OPEN",await api.Sql($"SELECT TrangThai FROM dbo.CaLamViec WHERE MaCaLamViec={nextId}"));
        Assert.Equal("0",await api.Sql($"SELECT COUNT(*) FROM dbo.CaLamViec WHERE MaCaLamViec={nextId} AND (KetThucLuc IS NOT NULL OR TienMatThucDem IS NOT NULL)"));
        await api.Sql("DROP TRIGGER dbo.TestFailFinishShift");
        nextClose["TienMatThucDem"]=750000;
        (await api.Send("POST","/api/pos/shifts/current/close",nextClose,nextStaff)).Expect(409,"Dư tiền cũng không tự chốt");
        nextClose["TienMatThucDem"]=500000;
        var closed=(await api.Send("POST","/api/pos/shifts/current/close",nextClose,nextStaff)).Expect(200,"Kết ca không cần lý do").Json;
        Assert.Equal(0,closed["ketQuaNghiemThu"]!.Number("tienThuDuoc"));
        Assert.Equal(0,closed["ketQuaNghiemThu"]!.Number("thuTienMat"));
        // Không đóng khi còn đơn pha chế hoặc giao dịch chưa rõ; vẫn giữ dữ liệu để bàn giao có kiểm soát.
        open["TenCa"]="Tối";
        var evening = (await api.Send("POST","/api/pos/shifts",open)).Expect(200,"Mở ca Tối").Json;
        var eveningId = evening.Number("shiftId");
        var eveningStaff = TestApi.Bearer(evening);
        await api.Sql($"INSERT dbo.DonHang(MaDonHienThi,MaCaLamViec,KenhDatHang,HinhThucNhanMon,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai) VALUES('FINISH-PENDING',{eveningId},'POS','TAKEAWAY',0,0,'PREPARING')");
        (await api.Send("POST","/api/pos/shifts/current/close",Body(("MaThaoTac",NewId()),("TienMatThucDem",500000)),eveningStaff)).Expect(409,"Đơn dở không được đóng mất nguồn");
        Assert.Equal("OPEN",await api.Sql($"SELECT TrangThai FROM dbo.CaLamViec WHERE MaCaLamViec={eveningId}"));
    }
}
