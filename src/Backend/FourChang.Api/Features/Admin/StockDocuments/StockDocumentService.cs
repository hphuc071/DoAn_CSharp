// UC: UC-ADM-04
// Chức năng: Lưu nháp, xác nhận và xử lý lịch phiếu nhập/xuất kho.
// Cần nhớ: Phiếu nháp chưa đổi kho; phiếu thành công không được sửa/hủy; phiếu tương lai chờ đến ngày.

using System.Data;
using FourChang.Api.Shared.Audit;
using System.Security.Claims;
using System.Text.Json;
using FourChang.Api.Shared.Operations;
using FourChang.Domain.Entities.Inventory;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.StockDocuments;

/// <summary>UC-ADM-04: lưu nháp và xác nhận phiếu nhập/xuất; phiếu thành công chỉ được xem.</summary>
public sealed class StockDocumentService(CafeDbContext db, TimeProvider clock)
{
    /// <summary>Đọc danh sách có dòng và nguồn gốc; không trả entity EF để tránh ghi từ giao diện.</summary>
    public async Task<IReadOnlyList<StockDocumentView>> Read()
    {
        var headers = await db.PhieuKho.AsNoTracking().OrderByDescending(x => x.MaPhieuKho).ToListAsync();
        var lines = await (from l in db.ChiTietPhieuKho.AsNoTracking() join n in db.NguyenLieu.AsNoTracking()
                           on l.MaNguyenLieu equals n.MaNguyenLieu select new { l, n.TenNguyenLieu }).ToListAsync();
        return headers.Select(p => new StockDocumentView(p.MaPhieuKho, p.SoPhieuKho, p.LoaiPhieu, p.TrangThai,
            p.TenNhaCungCapDaChot, p.MaMucDich, p.LyDo, null, p.MaPhieuBiDao, p.MaQuanLyLap,
            p.MaQuanLyDuyet, p.ThoiDiemTao, p.GhiSoLuc, p.PhienBanDuLieu,
            lines.Where(x => x.l.MaPhieuKho == p.MaPhieuKho).Select(x => new StockDocumentLineView(x.l.MaDongPhieuKho,
                x.l.MaNguyenLieu, x.TenNguyenLieu, x.l.DonViNhapDaChot, x.l.SoLuongTheoDonViNhap,
                x.l.ChenhLechDonViCoSo, x.l.DonGiaNhap, null, null,
                null, x.l.GhiChu)).ToList(), p.NgayThucHien, p.LoiXuLy)).ToList();
    }

