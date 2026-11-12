using System.Data;
using System.Globalization;
using System.Security.Claims;
using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Reports;

/// <summary>UC-ADM-10: phân biệt doanh thu hoàn tất, tiền thực thu và hoàn theo thời điểm chi; chụp mốc xuất bất biến.</summary>
public sealed class ReportAdministrationService(CafeDbContext db, ReportPermissions permissions, ReportSnapshotStore store)
{
    /// <summary>Tổng hợp cùng transaction, không dùng số 0 thay lỗi truy vấn; lưu snapshot để xuất đúng mốc đã xem.</summary>
    public async Task<ReportSnapshot> Read(ReportFilter filter, HttpContext context)
    {
        permissions.Demand("ReportsRead");
        if (filter.TuLuc.Kind == DateTimeKind.Unspecified || filter.DenLuc.Kind == DateTimeKind.Unspecified || filter.TuLuc >= filter.DenLuc)
            throw new AdminRule(400, "Chọn kỳ có múi giờ; cận cuối phải sau cận đầu.");
        var start = filter.TuLuc.ToUniversalTime(); var end = filter.DenLuc.ToUniversalTime();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var q = db.DonHang.AsNoTracking().AsQueryable();
        if (filter.Kenh is { Length: > 0 }) q = q.Where(x => x.KenhDatHang == filter.Kenh);
        if (filter.HinhThuc is { Length: > 0 }) q = q.Where(x => x.HinhThucNhanMon == filter.HinhThuc);
        if (filter.MaCa is { } shift) q = q.Where(x => x.MaCaLamViec == shift);
        // Không lọc đơn theo ngày tạo trước khi tính dòng tiền: thu/hoàn có thể thuộc đơn của kỳ trước.
        var orders = await q.ToListAsync(); var ids = orders.Select(x => x.MaDonHang).ToArray();
        var created = orders.Where(x => x.ThoiDiemTao >= start && x.ThoiDiemTao < end).ToList();
        var completed = orders.Where(x => x.TrangThai == "COMPLETED" && x.HoanTatLuc >= start && x.HoanTatLuc < end).ToList();
        var doneIds = completed.Select(x => x.MaDonHang).ToArray();
        var lines = await db.ChiTietDonHang.AsNoTracking().Where(x => ids.Contains(x.MaDonHang)).ToListAsync();
        var lineIds = lines.Select(x => x.MaChiTietDonHang).ToArray();
        var toppings = await db.MonThemChiTietDonHang.AsNoTracking().Where(x => lineIds.Contains(x.MaChiTietDonHang)).ToListAsync();
        var payments = await db.ThanhToan.AsNoTracking().Where(x => ids.Contains(x.MaDonHang) && x.XacMinhLuc >= start && x.XacMinhLuc < end && x.SoTienDaXacMinh > 0).ToListAsync();
        var bankTransfers = await (from b in db.GiaoDichNganHang.AsNoTracking() join p in db.ThanhToan.AsNoTracking() on b.MaThanhToan equals (long?)p.MaThanhToan
                                   where ids.Contains(p.MaDonHang) && b.ChieuGiaoDich == "IN" && b.TrangThaiXacMinh == "VERIFIED" && b.ThoiDiemNganHang >= start && b.ThoiDiemNganHang < end
                                   select new { b.MaGiaoDichNganHang, p.MaThanhToan, p.MaDonHang, b.SoTien, b.ThoiDiemNganHang }).ToListAsync();
        var bankLinked = await (from b in db.GiaoDichNganHang.AsNoTracking() join p in db.ThanhToan.AsNoTracking() on b.MaThanhToan equals (long?)p.MaThanhToan
                                where ids.Contains(p.MaDonHang) && b.ChieuGiaoDich == "IN" && b.TrangThaiXacMinh == "VERIFIED" select p.MaThanhToan).Distinct().ToListAsync();
        var refunds = await db.HoSoHoanTien.AsNoTracking().Where(x => ids.Contains(x.MaDonHang)).ToListAsync();
        var refundIds = refunds.Select(x => x.MaHoanTien).ToArray();
        var attempts = await db.LanChiHoanTien.AsNoTracking().Where(x => refundIds.Contains(x.MaHoanTien) && x.TrangThai == "SUCCEEDED" && x.CoKetQuaLuc >= start && x.CoKetQuaLuc < end).ToListAsync();
        var cancelled = await db.HuyMon.AsNoTracking().Where(x => lineIds.Contains(x.MaChiTietDonHang) && x.TrangThai == "ACCEPTED" && x.XuLyLuc >= start && x.XuLyLuc < end).ToListAsync();
        var servedLines = lines.Where(x => doneIds.Contains(x.MaDonHang)).ToList();
        decimal Unit(FourChang.Domain.Entities.Orders.ChiTietDonHang l) => l.GiaMonDaChot + toppings.Where(x => x.MaChiTietDonHang == l.MaChiTietDonHang).Sum(x => x.GiaMoiPhanDaChot * x.SoPhanMoiLy);
        var goods = servedLines.Sum(x => Unit(x) * (x.SoLuongDat - x.SoLuongDaHuy)); var ship = completed.Sum(x => x.PhiGiaoHangHienTai);
        var issuedComplaints = await db.HoaDon.AsNoTracking().Where(x => doneIds.Contains(x.MaDonHang) && x.LoaiHoaDon == "ADJUSTMENT" && x.TrangThai == "ISSUED" && x.MaHoanTien != null)
            .Select(x => new { x.MaHoanTien, x.SoTien }).ToListAsync();
        var complaints = -issuedComplaints.Sum(x => x.SoTien) + refunds.Where(x => doneIds.Contains(x.MaDonHang) && !issuedComplaints.Any(i => i.MaHoanTien == x.MaHoanTien) && x.LoaiNguon == "COMPLAINT" && x.TrangThai is "APPROVED" or "PROCESSING" or "PARTIALLY_REFUNDED" or "RECONCILING" or "REFUNDED").Sum(x => x.SoTienDuocDuyet ?? 0);
        var cash = payments.Where(x => x.PhuongThuc == "CASH").Sum(x => x.SoTienDaXacMinh);
        // Thu nhiều đợt khác kỳ phải theo từng giao dịch ngân hàng, không gom hết vào giờ xác minh cuối.
        var legacyBank = payments.Where(x => x.PhuongThuc == "BANK_TRANSFER" && !bankLinked.Contains(x.MaThanhToan)).ToList();
        var bank = bankTransfers.Sum(x => x.SoTien) + legacyBank.Sum(x => x.SoTienDaXacMinh);
        var cashRefund = attempts.Where(x => x.PhuongThuc == "CASH").Sum(x => x.SoTien);
        var bankRefund = attempts.Where(x => x.PhuongThuc == "BANK_TRANSFER").Sum(x => x.SoTien);
        var activeCancellations = cancelled.Where(x => orders.Single(o => o.MaDonHang == lines.Single(l => l.MaChiTietDonHang == x.MaChiTietDonHang).MaDonHang).TrangThai != "EXPIRED").ToList();
        var partialOrders = activeCancellations.Select(x => lines.Single(l => l.MaChiTietDonHang == x.MaChiTietDonHang).MaDonHang).Distinct().Count(id => orders.Single(o => o.MaDonHang == id).TrangThai != "CANCELLED");
        var kpi = new ReportKpis(goods, ship, complaints, goods + ship - complaints, cash, bank, cashRefund, bankRefund,
            cash + bank - cashRefund - bankRefund, created.Count, completed.Count, created.Count(x => x.TrangThai == "CANCELLED"), created.Count(x => x.TrangThai == "EXPIRED"),
            partialOrders, activeCancellations.Sum(x => x.SoLuongHuy), created.Count == 0 ? null : (decimal)created.Count(x => x.TrangThai == "COMPLETED") / created.Count);
        var variants = await db.CauHinhMon.AsNoTracking().ToDictionaryAsync(x => x.MaCauHinhMon, x => x.MaMon);
        var top = servedLines.GroupBy(x => variants[x.MaCauHinhMon]).Select(g => (IReadOnlyList<string?>)new string?[]
        { g.Key.ToString(), g.Last().TenMonDaChot, N(g.Sum(x => x.SoLuongDat - x.SoLuongDaHuy)), N(g.Sum(x => Unit(x) * (x.SoLuongDat - x.SoLuongDaHuy))),
            N(g.Sum(x => toppings.Where(t => t.MaChiTietDonHang == x.MaChiTietDonHang).Sum(t => t.GiaMoiPhanDaChot * t.SoPhanMoiLy) * (x.SoLuongDat - x.SoLuongDaHuy))) }).OrderByDescending(x => decimal.Parse(x[2]!, CultureInfo.InvariantCulture)).ToList();
        var stock = await (from n in db.NguyenLieu.AsNoTracking() join t in db.TonKho.AsNoTracking() on n.MaNguyenLieu equals t.MaNguyenLieu
                           join u in db.DonViTinh.AsNoTracking() on n.MaDonViCoSo equals u.MaDonVi
                           where t.TonThucTe - t.TongGiuCho <= n.NguongCanhBao
                           select new { n.MaNguyenLieu, n.TenNguyenLieu, u.KyHieu, t.TonThucTe, t.TongGiuCho, n.TonAnToan, n.NguongCanhBao }).ToListAsync();
        var tables = new List<ReportTable>
        {
            new("Món đã phục vụ", [new("Mã món"), new("Tên món"), new("Số ly", "number"), new("Tiền hàng gồm topping", "number"), new("Trong đó topping", "number")], top),
            new("Tỷ lệ đơn theo ngày tạo", [new("Trạng thái"), new("Số đơn", "number"), new("Tỷ lệ", "number")], created.GroupBy(x => x.TrangThai).Select(g => (IReadOnlyList<string?>)new string?[] { g.Key, N(g.Count()), N((decimal)g.Count() / created.Count) }).ToList()),
            new("Nguyên liệu cảnh báo", [new("Mã"), new("Nguyên liệu"), new("Đơn vị"), new("Thực tế", "number"), new("Giữ", "number"), new("An toàn", "number"), new("Ngưỡng", "number")],
                stock.Select(x => (IReadOnlyList<string?>)new string?[] { x.MaNguyenLieu.ToString(), x.TenNguyenLieu, x.KyHieu, N(x.TonThucTe), N(x.TongGiuCho), N(x.TonAnToan), N(x.NguongCanhBao) }).ToList()),
            new("Nguồn thu trong kỳ", [new("Mã thu"), new("Mã đơn"), new("Phương thức"), new("Thực thu", "number"), new("Thời điểm UTC")],
                payments.Where(x => x.PhuongThuc == "CASH").Concat(legacyBank).Select(x => (IReadOnlyList<string?>)new string?[] { x.MaThanhToan.ToString(), x.MaDonHang.ToString(), x.PhuongThuc + (x.PhuongThuc == "BANK_TRANSFER" ? " (số thu cũ chưa tách giao dịch)" : ""), N(x.SoTienDaXacMinh), x.XacMinhLuc?.ToString("O") })
                    .Concat(bankTransfers.Select(x => (IReadOnlyList<string?>)new string?[] { "GD-" + x.MaGiaoDichNganHang + "/TT-" + x.MaThanhToan, x.MaDonHang.ToString(), "BANK_TRANSFER", N(x.SoTien), x.ThoiDiemNganHang.ToString("O") })).ToList()),
            new("Hoàn thành công trong kỳ", [new("Mã lần chi"), new("Mã hồ sơ"), new("Phương thức"), new("Thực hoàn", "number"), new("Thời điểm UTC")],
                attempts.Select(x => (IReadOnlyList<string?>)new string?[] { x.MaLanChiHoan.ToString(), x.MaHoanTien.ToString(), x.PhuongThuc, N(x.SoTien), x.CoKetQuaLuc?.ToString("O") }).ToList()),
            new("Hủy món trong kỳ", [new("Mã hủy"), new("Mã đơn"), new("Mã dòng"), new("Số ly", "number"), new("Lý do")], activeCancellations.Select(x => (IReadOnlyList<string?>)new string?[] {
                x.MaHuyMon.ToString(), lines.Single(l => l.MaChiTietDonHang == x.MaChiTietDonHang).MaDonHang.ToString(), x.MaChiTietDonHang.ToString(), N(x.SoLuongHuy), x.LyDoHuy }).ToList())
        };
        var snapshot = new ReportSnapshot(Guid.NewGuid(), DateTime.UtcNow, filter, "Asia/Ho_Chi_Minh (UTC+07)",
            "Doanh thu: đơn COMPLETED theo giờ hoàn tất, bỏ ly hủy; tách ship và điều chỉnh khiếu nại đã duyệt tại mốc. Thu: tiền mặt theo giờ xác minh, chuyển khoản theo từng giao dịch ngân hàng; số thu cũ chưa tách giao dịch được ghi rõ riêng; hoàn: SUCCEEDED theo giờ chi. Tỷ lệ: trạng thái hiện tại của đơn tạo trong kỳ; mẫu số là tổng đơn tạo trong kỳ. Món nhóm theo ID, topping tách giá trị; hoàn chung xem bảng hoàn, không phân bổ giả theo món.", kpi, tables);
        await tx.CommitAsync();
        await store.Save(snapshot, long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!)); return snapshot;
    }

    /// <summary>Tra audit độc lập quyền báo cáo, phân trang và che dữ liệu bí mật; không có sửa/xóa.</summary>
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
