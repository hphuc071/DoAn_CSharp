using FourChang.Api.Shared.Operations;
using System.Text.Json;
using System.Text.RegularExpressions;
using FourChang.Api.Shared.WorkSessions;
using FourChang.Domain.Entities.Employees;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Staff;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Employees;

/// <summary>UC-ADM-11: quản lý hồ sơ; tuyệt đối không sinh tài khoản hoặc mã hiển thị nhân viên riêng.</summary>
public sealed class EmployeeService(CafeDbContext db)
{
    /// <summary>Ánh xạ đúng các trường công khai của hồ sơ; khóa nội bộ được dùng phân biệt người trùng tên.</summary>
    public static Employee View(NhanVien item) => new(item.MaNhanVien, item.HoTen, item.SoDienThoai,
        item.NgayVaoLam, item.TrangThai, item.PhienBanDuLieu, item.DiaChiLienHe);

    /// <summary>Tìm tiếng Việt không phân biệt dấu, lọc trạng thái và phân trang tại SQL với thứ tự ổn định.</summary>
    public async Task<IResult> Search(string? search, string? status, int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue ||
            search?.Length > 120 || status is not (null or "" or "ACTIVE" or "INACTIVE"))
            return Results.Problem(statusCode: 400, detail: "Bộ lọc hoặc phân trang không hợp lệ.");
        var query = db.NhanVien.AsNoTracking();
        var term = search?.Trim() ?? "";
        if (term.Length > 0) query = query.Where(x => EF.Functions.Collate(x.HoTen, "Vietnamese_100_CI_AI").Contains(term) ||
            x.SoDienThoai != null && x.SoDienThoai.Contains(term));
        if (!string.IsNullOrEmpty(status)) query = query.Where(x => x.TrangThai == status);
        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.MaNhanVien).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Results.Ok(new EmployeePage(items.Select(View).ToList(), page, pageSize, total));
    }

    /// <summary>Đọc lại hồ sơ trước khi xem/sửa để lấy phiên bản mới nhất.</summary>
    public async Task<IResult> Details(long id)
    {
        var item = await db.NhanVien.AsNoTracking().SingleOrDefaultAsync(x => x.MaNhanVien == id);
        return item is null ? Results.NotFound() : Results.Ok(View(item));
    }

    /// <summary>Tạo/sửa nguyên tử cùng audit và receipt; tên, điện thoại không bị áp đặt UNIQUE.</summary>
    public Task<IResult> Save(long? id, EmployeeInput input, HttpContext context) => AdminOperation.Execute(db,
        input.MaThaoTac, id is null ? "EMPLOYEE_CREATED" : "EMPLOYEE_UPDATED", "NhanVien", new { id, input }, context, async () =>
    {
        Validate(input);
        var item = id is null ? new NhanVien() : await db.NhanVien.SingleOrDefaultAsync(x => x.MaNhanVien == id)
            ?? throw new AdminRule(404, "Hồ sơ không còn tồn tại.");
        var before = id is null ? null : JsonSerializer.Serialize(View(item));
        if (id is not null)
        {
            AdminOperation.Version(input.PhienBanDuLieu, item.PhienBanDuLieu);
            if (input.TrangThai != item.TrangThai) { AdminOperation.Reason(input.LyDo); if (input.TrangThai == "INACTIVE") await CanChangeStatus(item, input.LyDo); }
        }
        else if (input.TrangThai != "ACTIVE") throw new AdminRule(400, "Hồ sơ mới phải ở trạng thái đang làm.");
        item.HoTen = input.HoTen.Trim(); item.SoDienThoai = string.IsNullOrWhiteSpace(input.SoDienThoai) ? null : input.SoDienThoai.Trim();
        item.DiaChiLienHe = string.IsNullOrWhiteSpace(input.DiaChiLienHe) ? null : input.DiaChiLienHe.Trim();
        item.NgayVaoLam = input.NgayVaoLam.Date; item.TrangThai = input.TrangThai; item.ThoiDiemCapNhat = DateTime.UtcNow;
        if (id is null) db.NhanVien.Add(item);
        await db.SaveChangesAsync();
        // Chỉ sửa hồ sơ gốc, không sửa snapshot OPEN_SHIFT hoặc giao dịch cũ.
        return (item.MaNhanVien, (object)View(item), before, input.LyDo?.Trim());
    });

    /// <summary>Ngừng/kích hoạt lại hồ sơ có lý do; ca cũ giữ nguyên và không tự mở lại.</summary>
    public Task<IResult> Status(long id, EmployeeStatusInput input, HttpContext context) => AdminOperation.Execute(db,
        input.MaThaoTac, "EMPLOYEE_STATUS", "NhanVien", new { id, input }, context, async () =>
    {

        if (input.TrangThai is not ("ACTIVE" or "INACTIVE")) throw new AdminRule(400, "Trạng thái không hợp lệ.");
        var item = await db.NhanVien.SingleOrDefaultAsync(x => x.MaNhanVien == id) ?? throw new AdminRule(404, "Không tìm thấy hồ sơ.");
        AdminOperation.Version(input.PhienBanDuLieu, item.PhienBanDuLieu);
        AdminOperation.Reason(input.LyDo);
        if (input.TrangThai == "INACTIVE") await CanChangeStatus(item, input.LyDo);
        var before = JsonSerializer.Serialize(View(item));
        item.TrangThai = input.TrangThai; item.ThoiDiemCapNhat = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (id, (object)View(item), before, input.LyDo.Trim());
    });

    /// <summary>Chặn ngừng khi còn ca và khi đang được chỉ định nhận bàn giao chưa mở ca.</summary>
    private async Task CanChangeStatus(NhanVien item, string? reason)
    {
        AdminOperation.Reason(reason);
        if (await db.CaLamViec.AnyAsync(x => x.MaNhanVien == item.MaNhanVien && x.KetThucLuc == null))
            throw new AdminRule(409, "Nhân viên còn ca chưa kết thúc. Mở UC-ADM-12 để chốt hoặc bàn giao trước.");
        if (await HandoverLink.HasPendingReceiver(db, item.MaNhanVien))
            throw new AdminRule(409, "Nhân viên đang nhận bàn giao. Cần tiếp nhận và xử lý ca trước khi đổi trạng thái.");
    }

    /// <summary>Xóa có điều kiện dưới cùng khóa với mở/bàn giao ca; FK và audit vẫn nằm trong giao dịch.</summary>
    public Task<IResult> Delete(long id, DeleteEmployeeInput input, HttpContext context) => AdminOperation.Execute(db,
        input.MaThaoTac, "EMPLOYEE_DELETED", "NhanVien", new { id, input }, context, async () =>
    {
        var item = await db.NhanVien.SingleOrDefaultAsync(x => x.MaNhanVien == id)
            ?? throw new AdminRule(404, "Không tìm thấy hồ sơ.");
        AdminOperation.Version(input.PhienBanDuLieu, item.PhienBanDuLieu);
        if (await db.CaLamViec.AnyAsync(x => x.MaNhanVien == id) || await HandoverLink.HasPendingReceiver(db, id))
            throw new AdminRule(409, "Nhân viên đã có lịch sử ca hoặc bàn giao. Dùng Ngừng làm để giữ lịch sử.");
        var before = JsonSerializer.Serialize(View(item));
        db.NhanVien.Remove(item);
        await db.SaveChangesAsync();
        return (id, (object)new { maNhanVien = id, daXoa = true }, before, "Xóa hồ sơ chưa có tham chiếu");
    });

    /// <summary>Kiểm tra trường bắt buộc và định dạng trước SQL; ngày chưa nhập không được đổi thành ngày mặc định.</summary>
    private static void Validate(EmployeeInput input)
    {
        if (string.IsNullOrWhiteSpace(input.HoTen) || input.HoTen.Trim().Length > 120)
            throw new AdminRule(400, "Họ tên bắt buộc, tối đa 120 ký tự.");
        if (!string.IsNullOrWhiteSpace(input.SoDienThoai) && !Regex.IsMatch(input.SoDienThoai.Trim(), @"^\+?[0-9]{8,15}$"))
            throw new AdminRule(400, "Số điện thoại gồm 8–15 chữ số, có thể bắt đầu bằng +.");
        if (input.NgayVaoLam == default)
            throw new AdminRule(400, "Ngày vào làm bắt buộc và phải hợp lệ.");
        if (input.DiaChiLienHe?.Trim().Length > 500) throw new AdminRule(400, "Địa chỉ liên hệ tối đa 500 ký tự.");
        if (input.TrangThai is not ("ACTIVE" or "INACTIVE")) throw new AdminRule(400, "Trạng thái không hợp lệ.");
    }
}
