using System.Data;
using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Inventory;

/// <summary>ADM-05 chỉ đọc: tách số dư, giữ chỗ, an toàn và lịch sử; không có API sửa tồn.</summary>
public sealed class InventoryService(CafeDbContext db)
{
    /// <summary>Đọc số dư nhất quán theo mốc; thiếu bản ghi tồn được báo chưa xác định, không giả là 0.</summary>
    public async Task<StockSnapshot> Read(string? search, string? status)
    {
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var data = await (from n in db.NguyenLieu.AsNoTracking()
                          join u in db.DonViTinh.AsNoTracking() on n.MaDonViCoSo equals u.MaDonVi
                          join t in db.TonKho.AsNoTracking() on n.MaNguyenLieu equals t.MaNguyenLieu into stock
                          from t in stock.DefaultIfEmpty()
                          orderby n.MaNguyenLieu
                          select new
                          {
                              n.MaNguyenLieu,
                              n.TenNguyenLieu,
                              u.KyHieu,
                              n.TonAnToan,
                              n.NguongCanhBao,
                              OnHand = t == null ? (decimal?)null : t.TonThucTe,
                              Held = t == null ? (decimal?)null : t.TongGiuCho,
                              At = t == null ? (DateTime?)null : t.ThoiDiemCapNhat,
                              Version = t == null ? null : t.PhienBanDuLieu
                          }).ToListAsync();
        var list = data.Select(x =>
        {
            var available = x.OnHand - x.Held - x.TonAnToan;
            var state = available is null ? "UNKNOWN" : available <= 0 ? "OUT" : x.OnHand - x.Held <= x.NguongCanhBao ? "LOW" : "AVAILABLE";
            return new StockView(x.MaNguyenLieu, x.TenNguyenLieu, x.KyHieu, x.OnHand, x.Held, x.TonAnToan, x.NguongCanhBao, available, state, x.At, x.Version);
        }).Where(x => string.IsNullOrWhiteSpace(search) || x.MaNguyenLieu.ToString() == search.Trim() || x.TenNguyenLieu.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase))
          .Where(x => string.IsNullOrWhiteSpace(status) || x.CanhBao == status).ToList();
        if (!string.IsNullOrEmpty(status) && status is not ("UNKNOWN" or "OUT" or "LOW" or "AVAILABLE")) throw new AdminRule(400, "Mức cảnh báo không hợp lệ.");
        var result = new StockSnapshot(DateTime.UtcNow, list);
        if (tx is not null) await tx.CommitAsync();
        return result;
    }

    /// <summary>Truy vết biến động đến đơn/ca hoặc phiếu nguồn và BOM liên quan; không sửa lịch sử.</summary>
    public async Task<StockDetail> Details(long id)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var summary = await Read(null, null);
        var item = summary.NguyenLieu.SingleOrDefault(x => x.MaNguyenLieu == id) ?? throw new AdminRule(404, "Không tìm thấy nguyên liệu.");
        var movements = await (from m in db.BienDongKho.AsNoTracking()
                               join l in db.ChiTietDonHang.AsNoTracking() on m.MaChiTietDonHang equals (long?)l.MaChiTietDonHang into orderLines
                               from l in orderLines.DefaultIfEmpty()
                               join o in db.DonHang.AsNoTracking() on l.MaDonHang equals o.MaDonHang into orders
                               from o in orders.DefaultIfEmpty()
                               join shift in db.CaLamViec.AsNoTracking() on o.MaCaLamViec equals (long?)shift.MaCaLamViec into shifts
                               from shift in shifts.DefaultIfEmpty()
                               join employee in db.NhanVien.AsNoTracking() on shift.MaNhanVien equals employee.MaNhanVien into employees
                               from employee in employees.DefaultIfEmpty()
                               join p in db.ChiTietPhieuKho.AsNoTracking() on m.MaDongPhieuKho equals (long?)p.MaDongPhieuKho into documentLines
                               from p in documentLines.DefaultIfEmpty()
                               where m.MaNguyenLieu == id
                               orderby m.PhatSinhLuc descending, m.MaBienDongKho descending
                               select new StockMovementView(m.MaBienDongKho, m.LoaiBienDong, m.SoLuongThayDoi, m.PhatSinhLuc, m.MaDongPhieuKho,
                                   m.MaChiTietDonHang, p == null ? null : p.MaPhieuKho, o == null ? null : o.MaDonHang,
                                   o == null ? null : o.MaCaLamViec, employee == null ? null : employee.HoTen, shift == null ? null : shift.MaMay, m.GhiChu)).ToListAsync();
        var reservations = await (from r in db.GiuChoNguyenLieu.AsNoTracking()
                                  join l in db.ChiTietDonHang on r.MaChiTietDonHang equals l.MaChiTietDonHang
                                  where r.MaNguyenLieu == id
                                  orderby r.MaChiTietDonHang descending
                                  select new StockReservationView(r.MaChiTietDonHang, l.MaDonHang, r.SoLuongGiuCho, r.SoLuongDaTieuHao, r.SoLuongDaGiaiPhong, r.TrangThai, r.HetHanLuc)).ToListAsync();
        var affected = await (from l in db.ChiTietCongThuc.AsNoTracking()
                              join r in db.CongThuc.AsNoTracking() on l.MaCongThuc equals r.MaCongThuc
                              where l.MaNguyenLieu == id
                              select new StockAffectedView(r.MaCongThuc, r.MaCauHinhMon, r.MaMonThem, r.TenCongThuc, r.TrangThai)).Distinct().ToListAsync();
        var result = new StockDetail(item, summary.DocLuc, movements, reservations, affected);
        await tx.CommitAsync(); return result;
    }
}
