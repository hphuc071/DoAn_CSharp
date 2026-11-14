using System.Data;
using FourChang.Domain.Entities.Orders;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Customer.Orders;

public interface ICustomerOrderReader
{
    Task<CustomerOrderPage> ListAsync(long customerId, CustomerOrderFilter filter, CancellationToken ct);
    Task<CustomerOrderDetail?> DetailAsync(long customerId, long orderId, CancellationToken ct);
}

public sealed class CustomerOrderReader(CafeDbContext db, IConfiguration? configuration = null) : ICustomerOrderReader
{
    // Shared by both queries; ownership is applied in SQL before pagination or loading children.
    public static IQueryable<DonHang> Owned(IQueryable<DonHang> orders, long customerId) =>
        orders.Where(x => customerId > 0 && x.MaKhachHang == customerId && x.KenhDatHang == "WEB");

    public static IQueryable<DonHang> Filter(IQueryable<DonHang> orders, CustomerOrderFilter filter)
    {
        if (filter.Status is not null) orders = orders.Where(x => x.TrangThai == filter.Status);
        if (filter.FromUtc is { } from) orders = orders.Where(x => x.ThoiDiemTao >= from);
        if (filter.ToUtc is { } to) orders = orders.Where(x => x.ThoiDiemTao < to);
        return orders;
    }

