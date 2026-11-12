using System.Data;
using System.Security.Claims;
using System.Text.Json;
using FourChang.Domain.Entities.Audit;
using FourChang.Domain.Entities.Orders;
using FourChang.Domain.Entities.Payments;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace FourChang.Api.Features.Customer.Orders;

// UC-WEB-08: Hủy toàn bộ đơn WEB trong trạng thái chờ xác nhận.
// Không thực hiện chi hoàn tiền ngân hàng; chỉ tạo HoSoHoanTien REQUESTED.
public static class CustomerOrderCancellation
{
    public sealed record CancelRequest(string? Reason, string? ExpectedVersion = null);

    public static void Map(WebApplication app)
    {
        app.MapPost("/api/customer/orders/{orderId:long}/cancel", CancelAsync)
           .RequireAuthorization("Customer");
    }

    public static async Task<IResult> CancelAsync(
        long orderId,
        CancelRequest request,
        HttpContext context,
        CafeDbContext db,
        CancellationToken ct,
        IConfiguration configuration)
    {
        // Fail before opening SQL, including manual API POST. Environment name never enables writes.
        if (!OrderIntegrationSafety.Enabled(configuration, "CancellationEnabled"))
            return Results.Json(new { code = "CANCELLATION_DISABLED" }, statusCode: 503);
        if (!long.TryParse(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var customerId) || customerId <= 0)
            return Results.Unauthorized();

        var reason = request.Reason?.Trim() ?? "";
        if (orderId <= 0 || reason.Length < 3 || reason.Length > 400 || reason.Any(char.IsControl) ||
            !OrderIdempotency.TryReadKey(context, out var operation) ||
            !OrderIntegrationSafety.ValidVersion(request.ExpectedVersion, out var expectedVersion))
            return Results.BadRequest(new
            {
                message = "Cần lý do 3–400 ký tự, Idempotency-Key GUID và ExpectedVersion rowversion hợp lệ."
            });

        // SERIALIZABLE + rowversion: các cập nhật đơn/kho/hoàn tiền cùng một giao dịch.
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        var hash = OrderIntegrationSafety.Hash(new { orderId, request.Reason, request.ExpectedVersion });
        if (!await OrderIntegrationSafety.LockAsync(db, tx, "4chang.order." + operation.ToString("N"), ct))
            return Results.Conflict(new { code = "OPERATION_BUSY" });
        var previous = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation, ct);
        if (previous is not null)
        {
            if (previous.MaKhachHang != customerId || previous.HanhDong != "CANCEL_ORDER" ||
                previous.MaDoiTuong != orderId.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                previous.MaBamYeuCau is null || !CryptographicOperations.FixedTimeEquals(previous.MaBamYeuCau, hash) ||
                string.IsNullOrWhiteSpace(previous.KetQuaDaChotJson))
                return Results.Conflict(new { code = "IDEMPOTENCY_CONFLICT" });
            return Results.Content(previous.KetQuaDaChotJson, "application/json", statusCode: 200);
        }
        if (await db.ThanhToan.AnyAsync(x => x.MaThaoTac == operation, ct))
            return Results.Conflict(new { code = "OPERATION_KEY_IN_USE" });
        if (!await OrderIntegrationSafety.LockAsync(db, tx, "4chang.orderstate." + orderId, ct))
            return Results.Conflict(new { code = "ORDER_BUSY" });

        var order = await db.DonHang.SingleOrDefaultAsync(x =>
            x.MaDonHang == orderId &&
            x.MaKhachHang == customerId &&
            x.KenhDatHang == "WEB", ct);

        if (order is null)
            return Results.NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (!order.PhienBanDuLieu.AsSpan().SequenceEqual(expectedVersion))
            return Results.Conflict(new { code = "VERSION_CONFLICT", message = "Đơn đã thay đổi; tải lại trạng thái." });

        if (order.TrangThai != "PENDING_CONFIRMATION")
            return Results.Conflict(new
            {
                message = "Đơn không còn ở trạng thái chờ xác nhận; hãy tải lại trạng thái."
            });

        var details = await db.ChiTietDonHang
            .Where(x => x.MaDonHang == orderId)
            .ToListAsync(ct);

        if (details.Count == 0 || details.Any(x =>
                x.SoLuongBatDauPha != 0 ||
                x.SoLuongPhaXong != 0 ||
                x.SoLuongDaHuy != 0 ||
                x.SoLuongDat <= 0))
            return Results.Conflict(new
            {
                message = "Đơn có chi tiết đã xử lý hoặc không hợp lệ; cần nhân viên hỗ trợ."
            });

        var detailIds = details.Select(x => x.MaChiTietDonHang).ToArray();
        var toppingLines = await db.MonThemChiTietDonHang.AsNoTracking()
            .Where(x => detailIds.Contains(x.MaChiTietDonHang))
            .ToListAsync(ct);

