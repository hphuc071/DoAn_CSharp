using FourChang.Domain.Entities.Orders;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Shared.Orders;

/// <summary>Công thức tiền duy nhất cho Admin/POS/hoàn tiền; chỉ đọc giá và số lượng đã chốt của đơn.</summary>
public sealed class OrderFinancials(CafeDbContext db)
{
    /// <summary>Tính nghĩa vụ hiện hành, thu xác minh, hoàn thành công và hạn mức đã duyệt đang xử lý.</summary>
    public async Task<OrderMoney> Read(DonHang order)
    {
        var lines = await db.ChiTietDonHang.AsNoTracking().Where(x => x.MaDonHang == order.MaDonHang).ToListAsync();
        var ids = lines.Select(x => x.MaChiTietDonHang).ToArray();
        var toppings = await db.MonThemChiTietDonHang.AsNoTracking().Where(x => ids.Contains(x.MaChiTietDonHang)).ToListAsync();
        var amount = lines.Sum(x => (x.GiaMonDaChot + toppings.Where(t => t.MaChiTietDonHang == x.MaChiTietDonHang).Sum(t => t.GiaMoiPhanDaChot * t.SoPhanMoiLy)) * (x.SoLuongDat - x.SoLuongDaHuy)) + order.PhiGiaoHangHienTai;
        var paid = await db.ThanhToan.Where(x => x.MaDonHang == order.MaDonHang).SumAsync(x => (decimal?)x.SoTienDaXacMinh) ?? 0;
        var refundIds = await db.HoSoHoanTien.Where(x => x.MaDonHang == order.MaDonHang).Select(x => x.MaHoanTien).ToListAsync();
        var refunded = await db.LanChiHoanTien.Where(x => refundIds.Contains(x.MaHoanTien) && x.TrangThai == "SUCCEEDED").SumAsync(x => (decimal?)x.SoTien) ?? 0;
        var held = await db.HoSoHoanTien.Where(x => x.MaDonHang == order.MaDonHang && (x.TrangThai == "APPROVED" || x.TrangThai == "PROCESSING" || x.TrangThai == "RECONCILING" || x.TrangThai == "PARTIALLY_REFUNDED"))
            .SumAsync(x => x.SoTienDuocDuyet) ?? 0;
        var returnedOnHeld = await (from attempt in db.LanChiHoanTien join r in db.HoSoHoanTien on attempt.MaHoanTien equals r.MaHoanTien
                                    where r.MaDonHang == order.MaDonHang && attempt.TrangThai == "SUCCEEDED" &&
                                    (r.TrangThai == "APPROVED" || r.TrangThai == "PROCESSING" || r.TrangThai == "RECONCILING" || r.TrangThai == "PARTIALLY_REFUNDED")
                                    select (decimal?)attempt.SoTien).SumAsync() ?? 0;
        // Khiếu nại sau phục vụ là điều chỉnh nghĩa vụ có nguồn; hủy đã nằm trong số lượng, không trừ hai lần.
        var issued = await db.HoaDon.AsNoTracking().Where(x => x.MaDonHang == order.MaDonHang && x.LoaiHoaDon == "ADJUSTMENT" && x.TrangThai == "ISSUED" && x.MaHoanTien != null)
            .Select(x => new { x.MaHoanTien, x.SoTien }).ToListAsync();
        var issuedSources = issued.Select(x => x.MaHoanTien!.Value).ToArray();
        // Chi thất bại chỉ giải phóng hạn mức; không được đảo một điều chỉnh nghĩa vụ đã phát hành.
        var complaint = -issued.Sum(x => x.SoTien) + (await db.HoSoHoanTien.Where(x => x.MaDonHang == order.MaDonHang && x.LoaiNguon == "COMPLAINT" && !issuedSources.Contains(x.MaHoanTien) &&
            (x.TrangThai == "APPROVED" || x.TrangThai == "PROCESSING" || x.TrangThai == "RECONCILING" || x.TrangThai == "PARTIALLY_REFUNDED" || x.TrangThai == "REFUNDED")).SumAsync(x => x.SoTienDuocDuyet) ?? 0);
        amount = order.TrangThai == "EXPIRED" ? 0 : Math.Max(0, amount - complaint);
        return new(amount, paid, refunded, Math.Max(0, held - returnedOnHeld), Math.Max(0, amount - paid + refunded), Math.Max(0, paid - refunded - amount));
    }
}
