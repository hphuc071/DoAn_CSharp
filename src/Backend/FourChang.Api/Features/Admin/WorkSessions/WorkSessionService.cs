using System.Text.Json;
using FourChang.Api.Shared.WorkSessions;
using FourChang.Domain.Entities.Employees;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.WorkSessions;

/// <summary>UC-ADM-13: đọc nguồn tiền thật, giám sát và xác nhận ca; không sửa thu/hoàn để ép khớp quỹ.</summary>
public sealed class WorkSessionService(CafeDbContext db)
{
    /// <summary>Đọc snapshot tên và thông tin ca bằng cùng hợp đồng với POS.</summary>
    private Task<WorkSessionInfo> View(CaLamViec shift) => new ShiftReconciliation(db).View(shift);

    /// <summary>Lọc theo khóa nhân viên, ngày UTC, tên ca, trạng thái, máy và phân trang ổn định.</summary>
    public async Task<IResult> Search(long? employee, DateTime? from, DateTime? to, string? name, string? status, string? machine, int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue ||
            employee <= 0 || from > to || machine?.Length > 50 || name is not (null or "" or "Sáng" or "Chiều" or "Tối") ||
            status is not (null or "" or "OPEN" or "LOCKED" or "CLOSED" or "HANDED_OVER"))
            return Results.Problem(statusCode: 400, detail: "Khoảng ngày hoặc bộ lọc ca không hợp lệ.");
        var query = db.CaLamViec.AsNoTracking();
        if (employee is not null) query = query.Where(x => x.MaNhanVien == employee);
        if (from is not null) query = query.Where(x => x.BatDauLuc >= from.Value.ToUniversalTime());
        if (to is not null) query = query.Where(x => x.BatDauLuc <= to.Value.ToUniversalTime());
        if (!string.IsNullOrEmpty(name)) query = query.Where(x => x.TenCa == name);
        if (!string.IsNullOrEmpty(status)) query = query.Where(x => x.TrangThai == status);
        if (!string.IsNullOrWhiteSpace(machine)) query = query.Where(x => x.MaMay == machine.Trim());
        var total = await query.CountAsync();
        var shifts = await query.OrderByDescending(x => x.MaCaLamViec).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var rows = new List<WorkSessionInfo>();
        foreach (var shift in shifts) rows.Add(await View(shift));
        return Results.Ok(new WorkSessionPage(rows, page, pageSize, total));
    }

    /// <summary>Đọc đối chiếu qua bộ đọc dùng chung với POS để hai màn hình không tính lệch số liệu.</summary>
    public Task<IResult> Details(long id) => new ShiftReconciliation(db).Details(id);

    /// <summary>Giữ điểm gọi nội bộ để kiểm tra nguồn trong chính giao dịch chốt/bàn giao.</summary>
    public Task<WorkSessionDetails> Detail(CaLamViec shift) => new ShiftReconciliation(db).Detail(shift);

    /// <summary>Chốt từ số thực đếm POS đã gửi; kiểm tra lại nguồn tiền ngay trong giao dịch, không chốt lần hai.</summary>
    public Task<IResult> Close(long id, CloseWorkSessionInput input, HttpContext context) => WorkforceOperation.Execute(db,
        input.MaThaoTac, "SHIFT_ADMIN_CLOSED", "CaLamViec", new { id, input }, context, async () =>
    {
        var shift = await Editable(id, input.PhienBanDuLieu);
        var detail = await Detail(shift);
        ValidateCount(shift, detail.TienMatDuKienHienTai);
        if (input.TienMatDuKienDaXem is null || input.TienMatDuKienDaXem != detail.TienMatDuKienHienTai)
            throw new WorkforceRule(409, "Nguồn tiền chưa được xác nhận hoặc đã thay đổi. Tải lại đối chiếu trước khi xác nhận.");
        if (detail.DonDangXuLy.Count > 0 || detail.KhoanChuaRo.Count > 0)
            throw new WorkforceRule(409, "Ca còn đơn dở hoặc khoản chưa rõ. Xử lý tại đúng nghiệp vụ hoặc bàn giao có liên kết trước khi chốt.");
        var before = JsonSerializer.Serialize(detail.Ca);
        Finish(shift, "CLOSED", detail.TienMatDuKienHienTai, context);
        await db.SaveChangesAsync();
        return (id, (object)await View(shift), before, shift.LyDoChenhLech);
    });

    /// <summary>Bàn giao có biên bản giữ nguyên chủ ca/giao dịch nguồn; ca nhận chỉ được tạo khi nhân viên mở POS.</summary>
    public Task<IResult> Handover(long id, HandoverWorkSessionInput input, HttpContext context) => WorkforceOperation.Execute(db,
        input.MaThaoTac, "SHIFT_HANDED_OVER", "CaLamViec", new { id, input }, context, async () =>
    {
        WorkforceOperation.Reason(input.LyDo);
        var shift = await Editable(id, input.PhienBanDuLieu);
        var machine = FourChang.Api.Features.Admin.Access.PosDeviceService.Machine(input.MaMayNhan);
        if (!await db.NhanVien.AnyAsync(x => x.MaNhanVien == input.MaNhanVienNhan && x.TrangThai == "ACTIVE") ||
            !await db.ThietBiPOS.AnyAsync(x => x.MaMay == machine && x.TrangThai == "ACTIVE"))
            throw new WorkforceRule(400, "Nhân viên nhận phải đang làm và máy nhận phải được cấp phép.");
        if (shift.MaNhanVien == input.MaNhanVienNhan && shift.MaMay == machine)
            throw new WorkforceRule(400, "Bàn giao cần đổi người hoặc máy.");
        if (await db.CaLamViec.AnyAsync(x => x.MaCaLamViec != id && x.KetThucLuc == null && (x.MaNhanVien == input.MaNhanVienNhan || x.MaMay == machine)) ||
            (await HandoverLink.Pending(db)).Any(x => x.MaNhanVienNhan == input.MaNhanVienNhan || x.MaMayNhan == machine))
            throw new WorkforceRule(409, "Nhân viên hoặc máy nhận có ca/bàn giao đang chờ.");
        var detail = await Detail(shift);
        ValidateCount(shift, detail.TienMatDuKienHienTai);
        if (input.TienMatDuKienDaXem is null || input.TienMatDuKienDaXem != detail.TienMatDuKienHienTai)
            throw new WorkforceRule(409, "Nguồn tiền chưa được xác nhận hoặc đã thay đổi. Tải lại đối chiếu trước khi xác nhận.");
        // Danh sách Admin đã xác nhận phải khớp nguồn hiện tại, không chấp nhận bỏ sót hoặc thêm mã giả.
        if (input.MaDonBanGiao is null || input.KhoanChuaRo is null ||
            !input.MaDonBanGiao.Order().SequenceEqual(detail.DonDangXuLy.Select(x => x.MaDonHang).Order()) ||
            !input.KhoanChuaRo.Order().SequenceEqual(detail.KhoanChuaRo.Order()))
            throw new WorkforceRule(409, "Danh sách đơn/khoản chưa rõ đã thay đổi. Tải lại để xác nhận bàn giao.");
        var before = JsonSerializer.Serialize(detail.Ca);
        Finish(shift, "HANDED_OVER", detail.TienMatDuKienHienTai, context);
        shift.GhiChu = input.LyDo.Trim();
        await db.SaveChangesAsync();
        var result = new HandoverInfo(id, input.MaNhanVienNhan, machine, input.LyDo.Trim(), input.MaDonBanGiao, input.KhoanChuaRo);
        return (id, (object)result, before, input.LyDo.Trim());
    });


    /// <summary>A4: cho tiếp tục ca khóa trên thiết bị hợp lệ; số đếm cũ giữ trong audit, nhân viên phải kiểm đếm lại trước chốt.</summary>
    public Task<IResult> Resume(long id, ResumeWorkSessionInput input, HttpContext context) => WorkforceOperation.Execute(db,
        input.MaThaoTac, "SHIFT_ADMIN_RESUMED", "CaLamViec", new { id, input }, context, async () =>
    {
        WorkforceOperation.Reason(input.LyDo);
        var shift = await Editable(id, input.PhienBanDuLieu);
        if (shift.TrangThai != "LOCKED") throw new WorkforceRule(409, "Chỉ tiếp tục ca đang khóa, không mở lại ca đã chốt.");
        if (!await db.NhanVien.AnyAsync(x => x.MaNhanVien == shift.MaNhanVien && x.TrangThai == "ACTIVE") ||
            !await db.ThietBiPOS.AnyAsync(x => x.MaMay == shift.MaMay && x.TrangThai == "ACTIVE"))
            throw new WorkforceRule(409, "Hồ sơ hoặc thiết bị không còn hợp lệ. Cần bàn giao sang máy/người được phép.");
        var before = JsonSerializer.Serialize(await View(shift));
        shift.TrangThai = "OPEN"; shift.TienMatThucDem = null; shift.LyDoChenhLech = null; shift.ThoiDiemCapNhat = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (id, (object)await View(shift), before, input.LyDo.Trim());
    });

    /// <summary>Đọc ca để ghi và chặn CLOSED/HANDED_OVER; không mở lại ca đã kết thúc.</summary>
    private async Task<CaLamViec> Editable(long id, byte[] version)
    {
        var shift = await db.CaLamViec.SingleOrDefaultAsync(x => x.MaCaLamViec == id) ?? throw new WorkforceRule(404, "Không tìm thấy ca.");
        WorkforceOperation.Version(version, shift.PhienBanDuLieu);
        if (shift.KetThucLuc is not null) throw new WorkforceRule(409, "Ca đã chốt/bàn giao. Chỉ được tra cứu.");
        return shift;
    }

    /// <summary>NULL khác 0; Admin không được nhập thay nhân viên hoặc bỏ qua lý do chênh lệch.</summary>
    private static void ValidateCount(CaLamViec shift, decimal expected)
    {
        if (shift.TienMatThucDem is null || shift.TienMatThucDem < 0 || expected < 0)
            throw new WorkforceRule(400, "Cần nhân viên gửi tiền thực đếm hợp lệ tại POS. Không tự coi chưa nhập là 0.");
        if (shift.TienMatThucDem != expected) WorkforceOperation.Reason(shift.LyDoChenhLech);
    }

    /// <summary>Chỉ chốt trạng thái và số liệu đối chiếu, không chỉnh sửa khoản thu/hoàn hoặc đổi nhân viên nguồn.</summary>
    private static void Finish(CaLamViec shift, string status, decimal expected, HttpContext context)
    {
        shift.TrangThai = status; shift.KetThucLuc = DateTime.UtcNow; shift.ThoiDiemCapNhat = DateTime.UtcNow;
        shift.TienMatDuKienCuoiCa = expected;
        shift.MaQuanLyDuyet = long.Parse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    }
}
