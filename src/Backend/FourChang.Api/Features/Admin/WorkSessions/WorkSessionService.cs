using FourChang.Api.Shared.Operations;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using FourChang.Api.Shared.WorkSessions;
using FourChang.Domain.Entities.Employees;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.WorkSessions;

/// <summary>UC-ADM-12: đọc nguồn tiền thật, giám sát và xác nhận ca; không sửa thu/hoàn để ép khớp quỹ.</summary>
public sealed class WorkSessionService(CafeDbContext db)
{
    /// <summary>Giữ snapshot tên lúc mở ca; dữ liệu cũ thiếu audit chỉ hiển thị tên hồ sơ hiện tại có nhãn.</summary>
    private async Task<WorkSessionInfo> View(CaLamViec shift)
    {
        var name = await db.NhatKyHeThong.Where(x => x.MaCaLamViec == shift.MaCaLamViec && x.HanhDong == "OPEN_SHIFT")
            .OrderBy(x => x.MaNhatKy).Select(x => x.TenNguoiThucHienDaChot).FirstOrDefaultAsync();
        name ??= (await db.NhanVien.Where(x => x.MaNhanVien == shift.MaNhanVien).Select(x => x.HoTen).SingleAsync()) + " (hồ sơ hiện tại)";
        return new(shift.MaCaLamViec, shift.MaCaHienThi, shift.MaNhanVien, name, shift.TenCa, shift.MaMay, shift.BatDauLuc,
            shift.KetThucLuc, shift.TrangThai, shift.TienMatDauCa, shift.TienMatDuKienCuoiCa, shift.TienMatThucDem,
            shift.LyDoChenhLech, shift.MaCaBanGiaoTruoc, shift.MaQuanLyDuyet, shift.PhienBanDuLieu);
    }

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
        if (to is not null) query = query.Where(x => x.BatDauLuc < to.Value.ToUniversalTime());
        if (!string.IsNullOrEmpty(name)) query = query.Where(x => x.TenCa == name);
        if (!string.IsNullOrEmpty(status)) query = query.Where(x => x.TrangThai == status);
        if (!string.IsNullOrWhiteSpace(machine)) query = query.Where(x => x.MaMay == machine.Trim());
        var total = await query.CountAsync();
        var shifts = await query.OrderByDescending(x => x.MaCaLamViec).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var rows = new List<WorkSessionInfo>();
        foreach (var shift in shifts) rows.Add(await View(shift));
        return Results.Ok(new WorkSessionPage(rows, page, pageSize, total));
    }

    /// <summary>Đọc ca và số liệu trong snapshot giao dịch ngắn để bảng đối chiếu không trộn hai thời điểm.</summary>
    public async Task<IResult> Details(long id)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var shift = await db.CaLamViec.AsNoTracking().SingleOrDefaultAsync(x => x.MaCaLamViec == id);
        if (shift is null) return Results.NotFound();
        var result = await Detail(shift);
        await tx.CommitAsync();
        return Results.Ok(result);
    }

    /// <summary>Tổng hợp tiền của chính ca, đơn/khoản chưa rõ từ chuỗi bàn giao, audit và biên bản liên quan.</summary>
    public async Task<WorkSessionDetails> Detail(CaLamViec shift)
    {
        var ids = await HandoverLink.Ancestors(db, shift.MaCaLamViec);
        var payments = await db.ThanhToan.AsNoTracking().Where(x => x.MaCaLamViec != null && ids.Contains(x.MaCaLamViec.Value)).ToListAsync();
        var refunds = await db.LanChiHoanTien.AsNoTracking().Where(x => x.MaCaLamViec != null && ids.Contains(x.MaCaLamViec.Value)).ToListAsync();
        var ownPayments = payments.Where(x => x.MaCaLamViec == shift.MaCaLamViec).ToList();
        var ownRefunds = refunds.Where(x => x.MaCaLamViec == shift.MaCaLamViec).ToList();
        var cashIn = ownPayments.Where(x => x.PhuongThuc == "CASH" && x.TrangThai == "VERIFIED").Sum(x => x.SoTienDaXacMinh);
        var cashOut = ownRefunds.Where(x => x.PhuongThuc == "CASH" && x.TrangThai == "SUCCEEDED").Sum(x => x.SoTien);
        var expected = shift.TienMatDauCa + cashIn - cashOut;
        var transactions = ownPayments.Select(x => new ShiftTransaction("Thu", x.MaThanhToan, x.MaDonHang, x.MaThanhToanHienThi,
            x.PhuongThuc, x.SoTienDaXacMinh, x.TrangThai)).Concat(ownRefunds.Select(x => new ShiftTransaction("Hoàn", x.MaLanChiHoan,
            null, x.MaHoanTien.ToString(), x.PhuongThuc, x.SoTien, x.TrangThai))).ToList();
        var unresolved = payments.Where(x => x.TrangThai is "PENDING" or "PARTIAL" or "RECONCILING")
            .Select(x => "PAYMENT:" + x.MaThanhToan).Concat(refunds.Where(x => x.TrangThai is "PENDING" or "RECONCILING")
                .Select(x => "REFUND:" + x.MaLanChiHoan)).Order().ToList();
        var orders = await db.DonHang.AsNoTracking().Where(x => x.MaCaLamViec != null && ids.Contains(x.MaCaLamViec.Value) &&
            x.TrangThai != "COMPLETED" && x.TrangThai != "CANCELLED" && x.TrangThai != "EXPIRED" && x.TrangThai != "DELIVERY_FAILED" && x.TrangThai != "REJECTED")
            .OrderBy(x => x.MaDonHang).Select(x => new ShiftOrder(x.MaDonHang, x.MaDonHienThi, x.TrangThai, x.MaCaLamViec)).ToListAsync();
        var objectId = shift.MaCaLamViec.ToString();
        var audits = await db.NhatKyHeThong.AsNoTracking().Where(x => x.MaCaLamViec == shift.MaCaLamViec || x.LoaiDoiTuong == "CaLamViec" && x.MaDoiTuong == objectId)
            .OrderByDescending(x => x.MaNhatKy).Select(x => new ShiftAudit(x.PhatSinhLuc, x.TenNguoiThucHienDaChot, x.HanhDong, x.LyDo)).ToListAsync();
        var handoverJson = await db.NhatKyHeThong.AsNoTracking().Where(x => x.LoaiDoiTuong == "CaLamViec" && x.MaDoiTuong == objectId && x.HanhDong == "SHIFT_HANDED_OVER")
            .Select(x => x.DuLieuSauJson).SingleOrDefaultAsync();
        var handover = handoverJson is null ? null : HandoverLink.Read(handoverJson);
        return new(await View(shift), cashIn, cashOut, expected, shift.TienMatThucDem is { } counted ? counted - expected : null,
            transactions, orders, unresolved, audits, handover, DateTime.UtcNow,
            Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
                shift.PhienBanDuLieu, expected,
                Payments = ownPayments.OrderBy(x => x.MaThanhToan).Select(x => new { x.MaThanhToan, x.PhienBanDuLieu }),
                Refunds = ownRefunds.OrderBy(x => x.MaLanChiHoan).Select(x => new { x.MaLanChiHoan, x.PhienBanDuLieu }),
                Orders = orders, Unresolved = unresolved }))),
            shift.TrangThai == "LOCKED" && await LastLifecycle(shift.MaCaLamViec) == "SHIFT_RECONCILIATION_SENT",
            await HasRecentResumeRequest(shift.MaCaLamViec), await LastLifecycle(shift.MaCaLamViec));
    }

    /// <summary>Chốt từ số thực đếm POS đã gửi; kiểm tra lại nguồn tiền ngay trong giao dịch, không chốt lần hai.</summary>
    public Task<IResult> Close(long id, CloseWorkSessionInput input, HttpContext context) => AdminOperation.Execute(db,
        input.MaThaoTac, "SHIFT_ADMIN_CLOSED", "CaLamViec", new { id, input }, context, async () =>
    {
        var shift = await Editable(id, input.PhienBanDuLieu);
        var detail = await Detail(shift);
        ValidateCount(shift, detail.TienMatDuKienHienTai);
        if (!detail.DangChoDoiChieu) throw new AdminRule(409, "Cần nhân viên Gửi đối chiếu tại POS; ca phải đang khóa chờ đối chiếu.");
        if (string.IsNullOrEmpty(input.MaDoiChieu) || input.MaDoiChieu != detail.MaDoiChieu)
            throw new AdminRule(409, "Nguồn tiền/đơn đã thay đổi. Tải lại đối chiếu trước khi xác nhận.");
        if (detail.DonDangXuLy.Count > 0 || detail.KhoanChuaRo.Count > 0)
            throw new AdminRule(409, "Ca còn đơn dở hoặc khoản chưa rõ. Xử lý tại đúng nghiệp vụ hoặc bàn giao có liên kết trước khi chốt.");
        var before = JsonSerializer.Serialize(detail.Ca);
        Finish(shift, "CLOSED", detail.TienMatDuKienHienTai, context);
        await db.SaveChangesAsync();
        return (id, (object)await View(shift), before, shift.LyDoChenhLech);
    });

    /// <summary>Bàn giao có biên bản giữ nguyên chủ ca/giao dịch nguồn; ca nhận chỉ được tạo khi nhân viên mở POS.</summary>
    public Task<IResult> Handover(long id, HandoverWorkSessionInput input, HttpContext context) => AdminOperation.Execute(db,
        input.MaThaoTac, "SHIFT_HANDED_OVER", "CaLamViec", new { id, input }, context, async () =>
    {
        AdminOperation.Reason(input.LyDo);
        var shift = await Editable(id, input.PhienBanDuLieu);
        var machine = FourChang.Api.Security.ShiftSessionCodec.Machine(string.IsNullOrWhiteSpace(input.MaMayNhan) ? shift.MaMay : input.MaMayNhan);
        if (!await db.NhanVien.AnyAsync(x => x.MaNhanVien == input.MaNhanVienNhan && x.TrangThai == "ACTIVE"))
            throw new AdminRule(400, "Nhân viên nhận phải đang làm. Mã máy chỉ dùng ghi lịch sử.");
        if (shift.MaNhanVien == input.MaNhanVienNhan && shift.MaMay == machine)
            throw new AdminRule(400, "Bàn giao cần đổi người hoặc máy.");
        if (await db.CaLamViec.AnyAsync(x => x.MaCaLamViec != id && x.KetThucLuc == null && (x.MaNhanVien == input.MaNhanVienNhan || x.MaMay == machine)) ||
            (await HandoverLink.Pending(db)).Any(x => x.MaNhanVienNhan == input.MaNhanVienNhan || x.MaMayNhan == machine))
            throw new AdminRule(409, "Nhân viên hoặc máy nhận có ca/bàn giao đang chờ.");
        var detail = await Detail(shift);
        ValidateCount(shift, detail.TienMatDuKienHienTai);
        if (!detail.DangChoDoiChieu) throw new AdminRule(409, "Cần nhân viên Gửi đối chiếu tại POS; ca phải đang khóa chờ đối chiếu.");
        if (string.IsNullOrEmpty(input.MaDoiChieu) || input.MaDoiChieu != detail.MaDoiChieu)
            throw new AdminRule(409, "Nguồn tiền/đơn đã thay đổi. Tải lại đối chiếu trước khi xác nhận.");
        // Danh sách Admin đã xác nhận phải khớp nguồn hiện tại, không chấp nhận bỏ sót hoặc thêm mã giả.
        if (input.MaDonBanGiao is null || input.KhoanChuaRo is null ||
            !input.MaDonBanGiao.Order().SequenceEqual(detail.DonDangXuLy.Select(x => x.MaDonHang).Order()) ||
            !input.KhoanChuaRo.Order().SequenceEqual(detail.KhoanChuaRo.Order()))
            throw new AdminRule(409, "Danh sách đơn/khoản chưa rõ đã thay đổi. Tải lại để xác nhận bàn giao.");
        var before = JsonSerializer.Serialize(detail.Ca);
        Finish(shift, "HANDED_OVER", detail.TienMatDuKienHienTai, context);
        shift.GhiChu = input.LyDo.Trim();
        await db.SaveChangesAsync();
        var result = new HandoverInfo(id, input.MaNhanVienNhan, machine, input.LyDo.Trim(), input.MaDonBanGiao, input.KhoanChuaRo);
        return (id, (object)result, before, input.LyDo.Trim());
    });

    /// <summary>Đọc sự kiện thay đổi trạng thái gần nhất, phân biệt khóa tạm với gửi đối chiếu.</summary>
    public Task<string?> LastLifecycle(long id)
    {
        var key = id.ToString();
        string[] actions = ["OPEN_SHIFT", "SHIFT_TEMP_LOCKED", "SHIFT_ADMIN_RESUMED", "SHIFT_RECONCILIATION_SENT", "SHIFT_RECOVERY_ISSUED", "CLOSE_SHIFT", "SHIFT_ADMIN_CLOSED", "SHIFT_HANDED_OVER"];
        return db.NhatKyHeThong.AsNoTracking().Where(x => (x.MaCaLamViec == id || x.LoaiDoiTuong == "CaLamViec" && x.MaDoiTuong == key) && actions.Contains(x.HanhDong))
            .OrderByDescending(x => x.MaNhatKy).Select(x => (string?)x.HanhDong).FirstOrDefaultAsync();
    }

    /// <summary>Chỉ công nhận yêu cầu phục hồi mới có phiên ký hợp lệ; yêu cầu cũ trước lần khóa khác không dùng lại.</summary>
    private async Task<bool> HasRecentResumeRequest(long id)
    {
        var key = id.ToString();
        var last = await db.NhatKyHeThong.AsNoTracking().Where(x => x.MaCaLamViec == id || x.LoaiDoiTuong == "CaLamViec" && x.MaDoiTuong == key)
            .OrderByDescending(x => x.MaNhatKy).Select(x => new { x.HanhDong, x.PhatSinhLuc }).FirstOrDefaultAsync();
        return last is { HanhDong: "SHIFT_RESUME_REQUESTED" } && last.PhatSinhLuc >= DateTime.UtcNow.AddMinutes(-5);
    }

    /// <summary>Admin chỉ mở khóa tạm khi POS còn giữ phiên đã được xác minh; ca chờ đối chiếu không tự mở bán lại.</summary>
    public Task<IResult> Resume(long id, ResumeWorkSessionInput input, HttpContext context) => AdminOperation.Execute(db,
        input.MaThaoTac, "SHIFT_ADMIN_RESUMED", "CaLamViec", new { id, input }, context, async () =>
    {
        AdminOperation.Reason(input.LyDo);
        var shift = await Editable(id, input.PhienBanDuLieu);
        if (shift.TrangThai != "LOCKED" || !await HasRecentResumeRequest(id))
            throw new AdminRule(409, "Cần POS dùng phiên hiện có yêu cầu tiếp tục ca; nếu mất phiên, cấp mã phục hồi chỉ đối chiếu.");
        var latestSent = await db.NhatKyHeThong.AsNoTracking().Where(x => x.MaCaLamViec == id && x.HanhDong == "SHIFT_RECONCILIATION_SENT").OrderByDescending(x => x.MaNhatKy).Select(x => (long?)x.MaNhatKy).FirstOrDefaultAsync();
        var latestLock = await db.NhatKyHeThong.AsNoTracking().Where(x => x.MaCaLamViec == id && x.HanhDong == "SHIFT_TEMP_LOCKED").OrderByDescending(x => x.MaNhatKy).Select(x => (long?)x.MaNhatKy).FirstOrDefaultAsync();
        if (latestLock is null || latestSent > latestLock || shift.TienMatThucDem is not null)
            throw new AdminRule(409, "Ca đã gửi đối chiếu không được mở bán lại bằng khôi phục khóa tạm. Hãy chốt hoặc bàn giao.");
        if (!await db.NhanVien.AnyAsync(x => x.MaNhanVien == shift.MaNhanVien && x.TrangThai == "ACTIVE"))
            throw new AdminRule(409, "Nhân viên không còn đang làm.");
        var before = JsonSerializer.Serialize(await View(shift));
        shift.TrangThai = "OPEN";
        shift.ThoiDiemCapNhat = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (id, (object)await View(shift), before, input.LyDo.Trim());
    });

    /// <summary>Đọc ca để ghi và chặn CLOSED/HANDED_OVER; không mở lại ca đã kết thúc.</summary>
    private async Task<CaLamViec> Editable(long id, byte[] version)
    {
        var shift = await db.CaLamViec.SingleOrDefaultAsync(x => x.MaCaLamViec == id) ?? throw new AdminRule(404, "Không tìm thấy ca.");
        AdminOperation.Version(version, shift.PhienBanDuLieu);
        if (shift.KetThucLuc is not null) throw new AdminRule(409, "Ca đã chốt/bàn giao. Chỉ được tra cứu.");
        return shift;
    }

    /// <summary>NULL khác 0; Admin không được nhập thay nhân viên hoặc bỏ qua lý do chênh lệch.</summary>
    private static void ValidateCount(CaLamViec shift, decimal expected)
    {
        if (shift.TienMatThucDem is null || shift.TienMatThucDem < 0 || expected < 0)
            throw new AdminRule(400, "Cần nhân viên gửi tiền thực đếm hợp lệ tại POS. Không tự coi chưa nhập là 0.");
        if (shift.TienMatThucDem != expected) AdminOperation.Reason(shift.LyDoChenhLech);
    }

    /// <summary>Chỉ chốt trạng thái và số liệu đối chiếu, không chỉnh sửa khoản thu/hoàn hoặc đổi nhân viên nguồn.</summary>
    private static void Finish(CaLamViec shift, string status, decimal expected, HttpContext context)
    {
        shift.TrangThai = status; shift.KetThucLuc = DateTime.UtcNow; shift.ThoiDiemCapNhat = DateTime.UtcNow;
        shift.TienMatDuKienCuoiCa = expected;
        shift.MaQuanLyDuyet = long.Parse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    }
}
