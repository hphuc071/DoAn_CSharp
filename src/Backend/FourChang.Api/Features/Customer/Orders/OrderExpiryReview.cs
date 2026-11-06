using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Customer.Orders;

// Phase 2B, read-only: expired PENDING_PAYMENT orders are candidates for
// reconciliation, NOT permission to cancel or release stock.
// Requires an Admin account; no payment or inventory mutation is performed.
internal static class OrderExpiryReview
{
    internal static void Map(WebApplication app)
    {
        app.MapGet("/api/admin/orders/expiry-review", async (
            CafeDbContext db, CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            var orders = await db.DonHang.AsNoTracking()
                .Where(o => o.KenhDatHang == "WEB" &&
                    o.TrangThai == "PENDING_PAYMENT" &&
                    o.HetHanLuc.HasValue && o.HetHanLuc.Value <= now)
                .OrderBy(o => o.HetHanLuc)
                .Take(100)
                .Select(o => new
                {
                    o.MaDonHang,
                    o.MaDonHienThi,
                    o.HetHanLuc
                })
                .ToListAsync(ct);

            if (orders.Count == 0)
                return (IResult)Results.Ok(new
                {
                    checkedAtUtc = now,
                    requiresExternalReconciliation = true,
                    message = "Không tìm thấy đơn chờ thanh toán đã quá hạn.",
                    candidates = Array.Empty<object>()
                });

            var orderIds = orders.Select(o => o.MaDonHang).ToArray();
            var payments = await db.ThanhToan.AsNoTracking()
                .Where(p => orderIds.Contains(p.MaDonHang))
                .Select(p => new
                {
                    p.MaDonHang, p.MaThanhToan, p.TrangThai,
                    p.SoTienDaXacMinh, p.PhuongThuc
                })
                .ToListAsync(ct);

            var paymentIds = payments.Select(p => p.MaThanhToan).ToArray();
            var linkedBankRecords = await db.GiaoDichNganHang.AsNoTracking()
                .Where(b => b.MaThanhToan.HasValue &&
                    paymentIds.Contains(b.MaThanhToan.Value))
                .Select(b => new { b.MaThanhToan, b.TrangThaiXacMinh })
                .ToListAsync(ct);

            var refundOrderIds = (await db.HoSoHoanTien.AsNoTracking()
                .Where(r => orderIds.Contains(r.MaDonHang))
                .Select(r => r.MaDonHang)
                .Distinct()
                .ToListAsync(ct)).ToHashSet();

            var details = await db.ChiTietDonHang.AsNoTracking()
                .Where(d => orderIds.Contains(d.MaDonHang))
                .Select(d => new
                {
                    d.MaDonHang, d.MaChiTietDonHang,
                    d.SoLuongBatDauPha, d.SoLuongPhaXong,
                    d.SoLuongDaHuy
                })
                .ToListAsync(ct);

            var detailIds = details.Select(d => d.MaChiTietDonHang).ToArray();
            var holds = await db.GiuChoNguyenLieu.AsNoTracking()
                .Where(h => detailIds.Contains(h.MaChiTietDonHang))
                .Select(h => new
                {
                    h.MaChiTietDonHang, h.SoLuongGiuCho,
                    h.SoLuongDaGiaiPhong, h.SoLuongDaTieuHao
                })
                .ToListAsync(ct);

            var result = orders.Select(order =>
            {
                var orderPayments = payments
                    .Where(p => p.MaDonHang == order.MaDonHang).ToArray();
                var ids = details
                    .Where(d => d.MaDonHang == order.MaDonHang)
                    .Select(d => d.MaChiTietDonHang).ToHashSet();
                var orderHolds = holds.Where(h => ids.Contains(h.MaChiTietDonHang)).ToArray();
                var relatedBank = linkedBankRecords
                    .Where(b => b.MaThanhToan.HasValue &&
                        orderPayments.Any(p => p.MaThanhToan == b.MaThanhToan.Value))
                    .ToArray();

                return new
                {
                    order.MaDonHang,
                    order.MaDonHienThi,
                    order.HetHanLuc,
                    paymentCount = orderPayments.Length,
                    paymentStatuses = orderPayments.Select(p => p.TrangThai).Distinct().ToArray(),
                    anyVerifiedAmount = orderPayments.Any(p => p.SoTienDaXacMinh > 0),
                    anyNonPendingPayment = orderPayments.Any(p => p.TrangThai != "PENDING"),
                    linkedBankRecordCount = relatedBank.Length,
                    linkedBankStatuses = relatedBank.Select(b => b.TrangThaiXacMinh).Distinct().ToArray(),
                    hasRefundRecord = refundOrderIds.Contains(order.MaDonHang),
                    hasProcessedItem = details.Any(d => d.MaDonHang == order.MaDonHang &&
                        (d.SoLuongBatDauPha != 0 || d.SoLuongPhaXong != 0 || d.SoLuongDaHuy != 0)),
                    hasConsumedIngredient = orderHolds.Any(h => h.SoLuongDaTieuHao != 0),
                    hasInvalidHold = orderHolds.Any(h => h.SoLuongGiuCho < 0 ||
                        h.SoLuongDaGiaiPhong < 0 ||
                        h.SoLuongDaGiaiPhong > h.SoLuongGiuCho),
                    requiresManualBankReconciliation = true,
                    canAutoExpire = false
                };
            }).ToList();

            return (IResult)Results.Ok(new
            {
                checkedAtUtc = now,
                requiresExternalReconciliation = true,
                message = "Chỉ rà soát: chưa có xác minh ngân hàng đủ tin cậy để tự hủy đơn hoặc giải phóng kho.",
                candidates = result
            });
        }).RequireAuthorization("Admin");
    }
}
