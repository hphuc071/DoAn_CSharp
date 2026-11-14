using System.Security.Claims;
using System.Text.Json;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Api.Security;
using FourChang.Api.Shared.Operations;
using FourChang.Domain.Entities.Orders;
using FourChang.Domain.Entities.Inventory;
using FourChang.Domain.Entities.Payments;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Presentation.Shared.Contracts.Staff;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Features.Staff.Sales;

/// <summary>Thu ngân POS thật: nhiều dòng cấu hình, chốt giá/BOM và giữ kho trong cùng giao dịch với thu tiền/audit.</summary>
public sealed class PosCheckoutService(CafeDbContext db, IConfiguration config, FourChang.Api.Features.Admin.Menu.CatalogReadService catalog)
{
    /// <summary>Trả trạng thái đã lưu, chỉ cho ca sở hữu đọc; không tạo lại đơn hoặc hóa đơn.</summary>
    public async Task<PosCheckoutReceipt> Read(long id, long shift)
    {
        var order = await db.DonHang.AsNoTracking().SingleOrDefaultAsync(x => x.MaDonHang == id && x.MaCaLamViec == shift)
            ?? throw new AdminRule(404, "Không tìm thấy đơn của ca này.");
        var payment = await db.ThanhToan.AsNoTracking().SingleAsync(x => x.MaDonHang == id);
        var invoice = await db.HoaDon.AsNoTracking().Where(x => x.MaDonHang == id && x.LoaiHoaDon == "ORIGINAL").Select(x => (long?)x.MaHoaDon).SingleOrDefaultAsync();
        return new(id, FourChang.Presentation.Shared.Contracts.Common.OrderCode.Format(order.MaDonHang, order.KenhDatHang), payment.MaThanhToan, payment.SoTienYeuCau,
            payment.PhuongThuc == "CASH" ? (payment.TienKhachDua ?? 0) - payment.SoTienDaXacMinh : 0, payment.TrangThai, invoice);
    }

