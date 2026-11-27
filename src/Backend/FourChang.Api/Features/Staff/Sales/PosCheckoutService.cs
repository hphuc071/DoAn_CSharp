// UC: UC-POS-02, UC-POS-05
// Chức năng: Tạo đơn tại quầy, chốt giá, giữ nguyên liệu và xử lý thanh toán theo phương thức chọn.
// Cần nhớ: Một lần thanh toán gửi lại không được tạo thêm đơn hoặc hóa đơn.

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
    /// <summary>
    /// UC-POS-02, UC-POS-05 — Xem lại kết quả bán hàng.
    ///
    /// Hàm này được dùng để:
    /// 1. Tìm đơn thuộc đúng ca đang yêu cầu xem.
    /// 2. Đọc thông tin thanh toán của đơn.
    /// 3. Đọc mã hóa đơn nếu đơn đã có hóa đơn.
    /// 4. Trả kết quả bán hàng cho giao diện hiển thị.
    ///
    /// Thông tin cần truyền vào:
    /// - id: Khóa của đơn hàng cần xử lý.
    /// - shift: Khóa ca làm việc đang yêu cầu xem.
    ///
    /// Cần nhớ: Hàm chỉ xem dữ liệu, không tạo đơn hoặc thu tiền thêm. Không có đơn thuộc ca này thì báo lỗi.
    /// </summary>
    public async Task<PosCheckoutReceipt> Read(long id, long shift)
    {
        var order = await db.DonHang.AsNoTracking().SingleOrDefaultAsync(x => x.MaDonHang == id && x.MaCaLamViec == shift)
            ?? throw new AdminRule(404, "Không tìm thấy đơn của ca này.");
        var payment = await db.ThanhToan.AsNoTracking().SingleAsync(x => x.MaDonHang == id);
        var invoice = await db.HoaDon.AsNoTracking().Where(x => x.MaDonHang == id && x.LoaiHoaDon == "ORIGINAL").Select(x => (long?)x.MaHoaDon).SingleOrDefaultAsync();
        return new(id, order.MaDonHienThi, payment.MaThanhToan, payment.SoTienYeuCau,
            0, payment.TrangThai, invoice);
    }

    /// <summary>
    /// UC-POS-02, UC-POS-05 — Thanh toán giỏ món tại quầy.
    ///
    /// Hàm này được dùng để:
    /// 1. Kiểm tra ca nhân viên có được phép bán hàng không.
    /// 2. Tính lại giá và kiểm tra các món, topping, nguyên liệu.
    /// 3. Tạo đơn tại quầy cùng các dòng món và lượng nguyên liệu giữ chỗ.
    /// 4. Ghi nhận phương thức thanh toán và tạo hóa đơn khi đủ điều kiện.
    /// 5. Trả kết quả để ứng dụng thông báo cho nhân viên.
    ///
    /// Thông tin cần truyền vào:
    /// - input: Giỏ món, hình thức nhận, phương thức thanh toán và tổng tiền đã chấp nhận.
    /// - context: Thông tin phiên và quyền của người đang thao tác.
    ///
    /// Sau khi xử lý: Biên nhận của đơn vừa xử lý hoặc kết quả cũ nếu cùng thao tác được gửi lại.
    /// </summary>
    public Task<IResult> Checkout(PosCheckoutInput input, HttpContext context) => AdminOperation.Execute(db,
        input.MaThaoTac, "POS_CHECKOUT", "DonHang", input, context, async () =>
    {

        // Bước 1: Kiểm tra phiên ca đang được phép bán hàng.
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

        // Bước 2: Tính lại giá và so với tổng tiền nhân viên đã chấp nhận.
        try { price = await OrderPricingService.CalculateOrderTotalAsync(db, "TAKEAWAY", null, input.Items, CancellationToken.None); }
        catch (OrderQuoteValidationException ex) { throw new AdminRule(400, ex.Message); }
        if (price.TongCong != input.TongTienDaChapNhan) throw new AdminRule(409, "Giá đã thay đổi. Kiểm tra lại món và xác nhận tổng mới.");
        if (input.PhuongThuc == "CASH" && (input.TienKhachDua is not { } tender || tender != price.TongCong || tender > 999999999999999999m || tender != decimal.Truncate(tender)))
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
            select new { n.MaNguyenLieu, n.TenNguyenLieu, n.DonViCoSo, n.DangSuDung }
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
                if (line.DinhMucCoSo <= 0m)
                    throw new AdminRule(409, "Lượng nguyên liệu của món không hợp lệ.");
                var factor = line.LoaiDinhMuc == "SUGAR" ? item.PhanTramDuong / 100m
                           : line.LoaiDinhMuc == "ICE" ? item.PhanTramDa / 100m : 1m;
                // No rounding here. Same ingredient may appear in multiple recipe lines.
                var perCup = line.DinhMucCoSo * factor;
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
                    if (line.DinhMucCoSo <= 0m)
                        throw new AdminRule(409, "Định mức nguyên liệu topping không hợp lệ.");
                    // Until configured otherwise, a topping recipe must be FIXED per topping serving.
                    // Do not silently treat SUGAR/ICE topping components as FIXED.
                    if (line.LoaiDinhMuc != "FIXED")
                        throw new AdminRule(409, "Công thức topping có tùy chọn đường/đá chưa được định nghĩa.");
                    var perPortion = line.DinhMucCoSo;
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
            var available = stock.TonThucTe - stock.TongGiuCho;
            if (available < needed)
                throw new AdminRule(409, $"Nguyên liệu '{ing.TenNguyenLieu}' vừa hết, vui lòng chỉnh lại giỏ hàng.");
        }


        var order = new DonHang { MaCaLamViec = grant.Shift,
            KenhDatHang = "POS", HinhThucNhanMon = input.HinhThuc, MaBan = table?.MaBan, MaBanDaChot = table?.MaBanHienThi,
            TrangThai = input.PhuongThuc == "CASH" ? "ACCEPTED" : "PENDING_PAYMENT", HetHanLuc = expiresAt,
            TiepNhanLuc = input.PhuongThuc == "CASH" ? now : null, ThoiDiemTao = now, ThoiDiemCapNhat = now };

        // Bước 3: Lưu đầu đơn tại quầy; các kiểm tra cấu hình/nguyên liệu ở phía trên phải hợp lệ.
        db.DonHang.Add(order); await db.SaveChangesAsync();
        // Sequence SQL dùng chung cho các máy POS, độc lập với khóa identity.
        // Mã hiển thị được cấp độc lập với identity khi lưu đơn.

        // Bước 4: Lưu các món trong giỏ và phần nguyên liệu cần giữ.
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
                    GiaMoiPhanDaChot = mt.GiaMoiPhan
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
                    DonViCoSoDaChot = ing.DonViCoSo,
                    // Snapshot column is decimal(18,4); the authoritative allocated
                    // total for stock is NguyenLieuChiTietDonHang.SoLuongGiuCho below.
                    DinhMucMoiLy = OrderBomAllocation.RoundUp4(perCup),
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


        // Bước 5: Ghi nhận thanh toán theo phương thức chọn; chuyển khoản phải theo luồng xác nhận tương ứng.
        var payment = new ThanhToan { MaThanhToanHienThi = "PAY-POS-" + order.MaDonHang, MaDonHang = order.MaDonHang, MaCaLamViec = grant.Shift,
            PhuongThuc = input.PhuongThuc, SoTienYeuCau = price.TongCong, SoTienDaXacMinh = input.PhuongThuc == "CASH" ? price.TongCong : 0,
            TrangThai = input.PhuongThuc == "CASH" ? "VERIFIED" : "PENDING",
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
    /// <summary>
    /// UC-POS-05, UC-WEB-07 — Tạo hóa đơn gốc nếu đơn chưa có hóa đơn gốc.
    ///
    /// Hàm này được dùng để:
    /// 1. Kiểm tra đơn đã có hóa đơn gốc chưa.
    /// 2. Nếu chưa có, đọc các món và topping đã lưu trong đơn.
    /// 3. Lưu hóa đơn cùng nội dung và giá tại thời điểm phát hành.
    ///
    /// Thông tin cần truyền vào:
    /// - db: Đối tượng dùng để đọc và ghi dữ liệu của hệ thống.
    /// - order: Thông tin đơn hàng đã được đọc từ database.
    /// - payment: Thông tin thanh toán liên quan đến đơn.
    /// - now: Thời điểm đang thực hiện việc xử lý.
    ///
    /// Cần nhớ: Nếu đơn đã có hóa đơn gốc thì dừng, không phát hành thêm hóa đơn gốc.
    /// </summary>
    public static async Task Issue(CafeDbContext db, DonHang order, ThanhToan payment, DateTime now)
    {
        if (await db.HoaDon.AnyAsync(x => x.MaDonHang == order.MaDonHang && x.LoaiHoaDon == "ORIGINAL")) return;
        var lines = await db.ChiTietDonHang.AsNoTracking().Where(x => x.MaDonHang == order.MaDonHang).ToListAsync();
        var ids = lines.Select(x => x.MaChiTietDonHang).ToArray();
        var toppings = await db.MonThemChiTietDonHang.AsNoTracking().Where(x => ids.Contains(x.MaChiTietDonHang)).ToListAsync();
        db.HoaDon.Add(new() { MaDonHang = order.MaDonHang, LoaiHoaDon = "ORIGINAL",
            SoTien = payment.SoTienYeuCau, TrangThai = "ISSUED", PhatHanhLuc = now, MaCaLamViec = order.MaCaLamViec,
            NoiDungHoaDonDaChotJson = JsonSerializer.Serialize(new { order.MaDonHienThi, lines, toppings, Total = payment.SoTienYeuCau }),
            ThoiDiemTao = now, ThoiDiemCapNhat = now });
        await db.SaveChangesAsync();
    }
}
