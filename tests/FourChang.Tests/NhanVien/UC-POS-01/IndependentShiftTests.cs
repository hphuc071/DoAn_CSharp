using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Staff;

/// <summary>Một nhân viên làm nhiều ca khác tên trong ngày; mỗi ca giữ tiền và lịch sử riêng.</summary>
public sealed class IndependentShiftTests
{
    /// <summary>Mở nhiều ca cùng nhân viên/máy, kể cả ca trước đã khóa; đóng ca này không sửa ca kia.</summary>
    [Fact, Trait("Feature", "IndependentShifts"), Trait("Layer", "API")]
    public async Task NhieuCaDocLap_KhongChanCaCu_VaKhongGopTien()
    {
        await using var api = await TestApi.Start("Independent-Shifts");
        var admin = await api.AdminLogin();
        var employee = await api.Employee(admin, "Nguyễn Minh Phúc");
        var input = Body(("MaNhanVien", employee.Number("maNhanVien")), ("HoTen", employee.Text("hoTen")),
            ("TenCa", "Sáng"), ("MaMay", "SHARED-POS"), ("TienMatDauCa", 500000), ("MaThaoTac", NewId()));
        var first = (await api.Send("POST", "/api/pos/shifts", input)).Expect(200, "Mở ca đầu").Json;
        var firstId = first.Number("shiftId");
        input["MaThaoTac"] = NewId();
        input["TienMatDauCa"] = 200000;
        (await api.Send("POST", "/api/pos/shifts", input)).Expect(409, "Không lặp ca Sáng dù đổi mã thao tác");
        input["TenCa"] = "Chiều";
        var second = (await api.Send("POST", "/api/pos/shifts", input)).Expect(200, "Mở ca Chiều khi ca Sáng còn mở").Json;
        var secondId = second.Number("shiftId");
        Assert.NotEqual(firstId, secondId);
        await api.Sql($"UPDATE dbo.CaLamViec SET TrangThai='LOCKED' WHERE MaCaLamViec={firstId}");
        input["MaThaoTac"] = NewId();
        input["TenCa"] = "Tối";
        var third = (await api.Send("POST", "/api/pos/shifts", input)).Expect(200, "Ca cũ khóa không chặn mở mới").Json;
        Assert.NotEqual(secondId, third.Number("shiftId"));
        var another = await api.Employee(admin, "Trần Ngọc Anh");
        input["MaNhanVien"] = another.Number("maNhanVien");
        input["HoTen"] = another.Text("hoTen");
        input["MaThaoTac"] = NewId();
        (await api.Send("POST", "/api/pos/shifts", input)).Expect(200, "Nhân viên khác mở trên cùng máy");
        (await api.Send("POST", "/api/pos/shifts/current/close",
            Body(("MaThaoTac", NewId()), ("TienMatThucDem", 200000)), TestApi.Bearer(second))).Expect(200, "Đóng riêng ca thứ hai");
        Assert.Equal("LOCKED", await api.Sql($"SELECT TrangThai FROM dbo.CaLamViec WHERE MaCaLamViec={firstId}"));
        Assert.Equal("500000", await api.Sql($"SELECT CONVERT(bigint,TienMatDauCa) FROM dbo.CaLamViec WHERE MaCaLamViec={firstId}"));
        Assert.Equal("OPEN", await api.Sql($"SELECT TrangThai FROM dbo.CaLamViec WHERE MaCaLamViec={third.Number("shiftId")}"));
        Assert.Equal("0", await api.Sql("SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.CaLamViec') AND name IN ('UX_Ca_MayDangMo','UX_Ca_NhanVienDangMo')"));
        Assert.Equal("2", await api.Sql("SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.CaLamViec') AND name IN ('IX_Ca_MayDangMo','IX_Ca_NhanVienDangMo') AND is_unique=0"));
    }

