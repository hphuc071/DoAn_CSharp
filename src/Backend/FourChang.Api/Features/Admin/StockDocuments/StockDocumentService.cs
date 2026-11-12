using System.Security.Claims;
using System.Text.Json;
using FourChang.Api.Shared.Operations;
using FourChang.Domain.Entities.Inventory;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.StockDocuments;

/// <summary>UC-ADM-06: nháp không đổi tồn; ghi sổ/đảo toàn phiếu nguyên tử cùng sổ kho, audit và outbox.</summary>
public sealed class StockDocumentService(CafeDbContext db)
{
    /// <summary>Đọc danh sách có dòng và nguồn gốc; không trả entity EF để tránh ghi từ giao diện.</summary>
    public async Task<IReadOnlyList<StockDocumentView>> Read()
    {
        var headers = await db.PhieuKho.AsNoTracking().OrderByDescending(x => x.MaPhieuKho).ToListAsync();
        var lines = await (from l in db.ChiTietPhieuKho.AsNoTracking() join n in db.NguyenLieu.AsNoTracking()
                           on l.MaNguyenLieu equals n.MaNguyenLieu select new { l, n.TenNguyenLieu }).ToListAsync();
        return headers.Select(p => new StockDocumentView(p.MaPhieuKho, p.SoPhieuKho, p.LoaiPhieu, p.TrangThai,
            p.TenNhaCungCapDaChot, p.MaMucDich, p.LyDo, p.MocKiemKe, p.MaPhieuBiDao, p.MaQuanLyLap,
            p.MaQuanLyDuyet, p.ThoiDiemTao, p.GhiSoLuc, p.PhienBanDuLieu,
            lines.Where(x => x.l.MaPhieuKho == p.MaPhieuKho).Select(x => new StockDocumentLineView(x.l.MaDongPhieuKho,
                x.l.MaNguyenLieu, x.TenNguyenLieu, x.l.DonViNhapDaChot, x.l.SoLuongTheoDonViNhap,
                x.l.ChenhLechDonViCoSo, x.l.DonGiaNhap, x.l.TonSoDaChot, x.l.SoLuongThucDem,
                x.l.PhienBanTonKhoDaChot, x.l.GhiChu)).ToList())).ToList();
    }

    /// <summary>Tạo/sửa nháp, chốt đơn vị g/ml và mốc kiểm kê; không cộng trừ bất cứ nguyên liệu nào.</summary>
    public Task<IResult> Save(long? id, StockDocumentInput input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "STOCK_DRAFT", "PhieuKho", new { id, input }, context, async () =>
        {
            AdminOperation.Reason(input.LyDo);
            if (input.LoaiPhieu is not ("RECEIPT" or "ISSUE" or "COUNT" or "ADJUSTMENT")) throw Bad("Loại phiếu không hợp lệ.");
            if (input.ThanhPhan is not { Count: > 0 and <= 500 } || input.ThanhPhan.Select(x => x.MaNguyenLieu).Distinct().Count() != input.ThanhPhan.Count)
                throw Bad("Phiếu cần 1–500 nguyên liệu, không trùng nguyên liệu.");
            if (input.LoaiPhieu == "ISSUE" && input.MaMucDich is not ("DAMAGED" or "EXPIRED" or "INTERNAL"))
                throw Bad("Chọn mục đích: hỏng, hết hạn hoặc sử dụng nội bộ; không xuất tiêu hao đơn thủ công.");
            if ((input.Nguon?.Length ?? 0) > 200) throw Bad("Nguồn nhập tối đa 200 ký tự.");
            var now = DateTime.UtcNow;
            var header = id is null ? new PhieuKho { SoPhieuKho = "PK-" + Guid.NewGuid().ToString("N"), TrangThai = "DRAFT",
                MaThaoTac = input.MaThaoTac, MaQuanLyLap = Actor(context), ThoiDiemTao = now } : await Find(id.Value);
            var before = id is null ? null : JsonSerializer.Serialize(header);
            if (id is not null)
            {
                AdminOperation.Version(input.PhienBanDuLieu, header.PhienBanDuLieu); Draft(header);
                db.ChiTietPhieuKho.RemoveRange(await db.ChiTietPhieuKho.Where(x => x.MaPhieuKho == id).ToListAsync());
                await db.SaveChangesAsync(); // Xóa dòng nháp trước khi thêm cùng nguyên liệu; vẫn ở trong transaction.
            }
            header.LoaiPhieu = input.LoaiPhieu; header.LyDo = input.LyDo.Trim(); header.TenNhaCungCapDaChot = input.Nguon?.Trim();
            header.MaMucDich = input.MaMucDich; header.ThoiDiemCapNhat = now;
            header.MocKiemKe = input.LoaiPhieu is "COUNT" or "ADJUSTMENT" ? now : null;
            if (id is null) db.PhieuKho.Add(header);
            await db.SaveChangesAsync();
            foreach (var line in input.ThanhPhan)
            {
                var n = await db.NguyenLieu.SingleOrDefaultAsync(x => x.MaNguyenLieu == line.MaNguyenLieu && x.DangSuDung)
                    ?? throw Bad("Nguyên liệu không tồn tại hoặc đã ngừng sử dụng.");
                var unit = await db.DonViTinh.SingleAsync(x => x.MaDonVi == n.MaDonViCoSo);
                if (!IngredientUnits.IsSupported(unit.KyHieu)) throw Bad("Chỉ sử dụng Gam hoặc Mililit cơ sở.");
                if (line.SoLuong < 0 || line.SoLuong > 99999999999999m || decimal.Round(line.SoLuong, 4) != line.SoLuong ||
                    (header.MocKiemKe is null && line.SoLuong == 0) || line.DonGiaNhap < 0 || (line.GhiChu?.Length ?? 0) > 500)
                    throw Bad("Số lượng cần dương (thực đếm cho phép 0), tối đa 4 số lẻ; giá nhập không âm.");
                var stock = await db.TonKho.SingleOrDefaultAsync(x => x.MaNguyenLieu == line.MaNguyenLieu)
                    ?? throw Bad("Chưa có số dư nguyên liệu để đối chiếu.");
                if (header.MocKiemKe is not null)
                {
                    AdminOperation.Version(line.PhienBanTonKho, stock.PhienBanDuLieu);
                    if (line.TonSoDaChot != stock.TonThucTe) throw new AdminRule(409, "Tồn sổ đã đổi. Đối chiếu và xác nhận lại số thực đếm.");
                }
                db.ChiTietPhieuKho.Add(new ChiTietPhieuKho { MaPhieuKho = header.MaPhieuKho, MaNguyenLieu = n.MaNguyenLieu,
                    MaDonViNhap = n.MaDonViCoSo, DonViNhapDaChot = unit.KyHieu, HeSoQuyDoiDaChot = 1,
                    SoLuongTheoDonViNhap = line.SoLuong, DonGiaNhap = line.DonGiaNhap, GhiChu = line.GhiChu,
                    TonSoDaChot = header.MocKiemKe is null ? null : stock.TonThucTe,
                    SoLuongThucDem = header.MocKiemKe is null ? null : line.SoLuong,
                    PhienBanTonKhoDaChot = header.MocKiemKe is null ? null : stock.PhienBanDuLieu.ToArray(),
                    ThoiDiemTao = now, ThoiDiemCapNhat = now });
            }
            await db.SaveChangesAsync();
            return (header.MaPhieuKho, (object)new AdminSaveReceipt(header.MaPhieuKho, header.PhienBanDuLieu), before, input.LyDo);
        });

