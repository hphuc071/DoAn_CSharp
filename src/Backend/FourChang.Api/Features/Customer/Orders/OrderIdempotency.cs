using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FourChang.Domain.Entities.Audit;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FourChang.Api.Features.Customer.Orders;

// Phase 2C: a stable GUID per checkout intent, stored with its payload hash
// and original receipt.  No new tables or schema changes are required.
// SQL Server transaction-scoped application lock serializes concurrent calls
// with the SAME key, including across different Backend processes.
internal static class OrderIdempotency
{
    private const string HeaderName = "Idempotency-Key";

    internal static bool TryReadKey(HttpContext context, out Guid key)
    {
        key = default;
        return context.Request.Headers.TryGetValue(HeaderName, out var values) &&
            values.Count == 1 &&
            Guid.TryParse(values[0], out key) && key != Guid.Empty;
    }

    internal static byte[] Hash(CreateCustomerOrderRequest request) =>
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request));

    internal static bool TryReadAcceptedTotal(HttpContext context, out decimal total)
    {
        total = default;
        return context.Request.Headers.TryGetValue("X-Checkout-Accepted-Total", out var values) &&
            values.Count == 1 && decimal.TryParse(values[0], NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out total) && total >= 0m;
    }

    internal static bool MatchesAcceptedTotal(decimal acceptedTotal, OrderPriceSummary price)
        => acceptedTotal >= 0m && price.TienHang >= 0m && price.PhiGiaoHang >= 0m &&
           price.TongCong == price.TienHang + price.PhiGiaoHang && acceptedTotal == price.TongCong;

    // null = first request for this key. Non-null = replay or conflict.
    internal static async Task<IResult?> ReplayAsync(
        CafeDbContext db, long customerId, Guid key, byte[] fingerprint, decimal acceptedTotal, CancellationToken ct)
    {
        var previous = await db.NhatKyHeThong.AsNoTracking()
            .SingleOrDefaultAsync(x => x.MaThaoTac == key, ct);
        if (previous is null)
        {
            // Do not create a second order if the audit receipt was removed
            // but the payment operation (which also has a UNIQUE key) remains.
            var paymentExists = await db.ThanhToan.AsNoTracking()
                .AnyAsync(x => x.MaThaoTac == key, ct);
            return paymentExists
                ? Results.Conflict(new { message = "Yêu cầu đã có thanh toán nhưng thiếu biên nhận; cần nhân viên kiểm tra." })
                : null;
        }

        return ReplayResult(previous, customerId, fingerprint, acceptedTotal);
    }

    // The original receipt binds consent without changing historical DTO hashes or audit schema.
    internal static IResult ReplayResult(NhatKyHeThong previous, long customerId, byte[] fingerprint, decimal acceptedTotal)
    {
        // A key used for a different owner/purpose must never expose a receipt.
        if (previous.LoaiTacNhan != "CUSTOMER" ||
            previous.MaKhachHang != customerId ||
            previous.HanhDong != "CREATE_CUSTOMER_ORDER" ||
            previous.MaBamYeuCau is null ||
            !CryptographicOperations.FixedTimeEquals(previous.MaBamYeuCau, fingerprint))
            return Results.Conflict(new { message = "Idempotency-Key đã được sử dụng cho yêu cầu khác." });

        if (string.IsNullOrWhiteSpace(previous.KetQuaDaChotJson))
            return Results.Conflict(new { message = "Không tìm thấy biên nhận đã lưu của yêu cầu này." });

        // Original creation receipt is intentionally replayed. Current order
        // status must be fetched via the authenticated orders endpoint.
        CustomerOrderReceipt? receipt;
        try { receipt = JsonSerializer.Deserialize<CustomerOrderReceipt>(previous.KetQuaDaChotJson); }
        catch (JsonException) { receipt = null; }
        if (receipt is not null && acceptedTotal != receipt.TongCong)
            return Results.Conflict(new { code = "CHECKOUT_ACCEPTED_TOTAL_CONFLICT",
                message = "Idempotency-Key đã được xác nhận với tổng tiền khác. Giữ nguyên thao tác gốc để đối soát." });
        return receipt is null
            ? Results.Conflict(new { message = "Biên nhận đã lưu không hợp lệ." })
            : Results.Ok(receipt);
    }

    internal static async Task<IResult?> LockAndReplayAsync(
        CafeDbContext db, IDbContextTransaction transaction,
        long customerId, Guid key, byte[] fingerprint, decimal acceptedTotal, CancellationToken ct)
    {
        // This command participates in the SAME EF transaction as all order,
        // stock, payment and audit writes. Never take a session-owned lock.
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText =
            "DECLARE @lockResult int; " +
            "EXEC @lockResult = sys.sp_getapplock " +
            "@Resource=@keyResource, @LockMode='Exclusive', " +
            "@LockOwner='Transaction', @LockTimeout=10000; " +
            "SELECT @lockResult;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@keyResource";
        parameter.DbType = DbType.String;
        parameter.Size = 255;
        parameter.Value = "4chang.order." + key.ToString("N");
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(ct);
        if (result is null || result is DBNull ||
            Convert.ToInt32(result, CultureInfo.InvariantCulture) < 0)
            return Results.Conflict(new { message = "Yêu cầu đang được xử lý. Vui lòng thử lại với cùng Idempotency-Key." });

        // Different GUIDs can share SERIALIZABLE audit/payment index gaps and stock rows.
        // Coordinate Customer Create before reading them, avoiding S-range -> insert conversion
        // deadlocks. Existing receipt-only replays bypass this gate; no automatic retry.
        if (!await OrderIntegrationSafety.LockAsync(db, transaction, "4chang.customerorder.create", ct))
            return Results.Conflict(new { code = "CUSTOMER_ORDER_CREATE_BUSY",
                message = "Đang xử lý đơn khác. Giữ nguyên Idempotency-Key và nội dung để thử lại." });

        // Double-check AFTER acquiring the transaction lock, to close the
        // race where two identical requests both saw "not found" initially.
        return await ReplayAsync(db, customerId, key, fingerprint, acceptedTotal, ct);
    }

    internal static NhatKyHeThong CreateAudit(
        long customerId, Guid key, byte[] fingerprint,
        long orderId, CustomerOrderReceipt receipt, DateTime now) => new()
    {
        MaLienKetXuLy = Guid.NewGuid(),
        MaThaoTac = key,
        LoaiTacNhan = "CUSTOMER",
        MaKhachHang = customerId,
        TenNguoiThucHienDaChot = "Khach hang #" + customerId.ToString(CultureInfo.InvariantCulture),
        HanhDong = "CREATE_CUSTOMER_ORDER",
        LoaiDoiTuong = "DonHang",
        MaDoiTuong = orderId.ToString(CultureInfo.InvariantCulture),
        KetQua = "SUCCESS",
        MaBamYeuCau = fingerprint,
        KetQuaDaChotJson = JsonSerializer.Serialize(receipt),
        PhatSinhLuc = now,
        ThoiDiemTao = now
    };
}
