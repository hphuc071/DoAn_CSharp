using System.Data;
using System.Text.Json;
using FourChang.Api.Shared.Operations;
using FourChang.Api.Shared.Orders;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Orders;

/// <summary>UC-ADM-07 đọc nhiều kênh, truy vết snapshot và đổi địa chỉ có chấp thuận; lệnh vòng đời dùng service chung.</summary>
public sealed class OrderAdministrationService(CafeDbContext db, OrderFinancials money, IConfiguration configuration)
{
    /// <summary>Tìm/lọc và phân trang ở máy chủ; tiền của từng dòng được đọc trong cùng transaction.</summary>
    public async Task<AdminPage<AdminOrderRow>> Read(AdminOrderFilter filter, long? currentShift = null)
    {
        if (filter.Trang < 1 || filter.KichThuocTrang is < 1 or > 100 || filter.TuLuc > filter.DenLuc) throw new AdminRule(400, "Bộ lọc/khoảng ngày không hợp lệ.");
        if (filter.NhomDon is not (null or "NORMAL" or "CANCELLATION" or "COUNTER_REFUND")) throw new AdminRule(400, "Nhóm đơn không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var query = db.DonHang.AsNoTracking().AsQueryable();
        if(filter.PhamViCa is not (null or "CURRENT" or "PREVIOUS" or "ALL"))throw new AdminRule(400,"Nhóm ca không hợp lệ.");
        var incoming=currentShift is {} active?await FourChang.Api.Shared.WorkSessions.HandoverLink.ForReceiver(db,active):null;
        var received=incoming?.MaDonBanGiao??[];
        if(currentShift is {} filterCa)
        {
            if(filter.PhamViCa=="CURRENT")query=query.Where(x=>x.MaCaLamViec==filterCa || x.MaCaLamViec==null&&x.KenhDatHang=="WEB"&&(x.TrangThai=="PENDING_CONFIRMATION"||x.TrangThai=="PENDING_PAYMENT"));
            else if(filter.PhamViCa=="PREVIOUS")query=query.Where(x=>received.Contains(x.MaDonHang));
        }
        // Lọc nhóm TRƯỚC Count/Skip/Take: tổng số và phân trang không bị sai vì lọc lại một trang ở UI.
        var waitingCancellation = from c in db.HuyMon join line in db.ChiTietDonHang on c.MaChiTietDonHang equals line.MaChiTietDonHang
            where c.TrangThai == "REQUESTED" select line.MaDonHang;
        if (filter.NhomDon == "CANCELLATION") query = query.Where(x => x.TrangThai == "CANCELLED" || waitingCancellation.Contains(x.MaDonHang));
        if (filter.NhomDon == "COUNTER_REFUND")
        {
            var cashRefundOrders = db.HoSoHoanTien.Where(x => x.PhuongThucHoan == "CASH" && x.MaQuanLyDuyet != null &&
                (x.TrangThai == "APPROVED" || x.TrangThai == "PARTIALLY_REFUNDED" || x.TrangThai == "REFUNDED")).Select(x => x.MaDonHang);
            query = query.Where(x => x.TrangThai == "CANCELLED" && cashRefundOrders.Contains(x.MaDonHang));
        }
        if (filter.NhomDon == "NORMAL") query = query.Where(x => x.TrangThai != "CANCELLED" && !waitingCancellation.Contains(x.MaDonHang));
        if (!string.IsNullOrWhiteSpace(filter.Kenh)) query = query.Where(x => x.KenhDatHang == filter.Kenh);
        if (!string.IsNullOrWhiteSpace(filter.HinhThuc)) query = query.Where(x => x.HinhThucNhanMon == filter.HinhThuc);
        if (!string.IsNullOrWhiteSpace(filter.TrangThai)) query = query.Where(x => x.TrangThai == filter.TrangThai);
        if (filter.TuLuc is { } start) query = query.Where(x => x.ThoiDiemTao >= start);
        if (filter.DenLuc is { } end) query = query.Where(x => x.ThoiDiemTao < end);
        if (filter.MaCa is { } shift) query = query.Where(x => x.MaCaLamViec == shift);
        if (!string.IsNullOrWhiteSpace(filter.TimKiem))
        {
            var text = filter.TimKiem.Trim();
            var shortCode = FourChang.Presentation.Shared.Contracts.Common.OrderCode.TryParse(text, out var channel, out var id);
            query = query.Where(x => shortCode && x.MaDonHang == id && x.KenhDatHang == channel || x.MaDonHienThi.Contains(text) || x.TenNguoiNhan != null && x.TenNguoiNhan.Contains(text));
        }
        var total = await query.CountAsync();
        var data = await query.OrderByDescending(x => x.MaDonHang).Skip((filter.Trang - 1) * filter.KichThuocTrang).Take(filter.KichThuocTrang).ToListAsync();
        var rows = new List<AdminOrderRow>();
        foreach (var order in data)
        {
            var row=await Row(order);
            if(currentShift is {} ca)row=row with {NguonCa=received.Contains(order.MaDonHang)?$"Bàn giao từ ca #{incoming!.MaCaNguon}":order.MaCaLamViec==ca?$"Ca này #{ca}":order.MaCaLamViec is {} other?$"Ca khác #{other}":"Đơn Web chờ nhận"};
            rows.Add(row);
        }
        var result = new AdminPage<AdminOrderRow>(rows, total, filter.Trang, filter.KichThuocTrang, DateTime.UtcNow);
        await tx.CommitAsync(); return result;
    }

    /// <summary>Liên kết đơn–dòng–giữ chỗ–tiêu hao–tiền–audit, không dựng lại giá hoặc BOM từ menu hiện tại.</summary>
    public async Task<AdminOrderDetail> Details(long id)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await db.DonHang.AsNoTracking().SingleOrDefaultAsync(x => x.MaDonHang == id) ?? throw new AdminRule(404, "Không tìm thấy đơn.");
        var lines = await db.ChiTietDonHang.AsNoTracking().Where(x => x.MaDonHang == id).ToListAsync();
        var ids = lines.Select(x => x.MaChiTietDonHang).ToArray();
        var topping = await db.MonThemChiTietDonHang.AsNoTracking().Where(x => ids.Contains(x.MaChiTietDonHang)).ToListAsync();
        var rows = lines.Select(x => new AdminOrderLine(x.MaChiTietDonHang, x.TenMonDaChot, x.KichCoDaChot, x.NhietDoDaChot, x.PhanTramDuong, x.PhanTramDa,
            x.GhiChu, x.SoLuongDat, x.SoLuongBatDauPha, x.SoLuongPhaXong, x.SoLuongDaHuy,
            x.GiaMonDaChot + topping.Where(t => t.MaChiTietDonHang == x.MaChiTietDonHang).Sum(t => t.SoPhanMoiLy * t.GiaMoiPhanDaChot), x.PhienBanDuLieu,
            string.Join(", ", topping.Where(t => t.MaChiTietDonHang == x.MaChiTietDonHang).Select(t => t.TenMonThemDaChot + " × " + t.SoPhanMoiLy)))).ToList();
        var ingredientRows = await (from n in db.NguyenLieuChiTietDonHang.AsNoTracking()
            join g in db.GiuChoNguyenLieu.AsNoTracking() on new { n.MaChiTietDonHang, n.MaNguyenLieu } equals new { g.MaChiTietDonHang, g.MaNguyenLieu }
            where ids.Contains(n.MaChiTietDonHang)
            select new AdminOrderIngredient(n.MaChiTietDonHang, n.MaNguyenLieu, n.TenNguyenLieuDaChot, n.DonViCoSoDaChot,
                n.DinhMucMoiLy, g.SoLuongGiuCho, g.SoLuongDaTieuHao, g.SoLuongDaGiaiPhong)).ToListAsync();
        var held = await db.GiuChoNguyenLieu.AsNoTracking().Where(x => ids.Contains(x.MaChiTietDonHang)).Select(x => new StockReservationView(x.MaChiTietDonHang, id, x.SoLuongGiuCho, x.SoLuongDaTieuHao, x.SoLuongDaGiaiPhong, x.TrangThai, x.HetHanLuc)).ToListAsync();
        var movements = await db.BienDongKho.AsNoTracking().Where(x => x.MaChiTietDonHang != null && ids.Contains(x.MaChiTietDonHang.Value))
            .Select(x => new StockMovementView(x.MaBienDongKho, x.LoaiBienDong, x.SoLuongThayDoi, x.PhatSinhLuc, x.MaDongPhieuKho,
                x.MaChiTietDonHang, null, id, order.MaCaLamViec, null, null, x.GhiChu)).ToListAsync();
        var history = await db.NhatKyHeThong.AsNoTracking().Where(x => x.LoaiDoiTuong == "DonHang" && x.MaDoiTuong == id.ToString())
            .OrderByDescending(x => x.PhatSinhLuc).ToListAsync();
        // Đọc thông tin nhận món và thanh toán trong cùng snapshot, không suy từ menu hoặc sửa hồ sơ khách.
        var table = order.MaBanDaChot;
        if (string.IsNullOrWhiteSpace(table) && order.MaBan is { } tableId)
            table = await db.Ban.AsNoTracking().Where(x => x.MaBan == tableId).Select(x => x.TenBan).SingleOrDefaultAsync();
        var payments = await db.ThanhToan.AsNoTracking().Where(x => x.MaDonHang == id).OrderBy(x => x.MaThanhToan)
            .Select(x => new AdminOrderPayment(x.PhuongThuc, x.TrangThai)).ToListAsync();
        // Địa chỉ hồ sơ chỉ để tham khảo; không ghi đè địa chỉ giao đã chốt của đơn.
        var customerAddress = order.MaKhachHang is { } customerId
            ? await db.DiaChiKhachHang.AsNoTracking().Where(x => x.MaKhachHang == customerId && x.DangSuDung)
                .OrderByDescending(x => x.LaDiaChiMacDinh).ThenBy(x => x.MaDiaChi).Select(x => x.DiaChiChiTiet).FirstOrDefaultAsync()
            : null;
        var result = new AdminOrderDetail(await Row(order), rows, order.DiaChiGiaoDaChot, held, movements,
            history.Select(x => new AdminTraceRow(x.MaNhatKy, x.PhatSinhLuc, x.TenNguoiThucHienDaChot, x.MaCaLamViec, x.MaMayDaChot,
                x.HanhDong, x.LyDo, x.KetQua, x.DuLieuTruocJson, x.DuLieuSauJson, x.MaLienKetXuLy)).ToList(), ingredientRows, order.TenNguoiNhan, order.DienThoaiNguoiNhan, table, payments, customerAddress);
        await tx.CommitAsync(); return result;
    }

