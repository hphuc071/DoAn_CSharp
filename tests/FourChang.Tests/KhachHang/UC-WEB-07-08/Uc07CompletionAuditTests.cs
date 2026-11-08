using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FourChang.Api.Features.Customer.Payments;
using FourChang.Domain.Entities.Payments;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Tests.Shared;
using FourChang.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Playwright;
using PaymentPage = FourChang.Web.Pages.Checkout.PaymentModel;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class Uc07CompletionAuditTests
{
    private sealed class Source(Func<CancellationToken, Task<ValidatedPaymentQr?>> get) : IPaymentQrSource
    {
        public int Calls;
        public Task<ValidatedPaymentQr?> GetAsync(CustomerOrderDetail order, CancellationToken ct)
        { Calls++; return get(ct); }
    }
    private static CustomerOrderDetail Current()
    {
        var order = OrderReadTests.Snapshot(); var deadline = DateTime.UtcNow.AddMinutes(15);
        return order with { ExpiresUtc = deadline, Payments = [order.Payments[0] with { ExpiresUtc = deadline }] };
    }
    private static PaymentPage Page(CustomerOrderDetail detail, IPaymentQrSource source) =>
        OrderReadTests.Page(new PaymentPage(OrderReadTests.Client(new(request => {
            Assert.Equal(HttpMethod.Get, request.Method);
            // Model the wire snapshot, not computed totals evaluated by the test serializer.
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(detail,
                options: new JsonSerializerOptions(JsonSerializerDefaults.Web) { IgnoreReadOnlyProperties = true }) };
        })), source));

    [Theory]
    [InlineData("unit")]
    [InlineData("sum")]
    [InlineData("refund")]
    public void Overflowing_snapshot_is_invalid_before_amounts_reach_payment_view(string scenario)
    {
        var order = Current();
        order = scenario switch {
            "unit" => order with { Lines = [order.Lines[0] with { BasePrice = decimal.MaxValue }] },
            "sum" => order with { ShippingFee = decimal.MaxValue, OriginalShippingFee = decimal.MaxValue },
            _ => order with { Refunds = [new(1, "TEST-REFUND", "REQUESTED", 1, null, decimal.MaxValue)] }
        };
        Assert.False(CustomerOrderPresentation.Valid(order, 7));
    }
    [Fact]
    public async Task Invalid_amount_snapshot_returns502_without_contacting_qr_source()
    {
        var order = Current(); order = order with { Lines = [order.Lines[0] with { BasePrice = decimal.MaxValue }] };
        var source = new Source(_ => Task.FromResult<ValidatedPaymentQr?>(null));
        var page = Page(order, source);
        Assert.IsType<PageResult>(await page.OnGetAsync(7, default));
        Assert.Equal(502, page.Response.StatusCode); Assert.Null(page.Detail); Assert.Null(page.Qr); Assert.Equal(0, source.Calls);
        Assert.Equal("no-store", page.Response.Headers.CacheControl.ToString());
    }
    [Theory]
    [InlineData("json")]
    [InlineData("content-type")]
    public async Task Malformed_qr_source_preserves_owned_snapshot_without_leaking_error(string scenario)
    {
        var source = new Source(_ => {
            if (scenario == "json") throw new JsonException("TEST-SENSITIVE-PROVIDER-DATA");
            throw new NotSupportedException("TEST-SENSITIVE-PROVIDER-DATA");
        });
        var page = Page(Current(), source);
        await page.OnGetAsync(7, default);
        Assert.NotNull(page.Detail); Assert.Null(page.Qr); Assert.NotNull(page.Error);
        Assert.DoesNotContain("TEST-SENSITIVE-PROVIDER-DATA", page.Error);
        Assert.Equal("PENDING", page.Detail.Payments[0].Status);
    }
    [Theory]
    [InlineData("paid")]
    [InlineData("elapsed")]
    public async Task Non_payable_snapshot_does_not_request_an_external_qr(string scenario)
    {
        var order = Current();
        order = scenario == "paid" ? order with { Order = order.Order with { Status = "PENDING_CONFIRMATION" },
            Payments = [order.Payments[0] with { Status = "VERIFIED", Verified = 60000, VerifiedUtc = DateTime.UtcNow }] }
            : order with { ExpiresUtc = DateTime.UtcNow.AddSeconds(-1) };
        var source = new Source(_ => Task.FromResult<ValidatedPaymentQr?>(null));
        var page = Page(order, source); await page.OnGetAsync(7, default);
        Assert.NotNull(page.Detail); Assert.Null(page.Qr); Assert.Equal(0, source.Calls);
    }
    [Fact]
    public async Task Request_cancellation_is_propagated_even_if_qr_source_ignores_token()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<ValidatedPaymentQr?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var ct = new CancellationTokenSource();
        var source = new Source(_ => { entered.SetResult(); return pending.Task; });
        var page = Page(Current(), source); var read = page.OnGetAsync(7, ct.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); ct.Cancel();
        try { await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read.WaitAsync(TimeSpan.FromSeconds(1))); }
        finally { pending.TrySetResult(null); }
        Assert.Null(page.Qr);
    }
    [Fact]
    public async Task Unresponsive_qr_source_has_a_bounded_read_and_is_cancelled_without_retry()
    {
        CancellationToken sourceToken = default;
        var pending = new TaskCompletionSource<ValidatedPaymentQr?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new Source(ct => { sourceToken = ct; return pending.Task; });
        var page = Page(Current(), source);
        try { await page.OnGetAsync(7, default).WaitAsync(TimeSpan.FromSeconds(7)); }
        finally { pending.TrySetResult(null); }
        Assert.True(sourceToken.IsCancellationRequested); Assert.Equal(1, source.Calls);
        Assert.NotNull(page.Detail); Assert.Null(page.Qr); Assert.NotNull(page.Error);
    }
    [Fact]
    public void Qr_without_explicit_utc_deadline_cannot_be_displayed()
    {
        var order = Current(); var deadline = order.ExpiresUtc!.Value;
        var qr = new ValidatedPaymentQr(3, 60000, "REF-FAKE-7", DateTime.SpecifyKind(deadline, DateTimeKind.Unspecified),
            "data:image/png;base64,iVBORw0KGgo="); // Signature fixture only, never a real VietQR.
        Assert.False(PaymentPage.CanDisplay(qr, order, DateTime.UtcNow));
    }
    [Theory]
    [InlineData("null-transaction")]
    [InlineData("non-utc")]
    [InlineData("too-large")]
    [InlineData("control-id")]
    public void Assessment_uses_the_same_authenticated_shape_boundary_as_sql_writer(string scenario)
    {
        var now = OrderReadTests.Now;
        var payment = new ThanhToan { MaThanhToan = 3, PhuongThuc = "BANK_TRANSFER", SoTienYeuCau = 60000,
            MaThamChieuChuyenKhoan = "REF-FAKE-7", TrangThai = "PENDING", HetHanLuc = now.AddMinutes(15) };
        var transfer = new GiaoDichNganHang { TenNhaCungCap = "TEST", MaTaiKhoanNganHangQuan = "TEST-ACCOUNT",
            MaGiaoDichBenNgoai = "TEST-TX", ChieuGiaoDich = "IN", SoTien = 60000,
            NoiDungChuyenKhoan = "REF-FAKE-7", ThoiDiemNganHang = now };
        switch (scenario) {
            case "non-utc": transfer.ThoiDiemNganHang = DateTime.SpecifyKind(now, DateTimeKind.Unspecified); break;
            case "too-large": transfer.SoTien = 1000000000000000000m; break;
            case "control-id": transfer.MaGiaoDichBenNgoai = "TEST\nTX"; break;
        }
        var evidence = new VerifiedBankTransfer(scenario == "null-transaction" ? null! : transfer, "VND");
        Assert.Equal(TransferAssessment.Rejected, PaymentVerificationPolicy.Assess(evidence, payment,
            "PENDING_PAYMENT", "TEST", "TEST-ACCOUNT", now, []));
        Assert.Equal(0, payment.SoTienDaXacMinh); Assert.Equal("PENDING", payment.TrangThai);
    }
}

