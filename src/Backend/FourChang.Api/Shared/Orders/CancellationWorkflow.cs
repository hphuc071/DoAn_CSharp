using FourChang.Api.Features.Staff.WorkSessions;
using FourChang.Api.Security;
using System.Security.Claims;
using System.Text.Json;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Api.Shared.Operations;
using FourChang.Api.Shared.WorkSessions;
using FourChang.Domain.Entities.Orders;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Orders;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Shared.Orders;

/// <summary>Khách đề nghị hoặc Staff gửi trực tiếp; Admin quyết định trước khi hủy và thay đổi tiền/kho.</summary>
public sealed class CancellationWorkflow(CafeDbContext db, OrderWorkflowService orders)
{
    /// <summary>Đọc nhóm yêu cầu đang chờ; trạng thái chuyển được chứng minh bằng audit gắn đúng nhóm hủy.</summary>
    public static async Task<CancellationInfo?> Pending(CafeDbContext db, long orderId)
    {
        var request = await (from c in db.HuyMon.AsNoTracking() join l in db.ChiTietDonHang on c.MaChiTietDonHang equals l.MaChiTietDonHang
            where l.MaDonHang == orderId && c.TrangThai == "REQUESTED"
            orderby c.MaHuyMon descending select c).FirstOrDefaultAsync();
        if (request is null) return null;
        var receipts = await db.NhatKyHeThong.AsNoTracking().Where(x => x.LoaiDoiTuong == "DonHang" && x.MaDoiTuong == orderId.ToString() &&
            x.HanhDong == "CANCELLATION_FORWARD" && x.KetQuaDaChotJson != null).Select(x => x.KetQuaDaChotJson!).ToListAsync();
        var forwarded = request.MaCaLamViec is not null && request.MaKhachHangYeuCau is null || receipts.Any(json => JsonSerializer.Deserialize<CancellationInfo>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))?.MaNhomHuy == request.MaNhomHuy);
        return new(request.MaNhomHuy, forwarded ? "FORWARDED" : "REQUESTED", request.LyDoHuy);
    }

    /// <summary>Cho khách xem quyết định gần nhất, không nhầm một yêu cầu cũ đã từ chối với yêu cầu mới đang chờ.</summary>
    public static async Task<CancellationInfo?> LatestDecision(CafeDbContext db, long orderId)
    {
        var last = await (from c in db.HuyMon.AsNoTracking() join l in db.ChiTietDonHang on c.MaChiTietDonHang equals l.MaChiTietDonHang
            where l.MaDonHang == orderId orderby c.MaHuyMon descending select c).FirstOrDefaultAsync();
        if (last is null || last.TrangThai == "REQUESTED") return null;
        var reason = last.LyDoHuy;
        if (last.TrangThai == "REJECTED")
        {
            var decisions = await db.NhatKyHeThong.AsNoTracking().Where(x => x.LoaiDoiTuong == "DonHang" && x.MaDoiTuong == orderId.ToString() &&
                x.HanhDong == "CANCELLATION_REJECT" && x.KetQuaDaChotJson != null).OrderByDescending(x => x.MaNhatKy).ToListAsync();
            reason = decisions.FirstOrDefault(x => JsonSerializer.Deserialize<CancellationInfo>(x.KetQuaDaChotJson!, new JsonSerializerOptions(JsonSerializerDefaults.Web))?.MaNhomHuy == last.MaNhomHuy)?.LyDo ?? reason;
        }
        return new(last.MaNhomHuy, last.TrangThai == "ACCEPTED" ? "APPROVED" : "REJECTED", reason);
    }

    /// <summary>Lệnh có version và mã chống trùng; chuyển không hủy, từ chối không sửa kho hoặc phát sinh hoàn.</summary>
    public Task<IResult> Execute(long id, CancellationCommand input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "CANCELLATION_" + input.HanhDong, "DonHang", new { id, input }, context, async () =>
        {
            var admin = context.User.IsInRole("ADMIN");
            if (admin ? input.HanhDong is not ("APPROVE" or "REJECT") : input.HanhDong != "FORWARD")
                throw new AdminRule(403, "Nhân viên chỉ chuyển yêu cầu; Quản lý duyệt hoặc từ chối.");
            var order = await db.DonHang.SingleOrDefaultAsync(x => x.MaDonHang == id) ?? throw new AdminRule(404, "Không tìm thấy đơn.");
            AdminOperation.Version(input.PhienBanDuLieu, order.PhienBanDuLieu); AdminOperation.Reason(input.LyDo);
            var pending = await Pending(db, id);
            var staffRequest=await db.HuyMon.AnyAsync(x=>x.MaNhomHuy==input.MaNhomHuy&&x.MaCaLamViec!=null&&x.MaKhachHangYeuCau==null);
            if (pending is null || pending.MaNhomHuy != input.MaNhomHuy ||
                (staffRequest ? order.TrangThai is not ("PENDING_CONFIRMATION" or "ACCEPTED" or "PREPARING") : order.TrangThai != "PENDING_CONFIRMATION"))
                throw new AdminRule(409, "Yêu cầu hoặc trạng thái đơn đã thay đổi.");
            if (input.HanhDong == "FORWARD" && pending.TrangThai != "REQUESTED" || admin && pending.TrangThai != "FORWARDED")
                throw new AdminRule(409, "Yêu cầu phải được Nhân viên chuyển đúng một lần trước khi Quản lý quyết định.");
            var before = JsonSerializer.Serialize(pending);
            var requests = await db.HuyMon.Where(x => x.MaNhomHuy == input.MaNhomHuy).ToListAsync();
            var now = DateTime.UtcNow;
            if (admin)
            {
                var actor = long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                if (input.HanhDong == "APPROVE")
                {
                    if (!input.DaKiemTraNguoiNhan) throw new AdminRule(400, "Cần xác minh người nhận trước duyệt hoàn tại quầy.");
                    if(staffRequest)
                    {
                        // Duyệt đúng phần nhân viên đã yêu cầu, dùng chung tính giá/kho và chống hoàn trùng.
                        if(requests.Count!=1)throw new AdminRule(409,"Nhóm yêu cầu nhân viên không hợp lệ.");
                        var request=requests[0];
                        var lines=await db.ChiTietDonHang.Where(x=>x.MaDonHang==id).ToListAsync();
                        await orders.Cancel(order,lines,new("CANCEL",request.LyDoHuy,order.PhienBanDuLieu,input.MaThaoTac,request.MaChiTietDonHang,request.SoLuongHuy),actor,null,request);
                        await db.SaveChangesAsync(); // Lưu hồ sơ mới trong transaction trước khi đọc để duyệt hạn mức.
                        var refunds=await db.HoSoHoanTien.Where(x=>x.MaHuyMon==request.MaHuyMon&&x.TrangThai=="REQUESTED").ToListAsync();
                        foreach(var refund in refunds)
                        {
                            refund.TrangThai="APPROVED";refund.SoTienDuocDuyet=refund.SoTienYeuCau;refund.MaQuanLyDuyet=actor;refund.DuyetLuc=now;
                            refund.PhuongThucHoan="CASH";refund.TenNguoiNhan=order.TenNguoiNhan??"Khách lẻ";
                        }
                    }
                    else await Approve(order, requests, actor, now);
                }
                foreach (var request in requests)
                {
                    request.TrangThai = input.HanhDong == "APPROVE" ? "ACCEPTED" : "REJECTED";
                    request.MaQuanLyDuyet = actor; request.XuLyLuc = request.ThoiDiemCapNhat = now;
                }
            }
            // Bump rowversion cả khi chuyển/từ chối, ngăn cửa sổ cũ ghi đè hoặc tiếp nhận trong lúc chờ.
            order.ThoiDiemCapNhat = now;
            db.SuKienDongBo.Add(new() { MaSuKien = Guid.NewGuid(), MaThaoTac = input.MaThaoTac, LoaiSuKien = "ORDER_CHANGED", NoiDungJson = JsonSerializer.Serialize(new { MaDonHang = id }), PhatSinhLuc = now });
            await db.SaveChangesAsync();
            var state = input.HanhDong switch { "FORWARD" => "FORWARDED", "APPROVE" => "APPROVED", _ => "REJECTED" };
            return (id, (object)new CancellationInfo(input.MaNhomHuy, state, input.LyDo), before, input.LyDo);
        }, allowStaff: true);

    /// <summary>Hủy toàn bộ nhóm đã xác minh trong cùng transaction: giữ giá chốt, giải phóng giữ chỗ, duyệt tiền mặt còn được hoàn.</summary>
    private async Task Approve(DonHang order, List<HuyMon> requests, long admin, DateTime now)
    {
        var lines = await db.ChiTietDonHang.Where(x => x.MaDonHang == order.MaDonHang).OrderBy(x => x.MaChiTietDonHang).ToListAsync();
        if (lines.Count != requests.Count || lines.Any(x => x.SoLuongBatDauPha != 0 || x.SoLuongDaHuy != 0 || x.SoLuongPhaXong != 0 ||
            !requests.Any(r => r.MaChiTietDonHang == x.MaChiTietDonHang && r.SoLuongHuy == x.SoLuongDat)))
            throw new AdminRule(409, "Dòng món đã thay đổi; không được duyệt hủy trên dữ liệu cũ.");
        if (await db.HoSoHoanTien.AnyAsync(x => x.MaDonHang == order.MaDonHang && x.TrangThai != "REJECTED"))
            throw new AdminRule(409, "Đơn có hồ sơ hoàn khác; cần Quản lý đối soát trước.");
        var ids = lines.Select(x => x.MaChiTietDonHang).ToArray();
        var holds = await db.GiuChoNguyenLieu.Where(x => ids.Contains(x.MaChiTietDonHang)).ToListAsync();
        if (holds.Count == 0 || ids.Any(id => !holds.Any(x => x.MaChiTietDonHang == id))) throw new AdminRule(409, "Thiếu dữ liệu giữ chỗ.");
        var error = await OrderInventoryRelease.ReleaseAllUnconsumedAsync(db, holds, "RELEASED", now, CancellationToken.None);
        if (error is not null) throw new AdminRule(409, error);
        foreach (var line in lines)
        {
            var request = requests.Single(x => x.MaChiTietDonHang == line.MaChiTietDonHang);
            var topping = await db.MonThemChiTietDonHang.Where(x => x.MaChiTietDonHang == line.MaChiTietDonHang).SumAsync(x => (decimal?)x.GiaMoiPhanDaChot * x.SoPhanMoiLy) ?? 0;
            request.TienHangGiam = (line.GiaMonDaChot + topping) * line.SoLuongDat;
            request.PhiGiaoHangGiam = line == lines[0] ? order.PhiGiaoHangHienTai : 0;
            line.SoLuongDaHuy = line.SoLuongDat; line.ThoiDiemCapNhat = now;
        }
        var customer = await db.TaiKhoanKhachHang.SingleAsync(x => x.MaKhachHang == order.MaKhachHang);
        foreach (var payment in await db.ThanhToan.Where(x => x.MaDonHang == order.MaDonHang && x.SoTienDaXacMinh > 0).ToListAsync())
        {
            db.HoSoHoanTien.Add(new() { MaHoanTienHienThi = "HT-" + Guid.NewGuid().ToString("N"), MaDonHang = order.MaDonHang,
                MaThanhToan = payment.MaThanhToan, MaHuyMon = requests[0].MaHuyMon, LoaiNguon = "CANCELLATION",
                LyDoHoan = requests[0].LyDoHuy, ChungCuYeuCau = "Yêu cầu khách / nhóm " + requests[0].MaNhomHuy,
                SoTienYeuCau = payment.SoTienDaXacMinh, SoTienDuocDuyet = payment.SoTienDaXacMinh,
                PhuongThucHoan = "CASH", TenNguoiNhan = customer.HoTen, TrangThai = "APPROVED",
                MaKhachHangYeuCau = order.MaKhachHang, MaQuanLyDuyet = admin, DuyetLuc = now });
        }
        order.TrangThai = "CANCELLED"; order.PhiGiaoHangHienTai = 0; order.HoanTatLuc = now;
    }

    /// <summary>Danh sách chi tại quầy không chứa tài khoản ngân hàng; chỉ trả hạn mức tiền mặt còn được duyệt.</summary>
    public async Task<IReadOnlyList<CounterRefund>> CounterRefunds(long orderId)
    {
        var records = await db.HoSoHoanTien.AsNoTracking().Where(x => x.MaDonHang == orderId && x.PhuongThucHoan == "CASH" &&
            (x.TrangThai == "APPROVED" || x.TrangThai == "PARTIALLY_REFUNDED")).ToListAsync();
        var result = new List<CounterRefund>();
        foreach (var record in records)
        {
            if (await db.LanChiHoanTien.AnyAsync(x => x.MaHoanTien == record.MaHoanTien && (x.TrangThai == "PENDING" || x.TrangThai == "RECONCILING"))) continue;
            var paid = await db.LanChiHoanTien.Where(x => x.MaHoanTien == record.MaHoanTien && x.TrangThai == "SUCCEEDED").SumAsync(x => (decimal?)x.SoTien) ?? 0;
            if (record.SoTienDuocDuyet > paid) result.Add(new(record.MaHoanTien, record.MaHoanTienHienThi, record.TenNguoiNhan ?? "", record.SoTienDuocDuyet!.Value - paid, record.PhienBanDuLieu));
        }
        return result;
    }

    /// <summary>Nhân viên xác nhận đã giao tiền thật cho khách; ghi lần chi gắn đúng ca và audit nguyên tử, không tự tạo hạn mức.</summary>
    public Task<IResult> PayAtCounter(long refundId, CounterRefundCommand input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "COUNTER_REFUND", "HoSoHoanTien", new { refundId, input }, context, async () =>
        {
            if (!context.User.IsInRole("STAFF")) throw new AdminRule(403, "Chỉ Nhân viên đang làm ca được ghi nhận chi tại quầy.");
            AdminOperation.Reason(input.ChungCu);
            if (!input.DaGiaoTienVaKhachDaNhan || string.IsNullOrWhiteSpace(input.NguoiNhan) || input.NguoiNhan.Length > 120 || input.NguoiNhan.Any(char.IsControl)) throw new AdminRule(400, "Xác minh người nhận và xác nhận đã giao tiền thực tế.");
            var refund = await db.HoSoHoanTien.SingleOrDefaultAsync(x => x.MaHoanTien == refundId) ?? throw new AdminRule(404, "Không tìm thấy hồ sơ.");
            AdminOperation.Version(input.PhienBanDuLieu, refund.PhienBanDuLieu);
            var remaining = (await CounterRefunds(refund.MaDonHang)).SingleOrDefault(x => x.MaHoanTien == refundId);
            if (remaining is null || refund.MaQuanLyDuyet is null || !EmployeeIdentity.Matches(refund.TenNguoiNhan ?? "", input.NguoiNhan))
                throw new AdminRule(409, "Hồ sơ chưa được duyệt chi tiền mặt, người nhận không khớp hoặc đã hoàn.");
            var shiftId = long.Parse(context.User.FindFirst("shift")!.Value);
            var shift = await db.CaLamViec.SingleAsync(x => x.MaCaLamViec == shiftId && x.TrangThai == "OPEN");
            var cashPending = await db.LanChiHoanTien.Where(x => x.MaCaLamViec == shiftId && x.PhuongThuc == "CASH" &&
                (x.TrangThai == "PENDING" || x.TrangThai == "RECONCILING")).SumAsync(x => (decimal?)x.SoTien) ?? 0;
            if (await ShiftCash.Expected(db, shift) - cashPending < remaining.SoTien) throw new AdminRule(409, "Quỹ ca không đủ tiền để hoàn. Liên hệ Quản lý.");
            var before = JsonSerializer.Serialize(new { refund.TrangThai, remaining.SoTien });
            var now = DateTime.UtcNow;
            db.LanChiHoanTien.Add(new() { MaHoanTien = refundId, SoThuTuLanChi = (await db.LanChiHoanTien.Where(x => x.MaHoanTien == refundId).MaxAsync(x => (int?)x.SoThuTuLanChi) ?? 0) + 1,
                MaChongChiLap = input.MaThaoTac, SoTien = remaining.SoTien, PhuongThuc = "CASH", NhaCungCap = "COUNTER_CASH",
                TrangThai = "SUCCEEDED", BatDauLuc = now, CoKetQuaLuc = now, MaCaLamViec = shiftId, ThamChieuChungCu = input.ChungCu });
            refund.TrangThai = "REFUNDED"; refund.ThoiDiemCapNhat = now;
            await db.SaveChangesAsync();
            return (refundId, (object)new { maHoanTien = refundId, trangThai = "REFUNDED", soTien = remaining.SoTien, maCa = shiftId }, before, input.ChungCu);
        }, allowStaff: true);
}
