using System.Security.Claims;
using System.Text.Json;
using FourChang.Api.Shared.Operations;
using FourChang.Api.Shared.Orders;
using FourChang.Domain.Entities.Payments;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Refunds;

/// <summary>Đối soát tiền vào thủ công từ sao kê do Admin kiểm tra; không dùng ảnh khách gửi làm xác nhận tự động.</summary>
public sealed class IncomingReconciliationService(CafeDbContext db, OrderFinancials financials)
{
    /// <summary>Ghép một giao dịch vào một khoản thu; thiếu tiền không nhận Web, tiền muộn không khôi phục đơn.</summary>
    public Task<IResult> Match(IncomingReconciliationInput input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "BANK_MANUAL_RECONCILE", "HoSoHoanTien", input, context, async () =>
        {
            AdminOperation.Reason(input.LyDo);
            if (!input.QuanLyDaDoiChieu || string.IsNullOrWhiteSpace(input.ChungCuSaoKe) || input.ChungCuSaoKe.Length > 500 ||
                string.IsNullOrWhiteSpace(input.NhaCungCap) || input.NhaCungCap.Length > 50 || string.IsNullOrWhiteSpace(input.TaiKhoanQuanAlias) || input.TaiKhoanQuanAlias.Length > 100 ||
                string.IsNullOrWhiteSpace(input.MaGiaoDich) || input.MaGiaoDich.Length > 150 || input.MaGiaoDich.Any(char.IsControl) || input.SoTien <= 0 || input.SoTien != decimal.Truncate(input.SoTien) || input.SoTien > 999999999999999999m ||
                input.ThoiDiemNganHang.Kind == DateTimeKind.Unspecified || input.ThoiDiemNganHang.ToUniversalTime() > DateTime.UtcNow)
                throw new AdminRule(400, "Cần sao kê đã đối chiếu, định danh tài khoản quán, mã giao dịch, số tiền và thời điểm hợp lệ.");
            var payment = await db.ThanhToan.SingleOrDefaultAsync(x => x.MaThanhToan == input.MaThanhToan && x.PhuongThuc == "BANK_TRANSFER")
                ?? throw new AdminRule(404, "Không có yêu cầu thu chuyển khoản này.");
            AdminOperation.Version(input.PhienBanDuLieu, payment.PhienBanDuLieu);
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"4chang.orderstate." + payment.MaDonHang},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51090,'Order lock timeout',1;");
            var order = await db.DonHang.SingleAsync(x => x.MaDonHang == payment.MaDonHang); var before = JsonSerializer.Serialize(new { payment.MaThanhToan, payment.SoTienDaXacMinh, payment.TrangThai, OrderState = order.TrangThai });
            var bank = await db.GiaoDichNganHang.SingleOrDefaultAsync(x => x.TenNhaCungCap == input.NhaCungCap && x.MaTaiKhoanNganHangQuan == input.TaiKhoanQuanAlias && x.MaGiaoDichBenNgoai == input.MaGiaoDich);
            if (bank is not null && (bank.ChieuGiaoDich != "IN" || bank.MaLanChiHoan is not null || bank.MaThanhToan is not null && bank.MaThanhToan != payment.MaThanhToan || bank.SoTien != input.SoTien))
                throw new AdminRule(409, "Giao dịch sai chiều/số tiền hoặc đã ghép đơn khác.");
            if (bank?.MaThanhToan == payment.MaThanhToan)
                return (payment.MaThanhToan, (object)new AdminSaveReceipt(payment.MaThanhToan, payment.PhienBanDuLieu), before, input.LyDo);
            if (bank is null)
            {
                bank = new GiaoDichNganHang { TenNhaCungCap = input.NhaCungCap, MaTaiKhoanNganHangQuan = input.TaiKhoanQuanAlias, MaGiaoDichBenNgoai = input.MaGiaoDich,
                    ChieuGiaoDich = "IN", SoTien = input.SoTien, ThoiDiemNganHang = input.ThoiDiemNganHang.ToUniversalTime() }; db.GiaoDichNganHang.Add(bank);
            }
            bank.MaThanhToan = payment.MaThanhToan; bank.TrangThaiXacMinh = "VERIFIED"; bank.XacMinhLuc = DateTime.UtcNow; bank.ThamChieuChungCu = input.ChungCuSaoKe;
            payment.SoTienDaXacMinh += bank.SoTien; payment.MaQuanLyXacMinh = long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            payment.XacMinhLuc = DateTime.UtcNow; payment.ThamChieuChungCu = input.ChungCuSaoKe; payment.ThoiDiemCapNhat = DateTime.UtcNow;
            payment.TrangThai = payment.SoTienDaXacMinh >= payment.SoTienYeuCau ? "VERIFIED" : "PARTIAL";
            await db.SaveChangesAsync();
            if (order.KenhDatHang == "WEB" && order.TrangThai == "PENDING_PAYMENT")
            {
                var onTime = order.HetHanLuc is null ? payment.SoTienDaXacMinh : await db.GiaoDichNganHang.Where(x => x.MaThanhToan == payment.MaThanhToan && x.ChieuGiaoDich == "IN" && x.TrangThaiXacMinh == "VERIFIED" && x.ThoiDiemNganHang <= order.HetHanLuc).SumAsync(x => (decimal?)x.SoTien) ?? 0;
                if (onTime >= payment.SoTienYeuCau) order.TrangThai = "PENDING_CONFIRMATION";
                else if (order.HetHanLuc <= DateTime.UtcNow)
                {
                    var ids = await db.ChiTietDonHang.Where(x => x.MaDonHang == order.MaDonHang).Select(x => x.MaChiTietDonHang).ToListAsync();
                    var holds = await db.GiuChoNguyenLieu.Where(x => ids.Contains(x.MaChiTietDonHang)).ToListAsync();
                    var releaseError = await FourChang.Api.Features.Customer.Orders.OrderInventoryRelease.ReleaseAllUnconsumedAsync(db, holds, "EXPIRED", DateTime.UtcNow, CancellationToken.None);
                    if (releaseError is not null) throw new AdminRule(409, releaseError);
                    order.TrangThai = "EXPIRED"; order.MaXuLyNgoaiLe = "PAYMENT_TIMEOUT"; order.PhiGiaoHangHienTai = 0;
                }
            }
            var balance = await financials.Read(order);
            var requested = await db.HoSoHoanTien.Where(x => x.MaDonHang == order.MaDonHang && x.TrangThai == "REQUESTED").SumAsync(x => (decimal?)x.SoTienYeuCau) ?? 0;
            var needed = Math.Max(0, balance.PhaiHoan - balance.DangGiuHanMuc - requested);
            if (needed > 0) db.HoSoHoanTien.Add(new() { MaHoanTienHienThi = "HT-" + Guid.NewGuid().ToString("N"), MaDonHang = order.MaDonHang, MaThanhToan = payment.MaThanhToan,
                LoaiNguon = order.TrangThai == "EXPIRED" ? "LATE_PAYMENT" : "OVERPAYMENT", SoTienYeuCau = Math.Min(needed, payment.SoTienDaXacMinh),
                LyDoHoan = input.LyDo, ChungCuYeuCau = input.ChungCuSaoKe, PhuongThucHoan = "BANK_TRANSFER", TrangThai = "REQUESTED", MaQuanLyYeuCau = payment.MaQuanLyXacMinh });
            order.ThoiDiemCapNhat = DateTime.UtcNow;
            db.SuKienDongBo.Add(new() { MaSuKien = Guid.NewGuid(), MaThaoTac = input.MaThaoTac, LoaiSuKien = "PAYMENT_RECONCILED", NoiDungJson = JsonSerializer.Serialize(new { order.MaDonHang }), PhatSinhLuc = DateTime.UtcNow });
            await db.SaveChangesAsync(); return (payment.MaThanhToan, (object)new AdminSaveReceipt(payment.MaThanhToan, payment.PhienBanDuLieu), before, "Đối soát thủ công: " + input.LyDo);
        });
}