    /// <summary>Gửi lặp cùng mã trả đúng ca; hai thao tác khác mã chọn cùng ca chỉ một yêu cầu được mở.</summary>
    [Fact, Trait("Feature", "IndependentShifts"), Trait("Layer", "API")]
    public async Task MoCaDongThoi_ChongTrungThaoTac_KhongChanCaMoi()
    {
        await using var api = await TestApi.Start("Independent-Shifts-Retry");
        var admin = await api.AdminLogin();
        var employee = await api.Employee(admin, "Lê Hoàng Thiện");
        var input = Body(("MaNhanVien", employee.Number("maNhanVien")), ("HoTen", employee.Text("hoTen")),
            ("TenCa", "Chiều"), ("MaMay", "RETRY-POS"), ("TienMatDauCa", 0), ("MaThaoTac", NewId()));
        var duplicate = await Task.WhenAll(api.Send("POST", "/api/pos/shifts", input), api.Send("POST", "/api/pos/shifts", input));
        var id = duplicate[0].Expect(200, "Yêu cầu đầu").Json.Number("shiftId");
        Assert.Equal(id, duplicate[1].Expect(200, "Lặp cùng thao tác").Json.Number("shiftId"));
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE MaCaLamViec={id} AND HanhDong='OPEN_SHIFT'"));
        input["TienMatDauCa"] = 1000;
        (await api.Send("POST", "/api/pos/shifts", input)).Expect(409, "Không đổi nội dung cùng mã");
        input["MaThaoTac"] = NewId();
        (await api.Send("POST", "/api/pos/shifts", input)).Expect(409, "Ca Chiều đã làm không mở lần hai");
        input["TenCa"] = "Tối";
        var other = input.Copy();
        other["MaThaoTac"] = NewId();
        var independent = await Task.WhenAll(api.Send("POST", "/api/pos/shifts", input), api.Send("POST", "/api/pos/shifts", other));
        Assert.Single(independent, x => x.Status == 200);
        Assert.Single(independent, x => x.Status == 409);
    }
    /// <summary>Ca đã đóng vẫn chiếm tên ca hôm nay; ngày UTC+07 mới cho phép chọn lại, nhân viên khác không bị ảnh hưởng.</summary>
    [Fact, Trait("Feature", "IndependentShifts"), Trait("Layer", "API")]
    public async Task CaDaDongKhongLapTrongNgay_NgayMoiDuocChonLai()
    {
        await using var api = await TestApi.Start("Daily-Shifts");
        var admin = await api.AdminLogin();
        var employee = await api.Employee(admin, "Lê Minh Anh");
        var input = Body(("MaNhanVien", employee.Number("maNhanVien")), ("HoTen", employee.Text("hoTen")),
            ("TenCa", "Sáng"), ("MaMay", "DAILY-A"), ("TienMatDauCa", 0), ("MaThaoTac", NewId()));
        var opened = (await api.Send("POST", "/api/pos/shifts", input)).Expect(200, "Chọn Sáng bất kể giờ thực tế").Json;
        var id = opened.Number("shiftId");
        (await api.Send("POST", "/api/pos/shifts/current/close", Body(("MaThaoTac", NewId()), ("TienMatThucDem", 0)), TestApi.Bearer(opened))).Expect(200, "Kết ca");
        input["MaThaoTac"] = NewId(); input["MaMay"] = "DAILY-B";
        (await api.Send("POST", "/api/pos/shifts", input)).Expect(409, "Đổi máy/đóng ca không cho lặp Sáng");
        var verified = (await api.Send("POST", "/api/pos/employees/verify", Body(("MaNhanVien", employee.Number("maNhanVien")), ("HoTen", employee.Text("hoTen"))))).Expect(200, "Ca đã làm trong ngày").Json;
        Assert.Equal("Sáng", Assert.Single(verified["caDaLamTrongNgay"]!.AsArray())!.GetValue<string>());
        // Chỉ thay thời điểm của dữ liệu test: trước ranh giới 00:00 địa phương đúng một giây.
        var previous = DateTime.UtcNow.AddHours(7).Date.AddHours(-7).AddSeconds(-1);
        await api.Sql($"UPDATE dbo.CaLamViec SET BatDauLuc='{previous:yyyy-MM-ddTHH:mm:ss}', KetThucLuc='{previous.AddSeconds(1):yyyy-MM-ddTHH:mm:ss}' WHERE MaCaLamViec={id}");
        (await api.Send("POST", "/api/pos/shifts", input)).Expect(200, "Ca ngày trước không chặn ngày mới");
    }}
