using System.Data.Common;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Api.Features.Customer.Payments;
using FourChang.Domain.Entities.Payments;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class Phase4IntegrationPreparationTests
{
    private sealed class SecurityLog : ILogger<SqlBankPaymentService>
    {
        public readonly List<(LogLevel Level, EventId Event, string Message, Exception? Error)> Entries = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format) =>
            Entries.Add((level, id, format(state, error), error));
    }
    private sealed class NoConnections : DbConnectionInterceptor
    {
        public int Attempts;
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        { Attempts++; throw new InvalidOperationException("SQL connection forbidden in NoDatabase tests"); }
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken ct = default)
        { Attempts++; throw new InvalidOperationException("SQL connection forbidden in NoDatabase tests"); }
    }
    private static CafeDbContext Db(NoConnections monitor) => new(new DbContextOptionsBuilder<CafeDbContext>()
        .UseSqlServer("Server=forbidden.invalid;Database=OfflineOnly;Integrated Security=true")
        .AddInterceptors(monitor).Options);
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(x => x.Key, x => x.Value)).Build();
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(OrderReadTests.Now); }

    [Theory]
    [InlineData(null)] [InlineData("false")] [InlineData("invalid")] [InlineData("true")]
    public async Task Default_or_unvalidated_flags_prevent_every_sql_writer_before_connection(string? flag)
    {
        var monitor = new NoConnections(); await using var db = Db(monitor);
        var config = Config(("Orders:CancellationEnabled", flag), ("Orders:PaymentProcessingEnabled", flag),
            ("Orders:ExpiryEnabled", flag), ("ASPNETCORE_ENVIRONMENT", "Development"), ("Orders:ExpiryStatusReadEnabled", "true"));
        var cancel = await CustomerOrderCancellation.CancelAsync(7, new("Lý do"), new DefaultHttpContext(), db, default, config);
        Assert.Equal(503, ((IStatusCodeHttpResult)cancel).StatusCode);
        var payment = await new SqlBankPaymentService(db, config, new UnavailablePaymentVerificationSource(), new Clock())
            .ProcessAsync(new byte[] { 1 }, new Dictionary<string, string>(), default);
        Assert.Equal(503, ((IStatusCodeHttpResult)payment).StatusCode);
        Assert.Equal("EXPIRY_DISABLED", Assert.Single(await new SqlOrderExpiryWorker(db, config, new UnavailableExpiryBankReconciliation(), new Clock()).RunOnceAsync(default)));
        Assert.Equal(0, monitor.Attempts); Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("guid")] [InlineData("version")] [InlineData("reason")]
    public async Task Enabled_cancel_still_validates_protocol_before_sql(string invalid)
    {
        var monitor = new NoConnections(); await using var db = Db(monitor);
        var context = new DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new(System.Security.Claims.ClaimTypes.NameIdentifier, "6")], "Mock"));
        context.Request.Headers["Idempotency-Key"] = invalid == "guid" ? "bad" : Guid.NewGuid().ToString();
        var result = await CustomerOrderCancellation.CancelAsync(7,
            new(invalid == "reason" ? "a" : "Đổi ý", invalid == "version" ? "AQ==" : Convert.ToBase64String(new byte[8])),
            context, db, default, Config(("Orders:SqlIntegrationValidated", "true"), ("Orders:CancellationEnabled", "true")));
        Assert.Equal(400, ((IStatusCodeHttpResult)result).StatusCode); Assert.Equal(0, monitor.Attempts);
    }

    [Theory]
    [InlineData(false, "EXPIRY_MAPPING_REQUIRED")] [InlineData(true, "EXPIRY_CONSUMERS_REQUIRED")]
    public async Task Expiry_requires_mapping_and_consumer_approval_before_sql(bool mapping, string expected)
    {
        var monitor = new NoConnections(); await using var db = Db(monitor);
        var config = Config(("Orders:SqlIntegrationValidated", "true"), ("Orders:ExpiryEnabled", "true"),
            ("Orders:ExpiryMappingApproved", mapping.ToString()));
        var result = await new SqlOrderExpiryWorker(db, config, new UnavailableExpiryBankReconciliation(), new Clock()).RunOnceAsync(default);
        Assert.Equal(expected, Assert.Single(result)); Assert.Equal(0, monitor.Attempts);
    }

    [Fact]
    public void Constraint_guard_accepts_only_exact_approved_status_set_in_sql_server_forms()
    {
        var states = CustomerOrderStates.Orders.Append("EXPIRED").ToArray();
        var sql = "([TrangThai] IN (" + string.Join(",", states.Select(x => "'" + x + "'")) + "))";
        Assert.True(SqlOrderExpiryWorker.IsApprovedConstraint(sql));
        Assert.True(SqlOrderExpiryWorker.IsApprovedConstraint(string.Join(" OR ", states.Select(x => "[TrangThai]='" + x + "'"))));
        Assert.False(SqlOrderExpiryWorker.IsApprovedConstraint(sql + " OR 1=1"));
        Assert.False(SqlOrderExpiryWorker.IsApprovedConstraint(sql.Replace("'EXPIRED'", "'SOMETHING_ELSE'")));
        Assert.False(SqlOrderExpiryWorker.IsApprovedConstraint(null));
    }

    [Theory]
    [InlineData("E X P I R E D")]
    [InlineData("EX[PIRED]")]
    [InlineData("EX(PIRED)")]
    [InlineData("EXPIRED ")]
    [InlineData("expired")]
    public void Expiry_schema_guard_preserves_literal_values_when_removing_sql_formatting(string drift)
    {
        var states = CustomerOrderStates.Orders.Append(drift);
        var sql = "([TrangThai] IN (" + string.Join(",", states.Select(x => "'" + x + "'")) + "))";
        Assert.False(SqlOrderExpiryWorker.IsApprovedConstraint(sql));
        Assert.False(SqlOrderExpiryWorker.IsApprovedConstraint(string.Join(" OR ", states.Select(x => "[TrangThai]='" + x + "'"))));
    }

    [Theory]
    [InlineData("RESERVED", 0, true)]
    [InlineData("PARTIAL", 4, true)]
    [InlineData("CONSUMED", 0, false)]
    [InlineData("RELEASED", 4, false)]
    [InlineData("EXPIRED", 4, false)]
    [InlineData("EXPIRED", 10, true)]
    public void Sql_expiry_rejects_inconsistent_terminal_holds_before_release(string status, int released, bool expected)
    {
        var hold = new FourChang.Domain.Entities.Inventory.GiuChoNguyenLieu
        {
            TrangThai = status, SoLuongGiuCho = 10, SoLuongDaGiaiPhong = released, SoLuongDaTieuHao = 0
        };
        Assert.Equal(expected, SqlOrderExpiryWorker.HoldReady(hold));
        Assert.Equal(released, hold.SoLuongDaGiaiPhong); // Validation never mutates the row.
    }

    [Fact]
    public void Reader_compatibility_is_opt_in_and_does_not_approve_a_write_mapping()
    {
        Assert.False(CustomerOrderStates.IsReadable("EXPIRED")); Assert.True(CustomerOrderStates.IsReadable("EXPIRED", true));
        Assert.NotNull(new CustomerOrderFilter(Status: "EXPIRED").Validate());
        Assert.Null(new CustomerOrderFilter(Status: "EXPIRED").Validate(true));
        Assert.False(CustomerOrderStates.Orders.Contains("EXPIRED"));
        var detail = OrderReadTests.Snapshot("EXPIRED");
        Assert.False(CustomerOrderPresentation.Valid(detail, 7)); Assert.True(CustomerOrderPresentation.Valid(detail, 7, true));
    }

    private sealed class Reader : ICustomerOrderReader
    {
        public int Reads;
        public Task<CustomerOrderPage> ListAsync(long owner, CustomerOrderFilter filter, CancellationToken ct)
        { Reads++; return Task.FromResult(new CustomerOrderPage([], filter.Page, filter.PageSize, 0, OrderReadTests.Now)); }
        public Task<CustomerOrderDetail?> DetailAsync(long owner, long id, CancellationToken ct) => throw new InvalidOperationException();
    }
    [Fact]
    public async Task Api_expired_filter_requires_its_own_server_read_flag()
    {
        var reader = new Reader(); var context = new DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new(System.Security.Claims.ClaimTypes.NameIdentifier, "6")], "Mock"));
        Assert.Equal(400, ((IStatusCodeHttpResult)await CustomerOrderReadEndpoints.ListAsync(context, reader, 1, 10, "EXPIRED", null, null, default)).StatusCode);
        using var services = new ServiceCollection().AddSingleton(Config(("Orders:ExpiryStatusReadEnabled", "true"))).BuildServiceProvider();
        context.RequestServices = services;
        Assert.Equal(200, ((IStatusCodeHttpResult)await CustomerOrderReadEndpoints.ListAsync(context, reader, 1, 10, "EXPIRED", null, null, default)).StatusCode);
        Assert.Equal(1, reader.Reads);
    }

    private sealed class Protocol(VerifiedBankTransfer? evidence) : IBankCallbackProtocol
    {
        public Task<VerifiedBankTransfer?> AuthenticateAndDecodeAsync(ReadOnlyMemory<byte> body,
            IReadOnlyDictionary<string, string> headers, CancellationToken ct) => Task.FromResult(evidence);
    }
    private static VerifiedBankTransfer Evidence() => new(new()
    {
        TenNhaCungCap = "TEST-PROTOCOL", MaTaiKhoanNganHangQuan = "TEST-ACCOUNT", MaGiaoDichBenNgoai = "TEST-TX",
        ChieuGiaoDich = "IN", SoTien = 60000, NoiDungChuyenKhoan = "REF", ThoiDiemNganHang = OrderReadTests.Now,
        MaThanhToan = 999, MaGiaoDichNganHang = 888
    }, "VND");

    [Theory]
    [InlineData("auth")] [InlineData("account")] [InlineData("currency")] [InlineData("direction")]
    [InlineData("fraction")] [InlineData("too-large")] [InlineData("transaction")]
    [InlineData("null-transaction")] [InlineData("blank-transaction")]
    public async Task Protocol_adapter_requires_authenticated_and_valid_receiver_payload(string problem)
    {
        VerifiedBankTransfer? evidence = Evidence();
        switch (problem)
        {
            case "auth": evidence = null; break;
            case "account": evidence!.Transaction.MaTaiKhoanNganHangQuan = "OTHER"; break;
            case "currency": evidence = evidence! with { Currency = "USD" }; break;
            case "direction": evidence!.Transaction.ChieuGiaoDich = "OUT"; break;
            case "fraction": evidence!.Transaction.SoTien = 0.5m; break;
            case "too-large": evidence!.Transaction.SoTien = 1000000000000000000m; break;
            case "transaction": evidence!.Transaction.MaGiaoDichBenNgoai = ""; break;
            case "null-transaction": evidence!.Transaction.MaGiaoDichBenNgoai = null!; break;
            case "blank-transaction": evidence!.Transaction.MaGiaoDichBenNgoai = "   "; break;
        }
        Assert.Null(await new BankCallbackAdapter(new Protocol(evidence), new("TEST-PROTOCOL", "TEST-ACCOUNT"))
            .AuthenticateAsync(new byte[] { 1 }, new Dictionary<string, string>(), default));
    }

    [Fact]
    public async Task Adapter_drops_sql_links_and_unavailable_protocol_cannot_authenticate()
    {
        var normalized = await new BankCallbackAdapter(new Protocol(Evidence()), new("TEST-PROTOCOL", "TEST-ACCOUNT"))
            .AuthenticateAsync(new byte[] { 1 }, new Dictionary<string, string>(), default);
        Assert.NotNull(normalized); Assert.Null(normalized.Transaction.MaThanhToan); Assert.Equal(0, normalized.Transaction.MaGiaoDichNganHang);
        Assert.Null(await new UnavailableBankCallbackProtocol().AuthenticateAndDecodeAsync(new byte[] { 1 }, new Dictionary<string, string>(), default));
    }

    [Fact]
    public async Task Payment_processor_with_explicit_flags_but_no_bank_authentication_never_opens_sql()
    {
        var monitor = new NoConnections(); await using var db = Db(monitor);
        var config = Config(("Orders:SqlIntegrationValidated", "true"), ("Orders:PaymentProcessingEnabled", "true"),
            ("Bank:Provider", "TEST-PROTOCOL"), ("Bank:ReceiverAccount", "TEST-ACCOUNT"));
        var log = new SecurityLog();
        var result = await new SqlBankPaymentService(db, config, new UnavailablePaymentVerificationSource(), new Clock(), log)
            .ProcessAsync(System.Text.Encoding.UTF8.GetBytes("TEST-SENSITIVE-CALLBACK"),
                new Dictionary<string, string> { ["Authorization"] = "Bearer TEST-SENSITIVE-HEADER" }, default);
        Assert.Equal(401, ((IStatusCodeHttpResult)result).StatusCode); Assert.Equal(0, monitor.Attempts);
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level); Assert.Equal(7001, entry.Event.Id); Assert.Null(entry.Error);
        Assert.Equal("Bank transfer evidence rejected: BANK_AUTHENTICATION_OR_RECEIVER_INVALID", entry.Message);
        Assert.DoesNotContain("TEST-SENSITIVE", entry.Message); Assert.DoesNotContain("TEST-ACCOUNT", entry.Message);
    }

    [Fact]
    public void Current_ef_model_has_durable_unique_operation_and_bank_identity_indexes_without_opening_sql()
    {
        var monitor = new NoConnections(); using var db = Db(monitor);
        var audit = db.Model.FindEntityType(typeof(FourChang.Domain.Entities.Audit.NhatKyHeThong))!;
        Assert.Contains(audit.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "MaThaoTac" }));
        var bank = db.Model.FindEntityType(typeof(GiaoDichNganHang))!;
        Assert.Contains(bank.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[]
            { "TenNhaCungCap", "MaTaiKhoanNganHangQuan", "MaGiaoDichBenNgoai" }));
        Assert.Equal(0, monitor.Attempts);
    }
}