    /// <summary>
    /// UC-ADM-04 — Lưu phiếu nhập/xuất ở trạng thái nháp.
    ///
    /// Hàm này được dùng để:
    /// 1. Kiểm tra loại phiếu, ngày và thông tin quản lý đã nhập.
    /// 2. Tạo phiếu mới hoặc kiểm tra phiếu được phép sửa.
    /// 3. Kiểm tra rồi lưu từng nguyên liệu, số lượng và đơn giá.
    /// 4. Lưu phiếu nháp để quản lý kiểm tra trước khi xác nhận.
    ///
    /// Thông tin cần truyền vào:
    /// - id: Khóa phiếu cần sửa; để trống nếu lập phiếu mới.
    /// - input: Loại phiếu, ngày, nguồn, ghi chú và danh sách nguyên liệu.
    /// - context: Thông tin phiên và quyền của người đang thao tác.
    ///
    /// Cần nhớ: Lưu nháp chưa làm tăng hoặc giảm số lượng trong kho. Muốn thay đổi kho, quản lý phải xác nhận phiếu.
    /// </summary>
    public Task<IResult> Save(long? id, StockDocumentInput input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "STOCK_DRAFT", "PhieuKho", new { id, input }, context, async () =>
        {

            // Bước 1: Kiểm tra loại phiếu, ngày, mục đích và danh sách nguyên liệu.
            var reason = string.IsNullOrWhiteSpace(input.LyDo) && input.LoaiPhieu == "RECEIPT" ? "Nhập nguyên liệu" : input.LyDo;
            AdminOperation.Reason(reason);
            var today = BusinessToday();
            var date = input.NgayThucHien ?? today;
            if (input.LoaiPhieu is not ("RECEIPT" or "ISSUE")) throw Bad("Chỉ lập phiếu nhập hoặc xuất kho. Kiểm kê/điều chỉnh cũ được giữ để xem lịch sử.");
            if (input.ThanhPhan is not { Count: > 0 and <= 500 } || input.ThanhPhan.Select(x => x.MaNguyenLieu).Distinct().Count() != input.ThanhPhan.Count)
                throw Bad("Phiếu cần 1–500 nguyên liệu, không trùng nguyên liệu.");
            if (input.LoaiPhieu == "ISSUE" && input.MaMucDich is not ("DAMAGED" or "EXPIRED" or "INTERNAL"))
                throw Bad("Chọn mục đích: hỏng, hết hạn hoặc sử dụng nội bộ; không xuất tiêu hao đơn thủ công.");
            if ((input.Nguon?.Length ?? 0) > 200) throw Bad("Nguồn nhập tối đa 200 ký tự.");
            var now = DateTime.UtcNow;

            // Bước 2: Tạo đầu phiếu mới hoặc kiểm tra phiếu được phép sửa.
            var header = id is null ? new PhieuKho { TrangThai = "DRAFT",
                MaThaoTac = input.MaThaoTac, MaQuanLyLap = Actor(context), ThoiDiemTao = now } : await Find(id.Value);
            var before = id is null ? null : JsonSerializer.Serialize(header);
            if (id is not null)
            {
                AdminOperation.Version(input.PhienBanDuLieu, header.PhienBanDuLieu); Editable(header);
                if (header.LoaiPhieu is not ("RECEIPT" or "ISSUE")) throw new AdminRule(409, "Phiếu kiểm kê/điều chỉnh cũ chỉ được xem lịch sử.");
                db.ChiTietPhieuKho.RemoveRange(await db.ChiTietPhieuKho.Where(x => x.MaPhieuKho == id).ToListAsync());
                await db.SaveChangesAsync(); // Xóa dòng nháp trước khi thêm cùng nguyên liệu; vẫn ở trong transaction.
            }
            header.LoaiPhieu = input.LoaiPhieu; header.LyDo = reason.Trim(); header.NgayThucHien = date; header.TenNhaCungCapDaChot = input.Nguon?.Trim();
            header.MaMucDich = input.MaMucDich; header.ThoiDiemCapNhat = now;
            header.TrangThai = "DRAFT"; header.LoiXuLy = null; header.MaQuanLyDuyet = null;
            if (id is null) db.PhieuKho.Add(header);
            await db.SaveChangesAsync();

            // Bước 3: Kiểm tra và lưu từng nguyên liệu cùng số lượng/giá; chưa cộng trừ tồn kho.
            foreach (var line in input.ThanhPhan)
            {
                var n = await db.NguyenLieu.SingleOrDefaultAsync(x => x.MaNguyenLieu == line.MaNguyenLieu && x.DangSuDung)
                    ?? throw Bad("Nguyên liệu không tồn tại hoặc đã ngừng sử dụng.");
                var unit = n.DonViCoSo;
                if (!IngredientUnits.IsSupported(unit)) throw Bad("Chỉ sử dụng Gam hoặc Mililit cơ sở.");
                if (line.SoLuong < 0 || line.SoLuong > 99999999999999m || decimal.Round(line.SoLuong, 4) != line.SoLuong ||
                    line.SoLuong == 0 || line.DonGiaNhap < 0 || (line.GhiChu?.Length ?? 0) > 500)
                    throw Bad("Số lượng cần dương, tối đa 4 số lẻ; giá nhập không âm.");
                db.ChiTietPhieuKho.Add(new ChiTietPhieuKho { MaPhieuKho = header.MaPhieuKho, MaNguyenLieu = n.MaNguyenLieu,
                    DonViNhapDaChot = unit,
                    SoLuongTheoDonViNhap = line.SoLuong, DonGiaNhap = line.DonGiaNhap, GhiChu = line.GhiChu,
                    ThoiDiemTao = now, ThoiDiemCapNhat = now });
            }
            await db.SaveChangesAsync();
            return (header.MaPhieuKho, (object)new AdminSaveReceipt(header.MaPhieuKho, header.PhienBanDuLieu), before, reason);
        });

