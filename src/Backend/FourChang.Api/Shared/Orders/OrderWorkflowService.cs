using System.Text.Json;
using FourChang.Api.Shared.Operations;
using FourChang.Domain.Entities.Orders;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Shared.Orders;

/// <summary>Lệnh đơn chung cho Admin và POS: không lùi trạng thái, không vượt tiền/kho hoặc tự hoàn nguyên liệu đã pha.</summary>
public sealed class OrderWorkflowService(CafeDbContext db, OrderFinancials money)
{
    /// <summary>Thực thi một lệnh có version, idempotency và audit; Staff chỉ được hành động trong ca OPEN.</summary>
    public Task<IResult> Execute(long id, OrderActionInput input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "ORDER_" + input.HanhDong, "DonHang", new { id, input }, context, async () =>
        {
            await LockOrder(id);
            var order = await db.DonHang.SingleOrDefaultAsync(x => x.MaDonHang == id) ?? throw new AdminRule(404, "Không tìm thấy đơn.");
            AdminOperation.Version(input.PhienBanDuLieu, order.PhienBanDuLieu); AdminOperation.Reason(input.LyDo);
            var admin = context.User.IsInRole("ADMIN");
            var actor = admin ? long.Parse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value) : (long?)null;
            var shift = admin ? null : (long?)long.Parse(context.User.FindFirst("shift")!.Value);
            if (shift is not null && !await db.CaLamViec.AnyAsync(x => x.MaCaLamViec == shift && x.TrangThai == "OPEN")) throw new AdminRule(409, "Ca không còn mở.");
            if (!admin && input.HanhDong is "RETRY_DELIVERY" or "FAIL_DELIVERY" or "EXPIRE") throw new AdminRule(403, "Chỉ Quản lý được chốt ngoại lệ giao/hết hạn.");
            var before = JsonSerializer.Serialize(order);
            var lines = await db.ChiTietDonHang.Where(x => x.MaDonHang == id).ToListAsync();
            if (lines.Count == 0) throw new AdminRule(409, "Đơn chưa có dòng món để xử lý.");
            var financial = await money.Read(order);
            var now = DateTime.UtcNow;
            switch (input.HanhDong)
            {
                case "EXPIRE":
                    Require(order.KenhDatHang == "WEB" && order.TrangThai == "PENDING_PAYMENT" && order.HetHanLuc <= now && financial.ConThu > 0,
                        "Chỉ chốt hết hạn đơn Web chờ thanh toán đã đến hạn và chưa thu đủ.");
                    Require(input.DaDoiChieuSaoKe && !string.IsNullOrWhiteSpace(input.ChungCu), "Cần Admin đối chiếu tiền đến hạn và chứng cứ sao kê, không suy thiếu tiền chỉ từ cache.");
                    var holds = await db.GiuChoNguyenLieu.Where(x => lines.Select(l => l.MaChiTietDonHang).Contains(x.MaChiTietDonHang)).ToListAsync();
                    var error = await FourChang.Api.Features.Customer.Orders.OrderInventoryRelease.ReleaseAllUnconsumedAsync(db, holds, "EXPIRED", now, CancellationToken.None);
                    Require(error is null, error ?? "Không giải phóng được giữ chỗ.");
                    order.TrangThai = "EXPIRED"; order.MaXuLyNgoaiLe = "PAYMENT_TIMEOUT"; order.PhiGiaoHangHienTai = 0;
                    foreach (var payment in await db.ThanhToan.Where(x => x.MaDonHang == id).ToListAsync())
                    {
                        if (payment.SoTienDaXacMinh > 0 && !await db.HoSoHoanTien.AnyAsync(x => x.MaThanhToan == payment.MaThanhToan && x.TrangThai != "REJECTED"))
                            db.HoSoHoanTien.Add(new() { MaHoanTienHienThi = "HT-" + Guid.NewGuid().ToString("N"), MaDonHang = id, MaThanhToan = payment.MaThanhToan,
                                LoaiNguon = "OVERPAYMENT", SoTienYeuCau = payment.SoTienDaXacMinh, PhuongThucHoan = "BANK_TRANSFER", TrangThai = "REQUESTED",
                                MaQuanLyYeuCau = actor, LyDoHoan = "Đơn hết hạn chờ thanh toán", ChungCuYeuCau = input.ChungCu });
                        payment.TrangThai = "EXPIRED";
                    }
                    break;
                case "ACCEPT":
                    Require(order.TrangThai == "PENDING_CONFIRMATION", "Đơn không còn chờ xác nhận.");
                    Require(order.KenhDatHang != "WEB" || financial.ConThu == 0, "Đơn Web chưa trả đủ tiền xác minh.");
                    Require(order.HetHanLuc is null || order.KenhDatHang != "WEB" || financial.ConThu == 0, "Đơn Web cần đối soát thời hạn.");
                    order.TrangThai = "ACCEPTED"; order.TiepNhanLuc = now; break;
                case "CANCEL":
                    Require(order.TrangThai is "PENDING_CONFIRMATION" or "ACCEPTED" or "PREPARING", "Không còn được hủy trước pha.");
                    await Cancel(order, lines, input, actor, shift); break;
                case "START":
                case "FINISH":
                    Require(order.TrangThai is "ACCEPTED" or "PREPARING", "Đơn chưa nhận hoặc đã kết thúc pha.");
                    var line = lines.SingleOrDefault(x => x.MaChiTietDonHang == input.MaDong) ?? throw new AdminRule(400, "Chọn dòng món.");
                    Require(input.SoLuong > 0, "Nhập số ly dương.");
                    if (input.HanhDong == "START")
                    {
                        Require(input.SoLuong <= line.SoLuongDat - line.SoLuongDaHuy - line.SoLuongBatDauPha, "Số ly đã pha hoặc đã hủy.");
                        await Consume(line, input.SoLuong, input.MaThaoTac); line.SoLuongBatDauPha += input.SoLuong;
                    }
                    else { Require(input.SoLuong <= line.SoLuongBatDauPha - line.SoLuongPhaXong, "Không thể hoàn tất số ly chưa bắt đầu pha."); line.SoLuongPhaXong += input.SoLuong; }
                    line.ThoiDiemCapNhat = now;
                    order.TrangThai = lines.All(x => x.SoLuongPhaXong == x.SoLuongDat - x.SoLuongDaHuy) ? "READY" : "PREPARING";
                    break;
                case "DELIVER":
                    Require(order.TrangThai == "READY" && order.HinhThucNhanMon == "DELIVERY" && financial.ConThu == 0, "Chỉ giao đơn sẵn sàng và đã thu đủ.");
                    order.TrangThai = "DELIVERING"; break;
                case "COMPLETE":
                    Require(financial.ConThu == 0, "Chưa thu đủ tiền, không được bàn giao.");
                    Require(order.HinhThucNhanMon == "DELIVERY" ? order.TrangThai == "DELIVERING" : order.TrangThai == "READY", "Chưa đến bước hoàn tất.");
                    order.TrangThai = "COMPLETED"; order.HoanTatLuc = now; break;
                case "FAIL_DELIVERY":
                    Require(order.HinhThucNhanMon == "DELIVERY" && order.TrangThai == "DELIVERING", "Đơn không ở trạng thái đang giao.");
                    Require(!string.IsNullOrWhiteSpace(input.ChungCu) && !string.IsNullOrWhiteSpace(input.ResolutionCode), "Cần chứng cứ và mã xử lý giao thất bại.");
                    Require(input.ResolutionCode!.Length <= 30, "Mã xử lý tối đa 30 ký tự.");
                    order.TrangThai = "DELIVERY_FAILED"; order.MaXuLyNgoaiLe = input.ResolutionCode; break;
                case "RETRY_DELIVERY":
                    Require(order.HinhThucNhanMon == "DELIVERY" && order.TrangThai == "DELIVERING", "Chỉ giao lại khi sự cố chưa được chốt kết thúc.");
                    Require(!string.IsNullOrWhiteSpace(input.ChungCu), "Cần xác nhận giao lại của khách và chứng cứ."); break;
                default: throw new AdminRule(400, "Hành động không hợp lệ.");
            }
            order.ThoiDiemCapNhat = now;
            db.SuKienDongBo.Add(new() { MaSuKien = Guid.NewGuid(), MaThaoTac = input.MaThaoTac, LoaiSuKien = "ORDER_CHANGED",
                NoiDungJson = JsonSerializer.Serialize(new { MaDonHang = id }), PhatSinhLuc = now });
            await db.SaveChangesAsync();
            return (id, (object)new OrderActionReceipt(id, order.TrangThai, order.PhienBanDuLieu), before, input.LyDo);
        }, allowStaff: true);

    /// <summary>Hủy đúng phần chưa pha; giải phóng giữ chỗ và tạo yêu cầu hoàn, không ghi chi thành công.</summary>
    private async Task Cancel(DonHang order, List<ChiTietDonHang> lines, OrderActionInput input, long? admin, long? shift)
    {
        var line = lines.SingleOrDefault(x => x.MaChiTietDonHang == input.MaDong) ?? throw new AdminRule(400, "Chọn dòng cần hủy.");
        Require(input.SoLuong > 0 && input.SoLuong <= line.SoLuongDat - line.SoLuongBatDauPha - line.SoLuongDaHuy, "Chỉ hủy số ly chưa bắt đầu pha.");
        var unitPrice = line.GiaMonDaChot + await db.MonThemChiTietDonHang.Where(x => x.MaChiTietDonHang == line.MaChiTietDonHang).SumAsync(x => (decimal?)x.GiaMoiPhanDaChot * x.SoPhanMoiLy) ?? line.GiaMonDaChot;
        var snapshots = await db.NguyenLieuChiTietDonHang.Where(x => x.MaChiTietDonHang == line.MaChiTietDonHang).ToListAsync();
        var holds = await db.GiuChoNguyenLieu.Where(x => x.MaChiTietDonHang == line.MaChiTietDonHang).ToListAsync();
        Require(snapshots.Count > 0 && snapshots.Count == holds.Count, "Thiếu snapshot/giữ chỗ; cần đối soát kho.");
        foreach (var h in holds)
        {
            var remainingCups = line.SoLuongDat - line.SoLuongBatDauPha - line.SoLuongDaHuy;
            var remaining = h.SoLuongGiuCho - h.SoLuongDaTieuHao - h.SoLuongDaGiaiPhong;
            var released = input.SoLuong == remainingCups ? remaining : decimal.Ceiling(remaining * input.SoLuong / remainingCups * 10000m) / 10000m;
            var stock = await db.TonKho.SingleAsync(x => x.MaNguyenLieu == h.MaNguyenLieu);
            Require(stock.TongGiuCho >= released && released <= remaining, "Giữ chỗ không khớp sổ kho.");
            stock.TongGiuCho -= released; stock.ThoiDiemCapNhat = DateTime.UtcNow;
            h.SoLuongDaGiaiPhong += released; h.TrangThai = h.SoLuongDaGiaiPhong == h.SoLuongGiuCho ? "RELEASED" : "PARTIAL";
            h.ThoiDiemCapNhat = DateTime.UtcNow;
        }
        line.SoLuongDaHuy += input.SoLuong;
        line.ThoiDiemCapNhat = DateTime.UtcNow;
        var cancelledAll = lines.All(x => x.SoLuongDaHuy == x.SoLuongDat);
        var shipping = cancelledAll ? order.PhiGiaoHangHienTai : 0;
        if (cancelledAll) { order.PhiGiaoHangHienTai = 0; order.TrangThai = "CANCELLED"; }
        else if (lines.All(x => x.SoLuongPhaXong == x.SoLuongDat - x.SoLuongDaHuy)) order.TrangThai = "READY";
        var cancellation = new HuyMon { MaNhomHuy = input.MaThaoTac, MaChiTietDonHang = line.MaChiTietDonHang, SoLuongHuy = input.SoLuong,
            LyDoHuy = input.LyDo, MaQuanLyYeuCau = admin, MaCaLamViec = shift, MaQuanLyDuyet = admin, TrangThai = "ACCEPTED",
            TienHangGiam = unitPrice * input.SoLuong, PhiGiaoHangGiam = shipping, XuLyLuc = DateTime.UtcNow };
        db.HuyMon.Add(cancellation); await db.SaveChangesAsync();
        var balance = await money.Read(order);
        var available = Math.Max(0, balance.PhaiHoan - balance.DangGiuHanMuc);
        var requested = await db.HoSoHoanTien.Where(x => x.MaDonHang == order.MaDonHang && x.TrangThai == "REQUESTED").SumAsync(x => (decimal?)x.SoTienYeuCau) ?? 0;
        var needed = Math.Max(0, available - requested);
        foreach (var payment in await db.ThanhToan.Where(x => x.MaDonHang == order.MaDonHang && x.SoTienDaXacMinh > 0).ToListAsync())
        {
            if (needed == 0) break;
            var obligations = await db.HoSoHoanTien.Where(x => x.MaThanhToan == payment.MaThanhToan && x.TrangThai != "REJECTED").SumAsync(x => (decimal?)x.SoTienYeuCau) ?? 0;
            var amount = Math.Min(needed, Math.Max(0, payment.SoTienDaXacMinh - obligations));
            if (amount == 0) continue;
            db.HoSoHoanTien.Add(new() { MaHoanTienHienThi = "HT-" + Guid.NewGuid().ToString("N"), MaDonHang = order.MaDonHang,
                MaThanhToan = payment.MaThanhToan, MaHuyMon = cancellation.MaHuyMon, SoTienYeuCau = amount,
                PhuongThucHoan = payment.PhuongThuc, LoaiNguon = "CANCELLATION", ChungCuYeuCau = "Nguồn hủy " + cancellation.MaHuyMon,
                LyDoHoan = input.LyDo, TrangThai = "REQUESTED", MaQuanLyYeuCau = admin, MaCaYeuCau = shift }); needed -= amount;
        }
    }

    /// <summary>Trừ kho ở START từ lượng đang giữ đã chốt, không tính lại BOM menu và không trừ ở COMPLETE.</summary>
    private async Task Consume(ChiTietDonHang line, int quantity, Guid operation)
    {
        var holds = await db.GiuChoNguyenLieu.Where(x => x.MaChiTietDonHang == line.MaChiTietDonHang).ToListAsync();
        Require(holds.Count > 0, "Thiếu giữ chỗ, cần đối soát trước pha.");
        foreach (var h in holds)
        {
            var remainingCups = line.SoLuongDat - line.SoLuongDaHuy - line.SoLuongBatDauPha;
            var remaining = h.SoLuongGiuCho - h.SoLuongDaTieuHao - h.SoLuongDaGiaiPhong;
            var use = quantity == remainingCups ? remaining : decimal.Ceiling(remaining * quantity / remainingCups * 10000m) / 10000m;
            var stock = await db.TonKho.SingleAsync(x => x.MaNguyenLieu == h.MaNguyenLieu);
            Require(use >= 0 && stock.TonThucTe >= use && stock.TongGiuCho >= use, "Kho/giữ chỗ không đủ, chưa thể pha.");
            stock.TonThucTe -= use; stock.TongGiuCho -= use; stock.ThoiDiemCapNhat = DateTime.UtcNow;
            h.SoLuongDaTieuHao += use; h.TrangThai = h.SoLuongDaTieuHao + h.SoLuongDaGiaiPhong == h.SoLuongGiuCho ? "CONSUMED" : "PARTIAL";
            h.ThoiDiemCapNhat = DateTime.UtcNow;
            if (use > 0) db.BienDongKho.Add(new() { MaNguyenLieu = h.MaNguyenLieu, MaChiTietDonHang = line.MaChiTietDonHang,
                LoaiBienDong = "ORDER_CONSUMPTION", SoLuongThayDoi = -use, MaThaoTac = operation, PhatSinhLuc = DateTime.UtcNow, GhiChu = "Bắt đầu pha từ giữ chỗ" });
        }
    }

    /// <summary>Cùng khóa vòng đời với hủy/thu Web để hai kênh không quyết định trên hai phiên bản khác nhau.</summary>
    private Task<int> LockOrder(long id) => db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"4chang.orderstate." + id},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51090,'Order lock timeout',1;");
    /// <summary>Điều kiện sai trả 409, không sửa nửa chừng và không ép chuyển trạng thái.</summary>
    private static void Require(bool valid, string message) { if (!valid) throw new AdminRule(409, message); }
}
