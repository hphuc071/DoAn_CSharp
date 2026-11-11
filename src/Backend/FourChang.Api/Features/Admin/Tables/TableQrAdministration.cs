using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Domain.Entities.Audit;
using FourChang.Infrastructure.Database.Context;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Tables;

// Dormant until an authorized QR protocol, issuer channel and isolated SQL fixture are approved.
// Never expose a QR secret in URLs, logs, audit rows or an idempotency replay response.
public sealed record TableQrMutationRequest(string? ExpectedVersion, string? Reason,
    DateTimeOffset? ExpiresAtUtc = null);
public sealed record TableQrAdminContext(long TableId, string TableCode, int QrVersion,
    string RowVersion, DateTimeOffset? ExpiresAtUtc, bool Active);
public sealed record TableQrMutationReceipt(long TableId, int QrVersion,
    DateTimeOffset? ExpiresAtUtc, bool Revoked);
public sealed record TableQrIssueReceipt(long TableId, int QrVersion,
    DateTimeOffset ExpiresAtUtc, string QrToken);

public static class TableQrAdministration
{
    private const string Issued = "TABLE_QR_ISSUED";
    private const string Revoked = "TABLE_QR_REVOKED";

    // Approval cannot be inferred from Development, table seed data or existing read-only previews.
    // The independent SQL validation switch must remain OFF until a new approved fixture is run.
    public static bool Ready(IConfiguration configuration) =>
        FulfillmentPreviewService.On(configuration, "Orders:TableQrIssuanceEnabled") &&
        FulfillmentPreviewService.On(configuration, "Orders:TableQrProtocolValidated") &&
        FulfillmentPreviewService.On(configuration, "Orders:SqlIntegrationValidated") &&
        FulfillmentPreviewService.On(configuration, "Orders:TableQr:IssuerApproved") &&
        FulfillmentPreviewService.On(configuration, "Orders:TableQr:IssuanceSqlValidated");

    public static bool ValidReason(string? reason)
    {
        if (reason is null) return false;
        var text = reason.Trim();
        return text.Length is >= 3 and <= 400 && !text.Any(char.IsControl);
    }

    public static bool ValidVersion(string? encoded, out byte[] version)
    {
        if (!OrderIntegrationSafety.ValidVersion(encoded, out version)) return false;
        return string.Equals(Convert.ToBase64String(version), encoded, StringComparison.Ordinal);
    }

    // Lifetime values must be approved/configured; there is no silent business default.
    public static bool ValidExpiry(IConfiguration configuration, DateTimeOffset? requested,
        DateTimeOffset now, out DateTime utc)
    {
        utc = default;
        if (requested is null || requested.Value.Offset != TimeSpan.Zero ||
            !int.TryParse(configuration["Orders:TableQr:MinLifetimeSeconds"], out var min) ||
            !int.TryParse(configuration["Orders:TableQr:MaxLifetimeSeconds"], out var max) ||
            min < 1 || max < min || max > 2_592_000)
            return false;
        var seconds = (requested.Value - now).TotalSeconds;
        if (seconds < min || seconds > max) return false;
        utc = requested.Value.UtcDateTime;
        return true;
    }

