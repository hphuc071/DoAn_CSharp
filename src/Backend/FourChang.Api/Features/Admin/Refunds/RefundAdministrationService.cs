using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FourChang.Api.Shared.Operations;
using FourChang.Api.Shared.Orders;
using FourChang.Domain.Entities.Payments;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Refunds;

/// <summary>UC-ADM-09: Admin duy nhất duyệt/chi/đối chiếu sao kê. Không giả tích hợp API chi ngân hàng.</summary>
public sealed class RefundAdministrationService(CafeDbContext db, OrderFinancials financials, IDataProtectionProvider protection)
{
    /// <summary>Đọc danh sách và từng lần chi; chỉ trả tài khoản đã che, không trả bí mật mã hóa.</summary>
    public async Task<IReadOnlyList<RefundView>> Read()
    {
        var rows = await db.HoSoHoanTien.AsNoTracking().OrderByDescending(x => x.MaHoanTien).ToListAsync();
        var attempts = await db.LanChiHoanTien.AsNoTracking().ToListAsync();
        return rows.Select(x => View(x, attempts.Where(a => a.MaHoanTien == x.MaHoanTien))).ToList();
    }

    /// <summary>Hiển thị riêng thu gốc, đã hoàn và hạn mức để không duyệt theo tổng hóa đơn chưa thu.</summary>
    public async Task<RefundDetail> Details(long id)
    {
        var record = (await Read()).SingleOrDefault(x => x.MaHoanTien == id) ?? throw new AdminRule(404, "Không tìm thấy hồ sơ hoàn.");
        var order = await db.DonHang.AsNoTracking().SingleAsync(x => x.MaDonHang == record.MaDonHang);
        var payment = await db.ThanhToan.AsNoTracking().SingleAsync(x => x.MaThanhToan == record.MaThanhToan);
        var returned = await Returned(payment.MaThanhToan);
        var history = await db.NhatKyHeThong.AsNoTracking().Where(x => x.LoaiDoiTuong == "HoSoHoanTien" && x.MaDoiTuong == id.ToString()).OrderByDescending(x => x.PhatSinhLuc).ToListAsync();
        return new(record, await financials.Read(order), payment.SoTienDaXacMinh, returned,
            history.Select(x => new AdminTraceRow(x.MaNhatKy, x.PhatSinhLuc, x.TenNguoiThucHienDaChot, x.MaCaLamViec, x.MaMayDaChot,
                x.HanhDong, x.LyDo, x.KetQua, x.DuLieuTruocJson, x.DuLieuSauJson, x.MaLienKetXuLy)).ToList());
    }

