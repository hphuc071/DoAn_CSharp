using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Admin;

/// <summary>Các biên đồng thời và phục hồi không được xác minh bằng mã máy hoặc họ tên đơn thuần.</summary>
[Collection("AdminFeatures"), Trait("UseCase", "UC-ADM-12"), Trait("Actor", "QuanLy")]
public sealed class WorkSessionConcurrencyTests(AdminFeatureFixture fixture)
{
    /// <summary>Mở ca và ngừng hồ sơ cùng lúc chỉ một bên thành công, không để INACTIVE có ca mới.</summary>
    [Fact]
    public async Task MoCa_DuaVoiNgungNhanVien_KhongDeHoSoNgungCoCaMoi()
    {
        var employee = await fixture.Send("POST", "/api/admin/employees", WorkforceScenarios.EmployeeInput());
        var id = employee.Number("maNhanVien");
        var open = fixture.Api.Send("POST", "/api/pos/shifts", Body(("MaNhanVien", id), ("HoTen", employee.Text("hoTen")), ("TenCa", "Tối"), ("MaMay", "RACE-" + Guid.NewGuid().ToString("N")[..10]), ("TienMatDauCa", 0)));
        var stop = fixture.Api.Send("POST", $"/api/admin/employees/{id}/status", Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", employee.Text("phienBanDuLieu")), ("TrangThai", "INACTIVE"), ("LyDo", "Kiểm tra đồng thời")), fixture.Admin);
        var responses = await Task.WhenAll(open, stop);
        Assert.Equal(1, responses.Count(x => x.Status == 200));
        var state = await fixture.Api.Sql($"SELECT TrangThai + ':' + CAST((SELECT COUNT(*) FROM dbo.CaLamViec WHERE MaNhanVien={id} AND KetThucLuc IS NULL) AS varchar) FROM dbo.NhanVien WHERE MaNhanVien={id}");
        Assert.Contains(state, new[] { "ACTIVE:1", "INACTIVE:0" });
    }

    /// <summary>Hai ca bàn giao cùng đích không chiếm người nhận hai lần, cả hai dùng cùng khóa server.</summary>
    [Fact]
    public async Task HaiCa_BanGiaoCungNguoiNhan_ChiMotBenThanhCong()
    {
        var first = await WorkforceScenarios.Open(fixture);
        var second = await WorkforceScenarios.Open(fixture);
        var receiver = await fixture.Send("POST", "/api/admin/employees", WorkforceScenarios.EmployeeInput());
        var a = await WorkforceScenarios.Submit(fixture, first.Session.Number("shiftId"), first.Headers);
        var b = await WorkforceScenarios.Submit(fixture, second.Session.Number("shiftId"), second.Headers);
        var machine = "BG-" + Guid.NewGuid().ToString("N")[..10];
        JsonObject Input(JsonNode detail)
        {
            var input = WorkforceScenarios.CloseInput(detail);
            input["MaNhanVienNhan"] = receiver.Number("maNhanVien"); input["MaMayNhan"] = machine; input["LyDo"] = "Bàn giao cuối ca";
            input["MaDonBanGiao"] = new JsonArray(); input["KhoanChuaRo"] = new JsonArray(); return input;
        }
        var responses = await Task.WhenAll(
            fixture.Api.Send("POST", $"/api/admin/work-sessions/{first.Session.Number("shiftId")}/handover", Input(a), fixture.Admin),
            fixture.Api.Send("POST", $"/api/admin/work-sessions/{second.Session.Number("shiftId")}/handover", Input(b), fixture.Admin));
        Assert.Equal(new[] { 200, 409 }, responses.Select(x => x.Status).Order().ToArray());
    }

    /// <summary>Cấp lại vô hiệu mã cũ, đổi mã chỉ một lần; mã đã quá hạn không được đổi dù biết đúng hồ sơ.</summary>
    [Fact]
    public async Task MaPhucHoi_CapLai_VaHetHan_KhongTaiSuDung()
    {
        var s = await WorkforceScenarios.Open(fixture);
        var id = s.Session.Number("shiftId");
        async Task<JsonNode> Issue()
        {
            var detail = await WorkforceScenarios.Detail(fixture, id);
            return await fixture.Send("POST", $"/api/admin/work-sessions/{id}/recovery", Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", detail["ca"]!.Text("phienBanDuLieu")), ("LyDo", "Cấp lại sau sự cố")));
        }
        var first = await Issue(); var second = await Issue();
        JsonObject Redeem(string code) => Body(("MaCaLamViec", id), ("MaNhanVien", s.Employee.Number("maNhanVien")), ("HoTen", s.Employee.Text("hoTen")), ("MaPhucHoi", code));
        (await fixture.Api.Send("POST", "/api/pos/shifts/recovery", Redeem(first.Text("maPhucHoi")))).Expect(409, "Mã cũ bị thay thế");
        var recovered = (await fixture.Api.Send("POST", "/api/pos/shifts/recovery", Redeem(second.Text("maPhucHoi")))).Expect(200, "Đổi mã mới").Json;
        await Issue();
        (await fixture.Api.Send("GET", "/api/pos/shifts/current/reconciliation", headers: TestApi.Bearer(recovered))).Expect(401, "Phiên cũ bị thay thế");
        // Mô phỏng biên bản đã hết hạn trong fixture riêng; không sửa/xóa bản ghi audit bất biến.
        var expiredCode = "PH-" + new string('A', 48);
        var ticket = JsonSerializer.Serialize(new { id = Guid.NewGuid(), shift = id, employee = s.Employee.Number("maNhanVien"), hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expiredCode))), expires = DateTime.UtcNow.AddMinutes(-1) });
        var admin = await fixture.Api.Sql("SELECT TOP(1) MaQuanLy FROM dbo.TaiKhoanQuanLy ORDER BY MaQuanLy");
        await fixture.Api.Sql($"INSERT dbo.NhatKyHeThong(MaLienKetXuLy,LoaiTacNhan,MaQuanLy,TenNguoiThucHienDaChot,HanhDong,LoaiDoiTuong,MaDoiTuong,DuLieuSauJson,KetQua,PhatSinhLuc) VALUES(NEWID(),'ADMIN',{admin},N'Kiểm thử hết hạn','SHIFT_RECOVERY_ISSUED','CaLamViec','{id}',N'{ticket}','SUCCEEDED',SYSUTCDATETIME());");
        (await fixture.Api.Send("POST", "/api/pos/shifts/recovery", Redeem(expiredCode))).Expect(409, "Mã hết hạn");
    }

    /// <summary>Khai báo lại mã/tên/máy không đủ để lấy token ca đang mở; thực đếm 0 được chấp nhận khi gửi rõ.</summary>
    [Fact]
    public async Task KhongPhucHoiBangMaMay_ThucDemKhongKhacThieu()
    {
        var s = await WorkforceScenarios.Open(fixture, 0);
        var id = s.Session.Number("shiftId");
        var detail = await WorkforceScenarios.Detail(fixture, id);
        var machine = detail["ca"]!.Text("maMay");
        (await fixture.Api.Send("POST", "/api/pos/shifts", Body(("MaNhanVien", s.Employee.Number("maNhanVien")), ("HoTen", s.Employee.Text("hoTen")), ("MaMay", machine), ("TenCa", "Sáng"), ("TienMatDauCa", 0)))).Expect(409, "Chỉ mã máy/tên không đủ");
        var pending = await WorkforceScenarios.Submit(fixture, id, s.Headers, 0);
        Assert.Equal(0m, pending["ca"]!["tienMatThucDem"]!.GetValue<decimal>());
        await fixture.Send("POST", $"/api/admin/work-sessions/{id}/close", WorkforceScenarios.CloseInput(pending));
    }
}