        var holds = await db.GiuChoNguyenLieu
            .Where(x => detailIds.Contains(x.MaChiTietDonHang))
            .ToListAsync(ct);
        if (holds.Count == 0 || detailIds.Any(id => !holds.Any(x => x.MaChiTietDonHang == id)) ||
            holds.Any(x => x.TrangThai is not ("RESERVED" or "PARTIAL" or "RELEASED" or "EXPIRED")))
            return Results.Conflict(new { code = "INVENTORY_RECONCILIATION_REQUIRED" });

        // All release checks and updates are shared with future expiration handling.
        // This method requires the caller's active SERIALIZABLE transaction.
        var payments = await db.ThanhToan
            .Where(x => x.MaDonHang == orderId)
            .ToListAsync(ct);

        var paymentIds = payments.Select(x => x.MaThanhToan).ToArray();
        var hasRefunds = await db.HoSoHoanTien.AnyAsync(x =>
            x.MaDonHang == orderId &&
            paymentIds.Contains(x.MaThanhToan), ct);

        if (hasRefunds)
            return Results.Conflict(new
            {
                message = "Đơn đã có hồ sơ hoàn tiền; cần đối soát trước khi hủy."
            });

        var now = DateTime.UtcNow;
        var releaseError = await OrderInventoryRelease.ReleaseAllUnconsumedAsync(db, holds, "RELEASED", now, ct);
        if (releaseError is not null) return Results.Conflict(new { message = releaseError });

        var groupId = operation;
        var originalStatus = order.TrangThai;
        var originalShippingFee = order.PhiGiaoHangHienTai;

        foreach (var detail in details)
        {
            var toppingsPerCup = toppingLines
                .Where(x => x.MaChiTietDonHang == detail.MaChiTietDonHang)
                .Sum(x => x.SoPhanMoiLy * x.GiaMoiPhanDaChot);

            var reduction = (detail.GiaMonDaChot + toppingsPerCup)
                * detail.SoLuongDat;

            db.HuyMon.Add(new HuyMon
            {
                MaNhomHuy = groupId,
                MaChiTietDonHang = detail.MaChiTietDonHang,
                SoLuongHuy = detail.SoLuongDat,
                LyDoHuy = reason,
                MaKhachHangYeuCau = customerId,
                TrangThai = "ACCEPTED",
                TienHangGiam = reduction,
                PhiGiaoHangGiam = detail == details[0]
                    ? originalShippingFee : 0m,
                XuLyLuc = now
            });

            detail.SoLuongDaHuy = detail.SoLuongDat;
        }

        decimal refundRequested = 0m;
        foreach (var payment in payments)
        {
            if (payment.SoTienDaXacMinh > 0)
            {
                // Yêu cầu duyệt/chi hoàn; không tự xác nhận đã hoàn tiền.
                db.HoSoHoanTien.Add(new HoSoHoanTien
                {
                    LoaiNguon = "CANCELLATION",
                    MaHoanTienHienThi = "HT-" + Guid.NewGuid().ToString("N"),
                    MaDonHang = orderId,
                    MaThanhToan = payment.MaThanhToan,
                    LyDoHoan = "Khách hủy đơn: " + reason,
                    SoTienYeuCau = payment.SoTienDaXacMinh,
                    PhuongThucHoan = "BANK_TRANSFER",
                    TrangThai = "REQUESTED",
                    MaKhachHangYeuCau = customerId
                });
                refundRequested += payment.SoTienDaXacMinh;
            }
            else if (payment.TrangThai == "PENDING")
            {
                payment.TrangThai = "EXPIRED";
            }
        }

        order.TrangThai = "CANCELLED";
        order.PhiGiaoHangHienTai = 0m;
        order.HoanTatLuc = now;

        var receipt = new
        {
            maDonHang = orderId, trangThai = "CANCELLED", soTienDeNghiHoan = refundRequested,
            thongBao = "Đã hủy và ghi nhận nghĩa vụ hoàn nếu có; chưa thực hiện chi hoàn."
        };
        db.NhatKyHeThong.Add(OrderAuditOutbox.Stamp(new NhatKyHeThong
        {
            MaLienKetXuLy = groupId,
            MaThaoTac = operation,
            MaBamYeuCau = hash,
            KetQuaDaChotJson = JsonSerializer.Serialize(receipt),
            LoaiTacNhan = "CUSTOMER",
            MaKhachHang = customerId,
            TenNguoiThucHienDaChot = context.User.Identity?.Name
                ?? $"CUSTOMER-{customerId}",
            HanhDong = "CANCEL_ORDER",
            LoaiDoiTuong = "DonHang",
            MaDoiTuong = orderId.ToString(),
            DuLieuTruocJson = JsonSerializer.Serialize(new
            {
                trangThai = originalStatus
            }),
            DuLieuSauJson = JsonSerializer.Serialize(new
            {
                trangThai = "CANCELLED",
                refundRequested
            }),
            LyDo = reason,
            KetQua = "SUCCESS",
            PhatSinhLuc = now
        }));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Results.Ok(receipt);
    }
}