    /// <summary>
    /// UC-ADM-04 — Xác nhận phiếu nháp để ghi kho hoặc hẹn ngày xử lý.
    ///
    /// Hàm này được dùng để:
    /// 1. Tìm phiếu và kiểm tra bản dữ liệu, trạng thái cùng các dòng nguyên liệu.
    /// 2. Nếu ngày thực hiện chưa đến thì ghi nhận lịch chờ xử lý.
    /// 3. Nếu ngày đã đến thì thực hiện nhập hoặc xuất kho.
    /// 4. Lưu trạng thái và trả kết quả xác nhận.
    ///
    /// Thông tin cần truyền vào:
    /// - id: Khóa phiếu kho cần xử lý.
    /// - input: Mã phiếu, phiên bản và mã thao tác xác nhận.
    /// - context: Thông tin phiên và quyền của người đang thao tác.
    ///
    /// Sau khi xử lý: Phiếu thành công hoặc đang xử lý theo ngày đã chọn.
    /// </summary>
    public Task<IResult> Post(long id, StockDocumentAction input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "STOCK_POST", "PhieuKho", new { id, input }, context, async () =>
        {

            // Bước 1: Đọc đúng phiếu và kiểm tra phiên bản, trạng thái trước khi xác nhận.
            var header = await Find(id); AdminOperation.Version(input.PhienBanDuLieu, header.PhienBanDuLieu); Editable(header);
            if (header.LoaiPhieu is not ("RECEIPT" or "ISSUE")) throw new AdminRule(409, "Phiếu kiểm kê/điều chỉnh cũ chỉ xem lịch sử.");
            if (!await db.ChiTietPhieuKho.AnyAsync(x => x.MaPhieuKho == id)) throw Bad("Phiếu chưa có nguyên liệu.");
            var reason = string.IsNullOrWhiteSpace(input.LyDo) ? header.LyDo ?? "Xác nhận phiếu kho" : input.LyDo;
            AdminOperation.Reason(reason); var before = JsonSerializer.Serialize(header);
            header.MaQuanLyDuyet = Actor(context); header.LoiXuLy = null;

            // Bước 2: Ngày tương lai chỉ hẹn xử lý; ngày đã đến thì ghi sổ nhập/xuất ngay.
            if (header.NgayThucHien > BusinessToday())
            {
                header.TrangThai = "SCHEDULED"; header.ThoiDiemCapNhat = DateTime.UtcNow;
            }
            else await ApplyCore(header, input.MaThaoTac, CancellationToken.None);
            await db.SaveChangesAsync();
            return (id, (object)new AdminSaveReceipt(id, header.PhienBanDuLieu), before, reason);
        });

    /// <summary>
    /// UC-ADM-04 — Hủy lịch của phiếu chưa ghi sổ thành công.
    ///
    /// Hàm này được dùng để:
    /// 1. Tìm phiếu và kiểm tra nó đang chờ lịch hoặc cần kiểm tra.
    /// 2. Kiểm tra lý do hủy và bản dữ liệu.
    /// 3. Hủy lịch xử lý rồi ghi nhận kết quả.
    ///
    /// Thông tin cần truyền vào:
    /// - id: Khóa phiếu kho cần xử lý.
    /// - input: Phiếu đang xử lý/cần kiểm tra, phiên bản và lý do.
    /// - context: Thông tin phiên và quyền của người đang thao tác.
    ///
    /// Cần nhớ: Không cho hủy phiếu đã nhập/xuất thành công; hủy lịch không tự tạo phiếu ngược.
    /// </summary>
    public Task<IResult> Cancel(long id, StockDocumentAction input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "STOCK_SCHEDULE_CANCEL", "PhieuKho", new { id, input }, context, async () =>
        {
            var header = await Find(id); AdminOperation.Version(input.PhienBanDuLieu, header.PhienBanDuLieu); AdminOperation.Reason(input.LyDo);
            if (header.TrangThai is not ("SCHEDULED" or "FAILED")) throw new AdminRule(409, "Chỉ hủy phiếu đang xử lý hoặc cần kiểm tra; tải lại trạng thái.");
            var before = JsonSerializer.Serialize(header);
            header.TrangThai = "CANCELLED"; header.ThoiDiemCapNhat = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return (id, (object)new AdminSaveReceipt(id, header.PhienBanDuLieu), before, input.LyDo);
        });