    /// <summary>Lập/bổ sung hồ sơ có nguồn, chứng cứ và người nhận; tài khoản được mã hóa và không xuất hiện trong audit.</summary>
    public Task<IResult> Save(long? id, RefundRequestInput input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "REFUND_REQUEST", "HoSoHoanTien", new { id, input }, context, async () =>
        {
            AdminOperation.Reason(input.LyDo); Evidence(input.ChungCu); Amount(input.SoTien);
            if (input.LoaiNguon is not ("CANCELLATION" or "OVERPAYMENT" or "LATE_PAYMENT" or "COMPLAINT")) throw Bad("Chọn nguồn hoàn cụ thể; hồ sơ LEGACY phải được đối soát.");
            var payment = await db.ThanhToan.SingleOrDefaultAsync(x => x.MaThanhToan == input.MaThanhToan && x.MaDonHang == input.MaDonHang && x.SoTienDaXacMinh > 0)
                ?? throw Bad("Không có khoản thu gốc đã xác minh thuộc đơn.");
            if (input.SoTien > payment.SoTienDaXacMinh) throw Bad("Yêu cầu vượt khoản thu gốc.");
            var order = await db.DonHang.SingleAsync(x => x.MaDonHang == input.MaDonHang);
            if (input.LoaiNguon == "COMPLAINT" && order.TrangThai is not ("COMPLETED" or "DELIVERY_FAILED")) throw Bad("Hoàn khiếu nại cần đơn đã phục vụ hoặc chốt giao thất bại.");
            if (input.LoaiNguon == "CANCELLATION" && !await (from c in db.HuyMon join l in db.ChiTietDonHang on c.MaChiTietDonHang equals l.MaChiTietDonHang
                where c.MaHuyMon == input.MaHuyMon && c.TrangThai == "ACCEPTED" && l.MaDonHang == input.MaDonHang select c).AnyAsync()) throw Bad("Nguồn hủy không hợp lệ.");
            if (input.PhuongThuc is not ("CASH" or "BANK_TRANSFER") || (order.KenhDatHang == "WEB" && input.PhuongThuc != "BANK_TRANSFER")) throw Bad("Hoàn tiền mặt cho đơn Web phải đi qua luồng duyệt hủy và hoàn tại quầy.");
            if (string.IsNullOrWhiteSpace(input.NguoiNhan) || input.NguoiNhan.Length > 120) throw Bad("Nhập người nhận tối đa 120 ký tự.");
            if (input.PhuongThuc == "BANK_TRANSFER" && (string.IsNullOrWhiteSpace(input.TaiKhoan) || input.TaiKhoan.Length > 50 || input.TaiKhoan.Any(char.IsControl) || string.IsNullOrWhiteSpace(input.NganHang) || input.NganHang.Length > 100)) throw Bad("Nhập thông tin ngân hàng/người nhận hợp lệ.");
            var record = id is null ? new HoSoHoanTien { MaHoanTienHienThi = "HT-" + Guid.NewGuid().ToString("N"), MaQuanLyYeuCau = Actor(context), TrangThai = "REQUESTED" }
                : await Find(id.Value);
            var before = id is null ? null : JsonSerializer.Serialize(View(record, []));
            if (id is not null)
            {
                AdminOperation.Version(input.PhienBanDuLieu, record.PhienBanDuLieu);
                if (record.TrangThai is not ("REQUESTED" or "REJECTED")) throw new AdminRule(409, "Không sửa hồ sơ đã duyệt hoặc đang chi.");
                if (await db.HoaDon.AnyAsync(x => x.MaHoanTien == id && x.TrangThai == "ISSUED") && input.LoaiNguon != record.LoaiNguon)
                    throw new AdminRule(409, "Không đổi nguồn hồ sơ đã có hóa đơn điều chỉnh.");
                if (record.MaDonHang != input.MaDonHang || record.MaThanhToan != input.MaThanhToan) throw Bad("Không đổi khoản thu gốc của hồ sơ cũ.");
            }
            record.MaDonHang = input.MaDonHang; record.MaThanhToan = input.MaThanhToan; record.MaHuyMon = input.MaHuyMon;
            record.LoaiNguon = input.LoaiNguon; record.ChungCuYeuCau = input.ChungCu.Trim(); record.SoTienYeuCau = input.SoTien;
            record.LyDoHoan = input.LyDo.Trim(); record.PhuongThucHoan = input.PhuongThuc; record.TenNguoiNhan = input.NguoiNhan.Trim();
            record.NganHangNguoiNhan = input.NganHang?.Trim(); record.TrangThai = "REQUESTED";
            if (input.PhuongThuc == "BANK_TRANSFER")
            {
                var account = input.TaiKhoan!.Trim(); record.TaiKhoanNhanDaChe = "****" + account[^Math.Min(4, account.Length)..];
                record.TaiKhoanNhanMaHoa = protection.CreateProtector("FourChang.RefundRecipient.v1").Protect(Encoding.UTF8.GetBytes(account));
            }
            else { record.TaiKhoanNhanDaChe = null; record.TaiKhoanNhanMaHoa = null; }
            record.ThoiDiemCapNhat = DateTime.UtcNow;
            if (id is null) db.HoSoHoanTien.Add(record);
            await db.SaveChangesAsync(); return Receipt(record, before, input.LyDo);
        }, auditInput: new { id, input.MaDonHang, input.MaThanhToan, input.MaHuyMon, input.LoaiNguon, input.SoTien, input.PhuongThuc,
            input.NguoiNhan, input.NganHang, TaiKhoan = "[ĐÃ CHE]", input.LyDo, input.ChungCu });

