using System.Data;
using System.Security.Claims;
using FourChang.Domain.Entities.Payments;
using FourChang.Domain.Entities.Inventory;
using FourChang.Domain.Entities.Orders;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Customer.Orders;

public static class CustomerOrdersEndpoints
{
    public static void MapCustomerOrders(this WebApplication app)
    {
        app.MapGet("/api/customer/orders", async (HttpContext context, CafeDbContext db) =>
        {
            var id = long.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            return await db.DonHang.AsNoTracking()
                .Where(x => x.MaKhachHang == id)
                .OrderByDescending(x => x.ThoiDiemTao)
                .Take(100)
                .Select(x => new { x.MaDonHang, x.MaDonHienThi, x.HinhThucNhanMon, x.TrangThai, x.ThoiDiemTao })
                .ToListAsync();
        }).RequireAuthorization("Customer");

        app.MapPost("/api/customer/orders/quote", async (CreateCustomerOrderRequest req, CafeDbContext db, CancellationToken ct) =>
        {
            try
            {
                var summary = await OrderPricingService.CalculateOrderTotalAsync(db, req.HinhThucNhanMon, req.KhoangCachKm, req.Items, ct);
                return Results.Ok(summary);
            }
            catch (OrderQuoteValidationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        app.MapPost("/api/customer/orders", async (CreateCustomerOrderRequest req, HttpContext context, CafeDbContext db, CancellationToken ct) =>
        {
            var claim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!long.TryParse(claim, out var customerId) || customerId <= 0)
                return Results.Unauthorized();
            if (!OrderIdempotency.TryReadKey(context, out var idempotencyKey))
                return Results.BadRequest(new { message = "Cần Idempotency-Key là một GUID khác rỗng cho mỗi lần xác nhận đặt hàng." });
            var requestFingerprint = OrderIdempotency.Hash(req);
            var earlyReplay = await OrderIdempotency.ReplayAsync(
                db, customerId, idempotencyKey, requestFingerprint, ct);
            if (earlyReplay is not null)
                return earlyReplay;

            if (req.Items is null || req.Items.Count == 0)
                return Results.BadRequest(new { message = "Giỏ hàng đang trống." });
            // Validate before stock checks or any database writes.
            OrderPriceSummary priceSummary;
            try
            {
                priceSummary = await OrderPricingService.CalculateOrderTotalAsync(
                    db, req.HinhThucNhanMon, req.KhoangCachKm, req.Items, ct);
            }
            catch (OrderQuoteValidationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            var now = DateTime.UtcNow;
            var expiresAt = now.AddMinutes(10);
            var variantIds = req.Items.Select(x => x.MaCauHinhMon).Distinct().ToList();
            var variants = await (
                from v in db.CauHinhMon.AsNoTracking()
                join m in db.Mon.AsNoTracking() on v.MaMon equals m.MaMon
                where variantIds.Contains(v.MaCauHinhMon) && v.DangBan && m.DangBan
                select new { v.MaCauHinhMon, v.MaMon, m.TenMon, v.KichCo, v.NhietDo, v.GiaBan }
            ).ToDictionaryAsync(x => x.MaCauHinhMon, ct);
            if (variants.Count != variantIds.Count)
                return Results.Conflict(new { message = "Một số món trong giỏ đã ngừng bán." });
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

            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var lockedReplay = await OrderIdempotency.LockAndReplayAsync(
                db, tx, customerId, idempotencyKey, requestFingerprint, ct);
            if (lockedReplay is not null)
                return lockedReplay;

            var stocks = await db.TonKho.ToDictionaryAsync(x => x.MaNguyenLieu, ct);
            var totalRequiredByIngredient = new Dictionary<long, decimal>();
            var perItemIngredientRequirements = new List<Dictionary<long, decimal>>();
            foreach (var item in req.Items)
            {
                var qty = item.SoLuong;
                var variantRecipe = recipes.FirstOrDefault(r => r.MaCauHinhMon == item.MaCauHinhMon);
                if (variantRecipe is null)
                    return Results.Conflict(new { message = $"Món {variants[item.MaCauHinhMon].TenMon} chưa có công thức pha chế." });
                var itemReq = new Dictionary<long, decimal>();
                foreach (var line in recipeLines.Where(l => l.MaCongThuc == variantRecipe.MaCongThuc))
                {
                    var factor = line.LoaiDinhMuc == "SUGAR" ? item.PhanTramDuong / 100m
                               : line.LoaiDinhMuc == "ICE" ? item.PhanTramDa / 100m : 1m;
                    var perCup = decimal.Ceiling(line.DinhMucCoSo * factor / (1m - line.TyLeHaoHut) * 10000m) / 10000m;
                    if (perCup > 0m)
                        itemReq[line.MaNguyenLieu] = itemReq.GetValueOrDefault(line.MaNguyenLieu) + perCup;
                }
                // Add the ingredient requirements of every topping serving as well.
                foreach (var top in item.Toppings ?? [])
                {
                    if (!toppingsMap.ContainsKey(top.MaMonThem) ||
                        !toppingRecipes.TryGetValue(top.MaMonThem, out var topRecipe))
                        return Results.Conflict(new { message = $"Topping {top.MaMonThem} chưa có công thức đang hiệu lực." });

                    var toppingLines = recipeLines.Where(l => l.MaCongThuc == topRecipe.MaCongThuc).ToList();
                    if (toppingLines.Count == 0)
                        return Results.Conflict(new { message = $"Topping {top.MaMonThem} chưa có định mức nguyên liệu." });

                    foreach (var line in toppingLines)
                    {
                        if (line.TyLeHaoHut is < 0m or >= 1m || line.DinhMucCoSo < 0m)
                            return Results.Conflict(new { message = "Định mức nguyên liệu topping không hợp lệ." });
                        // Topping recipes describe each topping portion, independent of drink sugar/ice choices.
                        var perPortion = decimal.Ceiling(line.DinhMucCoSo / (1m - line.TyLeHaoHut) * 10000m) / 10000m;
                        if (perPortion > 0m)
                            itemReq[line.MaNguyenLieu] = itemReq.GetValueOrDefault(line.MaNguyenLieu)
                                + perPortion * top.SoPhanMoiLy;
                    }
                }

                foreach (var kv in itemReq)
                    totalRequiredByIngredient[kv.Key] = totalRequiredByIngredient.GetValueOrDefault(kv.Key) + kv.Value * qty;
                perItemIngredientRequirements.Add(itemReq);
            }
            foreach (var (ingId, needed) in totalRequiredByIngredient)
            {
                if (!ingredientsMap.TryGetValue(ingId, out var ing) || !ing.DangSuDung || !stocks.TryGetValue(ingId, out var stock))
                    return Results.Conflict(new { message = "Nguyên liệu pha chế hiện không khả dụng." });
                var available = stock.TonThucTe - stock.TongGiuCho - ing.TonAnToan;
                if (available < needed)
                    return Results.Conflict(new { message = $"Nguyên liệu '{ing.TenNguyenLieu}' vừa hết, vui lòng chỉnh lại giỏ hàng." });
            }

            long? maBan = null;
            if (string.Equals(req.HinhThucNhanMon, "DINE_IN", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(req.MaBanHienThi))
            {
                var ban = await db.Ban.AsNoTracking().FirstOrDefaultAsync(b => b.MaBanHienThi == req.MaBanHienThi && b.DangSuDung, ct);
                maBan = ban?.MaBan;
            }

            // 16 hex characters (64 random bits) instead of only 9,000 possibilities per day.
            // Database UNIQUE index is still the final collision safeguard.
            var orderCode = $"ORD-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..16]}";
            var order = new DonHang
            {
                MaDonHienThi = orderCode,
                MaKhachHang = customerId,
                KenhDatHang = "WEB",
                HinhThucNhanMon = req.HinhThucNhanMon.ToUpperInvariant(),
                MaBan = maBan,
                MaBanDaChot = req.MaBanHienThi,
                TenNguoiNhan = req.TenNguoiNhan,
                DienThoaiNguoiNhan = req.DienThoaiNguoiNhan,
                DiaChiGiaoDaChot = req.DiaChiGiao,
                KhoangCachGiaoKm = req.KhoangCachKm,
                PhiGiaoHangBanDau = priceSummary.PhiGiaoHang,
                PhiGiaoHangHienTai = priceSummary.PhiGiaoHang,
                TrangThai = "PENDING_PAYMENT",
                GhiChu = req.GhiChu,
                HetHanLuc = expiresAt,
                ThoiDiemTao = now,
                ThoiDiemCapNhat = now
            };
            db.DonHang.Add(order);
            await db.SaveChangesAsync(ct);
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
                foreach (var (ingId, perCup) in itemReq)
                {
                    var ing = ingredientsMap[ingId];
                    var reservedQty = perCup * qty;
                    db.NguyenLieuChiTietDonHang.Add(new NguyenLieuChiTietDonHang
                    {
                        MaChiTietDonHang = detail.MaChiTietDonHang,
                        MaNguyenLieu = ingId,
                        TenNguyenLieuDaChot = ing.TenNguyenLieu,
                        DonViCoSoDaChot = ing.KyHieu,
                        DinhMucMoiLy = perCup,
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
            // Create an unpaid payment intent in the same transaction as the order.
            // QR generation and payment verification are NOT performed here.
            db.ThanhToan.Add(new ThanhToan
            {
                MaThanhToanHienThi = $"PAY-{order.MaDonHang:D12}",
                MaDonHang = order.MaDonHang,
                PhuongThuc = "BANK_TRANSFER",
                SoTienYeuCau = priceSummary.TongCong,
                SoTienDaXacMinh = 0m,
                TrangThai = "PENDING",
                MaThaoTac = idempotencyKey,
                HetHanLuc = expiresAt,
                ThoiDiemTao = now,
                ThoiDiemCapNhat = now
            });

            var receipt = new CustomerOrderReceipt(
                order.MaDonHang,
                order.MaDonHienThi,
                order.TrangThai,
                priceSummary.TienHang,
                priceSummary.PhiGiaoHang,
                priceSummary.TongCong,
                order.HetHanLuc);
            db.NhatKyHeThong.Add(OrderIdempotency.CreateAudit(
                customerId, idempotencyKey, requestFingerprint, order.MaDonHang, receipt, now));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Results.Created($"/api/customer/orders/{order.MaDonHang}", receipt);
        }).RequireAuthorization("Customer");

        // Giữ API hủy đơn UC-WEB-08 của Thái.
        CustomerOrderCancellation.Map(app);
        OrderExpiryReview.Map(app);
    }
}
