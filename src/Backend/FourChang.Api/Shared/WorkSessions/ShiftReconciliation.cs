using System.Text.Json;
using FourChang.Domain.Entities.Employees;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Shared.WorkSessions;

/// <summary>Bộ đọc đối chiếu dùng chung giữa Admin/POS, chỉ đọc nguồn và snapshot, không xác nhận thu/hoàn.</summary>
public sealed class ShiftReconciliation(CafeDbContext db)
{
    /// <summary>Giữ snapshot tên lúc mở ca; dữ liệu cũ thiếu audit chỉ hiển thị tên hồ sơ hiện tại có nhãn.</summary>
    public async Task<WorkSessionInfo> View(CaLamViec shift)
    {
        var name = await db.NhatKyHeThong.Where(x => x.MaCaLamViec == shift.MaCaLamViec && x.HanhDong == "OPEN_SHIFT")
            .OrderBy(x => x.MaNhatKy).Select(x => x.TenNguoiThucHienDaChot).FirstOrDefaultAsync();
        name ??= (await db.NhanVien.Where(x => x.MaNhanVien == shift.MaNhanVien).Select(x => x.HoTen).SingleAsync()) + " (hồ sơ hiện tại)";
        return new(shift.MaCaLamViec, shift.MaCaHienThi, shift.MaNhanVien, name, shift.TenCa, shift.MaMay, shift.BatDauLuc,
            shift.KetThucLuc, shift.TrangThai, shift.TienMatDauCa, shift.TienMatDuKienCuoiCa, shift.TienMatThucDem,
            shift.LyDoChenhLech, shift.MaCaBanGiaoTruoc, shift.MaQuanLyDuyet, shift.PhienBanDuLieu);
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
            x.TrangThai != "COMPLETED" && x.TrangThai != "CANCELLED" && x.TrangThai != "EXPIRED" && x.TrangThai != "FULFILLMENT_FAILED" && x.TrangThai != "DELIVERY_FAILED" && x.TrangThai != "REJECTED")
            .OrderBy(x => x.MaDonHang).Select(x => new ShiftOrder(x.MaDonHang, x.MaDonHienThi, x.TrangThai, x.MaCaLamViec)).ToListAsync();
        var objectId = shift.MaCaLamViec.ToString();
        var audits = await db.NhatKyHeThong.AsNoTracking().Where(x => x.MaCaLamViec == shift.MaCaLamViec || x.LoaiDoiTuong == "CaLamViec" && x.MaDoiTuong == objectId)
            .OrderByDescending(x => x.MaNhatKy).Select(x => new ShiftAudit(x.PhatSinhLuc, x.TenNguoiThucHienDaChot, x.HanhDong, x.LyDo)).ToListAsync();
        var handoverJson = await db.NhatKyHeThong.AsNoTracking().Where(x => x.LoaiDoiTuong == "CaLamViec" && x.MaDoiTuong == objectId && x.HanhDong == "SHIFT_HANDED_OVER")
            .Select(x => x.DuLieuSauJson).SingleOrDefaultAsync();
        var handover = handoverJson is null ? null : JsonSerializer.Deserialize<HandoverInfo>(handoverJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new(await View(shift), cashIn, cashOut, expected, shift.TienMatThucDem is { } counted ? counted - expected : null,
            transactions, orders, unresolved, audits, handover, DateTime.UtcNow);
    }

}