    /// <summary>Duyệt giữ hạn mức nguyên tử; từ chối có lý do không xóa nghĩa vụ hoàn thật của đơn.</summary>
    public Task<IResult> Decide(long id, RefundDecisionInput input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "REFUND_DECIDE", "HoSoHoanTien", new { id, input }, context, async () =>
        {
            var record = await Find(id); AdminOperation.Version(input.PhienBanDuLieu, record.PhienBanDuLieu); AdminOperation.Reason(input.LyDo);
            if (record.TrangThai != "REQUESTED") throw new AdminRule(409, "Hồ sơ không còn chờ duyệt.");
            var before = JsonSerializer.Serialize(View(record, []));
            if (!input.Duyet) { record.TrangThai = "REJECTED"; }
            else
            {
                Amount(input.SoTien); Evidence(record.ChungCuYeuCau);
                if (record.LoaiNguon == "LEGACY" || !input.DaKiemTraNguoiNhan || string.IsNullOrWhiteSpace(record.TenNguoiNhan) ||
                    record.PhuongThucHoan == "BANK_TRANSFER" && record.TaiKhoanNhanMaHoa is null) throw Bad("Cần đối soát nguồn và xác minh người nhận trước duyệt.");
                var payment = await db.ThanhToan.SingleAsync(x => x.MaThanhToan == record.MaThanhToan);
                var order = await db.DonHang.SingleAsync(x => x.MaDonHang == record.MaDonHang);
                var balance = await financials.Read(order);
                var heldForPayment = await Held(payment.MaThanhToan);
                var limit = payment.SoTienDaXacMinh - await Returned(payment.MaThanhToan) - heldForPayment;
                var issuedReduction = -(await db.HoaDon.Where(x => x.MaHoanTien == id && x.TrangThai == "ISSUED").SumAsync(x => (decimal?)x.SoTien) ?? 0);
                if (issuedReduction > 0 && input.SoTien != issuedReduction) throw new AdminRule(409, "Duyệt lại đúng khoản điều chỉnh đã phát hành; không thay số tiền chứng từ gốc.");
                var liability = issuedReduction > 0 ? balance.PhaiHoan - balance.DangGiuHanMuc : record.LoaiNguon == "COMPLAINT" ? balance.NghiaVu : Math.Max(0, balance.PhaiHoan - balance.DangGiuHanMuc);
                if (input.SoTien > record.SoTienYeuCau || input.SoTien > limit || input.SoTien > liability) throw new AdminRule(409, "Số tiền vượt hạn mức còn hoàn hoặc nghĩa vụ được phép điều chỉnh.");
                record.SoTienDuocDuyet = input.SoTien; record.TrangThai = "APPROVED";
                record.MaQuanLyDuyet = Actor(context); record.DuyetLuc = DateTime.UtcNow;
            }
            record.ThoiDiemCapNhat = DateTime.UtcNow; Emit(input.MaThaoTac, record.MaDonHang);
            await db.SaveChangesAsync(); return Receipt(record, before, input.LyDo);
        });