    /// <summary>Đổi địa chỉ sau nhận cần Admin xác nhận khả năng giao và khách đồng ý; giữ phí đã chốt, không tự phụ thu.</summary>
    public Task<IResult> Address(long id, OrderAddressInput input, HttpContext context) =>
        AdminOperation.Execute(db, input.MaThaoTac, "ORDER_ADDRESS", "DonHang", new { id, input }, context, async () =>
        {
            var order = await db.DonHang.SingleOrDefaultAsync(x => x.MaDonHang == id) ?? throw new AdminRule(404, "Không tìm thấy đơn.");
            AdminOperation.Version(input.PhienBanDuLieu, order.PhienBanDuLieu); AdminOperation.Reason(input.LyDo);
            if (order.HinhThucNhanMon != "DELIVERY" || order.TrangThai is not ("ACCEPTED" or "PREPARING" or "READY" or "DELIVERING")) throw new AdminRule(409, "Đơn không ở giai đoạn cho phép đổi địa chỉ giao.");
            if (!input.KhachDongY || !input.QuanLyXacNhanGiaoDuoc || string.IsNullOrWhiteSpace(input.ChungCu) || string.IsNullOrWhiteSpace(input.DiaChiMoi) || input.DiaChiMoi.Length > 1000 || input.ViDo is < -90 or > 90 || input.KinhDo is < -180 or > 180 || input.KhoangCachKm is < 0 or > 10)
                throw new AdminRule(400, "Cần địa chỉ trong phạm vi, tọa độ, chứng cứ và chấp thuận khách/Quản lý. Không tự coi khoảng cách là đã được API bản đồ xác minh.");
            var before = JsonSerializer.Serialize(order);
            order.DiaChiGiaoDaChot = input.DiaChiMoi.Trim(); order.ViDo = input.ViDo; order.KinhDo = input.KinhDo;
            order.KhoangCachGiaoKm = input.KhoangCachKm; order.ThoiDiemCapNhat = DateTime.UtcNow;
            db.SuKienDongBo.Add(new() { MaSuKien = Guid.NewGuid(), MaThaoTac = input.MaThaoTac, LoaiSuKien = "ORDER_CHANGED", NoiDungJson = JsonSerializer.Serialize(new { MaDonHang = id }), PhatSinhLuc = DateTime.UtcNow });
            await db.SaveChangesAsync(); return (id, (object)new OrderActionReceipt(id, order.TrangThai, order.PhienBanDuLieu), before, input.LyDo);
        });