    /// <summary>Chống lặp bằng audit; kiểm tra lại ca, menu, giá, lựa chọn, tồn tổng trước mọi ghi. Không tin giá UI.</summary>
    public Task<IResult> Checkout(PosCheckoutInput input, HttpContext context) => AdminOperation.Execute(db,
        input.MaThaoTac, "POS_CHECKOUT", "DonHang", input, context, async () =>
    {
        var grant = await ShiftSessionCodec.Resolve(context, db);
        if (grant is null || grant.ReconciliationOnly || !await db.CaLamViec.AnyAsync(x => x.MaCaLamViec == grant.Shift && x.TrangThai == "OPEN"))
            throw new AdminRule(403, "Cần phiên nhân viên và ca đang mở.");
        if (input.HinhThuc is not ("TAKEAWAY" or "DINE_IN") || input.PhuongThuc is not ("CASH" or "BANK_TRANSFER") || input.Items is null || input.Items.Count is < 1 or > 50 ||
            input.Items.Any(x => x is null || x.GhiChu?.Length > 500)) throw new AdminRule(400, "Kiểm tra giỏ hàng, hình thức và phương thức thanh toán.");
        Ban? table = null;
        if (input.HinhThuc == "DINE_IN") table = await db.Ban.SingleOrDefaultAsync(x => x.MaBan == input.MaBan && x.DangSuDung)
            ?? throw new AdminRule(400, "Chọn bàn đang hoạt động.");
        else if (input.MaBan is not null) throw new AdminRule(400, "Đơn mang đi không gắn bàn.");
        OrderPriceSummary price;
        try { price = await OrderPricingService.CalculateOrderTotalAsync(db, "TAKEAWAY", null, input.Items, CancellationToken.None); }
        catch (OrderQuoteValidationException ex) { throw new AdminRule(400, ex.Message); }
        if (price.TongCong != input.TongTienDaChapNhan) throw new AdminRule(409, "Giá đã thay đổi. Kiểm tra lại món và xác nhận tổng mới.");
        if (input.PhuongThuc == "CASH" && (input.TienKhachDua is not { } tender || tender < price.TongCong || tender > 999999999999999999m || tender != decimal.Truncate(tender)))
            throw new AdminRule(400, "Tiền khách đưa phải đủ tổng tiền và là số đồng nguyên.");
        var now = DateTime.UtcNow;
        var timeout = config.GetValue<int?>("Orders:PendingPaymentTimeoutMinutes");
        if (input.PhuongThuc == "BANK_TRANSFER" && (timeout is null or < 1 or > 1440))
            throw new AdminRule(503, "Chưa cấu hình thời hạn chờ chuyển khoản (1–1440 phút).");
        DateTime? expiresAt = input.PhuongThuc == "BANK_TRANSFER" ? now.AddMinutes(timeout!.Value) : null;
        var visible = await catalog.Read();
        if (input.Items.Any(item => !visible.Mon.Any(m => m.CauHinh.Any(v => v.MaCauHinhMon == item.MaCauHinhMon && v.ConHang))))
            throw new AdminRule(409, "Món/cấu hình vừa hết hoặc chưa được công bố. Kiểm tra lại giỏ.");
        var req = new CreateCustomerOrderRequest(input.HinhThuc, table?.MaBanHienThi, null, null, null, null, null, input.Items);
        var variantIds = req.Items.Select(x => x.MaCauHinhMon).Distinct().ToArray();
        var variants = await (from v in db.CauHinhMon.AsNoTracking() join m in db.Mon.AsNoTracking() on v.MaMon equals m.MaMon
            where variantIds.Contains(v.MaCauHinhMon) select new { v.MaCauHinhMon, m.TenMon, v.KichCo, v.NhietDo, v.GiaBan }).ToDictionaryAsync(x => x.MaCauHinhMon);
        var ct = CancellationToken.None;
        var recipes = await db.CongThuc.AsNoTracking()
            .Where(x => x.TrangThai == "ACTIVE" && x.HieuLucTu <= now && (x.HieuLucDen == null || x.HieuLucDen > now))
            .ToListAsync(ct);
        var recipeLines = await db.ChiTietCongThuc.AsNoTracking().ToListAsync(ct);
        var toppingsMap = await db.MonThem.AsNoTracking()
            .Where(x => x.DangSuDung)
            .ToDictionaryAsync(x => x.MaMonThem, ct);
        // The active topping recipe is keyed by MaMonThem, not by a null variant ID.
        var toppingRecipes = recipes.Where(r => r.MaMonThem.HasValue)
            .ToDictionary(r => r.MaMonThem!.Value);
        var ingredientsMap = await (
            from n in db.NguyenLieu.AsNoTracking()
            join d in db.DonViTinh.AsNoTracking() on n.MaDonViCoSo equals d.MaDonVi
            select new { n.MaNguyenLieu, n.TenNguyenLieu, d.KyHieu, n.TonAnToan, n.DangSuDung }
        ).ToDictionaryAsync(x => x.MaNguyenLieu, ct);

        var stocks = await db.TonKho.ToDictionaryAsync(x => x.MaNguyenLieu, ct);
        // Keep full decimal precision until aggregating each ingredient over the WHOLE order.
        var perItemIngredientRequirements = new List<Dictionary<long, decimal>>();
        foreach (var item in req.Items)
        {
            var variantRecipe = recipes.FirstOrDefault(r => r.MaCauHinhMon == item.MaCauHinhMon);
            if (variantRecipe is null)
                throw new AdminRule(409, $"Món {variants[item.MaCauHinhMon].TenMon} chưa có công thức pha chế.");
            var itemReq = new Dictionary<long, decimal>();
            var variantLines = recipeLines.Where(l => l.MaCongThuc == variantRecipe.MaCongThuc).ToList();
            if (variantLines.Count == 0)
                throw new AdminRule(409, "Công thức món chưa có định mức nguyên liệu.");
            foreach (var line in variantLines)
            {
                if (line.TyLeHaoHut is < 0m or >= 1m || line.DinhMucCoSo <= 0m)
                    throw new AdminRule(409, "Định mức hoặc tỷ lệ hao hụt món không hợp lệ.");
                var factor = line.LoaiDinhMuc == "SUGAR" ? item.PhanTramDuong / 100m
                           : line.LoaiDinhMuc == "ICE" ? item.PhanTramDa / 100m : 1m;
                // No rounding here. Same ingredient may appear in multiple recipe lines.
                var perCup = line.DinhMucCoSo * factor / (1m - line.TyLeHaoHut);
                if (perCup > 0m)
                    itemReq[line.MaNguyenLieu] = itemReq.GetValueOrDefault(line.MaNguyenLieu) + perCup;
            }
            // Add the ingredient requirements of every topping serving as well.
            foreach (var top in item.Toppings ?? [])
            {
                if (!toppingsMap.ContainsKey(top.MaMonThem) ||
                    !toppingRecipes.TryGetValue(top.MaMonThem, out var topRecipe))
                    throw new AdminRule(409, $"Topping {top.MaMonThem} chưa có công thức đang hiệu lực.");

                var toppingLines = recipeLines.Where(l => l.MaCongThuc == topRecipe.MaCongThuc).ToList();
                if (toppingLines.Count == 0)
                    throw new AdminRule(409, $"Topping {top.MaMonThem} chưa có định mức nguyên liệu.");

                foreach (var line in toppingLines)
                {
                    if (line.TyLeHaoHut is < 0m or >= 1m || line.DinhMucCoSo <= 0m)
                        throw new AdminRule(409, "Định mức nguyên liệu topping không hợp lệ.");
                    // Until configured otherwise, a topping recipe must be FIXED per topping serving.
                    // Do not silently treat SUGAR/ICE topping components as FIXED.
                    if (line.LoaiDinhMuc != "FIXED")
                        throw new AdminRule(409, "Công thức topping có tùy chọn đường/đá chưa được định nghĩa.");
                    var perPortion = line.DinhMucCoSo / (1m - line.TyLeHaoHut);
                    if (perPortion > 0m)
                        itemReq[line.MaNguyenLieu] = itemReq.GetValueOrDefault(line.MaNguyenLieu)
                            + perPortion * top.SoPhanMoiLy;
                }
            }

            perItemIngredientRequirements.Add(itemReq);
        }
        // BR-23/24: first sum every line's exact quantity, then round up ONCE
        // per ingredient and allocate the 0.0001 units back to order lines.
        OrderBomAllocation.Result bomAllocation;
        try
        {
            bomAllocation = OrderBomAllocation.Calculate(
                perItemIngredientRequirements, req.Items.Select(x => x.SoLuong).ToArray());
        }
        catch (InvalidOperationException ex)
        {
            throw new AdminRule(409, ex.Message);
        }
        var totalRequiredByIngredient = bomAllocation.TotalByIngredient;
        foreach (var (ingId, needed) in totalRequiredByIngredient)
        {
            if (!ingredientsMap.TryGetValue(ingId, out var ing) || !ing.DangSuDung || !stocks.TryGetValue(ingId, out var stock))
                throw new AdminRule(409, "Nguyên liệu pha chế hiện không khả dụng.");
            var available = stock.TonThucTe - stock.TongGiuCho - ing.TonAnToan;
            if (available < needed)
                throw new AdminRule(409, $"Nguyên liệu '{ing.TenNguyenLieu}' vừa hết, vui lòng chỉnh lại giỏ hàng.");
        }


        var order = new DonHang { MaDonHienThi = "TMP-" + Guid.NewGuid().ToString("N")[..24], MaCaLamViec = grant.Shift,
            KenhDatHang = "POS", HinhThucNhanMon = input.HinhThuc, MaBan = table?.MaBan, MaBanDaChot = table?.MaBanHienThi,
            TrangThai = input.PhuongThuc == "CASH" ? "ACCEPTED" : "PENDING_PAYMENT", HetHanLuc = expiresAt,
            TiepNhanLuc = input.PhuongThuc == "CASH" ? now : null, ThoiDiemTao = now, ThoiDiemCapNhat = now };
        db.DonHang.Add(order); await db.SaveChangesAsync();
        // Cùng quy ước Web: lấy identity SQL, không tự đếm số tại máy POS.
        order.MaDonHienThi = FourChang.Presentation.Shared.Contracts.Common.OrderCode.Format(order.MaDonHang, order.KenhDatHang);
        for (var idx = 0; idx < req.Items.Count; idx++)
        {
            var item = req.Items[idx];
            var v = variants[item.MaCauHinhMon];
            var qty = item.SoLuong;
            var variantRecipe = recipes.First(r => r.MaCauHinhMon == item.MaCauHinhMon);
            var detail = new ChiTietDonHang
            {
                MaDonHang = order.MaDonHang,
                MaCauHinhMon = v.MaCauHinhMon,
                MaCongThuc = variantRecipe.MaCongThuc,
                TenMonDaChot = v.TenMon,
                KichCoDaChot = v.KichCo,
                NhietDoDaChot = v.NhietDo,
                PhanTramDuong = item.PhanTramDuong,
                PhanTramDa = item.PhanTramDa,
                GiaMonDaChot = v.GiaBan,
                SoLuongDat = qty,
                GhiChu = item.GhiChu,
                ThoiDiemTao = now,
                ThoiDiemCapNhat = now
            };
            db.ChiTietDonHang.Add(detail);
            await db.SaveChangesAsync(ct);
            foreach (var top in item.Toppings ?? [])
            {
                // Both were checked before any writes. Never silently discard a topping.
                var mt = toppingsMap[top.MaMonThem];
                var topRecipe = toppingRecipes[top.MaMonThem];
                db.MonThemChiTietDonHang.Add(new MonThemChiTietDonHang
                {
                    MaChiTietDonHang = detail.MaChiTietDonHang,
                    MaMonThem = mt.MaMonThem,
                    MaCongThuc = topRecipe.MaCongThuc,
                    TenMonThemDaChot = mt.TenMonThem,
                    SoPhanMoiLy = top.SoPhanMoiLy,
                    GiaMoiPhanDaChot = mt.GiaMoiPhan,
                    ThoiDiemTao = now,
                    ThoiDiemCapNhat = now
                });
            }
            var itemReq = perItemIngredientRequirements[idx];
            var lineReservation = bomAllocation.ReservedByLine[idx];
            foreach (var (ingId, perCup) in itemReq)
            {
                var ing = ingredientsMap[ingId];
                var reservedQty = lineReservation[ingId];
                db.NguyenLieuChiTietDonHang.Add(new NguyenLieuChiTietDonHang
                {
                    MaChiTietDonHang = detail.MaChiTietDonHang,
                    MaNguyenLieu = ingId,
                    TenNguyenLieuDaChot = ing.TenNguyenLieu,
                    DonViCoSoDaChot = ing.KyHieu,
                    // Snapshot column is decimal(18,4); the authoritative allocated
                    // total for stock is GiuChoNguyenLieu.SoLuongGiuCho below.
                    DinhMucMoiLy = OrderBomAllocation.RoundUp4(perCup),
                    ThoiDiemTao = now
                });
                db.GiuChoNguyenLieu.Add(new GiuChoNguyenLieu
                {
                    MaChiTietDonHang = detail.MaChiTietDonHang,
                    MaNguyenLieu = ingId,
                    SoLuongGiuCho = reservedQty,
                    SoLuongDaTieuHao = 0m,
                    SoLuongDaGiaiPhong = 0m,
                    HetHanLuc = expiresAt,
                    TrangThai = "RESERVED",
                    ThoiDiemTao = now,
                    ThoiDiemCapNhat = now
                });
                stocks[ingId].TongGiuCho += reservedQty;
                stocks[ingId].ThoiDiemCapNhat = now;
            }
        }

        var payment = new ThanhToan { MaThanhToanHienThi = "PAY-POS-" + order.MaDonHang, MaDonHang = order.MaDonHang, MaCaLamViec = grant.Shift,
            PhuongThuc = input.PhuongThuc, SoTienYeuCau = price.TongCong, SoTienDaXacMinh = input.PhuongThuc == "CASH" ? price.TongCong : 0,
            TienKhachDua = input.PhuongThuc == "CASH" ? input.TienKhachDua : null, TrangThai = input.PhuongThuc == "CASH" ? "VERIFIED" : "PENDING",
            XacMinhLuc = input.PhuongThuc == "CASH" ? now : null, HetHanLuc = expiresAt, MaThaoTac = input.MaThaoTac,
            MaThamChieuChuyenKhoan = input.PhuongThuc == "BANK_TRANSFER" ? "POS" + order.MaDonHang : null, ThoiDiemTao = now, ThoiDiemCapNhat = now };
        db.ThanhToan.Add(payment); await db.SaveChangesAsync();
        if (input.PhuongThuc == "CASH") await CheckoutInvoice.Issue(db, order, payment, now);
        db.SuKienDongBo.Add(new() { MaSuKien = Guid.NewGuid(), MaThaoTac = input.MaThaoTac, LoaiSuKien = "POS_ORDER_CREATED",
            NoiDungJson = JsonSerializer.Serialize(new { order.MaDonHang }), PhatSinhLuc = now });
        await db.SaveChangesAsync();
        return (order.MaDonHang, (object)await Read(order.MaDonHang, grant.Shift), (string?)null, "Tạo đơn tại quầy và ghi nhận phương thức thu");
    }, allowStaff: true);
}