    /// <summary>Ghi nhận lần chi đang xử lý, chưa tăng tiền hoàn; không tạo lần chi mới khi kết quả cũ chưa rõ.</summary>
    public Task<IResult> Begin(long id, RefundAttemptInput input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "REFUND_BEGIN", "HoSoHoanTien", new { id, input }, context, async () =>
        {
            var record = await Find(id); AdminOperation.Version(input.PhienBanDuLieu, record.PhienBanDuLieu); Evidence(input.ChungCu); Amount(input.SoTien);
            if (record.TrangThai is not ("APPROVED" or "PARTIALLY_REFUNDED") || !input.DaKiemTraNguoiNhan) throw new AdminRule(409, "Hồ sơ chưa được duyệt/xác nhận người nhận.");
            var attempts = await db.LanChiHoanTien.Where(x => x.MaHoanTien == id).ToListAsync();
            if (attempts.Any(x => x.TrangThai is "PENDING" or "RECONCILING")) throw new AdminRule(409, "Lần chi trước chưa rõ; đối soát mã cũ, không chi lại.");
            if (record.SoTienDuocDuyet is not { } approved || input.SoTien > approved - attempts.Where(x => x.TrangThai == "SUCCEEDED").Sum(x => x.SoTien)) throw Bad("Vượt số tiền đã duyệt còn chi.");
            if (input.ThoiDiemChi.Kind == DateTimeKind.Unspecified || input.ThoiDiemChi.ToUniversalTime() > DateTime.UtcNow || input.ThoiDiemChi.Year < 2000) throw Bad("Thời điểm chi cần múi giờ và không ở tương lai.");
            if (record.PhuongThucHoan == "BANK_TRANSFER" && (string.IsNullOrWhiteSpace(input.MaGiaoDich) || input.MaGiaoDich.Length > 150 || input.MaGiaoDich.Any(char.IsControl))) throw Bad("Cần mã giao dịch chi ngân hàng.");
            if (input.MaGiaoDich is { } reference && await db.LanChiHoanTien.AnyAsync(x => x.MaThamChieuBenNgoai == reference && x.TrangThai != "FAILED")) throw new AdminRule(409, "Mã giao dịch đã dùng cho lần chi khác.");
            if (record.PhuongThucHoan == "CASH" && (input.MaCa is null || !await db.CaLamViec.AnyAsync(x => x.MaCaLamViec == input.MaCa && x.TrangThai == "OPEN"))) throw Bad("Hoàn tiền mặt phải gắn ca đang mở.");
            var before = JsonSerializer.Serialize(View(record, attempts));
            db.LanChiHoanTien.Add(new() { MaHoanTien = id, SoThuTuLanChi = attempts.Count + 1, MaChongChiLap = input.MaThaoTac,
                SoTien = input.SoTien, PhuongThuc = record.PhuongThucHoan, NhaCungCap = "MANUAL_STATEMENT", MaThamChieuBenNgoai = input.MaGiaoDich?.Trim(),
                TrangThai = "PENDING", BatDauLuc = input.ThoiDiemChi.ToUniversalTime(), MaCaLamViec = input.MaCa, ThamChieuChungCu = input.ChungCu });
            record.TrangThai = "PROCESSING"; record.ThoiDiemCapNhat = DateTime.UtcNow;
            await db.SaveChangesAsync(); return Receipt(record, before, "Ghi nhận đang chi; chưa xác minh thành công");
        });

