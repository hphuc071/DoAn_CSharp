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

// Khách gửi yêu cầu trong trạng thái chờ xác nhận; chỉ Admin duyệt sau khi Staff chuyển mới hủy thật.
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
        await FourChang.Api.Shared.Operations.AdminOperation.Lock(db);
        var hash = OrderIntegrationSafety.Hash(new { orderId, request.Reason, request.ExpectedVersion });
        if (!await OrderIntegrationSafety.LockAsync(db, tx, "4chang.order." + operation.ToString("N"), ct))
            return Results.Conflict(new { code = "OPERATION_BUSY" });
        var previous = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation, ct);
        if (previous is not null)
        {
            if (previous.MaKhachHang != customerId || previous.HanhDong is not ("CANCEL_ORDER" or "REQUEST_CANCEL_ORDER") ||
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

        if (await FourChang.Api.Shared.Orders.CancellationWorkflow.Pending(db, orderId) is not null)
            return Results.Conflict(new { message = "Đơn đã có yêu cầu đang chờ xử lý." });
        var now = DateTime.UtcNow;
        foreach (var detail in details)
            db.HuyMon.Add(new HuyMon { MaNhomHuy = operation, MaChiTietDonHang = detail.MaChiTietDonHang,
                SoLuongHuy = detail.SoLuongDat, LyDoHuy = reason, MaKhachHangYeuCau = customerId,
                TrangThai = "REQUESTED", TienHangGiam = 0, PhiGiaoHangGiam = 0, ThoiDiemTao = now, ThoiDiemCapNhat = now });
        // Chỉ cập nhật phiên bản: chưa hủy món, chưa giải phóng kho, chưa tạo hồ sơ hoàn tiền.
        order.ThoiDiemCapNhat = now;
        var receipt = new { maDonHang = orderId, trangThai = order.TrangThai, maNhomHuy = operation,
            trangThaiYeuCau = "REQUESTED", thongBao = "Đã gửi yêu cầu hủy. Nhân viên sẽ chuyển Quản lý xét duyệt." };
        db.NhatKyHeThong.Add(new NhatKyHeThong { MaLienKetXuLy = operation, MaThaoTac = operation,
            MaBamYeuCau = hash, KetQuaDaChotJson = JsonSerializer.Serialize(receipt), LoaiTacNhan = "CUSTOMER",
            MaKhachHang = customerId, TenNguoiThucHienDaChot = context.User.Identity?.Name ?? $"CUSTOMER-{customerId}",
            HanhDong = "REQUEST_CANCEL_ORDER", LoaiDoiTuong = "DonHang", MaDoiTuong = orderId.ToString(),
            DuLieuSauJson = JsonSerializer.Serialize(receipt), LyDo = reason, KetQua = "SUCCEEDED", PhatSinhLuc = now });
        db.SuKienDongBo.Add(new() { MaSuKien = Guid.NewGuid(), MaThaoTac = operation, LoaiSuKien = "ORDER_CHANGED", NoiDungJson = JsonSerializer.Serialize(new { MaDonHang = orderId }), PhatSinhLuc = now });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Results.Ok(receipt);
    }
}
