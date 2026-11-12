// Standalone, one-shot SQL fixture scenarios for UC06 DINE_IN.
// Never linked into the production API, Web, or FourChang.sln test discovery.
// All real SQL access passes the cloned, independently pinned FixtureSqlGuard.
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using FourChang.Api.Features.Admin.Tables;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Domain.Entities.Orders;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

static partial class ConsentRun
{
    private static readonly JsonSerializerOptions DineJson = new(JsonSerializerDefaults.Web);

    private static T Parsed<T>((HttpResponseMessage Response, string Body) response)
        => JsonSerializer.Deserialize<T>(response.Body, DineJson)
           ?? throw new SafetyStop("Expected an in-process HTTP JSON response");

    // Fixture creates a placeholder digest, never an issued bearer token.
    // A real authenticated Admin HTTP request below must issue the token.
    private static async Task<long> AddSyntheticTableAsync()
    {
        await using var db = Db();
        Check(!await db.Ban.AnyAsync(), "Fixture has no pre-existing table");
        var table = new Ban
        {
            MaBanHienThi = "DINEIN-FIX-01",
            TenBan = "Synthetic isolated table",
            MaBamQR = RandomNumberGenerator.GetBytes(32),
            PhienBanQR = 1,
            QRHetHanLuc = DateTime.UtcNow.AddHours(1),
            DangSuDung = true
        };
        db.Ban.Add(table);
        await db.SaveChangesAsync();
        return table.MaBan;
    }