    /// <summary>Admin đối chiếu sao kê/phiếu chi thật ở bước riêng; chưa rõ giữ hạn mức, thất bại chắc chắn mới giải phóng.</summary>
    public Task<IResult> Verify(long attemptId, RefundVerificationInput input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "REFUND_VERIFY", "HoSoHoanTien", new { attemptId, input }, context, async () =>
        {
            var attempt = await db.LanChiHoanTien.SingleOrDefaultAsync(x => x.MaLanChiHoan == attemptId) ?? throw new AdminRule(404, "Không tìm thấy lần chi.");
            AdminOperation.Version(input.PhienBanDuLieu, attempt.PhienBanDuLieu);
            if (attempt.TrangThai is not ("PENDING" or "RECONCILING")) throw new AdminRule(409, "Lần chi đã có kết quả chắc chắn.");
            var record = await Find(attempt.MaHoanTien); var before = JsonSerializer.Serialize(View(record, [attempt]));
            if (input.KetQua is not ("SUCCEEDED" or "FAILED" or "RECONCILING")) throw Bad("Kết quả đối soát không hợp lệ.");
            Evidence(input.ChungCuSaoKe);
            if (input.KetQua != "RECONCILING" && !input.QuanLyDaDoiChieu) throw Bad("Quản lý phải đối chiếu sao kê/phiếu chi; nhập mã giao dịch không đủ chứng minh đã chi.");
            if (input.KetQua == "SUCCEEDED")
            {
                if (attempt.PhuongThuc == "BANK_TRANSFER")
                {
                    if (string.IsNullOrWhiteSpace(input.TaiKhoanQuanAlias) || input.TaiKhoanQuanAlias.Length > 100) throw Bad("Cần định danh tài khoản quán trên sao kê.");
                    if (await db.GiaoDichNganHang.AnyAsync(x => x.MaGiaoDichBenNgoai == attempt.MaThamChieuBenNgoai)) throw new AdminRule(409, "Giao dịch ngân hàng đã được sử dụng; không tăng tiền hoàn lần nữa.");
                    db.GiaoDichNganHang.Add(new() { TenNhaCungCap = "MANUAL_STATEMENT", MaTaiKhoanNganHangQuan = input.TaiKhoanQuanAlias,
                        MaGiaoDichBenNgoai = attempt.MaThamChieuBenNgoai!, ChieuGiaoDich = "OUT", SoTien = attempt.SoTien,
                        ThoiDiemNganHang = attempt.BatDauLuc, TrangThaiXacMinh = "VERIFIED", XacMinhLuc = DateTime.UtcNow,
                        MaLanChiHoan = attemptId, ThamChieuChungCu = input.ChungCuSaoKe });
                }
                attempt.TrangThai = "SUCCEEDED"; attempt.CoKetQuaLuc = DateTime.UtcNow; attempt.MaQuanLyXacMinh = Actor(context);
                var previous = await db.LanChiHoanTien.Where(x => x.MaHoanTien == record.MaHoanTien && x.MaLanChiHoan != attemptId && x.TrangThai == "SUCCEEDED").SumAsync(x => (decimal?)x.SoTien) ?? 0;
                record.TrangThai = previous + attempt.SoTien == record.SoTienDuocDuyet ? "REFUNDED" : "PARTIALLY_REFUNDED";
            }
            else if (input.KetQua == "FAILED")
            {
                AdminOperation.Reason(input.LyDoThatBai); attempt.TrangThai = "FAILED"; attempt.LyDoThatBai = input.LyDoThatBai;
                attempt.CoKetQuaLuc = DateTime.UtcNow; attempt.MaQuanLyXacMinh = Actor(context);
                // Giải phóng phần chưa chi để duyệt lại; lịch sử duyệt/chi cũ còn nguyên trong audit.
                var alreadyPaid = await db.LanChiHoanTien.Where(x => x.MaHoanTien == record.MaHoanTien && x.MaLanChiHoan != attemptId && x.TrangThai == "SUCCEEDED").SumAsync(x => (decimal?)x.SoTien) ?? 0;
                if (alreadyPaid == 0) { record.TrangThai = "REQUESTED"; record.SoTienDuocDuyet = null; }
                else record.TrangThai = "PARTIALLY_REFUNDED"; // Giữ hạn mức còn lại khi hồ sơ đã chi một phần, cho đối soát/chi tiếp.
            }
            else { attempt.TrangThai = "RECONCILING"; record.TrangThai = "RECONCILING"; }
            attempt.ThamChieuChungCu = input.ChungCuSaoKe; attempt.ThoiDiemCapNhat = DateTime.UtcNow; record.ThoiDiemCapNhat = DateTime.UtcNow;
            Emit(input.MaThaoTac, record.MaDonHang); await db.SaveChangesAsync();
            return Receipt(record, before, "Đối chiếu thủ công bởi Quản lý; không phải xác nhận tự động của API ngân hàng");
        });

