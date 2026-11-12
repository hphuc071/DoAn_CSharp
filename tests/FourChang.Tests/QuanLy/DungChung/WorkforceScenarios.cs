using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Admin;

/// <summary>Dữ liệu cô lập dùng chung ADM-11/12; gọi API thật, không tạo tài khoản hoặc đăng ký máy POS.</summary>
internal static class WorkforceScenarios
{
    /// <summary>Tạo payload hồ sơ có mã chống trùng và địa chỉ tự nhiên, không dùng dữ liệu cá nhân thật.</summary>
    internal static JsonObject EmployeeInput(string? name = null) => Body(("HoTen", name ?? "Nguyễn Minh An " + Guid.NewGuid().ToString("N")[..6]),
        ("SoDienThoai", Phone()), ("DiaChiLienHe", "12 Đường Hoa Mai, Thành phố Hồ Chí Minh"),
        ("NgayVaoLam", "2026-11-01"), ("TrangThai", "ACTIVE"), ("MaThaoTac", NewId()));

    /// <summary>Mở ca qua luồng không thiết bị/mật khẩu; giữ token trong bộ nhớ của test.</summary>
    internal static async Task<(JsonNode Employee, JsonNode Session, Dictionary<string, string> Headers)> Open(AdminFeatureFixture f, int cash = 500000)
    {
        var employee = await f.Send("POST", "/api/admin/employees", EmployeeInput());
        var session = (await f.Api.Send("POST", "/api/pos/shifts", Body(("MaNhanVien", employee.Number("maNhanVien")), ("HoTen", employee.Text("hoTen")),
            ("TenCa", "Sáng"), ("MaMay", "POS-" + Guid.NewGuid().ToString("N")[..12]), ("TienMatDauCa", cash)))).Expect(200, "Mở ca không có đăng nhập POS").Json;
        return (employee, session, TestApi.Bearer(session));
    }

    /// <summary>Đọc phiên bản và mốc đối chiếu mới nhất từ máy chủ.</summary>
    internal static Task<JsonNode> Detail(AdminFeatureFixture f, long shift) => f.Send("GET", "/api/admin/work-sessions/" + shift);

    /// <summary>Gửi thực đếm từ phiên nhân viên, chưa đóng ca và chưa thay đổi nguồn tài chính.</summary>
    internal static async Task<JsonNode> Submit(AdminFeatureFixture f, long shift, Dictionary<string, string> headers, decimal counted = 500000)
    {
        var detail = await Detail(f, shift);
        var input = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", detail["ca"]!.Text("phienBanDuLieu")),
            ("TienMatThucDem", counted), ("LyDoChenhLech", "Kiểm đếm theo nguồn giao dịch"));
        (await f.Api.Send("POST", "/api/pos/shifts/current/reconciliation", input, headers)).Expect(200, "Gửi đối chiếu");
        return await Detail(f, shift);
    }

    /// <summary>Payload xác nhận giữ nguyên mốc Admin vừa xem, không tự đoán số tiền ở client.</summary>
    internal static JsonObject CloseInput(JsonNode detail) => Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", detail["ca"]!.Text("phienBanDuLieu")), ("MaDoiChieu", detail.Text("maDoiChieu")));
}
