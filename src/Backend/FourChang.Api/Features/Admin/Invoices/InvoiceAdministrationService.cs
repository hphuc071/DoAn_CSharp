using System.Text.Json;
using FourChang.Api.Shared.Operations;
using FourChang.Api.Shared.Orders;
using FourChang.Domain.Entities.Payments;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Invoices;

/// <summary>UC-ADM-08 chỉ đọc bản gốc đã phát hành; điều chỉnh tạo chứng từ mới và không thực hiện thu/chi.</summary>
public sealed class InvoiceAdministrationService(CafeDbContext db, OrderFinancials financials)
{
    /// <summary>Tra cứu phân trang theo số, đơn, khách hoặc ca; ngày lọc theo thời điểm phát hành.</summary>
    public async Task<AdminPage<AdminInvoiceView>> Read(string? search, DateTime? from, DateTime? to, long? shift, int page = 1)
    {
        if (page < 1 || from > to) throw new AdminRule(400, "Bộ lọc hóa đơn không hợp lệ.");
        var q = db.HoaDon.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(x => x.SoHoaDon.Contains(search) || x.MaDonHang.ToString() == search || x.TenNguoiMua != null && x.TenNguoiMua.Contains(search));
        if (from is { } start) q = q.Where(x => x.PhatHanhLuc >= start);
        if (to is { } end) q = q.Where(x => x.PhatHanhLuc < end);
        if (shift is { } id) q = q.Where(x => x.MaCaLamViec == id);
        var total = await q.CountAsync();
        var list = await q.OrderByDescending(x => x.MaHoaDon).Skip((page - 1) * 50).Take(50).ToListAsync();
        return new(list.Select(View).ToList(), total, page, 50, DateTime.UtcNow);
    }