    /// <summary>Đếm chi thành công theo khoản thu gốc, không cộng pending/failed vào R.</summary>
    private async Task<decimal> Returned(long payment) => await (from a in db.LanChiHoanTien join r in db.HoSoHoanTien on a.MaHoanTien equals r.MaHoanTien
        where r.MaThanhToan == payment && a.TrangThai == "SUCCEEDED" select (decimal?)a.SoTien).SumAsync() ?? 0;
    /// <summary>Giữ cả APPROVED/PROCESSING/RECONCILING; trừ phần đã chi khỏi phần còn đang giữ.</summary>
    private async Task<decimal> Held(long payment)
    {
        var ids = await db.HoSoHoanTien.Where(x => x.MaThanhToan == payment && (x.TrangThai == "APPROVED" || x.TrangThai == "PROCESSING" || x.TrangThai == "RECONCILING" || x.TrangThai == "PARTIALLY_REFUNDED")).ToListAsync();
        var keys = ids.Select(x => x.MaHoanTien).ToArray();
        var paid = await db.LanChiHoanTien.Where(x => keys.Contains(x.MaHoanTien) && x.TrangThai == "SUCCEEDED").SumAsync(x => (decimal?)x.SoTien) ?? 0;
        return Math.Max(0, ids.Sum(x => x.SoTienDuocDuyet ?? 0) - paid);
    }
    /// <summary>DTO che tài khoản, tách hồ sơ và từng lần chi để người xem không nhầm PROCESSING là đã hoàn.</summary>
    private static RefundView View(HoSoHoanTien x, IEnumerable<LanChiHoanTien> attempts) => new(x.MaHoanTien, x.MaHoanTienHienThi, x.MaDonHang,
        x.MaThanhToan, x.MaHuyMon, x.LoaiNguon, x.SoTienYeuCau, x.SoTienDuocDuyet, x.TrangThai, x.PhuongThucHoan, x.TenNguoiNhan,
        x.NganHangNguoiNhan, x.TaiKhoanNhanDaChe, x.LyDoHoan, x.ChungCuYeuCau, x.PhienBanDuLieu,
        attempts.OrderBy(x => x.SoThuTuLanChi).Select(a => new RefundAttemptView(a.MaLanChiHoan, a.SoThuTuLanChi, a.SoTien, a.PhuongThuc, a.TrangThai,
            a.MaThamChieuBenNgoai, a.BatDauLuc, a.CoKetQuaLuc, a.ThamChieuChungCu, a.PhienBanDuLieu)).ToList());
    /// <summary>Biên nhận chống trùng không chứa thông tin ngân hàng nhạy cảm.</summary>
    private static (long, object, string?, string?) Receipt(HoSoHoanTien record, string? before, string? reason) => (record.MaHoanTien, new AdminSaveReceipt(record.MaHoanTien, record.PhienBanDuLieu), before, reason);
    /// <summary>Tìm hồ sơ đang theo dõi để cập nhật với rowversion.</summary>
    private async Task<HoSoHoanTien> Find(long id) => await db.HoSoHoanTien.SingleOrDefaultAsync(x => x.MaHoanTien == id) ?? throw new AdminRule(404, "Không tìm thấy hồ sơ hoàn.");
    /// <summary>Lưu sự kiện cùng giao dịch để khách/POS biết thay đổi tài chính sau commit.</summary>
    private void Emit(Guid operation, long order) => db.SuKienDongBo.Add(new() { MaSuKien = Guid.NewGuid(), MaThaoTac = operation,
        LoaiSuKien = "REFUND_CHANGED", NoiDungJson = JsonSerializer.Serialize(new { MaDonHang = order }), PhatSinhLuc = DateTime.UtcNow });
    /// <summary>Chứng cứ là tham chiếu hồ sơ/sao kê, không nhận mật khẩu hoặc token ngân hàng.</summary>
    private static void Evidence(string? value) { if (string.IsNullOrWhiteSpace(value) || value.Length > 500) throw Bad("Cần tham chiếu chứng cứ, tối đa 500 ký tự."); }
    /// <summary>Tiền Việt dùng đồng nguyên dương và giới hạn decimal của database.</summary>
    private static void Amount(decimal value) { if (value <= 0 || value != decimal.Truncate(value) || value > 999999999999999999m) throw Bad("Số tiền phải là số đồng nguyên dương."); }
    /// <summary>Danh tính Admin lấy từ phiên được máy chủ xác thực.</summary>
    private static long Actor(HttpContext context) => long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    /// <summary>Thống nhất lỗi nhập liệu có thể giải thích trên giao diện.</summary>
    private static AdminRule Bad(string message) => new(400, message);
}