    // Only a random 256-bit bearer secret is issued; SQL persists SHA256(secret), never the secret.
    public static (string Token, byte[] Digest) CreateToken()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        try
        {
            return (WebEncoders.Base64UrlEncode(secret), SHA256.HashData(secret));
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    public static void MapTableQrAdministration(this WebApplication app)
    {
        app.MapGet("/api/admin/tables/{tableId:long}/qr/context", ReadContextAsync)
            .RequireAuthorization("Admin").RequireRateLimiting("auth");
        app.MapPost("/api/admin/tables/{tableId:long}/qr/issue", IssueAsync)
            .RequireAuthorization("Admin").RequireRateLimiting("auth");
        app.MapPost("/api/admin/tables/{tableId:long}/qr/revoke", RevokeAsync)
            .RequireAuthorization("Admin").RequireRateLimiting("auth");
    }

    // The generic Admin data grid intentionally hides rowversion; this purpose-bound read
    // provides only the version needed for optimistic concurrency, never the token/digest.
    public static async Task<IResult> ReadContextAsync(long tableId, HttpContext context,
        CafeDbContext db, IConfiguration config, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        if (!Ready(config))
            return Results.Json(new { code = "TABLE_QR_ISSUER_DISABLED" }, statusCode: 503);
        if (!context.Request.IsHttps)
            return Results.Json(new { code = "TABLE_QR_HTTPS_REQUIRED" }, statusCode: 503);
        if (!context.User.IsInRole("ADMIN") ||
            !long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                NumberStyles.None, CultureInfo.InvariantCulture, out var adminId) || adminId <= 0)
            return Results.Forbid();
        if (tableId <= 0) return Results.BadRequest(new { code = "TABLE_QR_TABLE_INVALID" });
        var table = await db.Ban.AsNoTracking().SingleOrDefaultAsync(x => x.MaBan == tableId, ct);
        if (table is null) return Results.NotFound();
        return Results.Ok(new TableQrAdminContext(table.MaBan, table.MaBanHienThi,
            table.PhienBanQR, Convert.ToBase64String(table.PhienBanDuLieu),
            table.QRHetHanLuc is { } expiry ? new DateTimeOffset(DateTime.SpecifyKind(expiry, DateTimeKind.Utc)) : null,
            table.DangSuDung));
    }

    public static Task<IResult> IssueAsync(long tableId, TableQrMutationRequest input,
        HttpContext context, CafeDbContext db, IConfiguration config, TimeProvider clock,
        CancellationToken ct) => ChangeAsync(tableId, input, context, db, config, clock, ct, issue: true);

    public static Task<IResult> RevokeAsync(long tableId, TableQrMutationRequest input,
        HttpContext context, CafeDbContext db, IConfiguration config, TimeProvider clock,
        CancellationToken ct) => ChangeAsync(tableId, input, context, db, config, clock, ct, issue: false);