/// <summary>Hóa đơn chốt từ dòng món đã lưu, không dùng giá thực đơn thay đổi sau thanh toán.</summary>
public static class CheckoutInvoice
{
    /// <summary>Phát hành một hóa đơn gốc khi đã thu đủ; caller sở hữu transaction và audit.</summary>
    public static async Task Issue(CafeDbContext db, DonHang order, ThanhToan payment, DateTime now)
    {
        if (await db.HoaDon.AnyAsync(x => x.MaDonHang == order.MaDonHang && x.LoaiHoaDon == "ORIGINAL")) return;
        var lines = await db.ChiTietDonHang.AsNoTracking().Where(x => x.MaDonHang == order.MaDonHang).ToListAsync();
        var ids = lines.Select(x => x.MaChiTietDonHang).ToArray();
        var toppings = await db.MonThemChiTietDonHang.AsNoTracking().Where(x => ids.Contains(x.MaChiTietDonHang)).ToListAsync();
        db.HoaDon.Add(new() { SoHoaDon = "HD-" + order.MaDonHang.ToString("D12"), MaDonHang = order.MaDonHang, LoaiHoaDon = "ORIGINAL",
            SoTien = payment.SoTienYeuCau, TrangThai = "ISSUED", PhatHanhLuc = now, MaCaLamViec = order.MaCaLamViec,
            NoiDungHoaDonDaChotJson = JsonSerializer.Serialize(new { order.MaDonHienThi, lines, toppings, Total = payment.SoTienYeuCau }),
            ThoiDiemTao = now, ThoiDiemCapNhat = now });
        await db.SaveChangesAsync();
    }
}
