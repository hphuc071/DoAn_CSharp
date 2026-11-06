using System.Data;
using System.Security.Claims;
using System.Text.Json;
using FourChang.Domain.Entities.Audit;
using FourChang.Domain.Entities.Orders;
using FourChang.Domain.Entities.Payments;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Customer.Orders;

// UC-WEB-08: Hủy toàn bộ đơn WEB trong trạng thái chờ xác nhận.
// Không thực hiện chi hoàn tiền ngân hàng; chỉ tạo HoSoHoanTien REQUESTED.
public static class CustomerOrderCancellation
{
    public sealed record CancelRequest(string? Reason);

    public static void Map(WebApplication app)
    {
        app.MapPost("/api/customer/orders/{orderId:long}/cancel", CancelAsync)
           .RequireAuthorization("Customer");
    }

    private static async Task<IResult> CancelAsync(
        long orderId,
        CancelRequest request,
        HttpContext context,
        CafeDbContext db,
        CancellationToken ct)
    {
        if (!long.TryParse(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var customerId) || customerId <= 0)
            return Results.Unauthorized();

        var reason = request.Reason?.Trim() ?? "";
        if (orderId <= 0 || reason.Length < 3 || reason.Length > 400)
            return Results.BadRequest(new
            {
                message = "Lý do hủy phải có từ 3 đến 400 ký tự."
            });

        // SERIALIZABLE + rowversion: các cập nhật đơn/kho/hoàn tiền cùng một giao dịch.
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        var order = await db.DonHang.SingleOrDefaultAsync(x =>
            x.MaDonHang == orderId &&
            x.MaKhachHang == customerId &&
            x.KenhDatHang == "WEB", ct);

        if (order is null)
            return Results.NotFound(new { message = "Không tìm thấy đơn hàng." });

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

        // Chỉ hỗ trợ hủy toàn đơn trước khi bắt đầu pha và tiêu hao nguyên liệu.
        if (holds.Any(x => x.SoLuongDaTieuHao != 0 ||
            x.SoLuongGiuCho < x.SoLuongDaGiaiPhong))
            return Results.Conflict(new
            {
                message = "Đơn đã sử dụng nguyên liệu hoặc dữ liệu giữ kho bất thường."
            });

        var ingredientIds = holds.Select(x => x.MaNguyenLieu)
            .Distinct().ToArray();
        var stock = await db.TonKho
            .Where(x => ingredientIds.Contains(x.MaNguyenLieu))
            .ToDictionaryAsync(x => x.MaNguyenLieu, ct);

        var releases = holds
            .Select(x => new
            {
                Hold = x,
                Remaining = x.SoLuongGiuCho - x.SoLuongDaGiaiPhong
            })
            .ToList();

        // Kiểm tra tổng lượng còn giữ trước khi cập nhật một bản ghi kho nào.
        foreach (var ingredient in releases.GroupBy(x => x.Hold.MaNguyenLieu))
        {
            var amount = ingredient.Sum(x => x.Remaining);
            if (amount < 0 || (amount > 0 &&
                (!stock.TryGetValue(ingredient.Key, out var item) ||
                 item.TongGiuCho < amount)))
                return Results.Conflict(new
                {
                    message = "Số lượng giữ kho không khớp; cần kiểm tra lại."
                });
        }

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
        var groupId = Guid.NewGuid();
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

        foreach (var item in releases)
        {
            if (item.Remaining <= 0) continue;
            item.Hold.SoLuongDaGiaiPhong += item.Remaining;
            item.Hold.TrangThai = "RELEASED";
            stock[item.Hold.MaNguyenLieu].TongGiuCho -= item.Remaining;
        }

        decimal refundRequested = 0m;
        foreach (var payment in payments)
        {
            if (payment.SoTienDaXacMinh > 0)
            {
                // Yêu cầu duyệt/chi hoàn; không tự xác nhận đã hoàn tiền.
                db.HoSoHoanTien.Add(new HoSoHoanTien
                {
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

        db.NhatKyHeThong.Add(new NhatKyHeThong
        {
            MaLienKetXuLy = groupId,
            MaThaoTac = Guid.NewGuid(),
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
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Results.Ok(new
        {
            maDonHang = orderId,
            trangThai = "CANCELLED",
            soTienDeNghiHoan = refundRequested,
            thongBao = refundRequested > 0
                ? "Đã hủy đơn và tạo yêu cầu hoàn tiền; chưa thực hiện chi hoàn."
                : "Đã hủy đơn và giải phóng nguyên liệu đang giữ."
        });
    }
}