    private static async Task<IResult> ChangeAsync(long tableId, TableQrMutationRequest input,
        HttpContext context, CafeDbContext db, IConfiguration config, TimeProvider clock,
        CancellationToken ct, bool issue)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        if (!Ready(config))
            return Results.Json(new { code = "TABLE_QR_ISSUER_DISABLED" }, statusCode: 503);
        if (!context.Request.IsHttps)
            return Results.Json(new { code = "TABLE_QR_HTTPS_REQUIRED" }, statusCode: 503);
        if (!context.User.IsInRole("ADMIN") ||
            !long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                NumberStyles.None, CultureInfo.InvariantCulture, out var adminId) || adminId <= 0)
            return Results.Forbid();
        if (input is null || tableId <= 0 || !ValidReason(input.Reason) ||
            !ValidVersion(input.ExpectedVersion, out var expectedRowVersion) ||
            !OrderIdempotency.TryReadKey(context, out var operation))
            return Results.BadRequest(new { code = "TABLE_QR_MUTATION_INVALID" });
        var now = clock.GetUtcNow();
        DateTime expiryUtc = default;
        if (issue ? !ValidExpiry(config, input.ExpiresAtUtc, now, out expiryUtc)
                  : input.ExpiresAtUtc is not null)
            return Results.BadRequest(new { code = "TABLE_QR_EXPIRY_INVALID" });

        var action = issue ? Issued : Revoked;
        var reason = input.Reason!.Trim();
        var fingerprint = OrderIntegrationSafety.Hash(new
        {
            Action = action, TableId = tableId, Reason = reason,
            RowVersion = input.ExpectedVersion, input.ExpiresAtUtc
        });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Shared GUID namespace with customer order/payment/audit; never a session lock.
        if (!await OrderIntegrationSafety.LockAsync(db, transaction,
            "4chang.order." + operation.ToString("N"), ct))
            return Results.Conflict(new { code = "TABLE_QR_OPERATION_BUSY" });
        if (!await OrderIntegrationSafety.LockAsync(db, transaction,
            "4chang.tableqr." + tableId.ToString(CultureInfo.InvariantCulture), ct))
            return Results.Conflict(new { code = "TABLE_QR_TABLE_BUSY" });

        var previous = await db.NhatKyHeThong.AsNoTracking()
            .SingleOrDefaultAsync(x => x.MaThaoTac == operation, ct);
        if (previous is not null)
        {
            if (previous.MaQuanLy != adminId || previous.HanhDong != action ||
                previous.MaDoiTuong != tableId.ToString(CultureInfo.InvariantCulture) ||
                previous.MaBamYeuCau is null ||
                !CryptographicOperations.FixedTimeEquals(previous.MaBamYeuCau, fingerprint))
                return Results.Conflict(new { code = "TABLE_QR_OPERATION_CONFLICT" });
            // Issued secrets are intentionally not recoverable. Reuse is a conflict, not a second rotation.
            if (issue || string.IsNullOrWhiteSpace(previous.KetQuaDaChotJson))
                return Results.Conflict(new { code = "TABLE_QR_OPERATION_ALREADY_APPLIED",
                    message = "QR đã được cấp; nếu mất bản secret, kiểm tra bàn rồi cấp mã mới bằng thao tác mới." });
            return Results.Content(previous.KetQuaDaChotJson, "application/json", statusCode: 200);
        }
        if (await db.ThanhToan.AsNoTracking().AnyAsync(x => x.MaThaoTac == operation, ct))
            return Results.Conflict(new { code = "TABLE_QR_OPERATION_KEY_IN_USE" });
        var table = await db.Ban.SingleOrDefaultAsync(x => x.MaBan == tableId, ct);
        if (table is null) return Results.NotFound();
        if (!table.PhienBanDuLieu.AsSpan().SequenceEqual(expectedRowVersion))
            return Results.Conflict(new { code = "TABLE_QR_VERSION_CONFLICT" });
        if (table.PhienBanQR <= 0 || table.PhienBanQR == int.MaxValue || (issue && !table.DangSuDung))
            return Results.Conflict(new { code = "TABLE_QR_STATE_INVALID" });

        var before = JsonSerializer.Serialize(new
        {
            table.MaBan, table.PhienBanQR, table.QRHetHanLuc, table.DangSuDung
        });
        string? token = null;
        if (issue)
        {
            var newQr = CreateToken();
            token = newQr.Token;
            table.MaBamQR = newQr.Digest;
            table.QRHetHanLuc = expiryUtc;
        }
        else
        {
            // Revoked token cannot match: use a new unissued digest, expire now, keep table availability unchanged.
            table.MaBamQR = RandomNumberGenerator.GetBytes(32);
            table.QRHetHanLuc = now.UtcDateTime;
        }
        table.PhienBanQR++;
        var receipt = new TableQrMutationReceipt(tableId, table.PhienBanQR,
            table.QRHetHanLuc is { } due ? new DateTimeOffset(DateTime.SpecifyKind(due, DateTimeKind.Utc)) : null,
            Revoked: !issue);
        var safeJson = JsonSerializer.Serialize(receipt, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        db.NhatKyHeThong.Add(new NhatKyHeThong
        {
            MaLienKetXuLy = operation, MaThaoTac = operation, LoaiTacNhan = "ADMIN",
            MaQuanLy = adminId, TenNguoiThucHienDaChot = context.User.Identity?.Name ?? "ADMIN",
            HanhDong = action, LoaiDoiTuong = "Ban",
            MaDoiTuong = tableId.ToString(CultureInfo.InvariantCulture),
            DuLieuTruocJson = before, DuLieuSauJson = safeJson,
            LyDo = reason, KetQua = "SUCCESS", MaBamYeuCau = fingerprint,
            KetQuaDaChotJson = safeJson, PhatSinhLuc = now.UtcDateTime
        });
        // Table rotation/revocation and audit must either both commit or both roll back.
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return issue
            ? Results.Ok(new TableQrIssueReceipt(tableId, table.PhienBanQR,
                new DateTimeOffset(expiryUtc), token!))
            : Results.Ok(receipt);
    }
}