    private static async Task<TableQrAdminContext> TableContextAsync(
        HttpClient client, long tableId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"/api/admin/tables/{tableId}/qr/context");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AdminToken);
        using var reply = await client.SendAsync(req);
        Check(reply.StatusCode == HttpStatusCode.OK, "Real Admin HTTP table context GET");
        return JsonSerializer.Deserialize<TableQrAdminContext>(
            await reply.Content.ReadAsStringAsync(), DineJson)!;
    }

    private static async Task<TableQrIssueReceipt> IssueAsync(HttpClient client, long tableId)
    {
        var context = await TableContextAsync(client, tableId);
        var res = await Send(client, AdminToken, $"/api/admin/tables/{tableId}/qr/issue",
            new TableQrMutationRequest(context.RowVersion, "Isolated SQL fixture QR issuance",
                DateTimeOffset.UtcNow.AddHours(1)), Guid.NewGuid());
        Check(res.Response.StatusCode == HttpStatusCode.OK, "Real Admin HTTP QR issue");
        return Parsed<TableQrIssueReceipt>(res);
    }

    private static async Task<FulfillmentPreview> PreviewAsync(
        HttpClient client, string customerToken, string qrToken, List<OrderItemInput> items)
    {
        var res = await Send(client, customerToken, "/api/customer/orders/fulfillment-preview",
            new FulfillmentPreviewRequest("DINE_IN", qrToken, null, null, null, null, null, items));
        Check(res.Response.StatusCode == HttpStatusCode.OK, "Real Customer HTTP DINE_IN preview");
        var preview = Parsed<FulfillmentPreview>(res);
        Check(preview.Method == "DINE_IN" && preview.TableCode == "DINEIN-FIX-01" &&
              !preview.CreationEnabled && preview.Price.TienHang == 10000m &&
              preview.Price.PhiGiaoHang == 0m && preview.Price.TongCong == 10000m,
              "Protected preview binds the expected table and total");
        return preview;
    }

    private static async Task AssertNoSecretsInAuditAsync(params string[] secrets)
    {
        await using var db = Db();
        var audits = await db.NhatKyHeThong.AsNoTracking()
            .Where(x => x.HanhDong == "TABLE_QR_ISSUED" || x.HanhDong == "TABLE_QR_REVOKED" ||
                        x.HanhDong == "CREATE_CUSTOMER_ORDER")
            .ToListAsync();
        var auditBodies = JsonSerializer.Serialize(audits);
        foreach (var secret in secrets)
            Check(!auditBodies.Contains(secret, StringComparison.Ordinal),
                "No QR bearer or protected preview proof in durable audit");
    }

    // Called by the copied, one-shot Program.cs after schema/seed and database identity checks.
    // This is real SQL + in-process authenticated HTTP, NOT a production payment/provider proof.
    public static async Task DineInSqlAsync(Fixture fixture)
    {
        Config["Orders:CustomerCreationEnabled"] = "false";
        Config["Orders:SqlIntegrationValidated"] = "true";
        Config["Orders:DineInCreationEnabled"] = "true";
        Config["Orders:DineIn:BusinessApproved"] = "true";
        Config["Orders:DineIn:SqlWriterValidated"] = "false";
        Config["Orders:TableQrIssuanceEnabled"] = "true";
        Config["Orders:TableQrProtocolValidated"] = "true";
        Config["Orders:TableQr:IssuerApproved"] = "true";
        Config["Orders:TableQr:IssuanceSqlValidated"] = "true";
        Config["Orders:FulfillmentPreviewEnabled"] = "true";
        Config["Orders:FulfillmentPreviewLifetimeSeconds"] = "300";
        Config["Orders:PendingPaymentTimeoutMinutes"] = "15";
        Config["Orders:TableQr:MinLifetimeSeconds"] = "10";
        Config["Orders:TableQr:MaxLifetimeSeconds"] = "7200";

        // The API here is built by the fixture, using real SessionAuthentication,
        // Customer/Admin role policies, and SQL-backed services. Not a fake login.
        await using var api = await ApiAsync();
        using var client = api.GetTestClient();
        client.BaseAddress = new Uri("https://localhost"); // mandatory for QR-bearing POST
        client.Timeout = TimeSpan.FromSeconds(50);
        var ownerToken = await LoginAsync(client, "A");
        var otherToken = await LoginAsync(client, "B");
        var tableId = await AddSyntheticTableAsync();
        var issued = await IssueAsync(client, tableId);
        Check(issued.TableId == tableId && issued.QrToken.Length == 43,
            "Issued canonical 256-bit QR token");

        await using (var db = Db())
        {
            var table = await db.Ban.AsNoTracking().SingleAsync(x => x.MaBan == tableId);
            var raw = WebEncoders.Base64UrlDecode(issued.QrToken);
            Check(CryptographicOperations.FixedTimeEquals(
                    SHA256.HashData(raw), table.MaBamQR),
                "Only SHA256(QR token) is held in the SQL table row");
            CryptographicOperations.ZeroMemory(raw);
        }

        var inputItems = Payload().Items;
        var preview = await PreviewAsync(client, ownerToken, issued.QrToken, inputItems);
        var payload = new DineInOrderCreateRequest(issued.QrToken, preview.Proof, inputItems);
        var baseline = await SnapshotAsync();
        Evidence["DineInBaseline"] = baseline;

        // Independently gated writer must remain 503 while its SQL approval flag is OFF.
        var off = await Send(client, ownerToken, "/api/customer/orders/dine-in",
            payload, Guid.NewGuid(), 10000m);
        Check(off.Response.StatusCode == HttpStatusCode.ServiceUnavailable,
            "DINE_IN writer OFF returns HTTP503");
        await EffectsAsync("DineInGateOff", baseline, 0, 0);
        Config["Orders:DineIn:SqlWriterValidated"] = "true"; // fixture process ONLY
        Config["Orders:CustomerCreationEnabled"] = "true"; // fixture process ONLY

        // Regression: the old route cannot be made to accept DINE_IN by toggling flags.
        var legacy = await Send(client, ownerToken, "/api/customer/orders",
            Payload() with { HinhThucNhanMon = "DINE_IN" }, Guid.NewGuid(), 10000m);
        Check(legacy.Response.StatusCode == HttpStatusCode.ServiceUnavailable,
            "Historical TAKEAWAY endpoint stays DINE_IN-closed");
        await EffectsAsync("DineInLegacyClosed", baseline, 0, 0);

        var op = Guid.NewGuid();
        var create = await Send(client, ownerToken, "/api/customer/orders/dine-in",
            payload, op, 10000m);
        Check(create.Response.StatusCode == HttpStatusCode.Created,
            "Real SQL-backed authenticated DINE_IN Create HTTP201");
        var receipt = Parsed<CustomerOrderReceipt>(create);
        Check(receipt.TongCong == 10000m && receipt.PhiGiaoHang == 0m &&
              receipt.TrangThai == "PENDING_PAYMENT", "DINE_IN receipt is unpaid");
        await EffectsAsync("DineInCreated", baseline, 1, 10m);
        await using (var db = Db())
        {
            var order = await db.DonHang.AsNoTracking().SingleAsync(x => x.MaDonHang == receipt.MaDonHang);
            var payment = await db.ThanhToan.AsNoTracking().SingleAsync(x => x.MaDonHang == order.MaDonHang);
            Check(order.HinhThucNhanMon == "DINE_IN" && order.MaBan == tableId &&
                  order.MaBanDaChot == "DINEIN-FIX-01" && order.MaKhachHang == fixture.CustomerId,
                "Committed order has authentic QR table and owner");
            Check(payment.TrangThai == "PENDING" && payment.PhuongThuc == "BANK_TRANSFER" &&
                  payment.SoTienYeuCau == 10000m && payment.SoTienDaXacMinh == 0,
                "SQL payment is only an unpaid bank-transfer intent");
            var lineIds = await db.ChiTietDonHang.AsNoTracking()
                .Where(d => d.MaDonHang == order.MaDonHang)
                .Select(d => d.MaChiTietDonHang).ToListAsync();
            Check(await db.GiuChoNguyenLieu.AsNoTracking()
                .CountAsync(x => lineIds.Contains(x.MaChiTietDonHang)) == 1,
                "SQL ingredient hold created exactly once");
        }

        var afterCreate = await SnapshotAsync();
        var same = await Send(client, ownerToken, "/api/customer/orders/dine-in",
            payload, op, 10000m);
        Check(same.Response.StatusCode == HttpStatusCode.OK &&
              Parsed<CustomerOrderReceipt>(same) == receipt,
              "Same GUID and payload replay original receipt HTTP200");
        var changed = await Send(client, ownerToken, "/api/customer/orders/dine-in",
            payload with { GhiChu = "Different customer intent" }, op, 10000m);
        Check(changed.Response.StatusCode == HttpStatusCode.Conflict,
              "Reusing GUID with changed payload returns HTTP409");
        var foreign = await Send(client, otherToken, "/api/customer/orders/dine-in",
            payload, op, 10000m);
        Check(foreign.Response.StatusCode == HttpStatusCode.Conflict,
              "Other Customer never receives original receipt");
        await EffectsAsync("DineInReplayAndConflicts", afterCreate, 0, 0);

        // Real authenticated Admin revoke invalidates NEW orders, not a committed receipt.
        var context = await TableContextAsync(client, tableId);
        var revoked = await Send(client, AdminToken, $"/api/admin/tables/{tableId}/qr/revoke",
            new TableQrMutationRequest(context.RowVersion, "Isolated SQL fixture revoke"), Guid.NewGuid());
        Check(revoked.Response.StatusCode == HttpStatusCode.OK,
            "Real Admin QR revoke HTTP200");
        var beforeRevokeCreate = await SnapshotAsync();
        var newAfterRevoke = await Send(client, ownerToken, "/api/customer/orders/dine-in",
            payload, Guid.NewGuid(), 10000m);
        Check(newAfterRevoke.Response.StatusCode == HttpStatusCode.Conflict,
            "Revoked QR rejects any new DINE_IN create");
        var oldReceipt = await Send(client, ownerToken, "/api/customer/orders/dine-in",
            payload, op, 10000m);
        Check(oldReceipt.Response.StatusCode == HttpStatusCode.OK &&
              Parsed<CustomerOrderReceipt>(oldReceipt) == receipt,
            "Committed operation replays after QR revocation");
        await EffectsAsync("DineInRevokedNewCreate", beforeRevokeCreate, 0, 0);

        // Inject a fixture-only fault BEFORE mandatory audit SaveChanges; EF transaction
        // must roll back order, payment, stock hold and journal together.
        var issuedAgain = await IssueAsync(client, tableId);
        var previewAgain = await PreviewAsync(client, ownerToken, issuedAgain.QrToken, inputItems);
        var secondPayload = new DineInOrderCreateRequest(issuedAgain.QrToken,
            previewAgain.Proof, inputItems);
        var beforeFault = await SnapshotAsync();
        try
        {
            SaveControl.AuditFault = true;
            var failed = await Send(client, ownerToken, "/api/customer/orders/dine-in",
                secondPayload, Guid.NewGuid(), 10000m);
            Check(failed.Response.StatusCode == HttpStatusCode.InternalServerError,
                "Injected fixture-only audit fault fails the Create request");
        }
        finally { SaveControl.AuditFault = false; }
        await EffectsAsync("DineInAuditFaultRollback", beforeFault, 0, 0);
        await AssertNoSecretsInAuditAsync(issued.QrToken, issuedAgain.QrToken,
            preview.Proof, previewAgain.Proof);
        Evidence["DineInScope"] = new
        {
            kind = "REAL_SQL_IN_PROCESS_HTTP_FIXTURE_ONLY",
            authentication = "SessionAuthentication + Customer and Admin policies",
            bankPaymentVerification = false,
            productionFlagsModified = false,
            sqlTarget = "127.0.0.1:14341/FourChangUc06DineInFixtureV1",
            covered = new[] { "Admin issue", "Create", "BOM/stock hold", "payment intent",
                "same-key replay", "changed-key conflict", "foreign owner", "Admin revoke",
                "rollback before audit save", "no QR secret in SQL audit" },
            notCovered = new[] { "concurrent QR revoke/Create race", "real bank payment verification",
                "real frontend QR scan/login handoff", "production reverse proxy" }
        };
        await api.StopAsync();
    }
}