    /// <summary>Ghi sổ một lần; lỗi một dòng hoặc audit đều rollback toàn phiếu. Không đụng giữ chỗ của đơn.</summary>
    public Task<IResult> Post(long id, StockDocumentAction input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "STOCK_POST", "PhieuKho", new { id, input }, context, async () =>
        {
            var header = await Find(id); AdminOperation.Version(input.PhienBanDuLieu, header.PhienBanDuLieu); Draft(header);
            AdminOperation.Reason(input.LyDo); var before = JsonSerializer.Serialize(header);
            var lines = await db.ChiTietPhieuKho.Where(x => x.MaPhieuKho == id).ToListAsync();
            if (lines.Count == 0) throw Bad("Phiếu chưa có nguyên liệu.");
            foreach (var line in lines)
            {
                var stock = await db.TonKho.SingleAsync(x => x.MaNguyenLieu == line.MaNguyenLieu);
                decimal delta;
                if (header.LoaiPhieu is "COUNT" or "ADJUSTMENT")
                {
                    AdminOperation.Version(line.PhienBanTonKhoDaChot, stock.PhienBanDuLieu);
                    if (line.TonSoDaChot != stock.TonThucTe || line.SoLuongThucDem is null) throw new AdminRule(409, "Có phát sinh sau mốc kiểm kê; cần đối chiếu lại.");
                    delta = line.SoLuongThucDem.Value - stock.TonThucTe;
                }
                else delta = header.LoaiPhieu == "RECEIPT" ? line.SoLuongTheoDonViNhap : -line.SoLuongTheoDonViNhap;
                await Move(header, line, stock, delta, input.MaThaoTac, null);
            }
            await db.SaveChangesAsync(); // Lưu dòng khi header còn DRAFT, sau đó mới đóng bất biến; vẫn cùng transaction.
            header.TrangThai = "POSTED"; header.GhiSoLuc = DateTime.UtcNow; header.MaQuanLyDuyet = Actor(context);
            header.ThoiDiemCapNhat = DateTime.UtcNow; Emit(input.MaThaoTac, id);
            await db.SaveChangesAsync();
            return (id, (object)new AdminSaveReceipt(id, header.PhienBanDuLieu), before, input.LyDo);
        });