    public async Task<CustomerOrderPage> ListAsync(long customerId, CustomerOrderFilter filter, CancellationToken ct)
    {
        var expiryReads = bool.TryParse(configuration?["Orders:ExpiryStatusReadEnabled"], out var enabled) && enabled;
        if (filter.Validate(expiryReads) is { } error) throw new ArgumentException(error);
        var query = Filter(Owned(db.DonHang.AsNoTracking(), customerId), filter);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.ThoiDiemTao).ThenByDescending(x => x.MaDonHang)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        // Chỉ đọc hồ sơ của các đơn đã lọc theo chủ sở hữu. Không dựa riêng CANCELLED để hứa hoàn tiền.
        var ids = rows.Where(x => x.TrangThai == "CANCELLED").Select(x => x.MaDonHang).ToArray();
        var counterOrders = await db.HoSoHoanTien.AsNoTracking().Where(x => ids.Contains(x.MaDonHang) && x.PhuongThucHoan == "CASH" &&
            (x.TrangThai == "APPROVED" || x.TrangThai == "PARTIALLY_REFUNDED") && x.SoTienDuocDuyet >
            (db.LanChiHoanTien.Where(a => a.MaHoanTien == x.MaHoanTien && a.TrangThai == "SUCCEEDED").Sum(a => (decimal?)a.SoTien) ?? 0))
            .Select(x => x.MaDonHang).Distinct().ToListAsync(ct);
        return new(rows.Select(x => Summary(x) with { CanReceiveCounterRefund = counterOrders.Contains(x.MaDonHang) }).ToArray(), filter.Page, filter.PageSize, count, DateTime.UtcNow);
    }

    public async Task<CustomerOrderDetail?> DetailAsync(long customerId, long orderId, CancellationToken ct)
    {
        if (customerId <= 0 || orderId <= 0) return null;
        // Short, read-only transaction: related snapshots must not straddle a cancellation/payment commit.
        // No SaveChanges, DML or external provider calls in this reader.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await Owned(db.DonHang.AsNoTracking(), customerId)
            .SingleOrDefaultAsync(x => x.MaDonHang == orderId, ct);
        if (order is null) return null;
        var lines = await db.ChiTietDonHang.AsNoTracking().Where(x => x.MaDonHang == orderId)
            .OrderBy(x => x.MaChiTietDonHang).ToListAsync(ct);
        var ids = lines.Select(x => x.MaChiTietDonHang).ToArray();
        var toppings = await db.MonThemChiTietDonHang.AsNoTracking()
            .Where(x => ids.Contains(x.MaChiTietDonHang)).ToListAsync(ct);
        var payments = await db.ThanhToan.AsNoTracking().Where(x => x.MaDonHang == orderId)
            .OrderBy(x => x.MaThanhToan).ToListAsync(ct);
        var refunds = await db.HoSoHoanTien.AsNoTracking().Where(x => x.MaDonHang == orderId)
            .OrderBy(x => x.MaHoanTien).ToListAsync(ct);
        var refundIds = refunds.Select(x => x.MaHoanTien).ToArray();
        var disbursements = await db.LanChiHoanTien.AsNoTracking()
            .Where(x => refundIds.Contains(x.MaHoanTien) && x.TrangThai == "SUCCEEDED").ToListAsync(ct);
        var invoices = await db.HoaDon.AsNoTracking().Where(x => x.MaDonHang == orderId)
            .OrderBy(x => x.MaHoaDon).ToListAsync(ct);
        var result = new CustomerOrderDetail(Summary(order), Convert.ToBase64String(order.PhienBanDuLieu),
            Utc(order.HetHanLuc), Utc(order.TiepNhanLuc), Utc(order.HoanTatLuc),
            order.PhiGiaoHangBanDau, order.PhiGiaoHangHienTai,
            lines.Select(x => new CustomerOrderLine(x.MaChiTietDonHang, x.TenMonDaChot, x.KichCoDaChot,
                x.NhietDoDaChot, x.PhanTramDuong, x.PhanTramDa, x.SoLuongDat, x.SoLuongDaHuy,
                x.SoLuongBatDauPha, x.SoLuongPhaXong, x.GiaMonDaChot, x.GhiChu,
                toppings.Where(t => t.MaChiTietDonHang == x.MaChiTietDonHang)
                    .OrderBy(t => t.MaMonThem).Select(t => new CustomerOrderTopping(t.TenMonThemDaChot,
                        t.SoPhanMoiLy, t.GiaMoiPhanDaChot)).ToArray())).ToArray(),
            payments.Select(x => new CustomerPaymentSnapshot(x.MaThanhToan, x.MaThanhToanHienThi,
                x.PhuongThuc, x.TrangThai, x.SoTienYeuCau, x.SoTienDaXacMinh,
                x.MaThamChieuChuyenKhoan, Utc(x.HetHanLuc), Utc(x.XacMinhLuc))).ToArray(),
            refunds.Select(x => new CustomerRefundSnapshot(x.MaHoanTien, x.MaHoanTienHienThi,
                x.TrangThai, x.SoTienYeuCau, x.SoTienDuocDuyet,
                disbursements.Where(d => d.MaHoanTien == x.MaHoanTien).Sum(d => d.SoTien), x.PhuongThucHoan)).ToArray(),
            invoices.Select(x => new CustomerInvoiceSnapshot(x.MaHoaDon, x.SoHoaDon, x.LoaiHoaDon,
                x.TrangThai, x.SoTien, Utc(x.PhatHanhLuc))).ToArray(), DateTime.UtcNow,
            -invoices.Where(x => x.LoaiHoaDon == "ADJUSTMENT" && x.TrangThai == "ISSUED" && x.MaHoanTien != null).Sum(x => x.SoTien)
            + refunds.Where(x => !invoices.Any(i => i.MaHoanTien == x.MaHoanTien && i.TrangThai == "ISSUED") && x.LoaiNguon == "COMPLAINT" &&
                x.TrangThai is "APPROVED" or "PROCESSING" or "PARTIALLY_REFUNDED" or "RECONCILING" or "REFUNDED").Sum(x => x.SoTienDuocDuyet ?? 0));
        result = result with { YeuCauHuy = await FourChang.Api.Shared.Orders.CancellationWorkflow.Pending(db, orderId), KetQuaYeuCauHuy = await FourChang.Api.Shared.Orders.CancellationWorkflow.LatestDecision(db, orderId) };
        await tx.CommitAsync(ct);
        return result;
    }

    private static DateTime? Utc(DateTime? value) => value is { } time ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : null;
    private static CustomerOrderSummary Summary(DonHang order) => new(order.MaDonHang,
        FourChang.Presentation.Shared.Contracts.Common.OrderCode.Format(order.MaDonHang, order.KenhDatHang), order.TrangThai, order.HinhThucNhanMon,
        DateTime.SpecifyKind(order.ThoiDiemTao, DateTimeKind.Utc),
        DateTime.SpecifyKind(order.ThoiDiemCapNhat, DateTimeKind.Utc));
}
