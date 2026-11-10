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
        app.MapGet("/api/customer/orders", async (HttpContext context, CafeDbContext db,
            ICustomerOrderReader reader, int? page, int? pageSize, string? status,
            DateTime? fromUtc, DateTime? toUtc, CancellationToken ct) =>
        {
            // Explicit pagination selects the new snapshot contract; legacy callers keep their array.
            if (page.HasValue || pageSize.HasValue || status is not null || fromUtc.HasValue || toUtc.HasValue)
                return await CustomerOrderReadEndpoints.ListAsync(context, reader,
                    page, pageSize, status, fromUtc, toUtc, ct);
            context.Response.Headers.CacheControl = "no-store";
            if (!CustomerOrderReadEndpoints.Owner(context, out var id)) return Results.Unauthorized();
            return Results.Ok(await db.DonHang.AsNoTracking()
                .Where(x => x.MaKhachHang == id)
                .OrderByDescending(x => x.ThoiDiemTao)
                .Take(100)
                .Select(x => new { x.MaDonHang, x.MaDonHienThi, x.HinhThucNhanMon, x.TrangThai, x.ThoiDiemTao })
                .ToListAsync(ct));
        }).RequireAuthorization("Customer");

        app.MapPost("/api/customer/orders/quote", async (CreateCustomerOrderRequest req, CafeDbContext db, CancellationToken ct) =>
        {
            // BR-16: fail closed on a hybrid TAKEAWAY/table/delivery payload at the
            // API boundary too; direct callers must not bypass the Web validation.
            if (req is null || !string.Equals(req.HinhThucNhanMon, "TAKEAWAY", StringComparison.Ordinal) ||
                req.MaBanHienThi is not null || req.DiaChiGiao is not null ||
                req.KhoangCachKm is not null || (req.GhiChu?.Length ?? 0) > 500)
                return Results.BadRequest(new { message = "Chỉ báo giá mang đi không kèm dữ liệu bàn/giao hàng." });
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

        app.MapPost("/api/customer/orders", CreateAsync).RequireAuthorization("Customer");
        app.MapPost("/api/customer/orders/fulfillment-preview", FulfillmentPreviewAsync)
            .RequireAuthorization("Customer");

        // Giữ API hủy đơn UC-WEB-08 của Thái.
        CustomerOrderCancellation.Map(app);
        OrderExpiryReview.Map(app);
    }
    public static async Task<IResult> FulfillmentPreviewAsync(FulfillmentPreviewRequest request,
        HttpContext context, [Microsoft.AspNetCore.Mvc.FromServices] FulfillmentPreviewService service,
        CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!CustomerOrderReadEndpoints.Owner(context, out var owner)) return Results.Unauthorized();
        try { return Results.Ok(await service.PreviewAsync(owner, request, ct)); }
        catch (FulfillmentUnavailable ex) { return Results.Json(new { code = ex.Code }, statusCode: ex.Status); }
        catch (OrderQuoteValidationException) { return Results.Json(new { code = "CART_INVALID" }, statusCode: 400); }
    }
    // A separate Backend capability: Web configuration cannot secure a direct API caller.
    public static async Task<IResult> CreateAsync(CreateCustomerOrderRequest req, HttpContext context,
        CafeDbContext db, CancellationToken ct, IConfiguration configuration)
    {
        if (!bool.TryParse(configuration["Orders:CustomerCreationEnabled"], out var enabled) || !enabled)
            return Results.Json(new { code = "CUSTOMER_ORDER_CREATION_DISABLED" }, statusCode: 503);
        var claim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(claim, out var customerId) || customerId <= 0)
            return Results.Unauthorized();
        if (!OrderIdempotency.TryReadKey(context, out var idempotencyKey))
            return Results.BadRequest(new { message = "Cần Idempotency-Key là một GUID khác rỗng cho mỗi lần xác nhận đặt hàng." });
        // Read-only previews do not authorize writes. No flag may bypass the missing atomic
        // QR/quote revalidation + snapshot integration. Preserve the existing TAKEAWAY protocol.
        if (string.Equals(req.HinhThucNhanMon, "DINE_IN", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(req.HinhThucNhanMon, "DELIVERY", StringComparison.OrdinalIgnoreCase))
            return Results.Json(new { code = "FULFILLMENT_CREATION_NOT_VALIDATED" }, statusCode: 503);
        // Customer WEB always requires consent, including direct API calls and replays.
        // This is independent of POS/Admin contracts and precedes any business SQL.
        if (!OrderIdempotency.TryReadAcceptedTotal(context, out var acceptedTotal))
            return Results.BadRequest(new { code = "CHECKOUT_ACCEPTED_TOTAL_INVALID",
                message = "Cần một X-Checkout-Accepted-Total duy nhất, không âm và theo định dạng số bất biến." });
        var requestFingerprint = OrderIdempotency.Hash(req);
        var earlyReplay = await OrderIdempotency.ReplayAsync(
            db, customerId, idempotencyKey, requestFingerprint, acceptedTotal, ct);
        if (earlyReplay is not null)
            return earlyReplay;

        // Historical DTO hashes/receipts are retained. Validate new operations only after replay.
        if (!string.Equals(req.HinhThucNhanMon, "TAKEAWAY", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(req.TenNguoiNhan) || req.TenNguoiNhan.Length > 120 ||
                req.TenNguoiNhan.Any(char.IsControl) ||
                !System.Text.RegularExpressions.Regex.IsMatch(req.DienThoaiNguoiNhan ?? "", @"^\+?[0-9]{9,15}\z") ||
                req.MaBanHienThi is not null || req.DiaChiGiao is not null || req.KhoangCachKm is not null)
            return Results.BadRequest(new { message = "Thông tin nhận mang đi không hợp lệ." });

        // Replays above keep their original receipt. New operations must not write
        // an order note longer than the Web/quote contract (including direct API callers).
        if ((req.GhiChu?.Length ?? 0) > 500)
            return Results.BadRequest(new { code = "CHECKOUT_NOTE_TOO_LONG",
                message = "Ghi chú đơn hàng không được vượt 500 ký tự." });

        if (req.Items is null || req.Items.Count == 0)
            return Results.BadRequest(new { message = "Giỏ hàng đang trống." });
        // Hold menu/recipe read locks until commit, so the authoritative total and persisted
        // line/topping/BOM snapshots cannot come from different menu versions.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var lockedReplay = await OrderIdempotency.LockAndReplayAsync(
            db, tx, customerId, idempotencyKey, requestFingerprint, acceptedTotal, ct);
        if (lockedReplay is not null) return lockedReplay;
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
        if (!OrderIdempotency.MatchesAcceptedTotal(acceptedTotal, priceSummary))
            return Results.Conflict(new { code = "CHECKOUT_PRICE_CHANGED", message = "Giá đã thay đổi. Vui lòng lấy báo giá mới và xác nhận lại trước khi đặt." });
        // BR-17: payment deadline is an operator-configured policy, not a fixed 10-minute SLA.
        // Refuse new order creation if the policy is missing or invalid; do not guess a deadline.
        var timeoutText = configuration["Orders:PendingPaymentTimeoutMinutes"];
        if (!int.TryParse(timeoutText, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var timeoutMinutes)
            || timeoutMinutes is < 1 or > 1440)
        {
            return Results.Problem(
                "Chưa cấu hình thời hạn chờ thanh toán hợp lệ cho đơn Web (1 đến 1440 phút).",
                statusCode: 503);
        }
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(timeoutMinutes);
        // Keep old idempotent receipts: optional pickup checks run only AFTER replay.
        var pickupError = TakeawayPickupSchedule.Validate(req.ThoiDiemHenNhan, configuration,
            new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)));
        if (pickupError is not null)
            return Results.Json(new { code = pickupError,
                message = "Giờ nhận chưa hợp lệ hoặc chính sách hẹn giờ chưa được phê duyệt. Giữ nguyên mã thao tác để đối soát." },
                statusCode: pickupError.Contains("POLICY", StringComparison.Ordinal) || pickupError.EndsWith("DISABLED", StringComparison.Ordinal) ? 503 : 409);
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

        var stocks = await db.TonKho.ToDictionaryAsync(x => x.MaNguyenLieu, ct);
        // Keep full decimal precision until aggregating each ingredient over the WHOLE order.
        var perItemIngredientRequirements = new List<Dictionary<long, decimal>>();
        foreach (var item in req.Items)
        {
            var variantRecipe = recipes.FirstOrDefault(r => r.MaCauHinhMon == item.MaCauHinhMon);
            if (variantRecipe is null)
                return Results.Conflict(new { message = $"Món {variants[item.MaCauHinhMon].TenMon} chưa có công thức pha chế." });
            var itemReq = new Dictionary<long, decimal>();
            var variantLines = recipeLines.Where(l => l.MaCongThuc == variantRecipe.MaCongThuc).ToList();
            if (variantLines.Count == 0)
                return Results.Conflict(new { message = "Công thức món chưa có định mức nguyên liệu." });
            foreach (var line in variantLines)
            {
                if (line.TyLeHaoHut is < 0m or >= 1m || line.DinhMucCoSo <= 0m)
                    return Results.Conflict(new { message = "Định mức hoặc tỷ lệ hao hụt món không hợp lệ." });
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
                    return Results.Conflict(new { message = $"Topping {top.MaMonThem} chưa có công thức đang hiệu lực." });

                var toppingLines = recipeLines.Where(l => l.MaCongThuc == topRecipe.MaCongThuc).ToList();
                if (toppingLines.Count == 0)
                    return Results.Conflict(new { message = $"Topping {top.MaMonThem} chưa có định mức nguyên liệu." });

                foreach (var line in toppingLines)
                {
                    if (line.TyLeHaoHut is < 0m or >= 1m || line.DinhMucCoSo <= 0m)
                        return Results.Conflict(new { message = "Định mức nguyên liệu topping không hợp lệ." });
                    // Until configured otherwise, a topping recipe must be FIXED per topping serving.
                    // Do not silently treat SUGAR/ICE topping components as FIXED.
                    if (line.LoaiDinhMuc != "FIXED")
                        return Results.Conflict(new { message = "Công thức topping có tùy chọn đường/đá chưa được định nghĩa." });
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
            return Results.Conflict(new { message = ex.Message });
        }
        var totalRequiredByIngredient = bomAllocation.TotalByIngredient;
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
            ThoiDiemHenNhan = req.ThoiDiemHenNhan?.UtcDateTime,
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
        db.NhatKyHeThong.Add(OrderAuditOutbox.Stamp(OrderIdempotency.CreateAudit(
            customerId, idempotencyKey, requestFingerprint, order.MaDonHang, receipt, now)));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Results.Created($"/api/customer/orders/{order.MaDonHang}", receipt);
    }

}