    /// <summary>Giữ nguyên JSON snapshot; tiền thu/hoàn hiển thị riêng, không dựng hóa đơn từ giá menu hiện hành.</summary>
    public async Task<AdminInvoiceDetail> Details(long id)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var invoice = await db.HoaDon.AsNoTracking().SingleOrDefaultAsync(x => x.MaHoaDon == id) ?? throw new AdminRule(404, "Không tìm thấy hóa đơn.");
        var order = await db.DonHang.AsNoTracking().SingleAsync(x => x.MaDonHang == invoice.MaDonHang);
        var root = invoice.MaHoaDonGoc ?? id;
        var related = await db.HoaDon.AsNoTracking().Where(x => x.MaHoaDonGoc == root).ToListAsync();
        var audits = await db.NhatKyHeThong.AsNoTracking().Where(x => x.LoaiDoiTuong == "HoaDon" && x.MaDoiTuong == id.ToString()).OrderByDescending(x => x.PhatSinhLuc).ToListAsync();
        var result = new AdminInvoiceDetail(View(invoice), invoice.NoiDungHoaDonDaChotJson, await financials.Read(order), related.Select(View).ToList(),
            audits.Select(x => new AdminTraceRow(x.MaNhatKy, x.PhatSinhLuc, x.TenNguoiThucHienDaChot, x.MaCaLamViec, x.MaMayDaChot, x.HanhDong, x.LyDo, x.KetQua,
                x.DuLieuTruocJson, x.DuLieuSauJson, x.MaLienKetXuLy)).ToList());
        await tx.CommitAsync(); return result;
    }

    /// <summary>Phát hành điều chỉnh từ nguồn hủy hợp lệ duy nhất; nguồn đã dùng trả lại chứng từ cũ, không giảm hai lần.</summary>
    public Task<IResult> Adjust(long id, InvoiceAdjustmentInput input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "INVOICE_ADJUST", "HoaDon", new { id, input }, context, async () =>
        {
            var original = await db.HoaDon.SingleOrDefaultAsync(x => x.MaHoaDon == id) ?? throw new AdminRule(404, "Không tìm thấy hóa đơn.");
            AdminOperation.Version(input.PhienBanDuLieu, original.PhienBanDuLieu); AdminOperation.Reason(input.LyDo);
            if (original.LoaiHoaDon != "ORIGINAL" || original.TrangThai != "ISSUED") throw new AdminRule(409, "Chỉ điều chỉnh hóa đơn gốc đã phát hành.");
            long? cancelId = input.MaHuyMon; long? refundId = input.MaHoanTien; decimal reduction;
            if (cancelId is not null && refundId is not null) throw new AdminRule(400, "Chọn đúng một nguồn điều chỉnh.");
            HoSoHoanTien? refund = null;
            if (refundId is { } refundKey)
            {
                refund = await db.HoSoHoanTien.SingleOrDefaultAsync(x => x.MaHoanTien == refundKey && x.MaDonHang == original.MaDonHang && x.SoTienDuocDuyet > 0)
                    ?? throw new AdminRule(409, "Hồ sơ hoàn chưa được duyệt hoặc không thuộc hóa đơn.");
                if (refund.LoaiNguon == "CANCELLATION" && refund.MaHuyMon is { } cancellation) { cancelId = cancellation; refundId = null; }
                else if (refund.LoaiNguon != "COMPLAINT" || refund.TrangThai is not ("APPROVED" or "PROCESSING" or "PARTIALLY_REFUNDED" or "REFUNDED" or "RECONCILING"))
                    throw new AdminRule(409, "Hoàn tiền thừa/muộn không làm giảm nghĩa vụ hóa đơn.");
            }
            if (cancelId is null && refundId is null) throw new AdminRule(400, "Thiếu nguồn điều chỉnh.");
            var prior = await db.HoaDon.SingleOrDefaultAsync(x => x.MaHoaDonGoc == id && (cancelId != null && x.MaHuyMon == cancelId || refundId != null && x.MaHoanTien == refundId));
            if (prior is not null) return (prior.MaHoaDon, (object)new AdminSaveReceipt(prior.MaHoaDon, prior.PhienBanDuLieu), (string?)null, input.LyDo);
            if (cancelId is not null)
            {
                var source = await (from c in db.HuyMon join line in db.ChiTietDonHang on c.MaChiTietDonHang equals line.MaChiTietDonHang
                                    where c.MaHuyMon == cancelId && line.MaDonHang == original.MaDonHang && c.TrangThai == "ACCEPTED" select c).SingleOrDefaultAsync()
                    ?? throw new AdminRule(409, "Không có nguồn hủy hợp lệ thuộc đơn này.");
                reduction = source.TienHangGiam + source.PhiGiaoHangGiam;
            }
            else reduction = refund!.SoTienDuocDuyet!.Value;
            var adjusted = await db.HoaDon.Where(x => x.MaHoaDonGoc == id && x.TrangThai == "ISSUED").SumAsync(x => (decimal?)x.SoTien) ?? 0;
            if (reduction <= 0 || original.SoTien + adjusted - reduction < 0) throw new AdminRule(409, "Điều chỉnh vượt giá trị hóa đơn còn lại.");
            var now = DateTime.UtcNow;
            var invoice = new HoaDon { MaDonHang = original.MaDonHang, MaHoaDonGoc = id, MaHuyMon = cancelId, MaHoanTien = refundId,
                LoaiHoaDon = "ADJUSTMENT", SoHoaDon = "DC-" + Guid.NewGuid().ToString("N"), TrangThai = "ISSUED", SoTien = -reduction,
                TenNguoiMua = original.TenNguoiMua, MaCaLamViec = original.MaCaLamViec, LyDo = input.LyDo, PhatHanhLuc = now,
                MaQuanLyPhatHanh = long.Parse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value),
                NoiDungHoaDonDaChotJson = JsonSerializer.Serialize(new { Version = 1, SourceType = cancelId is null ? "COMPLAINT" : "CANCELLATION", SourceId = cancelId ?? refundId,
                    OriginalInvoiceId = id, OriginalNumber = original.SoHoaDon, Amount = -reduction,
                    Reason = input.LyDo, IssuedUtc = now }) };
            db.HoaDon.Add(invoice); await db.SaveChangesAsync();
            // Điều chỉnh chứng từ không trừ tiền thu/hoàn hoặc số lượng món lần nữa.
            return (invoice.MaHoaDon, (object)new AdminSaveReceipt(invoice.MaHoaDon, invoice.PhienBanDuLieu), JsonSerializer.Serialize(View(original)), input.LyDo);
        });

    /// <summary>Ghi job in/xuất riêng; lỗi máy in hoặc hủy lưu không phát hành hóa đơn hay thu tiền lần nữa.</summary>
    public Task<IResult> Log(long id, DocumentLogInput input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "DOCUMENT_" + input.HanhDong, "HoaDon", new { id, input }, context, async () =>
        {
            var invoice = await db.HoaDon.AsNoTracking().SingleOrDefaultAsync(x => x.MaHoaDon == id && x.TrangThai == "ISSUED") ?? throw new AdminRule(404, "Hóa đơn chưa được phát hành.");
            if (input.HanhDong is not ("PREVIEW" or "PRINT_STARTED" or "PRINT_SENT" or "PRINT_FAILED" or "PDF_SAVED") || (input.Loi?.Length ?? 0) > 500 || (input.ThamChieu?.Length ?? 0) > 200)
                throw new AdminRule(400, "Thông tin job không hợp lệ.");
            var copy = await db.NhatKyHeThong.AnyAsync(x => x.LoaiDoiTuong == "HoaDon" && x.MaDoiTuong == id.ToString() && x.HanhDong == "DOCUMENT_PRINT_SENT");
            return (invoice.MaHoaDon, (object)new DocumentLogReceipt(invoice.MaHoaDon, copy, input.MaThaoTac), (string?)null, input.Loi);
        });

    /// <summary>Chuyển entity bất biến thành DTO; không truyền entity tracked tới client.</summary>
    private static AdminInvoiceView View(HoaDon x) => new(x.MaHoaDon, x.SoHoaDon, x.MaDonHang, x.LoaiHoaDon,
        x.MaHoaDonGoc, x.MaHuyMon, x.TenNguoiMua, x.SoTien, x.TrangThai, x.PhatHanhLuc, x.MaCaLamViec, x.PhienBanDuLieu, x.MaHoanTien);
}