    /// <summary>
    /// UC-ADM-04 — Xử lý một phiếu đã xác nhận khi đến ngày.
    ///
    /// Hàm này được dùng để:
    /// 1. Khóa và đọc lại phiếu đã xác nhận có lịch đến hạn.
    /// 2. Thử thực hiện việc nhập hoặc xuất kho cho phiếu.
    /// 3. Nếu thành công thì ghi sổ; nếu không đủ điều kiện thì lưu lỗi để quản lý kiểm tra.
    ///
    /// Thông tin cần truyền vào:
    /// - id: Khóa phiếu kho cần xử lý.
    /// - ct: Tín hiệu yêu cầu dừng tác vụ nếu không cần xử lý tiếp.
    ///
    /// Sau khi xử lý: Tồn kho và trạng thái được cập nhật theo kết quả xử lý.
    /// </summary>
    public async Task ApplyScheduled(long id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AdminOperation.Lock(db);
        var header = await db.PhieuKho.SingleOrDefaultAsync(x => x.MaPhieuKho == id, ct);
        if (header is null || header.TrangThai != "SCHEDULED" || header.NgayThucHien is null || header.NgayThucHien > BusinessToday()) return;
        var before = JsonSerializer.Serialize(header);
        var operation = Guid.NewGuid();
        string? error = null;
        try { await ApplyCore(header, operation, ct); }
        catch (AdminRule ex)
        {
            // ApplyCore kiểm toàn bộ dòng trước khi sửa kho; lỗi nghiệp vụ không để lại thay đổi một phần.
            error = ex.Message;
            header.TrangThai = "FAILED"; header.LoiXuLy = error[..Math.Min(error.Length, 500)];
            header.ThoiDiemCapNhat = DateTime.UtcNow;
        }
        var audit = AuditFactory.Create("SYSTEM", 0, error is null ? "STOCK_SCHEDULE_POSTED" : "STOCK_SCHEDULE_FAILED", "PhieuKho", id);
        audit.MaThaoTac = operation; audit.DuLieuTruocJson = before;
        audit.DuLieuSauJson = JsonSerializer.Serialize(header); audit.LyDo = error ?? "Đến ngày thực hiện phiếu kho đã được Quản lý xác nhận";
        audit.KetQua = error is null ? "SUCCEEDED" : "FAILED";
        db.NhatKyHeThong.Add(audit);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// UC-ADM-04 — Cộng hoặc trừ kho cho toàn bộ các dòng phiếu hợp lệ.
    ///
    /// Hàm này được dùng để:
    /// 1. Đọc các dòng nguyên liệu trong phiếu.
    /// 2. Kiểm tra điều kiện nhập/xuất, đặc biệt là tồn còn được phép xuất.
    /// 3. Cộng hoặc trừ số lượng và ghi biến động kho.
    /// 4. Đánh dấu phiếu thành công sau khi các dòng đã được xử lý hợp lệ.
    ///
    /// Thông tin cần truyền vào:
    /// - header: Thông tin đầu phiếu nhập hoặc xuất kho đang xử lý.
    /// - operation: Mã nhận biết lần thao tác này, giúp kiểm tra yêu cầu bị gửi lại.
    /// - ct: Tín hiệu yêu cầu dừng tác vụ nếu không cần xử lý tiếp.
    ///
    /// Cần nhớ: Mọi dòng cùng thuộc giao dịch của bên gọi. Nếu có lỗi thì không được để kho thay đổi một phần.
    /// </summary>
    private async Task ApplyCore(PhieuKho header, Guid operation, CancellationToken ct)
    {
        if (header.LoaiPhieu is not ("RECEIPT" or "ISSUE") || header.MaQuanLyDuyet is null) throw new AdminRule(409, "Phiếu chưa đủ điều kiện ghi sổ.");
        var lines = await db.ChiTietPhieuKho.Where(x => x.MaPhieuKho == header.MaPhieuKho).ToListAsync(ct);
        if (lines.Count == 0) throw Bad("Phiếu chưa có nguyên liệu.");
        var prepared = new List<(ChiTietPhieuKho Line, TonKho Stock, decimal Delta)>();
        foreach (var line in lines)
        {
            var ingredient = await db.NguyenLieu.SingleOrDefaultAsync(x => x.MaNguyenLieu == line.MaNguyenLieu && x.DangSuDung, ct)
                ?? throw new AdminRule(409, "Nguyên liệu đã ngừng sử dụng. Kiểm tra lại phiếu.");
            var stock = await db.TonKho.SingleOrDefaultAsync(x => x.MaNguyenLieu == line.MaNguyenLieu, ct);
            if (stock is null)
            {
                if (header.LoaiPhieu != "RECEIPT") throw new AdminRule(409, $"{ingredient.TenNguyenLieu}: chưa có tồn kho để xuất.");
                stock = new TonKho { MaNguyenLieu = line.MaNguyenLieu, ThoiDiemTao = DateTime.UtcNow, ThoiDiemCapNhat = DateTime.UtcNow };
            }
            var delta = header.LoaiPhieu == "RECEIPT" ? line.SoLuongTheoDonViNhap : -line.SoLuongTheoDonViNhap;
            if (stock.TonThucTe + delta < stock.TongGiuCho) throw new AdminRule(409, $"{ingredient.TenNguyenLieu}: không đủ tồn không giữ chỗ; chưa trừ kho toàn phiếu.");
            if (stock.TonThucTe + delta > 99999999999999.9999m) throw new AdminRule(409, "Số tồn sau nhập vượt giới hạn lưu trữ.");
            prepared.Add((line, stock, delta));
        }
        foreach (var (line, stock, delta) in prepared)
        {
            if (db.Entry(stock).State == EntityState.Detached) db.TonKho.Add(stock);
            await Move(header, line, stock, delta, operation, null);
        }
        await db.SaveChangesAsync(ct); // Lưu chi tiết trước khi khóa bất biến POSTED; vẫn cùng transaction.

        // Chỉ sau khi các dòng đã ghi kho hợp lệ mới đánh dấu phiếu thành công và khóa sửa.
        header.TrangThai = "POSTED"; header.GhiSoLuc = DateTime.UtcNow; header.LoiXuLy = null;
        header.ThoiDiemCapNhat = DateTime.UtcNow; Emit(operation, header.MaPhieuKho);
    }

    /// <summary>Giữ endpoint cũ để báo rõ quy tắc mới; không phát lại thao tác hủy hoặc tạo bút toán ngược mới.</summary>
    public Task<IResult> Reverse(long id, StockDocumentAction input, HttpContext context) =>
        Task.FromResult<IResult>(Results.Problem(statusCode: 409, detail: "Phiếu đã thành công không được hủy, sửa hoặc xóa."));

    /// <summary>Chỉ xóa nháp không tham chiếu; phiếu đã ghi sổ luôn giữ lại để truy vết.</summary>
    public Task<IResult> Delete(long id, AdminDeleteInput input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "STOCK_DELETE", "PhieuKho", new { id, input }, context, async () =>
        {
            var header = await Find(id); AdminOperation.Version(input.PhienBanDuLieu, header.PhienBanDuLieu); Draft(header);
            if (header.LoaiPhieu is not ("RECEIPT" or "ISSUE")) throw new AdminRule(409, "Phiếu kiểm kê/điều chỉnh cũ chỉ được xem lịch sử.");
            if (await db.PhieuKho.AnyAsync(x => x.MaPhieuBiDao == id)) throw new AdminRule(409, "Phiếu đã được tham chiếu.");
            var before = JsonSerializer.Serialize(header);
            db.ChiTietPhieuKho.RemoveRange(await db.ChiTietPhieuKho.Where(x => x.MaPhieuKho == id).ToListAsync());
            db.PhieuKho.Remove(header); await db.SaveChangesAsync();
            return (id, (object)new AdminSaveReceipt(id, null, true), before, (string?)null);
        });

    /// <summary>Áp dụng biến động đã kiểm tra; chênh lệch 0 không tạo dòng sổ giả hay đổi phiên bản tồn.</summary>
    private Task Move(PhieuKho header, ChiTietPhieuKho line, TonKho stock, decimal delta, Guid operation, long? reversed)
    {
        if (stock.TonThucTe + delta < stock.TongGiuCho) throw new AdminRule(409, "Không đủ tồn không giữ chỗ; chặn toàn phiếu.");
        line.ChenhLechDonViCoSo = delta;
        if (delta == 0) return Task.CompletedTask;
        stock.TonThucTe += delta; stock.ThoiDiemCapNhat = DateTime.UtcNow;
        db.BienDongKho.Add(new BienDongKho { MaNguyenLieu = line.MaNguyenLieu, MaDongPhieuKho = line.MaDongPhieuKho == 0 ? null : line.MaDongPhieuKho,
            LoaiBienDong = header.LoaiPhieu, SoLuongThayDoi = delta, MaThaoTac = operation, MaBienDongBiDao = reversed,
            PhatSinhLuc = DateTime.UtcNow, GhiChu = header.LyDo });
        return Task.CompletedTask;
    }

    /// <summary>Outbox cùng transaction để menu Web/POS đọc lại khả dụng sau biến động kho.</summary>
    private void Emit(Guid operation, long id) => db.SuKienDongBo.Add(new()
    { MaSuKien = Guid.NewGuid(), MaThaoTac = operation, LoaiSuKien = "MENU_CHANGED", NoiDungJson = JsonSerializer.Serialize(new { PhieuKho = id }), PhatSinhLuc = DateTime.UtcNow });
    /// <summary>Ngày nghiệp vụ quán theo UTC+07, độc lập múi giờ của máy chủ.</summary>
    private DateOnly BusinessToday() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
    /// <summary>Tìm bản ghi hoặc trả lỗi nghiệp vụ 404.</summary>
    private async Task<PhieuKho> Find(long id) => await db.PhieuKho.SingleOrDefaultAsync(x => x.MaPhieuKho == id) ?? throw new AdminRule(404, "Không tìm thấy phiếu kho.");
    /// <summary>Không cho sửa/xóa/ghi sổ lại chứng từ đã ghi sổ.</summary>
    private static void Draft(PhieuKho header) { if (header.TrangThai != "DRAFT") throw new AdminRule(409, "Phiếu không còn là nháp; không được sửa hoặc xóa trực tiếp."); }
    /// <summary>Chỉ nháp hoặc phiếu xử lý thất bại được sửa/xác nhận lại. Lịch đã duyệt được khóa.</summary>
    private static void Editable(PhieuKho header)
    {
        if (header.TrangThai is not ("DRAFT" or "FAILED")) throw new AdminRule(409, "Phiếu đã xác nhận, không được sửa hoặc xác nhận lại.");
    }
    /// <summary>Lấy Admin từ phiên đã xác thực, không nhận mã người duyệt do UI tự khai.</summary>
    private static long Actor(HttpContext context) => long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    /// <summary>Giữ thông báo lỗi nhập liệu rõ ràng và mã HTTP nhất quán.</summary>
    private static AdminRule Bad(string message) => new(400, message);
}
