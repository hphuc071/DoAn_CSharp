// UC: UC-POS-03, UC-ADM-05
// Chức năng: Xử lý tiếp nhận, bắt đầu pha, pha xong, giao và hoàn tất đơn.
// Cần nhớ: Bắt đầu pha mới ghi nhận tiêu hao; pha xong không trừ nguyên liệu lần thứ hai.

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
    /// <summary>
    /// UC-POS-03, UC-ADM-05 — Thực hiện một bước trong quá trình xử lý đơn.
    ///
    /// Hàm này được dùng để:
    /// 1. Kiểm tra người thao tác, ca làm việc và bản dữ liệu của đơn.
    /// 2. Kiểm tra đơn có đang chờ giải quyết yêu cầu hủy không.
    /// 3. Thực hiện đúng hành động được yêu cầu: tiếp nhận, pha, giao hoặc hoàn tất.
    /// 4. Lưu trạng thái mới và thông tin cập nhật đơn.
    ///
    /// Thông tin cần truyền vào:
    /// - id: Khóa của đơn hàng cần xử lý.
    /// - input: Mã đơn, hành động, số ly nếu pha chế, phiên bản đơn và người thực hiện.
    /// - context: Thông tin phiên và quyền của người đang thao tác.
    ///
    /// Sau khi xử lý: Trạng thái mới của đơn hoặc lỗi; không ép chuyển sai bước.
    /// </summary>
    public Task<IResult> Execute(long id, OrderActionInput input, HttpContext context)
    {
        // Kiểm trước receipt/idempotency: không phát lại một lệnh Admin đã bị loại khỏi phạm vi UC-ADM-05.
        if (context.User.IsInRole("ADMIN") && input.HanhDong != "FAIL_DELIVERY")
            return Task.FromResult<IResult>(Results.Problem(statusCode: 403, detail: "Quản lý chỉ theo dõi đơn và duyệt yêu cầu hủy. Tiếp nhận/pha chế/bàn giao thuộc Nhân viên."));
        return AdminOperation.Execute(db, input.MaThaoTac, "ORDER_" + input.HanhDong, "DonHang", new { id, input }, context, async () =>
        {

            // Bước 1: Khóa đơn để hai thao tác đồng thời không cùng quyết định trên dữ liệu cũ.
            await LockOrder(id);
            var order = await db.DonHang.SingleOrDefaultAsync(x => x.MaDonHang == id) ?? throw new AdminRule(404, "Không tìm thấy đơn.");
            AdminOperation.Version(input.PhienBanDuLieu, order.PhienBanDuLieu);
            var reason = input.LyDo;
            // Bước pha/bàn giao thường lệ không yêu cầu gõ lý do; nhật ký vẫn có nội dung rõ ràng.
            if ((input.HanhDong is "ACCEPT" or "START" or "FINISH" or "DELIVER" or "COMPLETE") && string.IsNullOrWhiteSpace(reason))
                reason = input.HanhDong switch { "ACCEPT" => "Tiếp nhận đơn hàng", "START" => "Bắt đầu pha theo đơn", "FINISH" => "Xác nhận pha xong theo đơn", "DELIVER" => "Bắt đầu giao hàng", _ => "Hoàn tất đơn hàng" };
            AdminOperation.Reason(reason);
            var admin = context.User.IsInRole("ADMIN");
            var actor = admin ? long.Parse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value) : (long?)null;
            var shift = admin ? null : (long?)long.Parse(context.User.FindFirst("shift")!.Value);
            if (shift is not null && !await db.CaLamViec.AnyAsync(x => x.MaCaLamViec == shift && x.TrangThai == "OPEN")) throw new AdminRule(409, "Ca không còn mở.");
            if (!admin && input.HanhDong is "RETRY_DELIVERY" or "FAIL_DELIVERY" or "EXPIRE") throw new AdminRule(403, "Chỉ Quản lý được chốt ngoại lệ giao/hết hạn.");
            if (await CancellationWorkflow.Pending(db, id) is not null)
                throw new AdminRule(409, "Đơn đang chờ xét yêu cầu hủy. Chuyển Quản lý duyệt/từ chối trước khi xử lý tiếp.");
            var before = JsonSerializer.Serialize(order);
            var lines = await db.ChiTietDonHang.Where(x => x.MaDonHang == id).ToListAsync();
            if (lines.Count == 0) throw new AdminRule(409, "Đơn chưa có dòng món để xử lý.");
            var financial = await money.Read(order);
            var now = DateTime.UtcNow;

            // Bước 2: Kiểm tra điều kiện riêng của hành động tiếp nhận, pha chế, giao hoặc hoàn tất.
            switch (input.HanhDong)
            {
                case "EXPIRE":
                    Require(order.KenhDatHang == "WEB" && order.TrangThai == "PENDING_PAYMENT" && order.HetHanLuc <= now && financial.ConThu > 0,
                        "Chỉ chốt hết hạn đơn Web chờ thanh toán đã đến hạn và chưa thu đủ.");
                    Require(input.DaDoiChieuSaoKe && !string.IsNullOrWhiteSpace(input.ChungCu), "Cần Admin đối chiếu tiền đến hạn và chứng cứ sao kê, không suy thiếu tiền chỉ từ cache.");
                    var holds = await db.NguyenLieuChiTietDonHang.Where(x => lines.Select(l => l.MaChiTietDonHang).Contains(x.MaChiTietDonHang)).ToListAsync();
                    var error = await FourChang.Api.Features.Customer.Orders.OrderInventoryRelease.ReleaseAllUnconsumedAsync(db, holds, "EXPIRED", now, CancellationToken.None);
                    Require(error is null, error ?? "Không giải phóng được giữ chỗ.");
                    order.TrangThai = "EXPIRED"; order.MaXuLyNgoaiLe = "PAYMENT_TIMEOUT"; order.PhiGiaoHangHienTai = 0;
                    foreach (var payment in await db.ThanhToan.Where(x => x.MaDonHang == id).ToListAsync())
                    {
                        if (payment.SoTienDaXacMinh > 0 && !await db.HoSoHoanTien.AnyAsync(x => x.MaThanhToan == payment.MaThanhToan && x.TrangThai != "REJECTED"))
                            db.HoSoHoanTien.Add(new() { MaDonHang = id, MaThanhToan = payment.MaThanhToan,
                                LoaiNguon = "OVERPAYMENT", SoTienYeuCau = payment.SoTienDaXacMinh, PhuongThucHoan = "BANK_TRANSFER", TrangThai = "REQUESTED",
                                MaQuanLyYeuCau = actor, LyDoHoan = "Đơn hết hạn chờ thanh toán", ChungCuYeuCau = input.ChungCu });
                        payment.TrangThai = "EXPIRED";
                    }
                    break;
                case "ACCEPT":
                    Require(order.TrangThai == "PENDING_CONFIRMATION", "Đơn không còn chờ xác nhận.");
                    Require(order.KenhDatHang != "WEB" || financial.ConThu == 0, "Đơn Web chưa trả đủ tiền xác minh.");
                    Require(order.HetHanLuc is null || order.KenhDatHang != "WEB" || financial.ConThu == 0, "Đơn Web cần đối soát thời hạn.");
                    if (shift is not null && order.MaCaLamViec is null) order.MaCaLamViec = shift;
                    order.TrangThai = "ACCEPTED"; order.TiepNhanLuc = now; break;
                case "CANCEL":
                    Require(order.TrangThai is "PENDING_CONFIRMATION" or "ACCEPTED" or "PREPARING", "Không còn được hủy trước pha.");
                    if (!admin) await RequestCancellation(order, lines, input, shift!.Value);
                    else await Cancel(order, lines, input, actor, shift);
                    break;
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
                    Require((input.ResolutionCode?.Length ?? 0) <= 30, "Mã xử lý tối đa 30 ký tự.");
                    order.TrangThai = "DELIVERY_FAILED";
                    order.MaXuLyNgoaiLe = string.IsNullOrWhiteSpace(input.ResolutionCode) ? "DELIVERY_FAILED" : input.ResolutionCode; break;
                case "RETRY_DELIVERY":
                    Require(order.HinhThucNhanMon == "DELIVERY" && order.TrangThai == "DELIVERING", "Chỉ giao lại khi sự cố chưa được chốt kết thúc.");
                    Require(!string.IsNullOrWhiteSpace(input.ChungCu), "Cần xác nhận giao lại của khách và chứng cứ."); break;
                default: throw new AdminRule(400, "Hành động không hợp lệ.");
            }
            order.ThoiDiemCapNhat = now;
            db.SuKienDongBo.Add(new() { MaSuKien = Guid.NewGuid(), MaThaoTac = input.MaThaoTac, LoaiSuKien = "ORDER_CHANGED",
                NoiDungJson = JsonSerializer.Serialize(new { MaDonHang = id }), PhatSinhLuc = now });

            // Bước 3: Lưu trạng thái và sự kiện; giao dịch chung sẽ lưu kết quả chống gửi lặp.
            await db.SaveChangesAsync();
            return (id, (object)new OrderActionReceipt(id, order.TrangThai, order.PhienBanDuLieu), before, reason);
        }, allowStaff: true);
    }

    /// <summary>Nhân viên chỉ gửi phần chưa pha cho Quản lý: không giải phóng kho, đổi trạng thái đơn hay tạo hoàn.</summary>
    private async Task RequestCancellation(DonHang order,List<ChiTietDonHang> lines,OrderActionInput input,long shift)
    {
        var line=lines.SingleOrDefault(x=>x.MaChiTietDonHang==input.MaDong)??throw new AdminRule(400,"Chọn dòng cần yêu cầu hủy.");
        Require(input.SoLuong>0&&input.SoLuong<=line.SoLuongDat-line.SoLuongBatDauPha-line.SoLuongDaHuy,"Chỉ yêu cầu hủy số ly chưa bắt đầu pha.");
        db.HuyMon.Add(new(){MaNhomHuy=input.MaThaoTac,MaChiTietDonHang=line.MaChiTietDonHang,SoLuongHuy=input.SoLuong,
            LyDoHuy=input.LyDo,MaCaLamViec=shift,TrangThai="REQUESTED",TienHangGiam=0,PhiGiaoHangGiam=0});
        await db.SaveChangesAsync();
    }

    /// <summary>Hủy đúng phần chưa pha; giải phóng giữ chỗ và tạo yêu cầu hoàn, không ghi chi thành công.</summary>
    internal async Task Cancel(DonHang order, List<ChiTietDonHang> lines, OrderActionInput input, long? admin, long? shift, HuyMon? request = null)
    {
        var line = lines.SingleOrDefault(x => x.MaChiTietDonHang == input.MaDong) ?? throw new AdminRule(400, "Chọn dòng cần hủy.");
        Require(input.SoLuong > 0 && input.SoLuong <= line.SoLuongDat - line.SoLuongBatDauPha - line.SoLuongDaHuy, "Chỉ hủy số ly chưa bắt đầu pha.");
        var unitPrice = line.GiaMonDaChot + await db.MonThemChiTietDonHang.Where(x => x.MaChiTietDonHang == line.MaChiTietDonHang).SumAsync(x => (decimal?)x.GiaMoiPhanDaChot * x.SoPhanMoiLy) ?? line.GiaMonDaChot;
        var snapshots = await db.NguyenLieuChiTietDonHang.Where(x => x.MaChiTietDonHang == line.MaChiTietDonHang).ToListAsync();
        var holds = await db.NguyenLieuChiTietDonHang.Where(x => x.MaChiTietDonHang == line.MaChiTietDonHang).ToListAsync();
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
        var cancellation = request ?? new HuyMon { MaNhomHuy = input.MaThaoTac, MaChiTietDonHang = line.MaChiTietDonHang,
            SoLuongHuy = input.SoLuong, LyDoHuy = input.LyDo, MaQuanLyYeuCau = admin, MaCaLamViec = shift };
        cancellation.MaQuanLyDuyet=admin; cancellation.TrangThai="ACCEPTED";
        cancellation.TienHangGiam=unitPrice*input.SoLuong; cancellation.PhiGiaoHangGiam=shipping;
        cancellation.XuLyLuc=cancellation.ThoiDiemCapNhat=DateTime.UtcNow;
        if(request is null)db.HuyMon.Add(cancellation);
        await db.SaveChangesAsync();
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
            db.HoSoHoanTien.Add(new() { MaDonHang = order.MaDonHang,
                MaThanhToan = payment.MaThanhToan, MaHuyMon = cancellation.MaHuyMon, SoTienYeuCau = amount,
                PhuongThucHoan = payment.PhuongThuc, LoaiNguon = "CANCELLATION", ChungCuYeuCau = "Nguồn hủy " + cancellation.MaHuyMon,
                LyDoHoan = input.LyDo, TrangThai = "REQUESTED", MaQuanLyYeuCau = admin, MaCaYeuCau = shift }); needed -= amount;
        }
    }

    /// <summary>
    /// UC-POS-03 — Ghi nhận nguyên liệu dùng để pha món.
    ///
    /// Hàm này được dùng để:
    /// 1. Lấy phần nguyên liệu đã giữ cho dòng món.
    /// 2. Tính lượng cần dùng cho số ly bắt đầu pha.
    /// 3. Kiểm tra tồn thực tế và lượng đang giữ có đủ không.
    /// 4. Trừ lượng đã dùng khỏi tồn thực tế, đồng thời giảm lượng đang giữ.
    /// 5. Cập nhật lượng đã tiêu hao và ghi lịch sử biến động kho.
    ///
    /// Thông tin cần truyền vào:
    /// - line: Dòng món đang được chọn để bắt đầu pha.
    /// - quantity: Số ly sẽ bắt đầu pha trong lần thao tác này.
    /// - operation: Mã nhận biết lần thao tác này, giúp kiểm tra yêu cầu bị gửi lại.
    ///
    /// Cần nhớ: Không đủ nguyên liệu thì báo lỗi để bước xử lý không được hoàn tất. Bước “Pha xong” không gọi lại việc trừ nguyên liệu.
    /// </summary>
    private async Task Consume(ChiTietDonHang line, int quantity, Guid operation)
    {
        var holds = await db.NguyenLieuChiTietDonHang.Where(x => x.MaChiTietDonHang == line.MaChiTietDonHang).ToListAsync();
        Require(holds.Count > 0, "Thiếu giữ chỗ, cần đối soát trước pha.");
        foreach (var h in holds)
        {
            var remainingCups = line.SoLuongDat - line.SoLuongDaHuy - line.SoLuongBatDauPha;
            var remaining = h.SoLuongGiuCho - h.SoLuongDaTieuHao - h.SoLuongDaGiaiPhong;
            var use = quantity == remainingCups ? remaining : decimal.Ceiling(remaining * quantity / remainingCups * 10000m) / 10000m;
            var stock = await db.TonKho.SingleAsync(x => x.MaNguyenLieu == h.MaNguyenLieu);
            Require(use >= 0 && stock.TonThucTe >= use && stock.TongGiuCho >= use, "Kho/giữ chỗ không đủ, chưa thể pha.");

            // Đủ nguyên liệu mới trừ tồn thực tế và giảm lượng đang giữ tương ứng số ly bắt đầu pha.
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
