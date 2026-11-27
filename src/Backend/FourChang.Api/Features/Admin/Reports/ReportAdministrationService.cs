// UC: UC-ADM-07
// Chức năng: Tổng hợp đơn hoàn thành, món bán chạy, tiền đã hoàn và nhật ký.
// Cần nhớ: Chỉ cộng đơn đủ trạng thái theo bộ lọc; giữ khoản thu, khoản hoàn và tiền mặt đúng ý nghĩa.

using System.Data;
using System.Globalization;
using System.Security.Claims;
using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Reports;

/// <summary>UC-ADM-07: báo cáo tinh gọn theo đơn hoàn thành và tiền đã hoàn; nhật ký chỉ đọc.</summary>
public sealed class ReportAdministrationService(CafeDbContext db, ReportPermissions permissions, ReportSnapshotStore store)
{
    /// <summary>
    /// UC-ADM-07 — Tổng hợp báo cáo bán hàng tại một mốc dữ liệu.
    ///
    /// Hàm này được dùng để:
    /// 1. Kiểm tra người dùng và bộ lọc báo cáo.
    /// 2. Lấy các đơn đã hoàn thành, các món đã bán và các lần hoàn tiền thành công.
    /// 3. Tính các chỉ số và tạo bộ dữ liệu báo cáo tại thời điểm đọc.
    ///
    /// Thông tin cần truyền vào:
    /// - filter: Các điều kiện người dùng chọn để lọc dữ liệu.
    /// - context: Thông tin phiên và quyền của người đang thao tác.
    ///
    /// Cần nhớ: Chỉ cộng các đơn đáp ứng trạng thái hoàn thành theo bộ lọc; không gộp đơn chưa hoàn thành vào doanh thu này.
    /// </summary>
    public async Task<ReportSnapshot> Read(ReportFilter filter, HttpContext context)
    {
        permissions.Demand("ReportsRead");
        if (filter.TuLuc.Kind == DateTimeKind.Unspecified || filter.DenLuc.Kind == DateTimeKind.Unspecified || filter.TuLuc >= filter.DenLuc ||
            filter.Kenh is not (null or "WEB" or "POS") || filter.HinhThuc is not (null or "DINE_IN" or "TAKEAWAY" or "DELIVERY") || filter.MaCa is <= 0)
            throw new AdminRule(400, "Chọn khoảng ngày, nguồn đơn, hình thức nhận và ca hợp lệ.");
        var start = filter.TuLuc.ToUniversalTime(); var end = filter.DenLuc.ToUniversalTime();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var orders = db.DonHang.AsNoTracking().AsQueryable();
        if (filter.Kenh is { } channel) orders = orders.Where(x => x.KenhDatHang == channel);
        if (filter.HinhThuc is { } fulfillment) orders = orders.Where(x => x.HinhThucNhanMon == fulfillment);
        var refundOrders = orders; // Hoàn tiền thuộc ca chi thực tế, không chuyển giao dịch từ ca trước.
        if (filter.MaCa is { } shift) orders = FourChang.Api.Shared.WorkSessions.HandoverLink.CompletedByShift(db, orders, shift);
        // Ngày hoàn thành độc lập ngày tạo/thanh toán: đã thu nhưng chưa giao xong chưa tính doanh thu.
        var completed = await orders.Where(x => x.TrangThai == "COMPLETED" && x.HoanTatLuc >= start && x.HoanTatLuc < end)
            .OrderByDescending(x => x.HoanTatLuc).ThenByDescending(x => x.MaDonHang).ToListAsync();
        var ids = completed.Select(x => x.MaDonHang).ToArray();
        var lines = await db.ChiTietDonHang.AsNoTracking().Where(x => ids.Contains(x.MaDonHang)).ToListAsync();
        var lineIds = lines.Select(x => x.MaChiTietDonHang).ToArray();
        var toppings = await db.MonThemChiTietDonHang.AsNoTracking().Where(x => lineIds.Contains(x.MaChiTietDonHang)).ToListAsync();
        decimal Unit(FourChang.Domain.Entities.Orders.ChiTietDonHang line) => line.GiaMonDaChot +
            toppings.Where(x => x.MaChiTietDonHang == line.MaChiTietDonHang).Sum(x => x.GiaMoiPhanDaChot * x.SoPhanMoiLy);

        // Giữ giảm trừ khiếu nại có chứng từ cũ; không trừ hai lần khi hồ sơ và chứng từ cùng một nguồn.
        var adjustments = await db.HoaDon.AsNoTracking().Where(x => ids.Contains(x.MaDonHang) && x.LoaiHoaDon == "ADJUSTMENT" && x.TrangThai == "ISSUED" && x.MaHoanTien != null).ToListAsync();
        var complaints = await db.HoSoHoanTien.AsNoTracking().Where(x => ids.Contains(x.MaDonHang) && x.LoaiNguon == "COMPLAINT" &&
            (x.TrangThai == "APPROVED" || x.TrangThai == "PROCESSING" || x.TrangThai == "PARTIALLY_REFUNDED" || x.TrangThai == "RECONCILING" || x.TrangThai == "REFUNDED")).ToListAsync();
        decimal Reduction(long order) => -adjustments.Where(x => x.MaDonHang == order).Sum(x => x.SoTien) +
            complaints.Where(x => x.MaDonHang == order && !adjustments.Any(a => a.MaHoanTien == x.MaHoanTien)).Sum(x => x.SoTienDuocDuyet ?? 0);
        decimal Total(long order, decimal shipping) => lines.Where(x => x.MaDonHang == order).Sum(x => Unit(x) * (x.SoLuongDat - x.SoLuongDaHuy)) + shipping - Reduction(order);
        var goods = lines.Sum(x => Unit(x) * (x.SoLuongDat - x.SoLuongDaHuy));
        var shippingTotal = completed.Sum(x => x.PhiGiaoHangHienTai);
        var reduction = completed.Sum(x => Reduction(x.MaDonHang));

        // Hoàn tiền theo ngày chi thành công, gồm cả đơn đã hủy hoặc đơn hoàn thành ở kỳ trước.
        var paidRefunds = await (from a in db.LanChiHoanTien.AsNoTracking()
            join r in db.HoSoHoanTien.AsNoTracking() on a.MaHoanTien equals r.MaHoanTien
            join o in refundOrders on r.MaDonHang equals o.MaDonHang
            where a.TrangThai == "SUCCEEDED" && a.CoKetQuaLuc >= start && a.CoKetQuaLuc < end && (filter.MaCa == null || a.MaCaLamViec == filter.MaCa)
            orderby a.CoKetQuaLuc descending, a.MaLanChiHoan descending
            select new { o.MaDonHang, o.MaDonHienThi, r.TenNguoiNhan, a.SoTien, a.PhuongThuc, a.CoKetQuaLuc }).ToListAsync();
        var cashRefund = paidRefunds.Where(x => x.PhuongThuc == "CASH").Sum(x => x.SoTien);
        var bankRefund = paidRefunds.Where(x => x.PhuongThuc == "BANK_TRANSFER").Sum(x => x.SoTien);
        var variantIds = lines.Select(x => x.MaCauHinhMon).Distinct().ToArray();
        var products = await db.CauHinhMon.AsNoTracking().Where(x => variantIds.Contains(x.MaCauHinhMon)).ToDictionaryAsync(x => x.MaCauHinhMon, x => x.MaMon);
        var bestsellers = lines.GroupBy(x => products[x.MaCauHinhMon])
            .Select(g => new { Id = g.Key, Name = g.Last().TenMonDaChot, Quantity = g.Sum(x => x.SoLuongDat - x.SoLuongDaHuy), Amount = g.Sum(x => Unit(x) * (x.SoLuongDat - x.SoLuongDaHuy)) })
            .Where(x => x.Quantity > 0).OrderByDescending(x => x.Quantity).ThenBy(x => x.Id).ToList();
        var tables = new List<ReportTable>
        {
            new("Đơn hoàn thành", [new("Mã đơn"), new("Khách hàng"), new("Tổng tiền", "number"), new("Hoàn thành lúc", "datetime")],
                completed.Select(x => (IReadOnlyList<string?>)new string?[] { x.MaDonHienThi, x.TenNguoiNhan ?? "Khách tại quầy", N(Total(x.MaDonHang, x.PhiGiaoHangHienTai)), x.HoanTatLuc?.ToString("O") }).ToList()),
            new("Món bán chạy", [new("Tên món"), new("Số ly", "number"), new("Tiền món", "number")],
                bestsellers.Select(x => (IReadOnlyList<string?>)new string?[] { x.Name, N(x.Quantity), N(x.Amount) }).ToList()),
            new("Tiền đã hoàn", [new("Mã đơn"), new("Người nhận"), new("Phương thức"), new("Số tiền", "number"), new("Trả tiền lúc", "datetime")],
                paidRefunds.Select(x => (IReadOnlyList<string?>)new string?[] { x.MaDonHienThi, x.TenNguoiNhan, x.PhuongThuc == "CASH" ? "Tiền mặt" : "Chuyển khoản", N(x.SoTien), x.CoKetQuaLuc?.ToString("O") }).ToList())
        };
        // Các trường KPI cũ còn trong hợp đồng để đọc snapshot lịch sử; báo cáo mới không sử dụng các phân tích đã bỏ.
        var kpis = new ReportKpis(goods, shippingTotal, reduction, goods + shippingTotal - reduction, 0, 0, cashRefund, bankRefund, 0, 0, completed.Count, 0, 0, 0, 0, null);
        var snapshot = new ReportSnapshot(Guid.NewGuid(), DateTime.UtcNow, filter, "Asia/Ho_Chi_Minh (UTC+07)",
            "Tổng tiền chỉ tính đơn đã hoàn thành trong kỳ, gồm phí giao và giảm trừ hợp lệ. Tiền hoàn tính riêng theo ngày trả thành công, không trừ lần hai khỏi tổng đơn. Món bán chạy bỏ số ly đã hủy.", kpis, tables, "REPORT_SIMPLE");
        await tx.CommitAsync();
        await store.Save(snapshot, long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!));
        return snapshot;
    }
    /// <summary>
    /// UC-ADM-07 — Tra cứu nhật ký hệ thống theo bộ lọc.
    ///
    /// Hàm này được dùng để:
    /// 1. Kiểm tra điều kiện xem nhật ký.
    /// 2. Tìm các thao tác phù hợp bộ lọc và trang cần xem.
    /// 3. Trả phần thông tin nhật ký được phép hiển thị.
    ///
    /// Thông tin cần truyền vào:
    /// - filter: Các điều kiện người dùng chọn để lọc dữ liệu.
    /// - context: Thông tin phiên và quyền của người đang thao tác.
    ///
    /// Sau khi xử lý: Nhật ký chỉ đọc, không sửa hoặc xóa lịch sử.
    /// </summary>
    public async Task<AdminPage<AdminTraceRow>> Audit(AuditFilter filter, HttpContext? context = null)
    {
        permissions.Demand("AuditRead");
        if (filter.Trang < 1 || filter.TuLuc > filter.DenLuc) throw new AdminRule(400, "Bộ lọc nhật ký không hợp lệ.");
        var q = db.NhatKyHeThong.AsNoTracking().AsQueryable();
        if (filter.TuLuc is { } from) q = q.Where(x => x.PhatSinhLuc >= from);
        if (filter.DenLuc is { } to) q = q.Where(x => x.PhatSinhLuc < to);
        if (!string.IsNullOrWhiteSpace(filter.HanhDong)) q = q.Where(x => x.HanhDong.Contains(filter.HanhDong));
        if (!string.IsNullOrWhiteSpace(filter.LoaiDoiTuong)) q = q.Where(x => x.LoaiDoiTuong == filter.LoaiDoiTuong);
        if (!string.IsNullOrWhiteSpace(filter.MaDoiTuong)) q = q.Where(x => x.MaDoiTuong == filter.MaDoiTuong);
        if (!string.IsNullOrWhiteSpace(filter.Nguoi)) q = q.Where(x => x.TenNguoiThucHienDaChot.Contains(filter.Nguoi));
        if (filter.MaCa is { } shift) q = q.Where(x => x.MaCaLamViec == shift);
        if (!string.IsNullOrWhiteSpace(filter.MaMay)) q = q.Where(x => x.MaMayDaChot == filter.MaMay);
        if (filter.CorrelationId is { } correlation) q = q.Where(x => x.MaLienKetXuLy == correlation);
        var total = await q.CountAsync(); var data = await q.OrderByDescending(x => x.MaNhatKy).Skip((filter.Trang - 1) * 50).Take(50).ToListAsync();
        var shiftIds = data.Where(x => x.MaCaLamViec != null).Select(x => x.MaCaLamViec!.Value).ToArray();
        var employeeIds = await db.CaLamViec.AsNoTracking().Where(x => shiftIds.Contains(x.MaCaLamViec)).ToDictionaryAsync(x => x.MaCaLamViec, x => x.MaNhanVien);
        if (context is not null)
        {
            var access = FourChang.Api.Shared.Audit.AuditFactory.Create("ADMIN", long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!), "AUDIT_READ", "NhatKyHeThong", 0);
            access.TenNguoiThucHienDaChot = context.User.Identity?.Name ?? "Quản lý";
            access.DuLieuSauJson = System.Text.Json.JsonSerializer.Serialize(new { filter, TongSo=total });
            db.NhatKyHeThong.Add(access); await db.SaveChangesAsync();
        }
        return new(data.Select(x => new AdminTraceRow(x.MaNhatKy, x.PhatSinhLuc, x.TenNguoiThucHienDaChot, x.MaCaLamViec, x.MaMayDaChot, x.HanhDong, x.LyDo,
            x.KetQua, AuditRedaction.Clean(x.DuLieuTruocJson), AuditRedaction.Clean(x.DuLieuSauJson), x.MaLienKetXuLy, x.MaCaLamViec is { } ca && employeeIds.TryGetValue(ca, out var employee) ? employee : null, x.LoaiDoiTuong, x.MaDoiTuong)).ToList(), total, filter.Trang, 50, DateTime.UtcNow);
    }

    /// <summary>Chụp audit theo mốc cố định và đầy đủ các trang, không chỉ xuất 50 dòng đang nhìn thấy.</summary>
    public async Task<ReportSnapshot> AuditSnapshot(AuditFilter filter, HttpContext context)
    {
        permissions.Demand("AuditRead"); permissions.Demand("AuditExport");
        var now = DateTime.UtcNow; var end = filter.DenLuc is { } proposed && proposed < now ? proposed : now;
        var rows = new List<IReadOnlyList<string?>>(); var page = 1;
        while (true)
        {
            var data = await Audit(filter with { DenLuc = end, Trang = page });
            rows.AddRange(data.DuLieu.Select(x => (IReadOnlyList<string?>)new string?[] { x.Ma.ToString(), x.Luc.ToString("O"), x.Nguoi, x.MaCa?.ToString(), x.MaMay,
                x.HanhDong, x.KetQua, x.LyDo, x.Truoc, x.Sau, x.CorrelationId.ToString(), x.MaNhanVien?.ToString(), x.LoaiDoiTuong, x.MaDoiTuong }));
            if (page * data.KichThuocTrang >= data.TongSo) break; page++;
        }
        var table = new ReportTable("Nhật ký đã che dữ liệu", [new("Mã log"), new("UTC"), new("Người / dịch vụ"), new("Mã ca"), new("Mã máy"),
            new("Hành động"), new("Kết quả"), new("Lý do"), new("Trước"), new("Sau"), new("Correlation"), new("Mã nhân viên"), new("Loại đối tượng"), new("Mã đối tượng")], rows);
        var result = new ReportSnapshot(Guid.NewGuid(), now, new(filter.TuLuc ?? DateTime.UnixEpoch, end), "Asia/Ho_Chi_Minh (UTC+07)",
            "Nhật ký bất biến theo bộ lọc: " + System.Text.Json.JsonSerializer.Serialize(filter) + ". Mã/tên nhân viên được đối chiếu hồ sơ, không chứng minh xác thực cá nhân; mã máy chỉ để truy vết.",
            new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null), [table], "AUDIT");
        await store.Save(result, long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!), true); return result;
    }
    /// <summary>Định dạng số xuất bất biến theo máy chủ, không dùng số thực hoặc dấu phân cách tùy máy khách.</summary>
    private static string N(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