    /// <summary>Chuẩn hóa một dòng giám sát với ca/người làm và cảnh báo có nội dung rõ ràng.</summary>
    private async Task<AdminOrderRow> Row(FourChang.Domain.Entities.Orders.DonHang order)
    {
        var balance = await money.Read(order);
        var shift = order.MaCaLamViec is null ? null : await db.CaLamViec.AsNoTracking().SingleOrDefaultAsync(x => x.MaCaLamViec == order.MaCaLamViec);
        var employee = shift is null ? null : await db.NhanVien.AsNoTracking().SingleOrDefaultAsync(x => x.MaNhanVien == shift.MaNhanVien);
        var waitingLimit = double.TryParse(configuration["Operations:WaitingWarningMinutes"], out var minutes) && minutes > 0 ? (double?)minutes : null;
        var warning = order.TrangThai == "DELIVERY_FAILED" ? "Giao thất bại" : balance.PhaiHoan > 0 ? "Cần xử lý hoàn" : balance.ConThu > 0 ? "Chưa thu đủ" :
            order.TrangThai == "PENDING_CONFIRMATION" && waitingLimit is { } limit && DateTime.UtcNow - order.ThoiDiemTao > TimeSpan.FromMinutes(limit) ? "Chờ xác nhận lâu" : "";
        var pending = await CancellationWorkflow.Pending(db, order.MaDonHang);
        if (pending is not null) warning = pending.TrangThai == "REQUESTED" ? "Khách yêu cầu hủy · chờ Nhân viên chuyển" : "Yêu cầu hủy · chờ Quản lý duyệt";
        return new(order.MaDonHang, FourChang.Presentation.Shared.Contracts.Common.OrderCode.Format(order.MaDonHang, order.KenhDatHang), order.KenhDatHang, order.HinhThucNhanMon, order.TrangThai, order.MaKhachHang,
            order.MaCaLamViec, employee?.HoTen, shift?.MaMay, order.ThoiDiemTao, order.HoanTatLuc, Math.Max(0, balance.NghiaVu - order.PhiGiaoHangHienTai),
            order.PhiGiaoHangHienTai, balance, order.PhienBanDuLieu, warning, pending);
    }
}