// Synthetic QR DOM/clock only. Every URL is intercepted; no bank QR or API process.
[Trait("Safety", "NoDatabase")]
public sealed class Uc07QrDeadlineBrowserTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("missing")]
    [InlineData("invalid")]
    public async Task Qr_hides_at_deadline_or_invalid_metadata_without_writes_or_status_changes(string scenario)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
        await using var context = await browser.NewContextAsync(); var page = await context.NewPageAsync();
        var now = new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc); var reads = 0;
        await page.Clock.InstallAsync(new() { TimeString = now.ToString("O") });
        await context.RouteAsync("**/*", async route => {
            Assert.Equal("GET", route.Request.Method); reads++;
            var expiry = scenario == "valid" ? now.AddMinutes(1).ToString("O") : scenario == "invalid" ? "invalid" : "";
            await route.FulfillAsync(new() { ContentType = "text/html", Body = $"""
                <p id="order-snapshot-connection" hidden></p><p id="payment-qr-expiry" hidden></p>
                <span id="payment-state">PENDING</span><div data-order-private>Owned snapshot</div>
                <div data-payment-qr data-qr-expires-utc="{expiry}">Synthetic QR transport fixture</div>
                """ });
        });
        await page.GotoAsync("http://mock.invalid/Checkout/Payment?orderId=7");
        await page.EvaluateAsync("localStorage.setItem('fourchang.checkout-intent.v1','original-pending')");
        var script = await File.ReadAllTextAsync(Path.Combine(TestApi.FindRoot(), "src", "Presentation", "FourChang.Web", "wwwroot", "js", "order-snapshot.js"));
        await page.AddScriptTagAsync(new() { Content = script });
        if (scenario == "valid") {
            await Assertions.Expect(page.Locator("[data-payment-qr]")).ToBeVisibleAsync();
            await page.Clock.FastForwardAsync(60001);
        }
        await Assertions.Expect(page.Locator("[data-payment-qr]")).ToBeHiddenAsync(new() { Timeout = 1000 });
        await Assertions.Expect(page.Locator("#payment-qr-expiry")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#payment-state")).ToHaveTextAsync("PENDING");
        await Assertions.Expect(page.Locator("[data-order-private]")).ToBeVisibleAsync();
        Assert.Equal("original-pending", await page.EvaluateAsync<string>("localStorage.getItem('fourchang.checkout-intent.v1')"));
        Assert.Equal(1, reads);
    }
}