    /// <summary>Đảo bằng phiếu mới, giữ nguyên bản gốc; kiểm tra tồn/giữ chỗ hiện tại và chặn đảo hai lần.</summary>
    public Task<IResult> Reverse(long id, StockDocumentAction input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "STOCK_REVERSE", "PhieuKho", new { id, input }, context, async () =>
        {
            var original = await Find(id); AdminOperation.Version(input.PhienBanDuLieu, original.PhienBanDuLieu); AdminOperation.Reason(input.LyDo);
            if (original.TrangThai != "POSTED" || original.LoaiPhieu == "REVERSAL" || await db.PhieuKho.AnyAsync(x => x.MaPhieuBiDao == id))
                throw new AdminRule(409, "Chỉ đảo phiếu đã ghi sổ chưa có phiếu đảo.");
            var now = DateTime.UtcNow;
            var header = new PhieuKho { SoPhieuKho = "PK-" + Guid.NewGuid().ToString("N"), LoaiPhieu = "REVERSAL", TrangThai = "DRAFT",
                MaPhieuBiDao = id, LyDo = input.LyDo.Trim(), MaQuanLyLap = Actor(context), MaThaoTac = input.MaThaoTac,
                ThoiDiemTao = now, ThoiDiemCapNhat = now };
            db.PhieuKho.Add(header); await db.SaveChangesAsync();
            foreach (var old in await db.ChiTietPhieuKho.Where(x => x.MaPhieuKho == id).ToListAsync())
            {
                var line = new ChiTietPhieuKho { MaPhieuKho = header.MaPhieuKho, MaNguyenLieu = old.MaNguyenLieu,
                    MaDonViNhap = old.MaDonViNhap, DonViNhapDaChot = old.DonViNhapDaChot, HeSoQuyDoiDaChot = old.HeSoQuyDoiDaChot,
                    SoLuongTheoDonViNhap = Math.Abs(old.ChenhLechDonViCoSo), DonGiaNhap = old.DonGiaNhap, ThoiDiemTao = now, ThoiDiemCapNhat = now };
                db.ChiTietPhieuKho.Add(line); await db.SaveChangesAsync();
                var movement = await db.BienDongKho.SingleOrDefaultAsync(x => x.MaDongPhieuKho == old.MaDongPhieuKho);
                var stock = await db.TonKho.SingleAsync(x => x.MaNguyenLieu == old.MaNguyenLieu);
                await Move(header, line, stock, -old.ChenhLechDonViCoSo, input.MaThaoTac, movement?.MaBienDongKho);
            }
            await db.SaveChangesAsync();
            header.TrangThai = "POSTED"; header.GhiSoLuc = now; header.MaQuanLyDuyet = Actor(context); Emit(input.MaThaoTac, header.MaPhieuKho);
            await db.SaveChangesAsync();
            return (header.MaPhieuKho, (object)new AdminSaveReceipt(header.MaPhieuKho, header.PhienBanDuLieu), JsonSerializer.Serialize(original), input.LyDo);
        });

    /// <summary>Chỉ xóa nháp không tham chiếu; phiếu đã ghi sổ luôn giữ lại để truy vết.</summary>
    public Task<IResult> Delete(long id, AdminDeleteInput input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "STOCK_DELETE", "PhieuKho", new { id, input }, context, async () =>
        {
            var header = await Find(id); AdminOperation.Version(input.PhienBanDuLieu, header.PhienBanDuLieu); Draft(header);
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
            PhatSinhLuc = DateTime.UtcNow, ThoiDiemTao = DateTime.UtcNow, GhiChu = header.LyDo });
        return Task.CompletedTask;
    }

    /// <summary>Outbox cùng transaction để menu Web/POS đọc lại khả dụng sau biến động kho.</summary>
    private void Emit(Guid operation, long id) => db.SuKienDongBo.Add(new()
    { MaSuKien = Guid.NewGuid(), MaThaoTac = operation, LoaiSuKien = "MENU_CHANGED", NoiDungJson = JsonSerializer.Serialize(new { PhieuKho = id }), PhatSinhLuc = DateTime.UtcNow });
    /// <summary>Tìm bản ghi hoặc trả lỗi nghiệp vụ 404.</summary>
    private async Task<PhieuKho> Find(long id) => await db.PhieuKho.SingleOrDefaultAsync(x => x.MaPhieuKho == id) ?? throw new AdminRule(404, "Không tìm thấy phiếu kho.");
    /// <summary>Không cho sửa/xóa/ghi sổ lại chứng từ đã ghi sổ.</summary>
    private static void Draft(PhieuKho header) { if (header.TrangThai != "DRAFT") throw new AdminRule(409, "Phiếu đã ghi sổ; chỉ được tạo phiếu đảo."); }
    /// <summary>Lấy Admin từ phiên đã xác thực, không nhận mã người duyệt do UI tự khai.</summary>
    private static long Actor(HttpContext context) => long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    /// <summary>Giữ thông báo lỗi nhập liệu rõ ràng và mã HTTP nhất quán.</summary>
    private static AdminRule Bad(string message) => new(400, message);
}
