using System.Net;
using System.Net.Http.Json;
using FourChang.Api.Features.Customer.Payments;
using FourChang.Domain.Entities.Payments;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PaymentPage = FourChang.Web.Pages.Checkout.PaymentModel;

namespace FourChang.Tests;

// All callbacks below are test-only evidence. No callback endpoint, SQL connection or bank API is used.
[Trait("Safety", "NoDatabase")]
public sealed class PaymentOfflineTests
{
    private static readonly DateTime Now = OrderReadTests.Now;
    private static ThanhToan Payment() => new() { MaThanhToan = 3, PhuongThuc = "BANK_TRANSFER",
        SoTienYeuCau = 60000, MaThamChieuChuyenKhoan = "REF-FAKE-7", TrangThai = "PENDING", HetHanLuc = Now.AddMinutes(15) };
    private static VerifiedBankTransfer Evidence(decimal amount = 60000) => new(new()
    { TenNhaCungCap = "MOCK", MaTaiKhoanNganHangQuan = "TEST-ACCOUNT", MaGiaoDichBenNgoai = "TX-FAKE-1",
      ChieuGiaoDich = "IN", SoTien = amount, NoiDungChuyenKhoan = "REF-FAKE-7", ThoiDiemNganHang = Now, MaThanhToan = 3 }, "VND");

    private sealed class FakeSource(VerifiedBankTransfer evidence) : IPaymentVerificationSource
    {
        public Task<VerifiedBankTransfer?> AuthenticateAsync(ReadOnlyMemory<byte> body,
            IReadOnlyDictionary<string, string> headers, CancellationToken ct) => Task.FromResult(
                headers.GetValueOrDefault("TEST-ONLY-SIGNATURE") == "mock-valid" ? evidence : null);
    }
    private static TransferAssessment Assess(VerifiedBankTransfer? evidence, ThanhToan? payment = null,
        string order = "PENDING_PAYMENT", DateTime? now = null, IReadOnlyCollection<GiaoDichNganHang>? recorded = null) =>
        PaymentVerificationPolicy.Assess(evidence, payment ?? Payment(), order, "MOCK", "TEST-ACCOUNT", now ?? Now, recorded ?? []);

    [Theory]
    [InlineData("mock-valid", TransferAssessment.Exact)] [InlineData("invalid", TransferAssessment.Rejected)]
    [InlineData("", TransferAssessment.Rejected)]
    public async Task Mock_callback_must_pass_source_authentication(string signature, TransferAssessment expected)
    {
        var source = new FakeSource(Evidence());
        var evidence = await source.AuthenticateAsync(new byte[] { 1 }, new Dictionary<string, string>
            { ["TEST-ONLY-SIGNATURE"] = signature }, default);
        Assert.Equal(expected, Assess(evidence));
    }

    [Theory]
    [InlineData(50000, TransferAssessment.Partial)] [InlineData(60000, TransferAssessment.Exact)]
    [InlineData(70000, TransferAssessment.Excess)] [InlineData(0, TransferAssessment.Rejected)]
    [InlineData(-1, TransferAssessment.Rejected)] [InlineData(0.5, TransferAssessment.Rejected)]
    public void Mock_under_exact_over_and_invalid_amounts_do_not_mutate_financial_entities(decimal amount, TransferAssessment expected)
    {
        var payment = Payment(); var evidence = Evidence(amount);
        Assert.Equal(expected, Assess(evidence, payment));
        Assert.Equal(0, payment.SoTienDaXacMinh); Assert.Equal("PENDING", payment.TrangThai);
        Assert.Equal(amount, evidence.Transaction.SoTien);
    }

    [Fact]
    public void Partial_receipts_accumulate_for_assessment_only()
    {
        var payment = Payment(); payment.SoTienDaXacMinh = 40000; payment.TrangThai = "PARTIAL";
        Assert.Equal(TransferAssessment.Exact, Assess(Evidence(20000), payment));
        Assert.Equal(40000, payment.SoTienDaXacMinh);
    }

    [Fact]
    public void Duplicate_mock_callback_never_counts_again_even_after_expiry()
    {
        var evidence = Evidence();
        Assert.Equal(TransferAssessment.Duplicate, Assess(evidence, now: Now.AddHours(1), recorded: [evidence.Transaction]));
        var changed = Evidence(60001);
        Assert.Equal(TransferAssessment.Conflict, Assess(changed, recorded: [evidence.Transaction]));
    }

    [Fact]
    public void Duplicate_ledger_corruption_and_wrong_payment_association_fail_closed()
    {
        var evidence = Evidence();
        Assert.Equal(TransferAssessment.Conflict, Assess(evidence, recorded: [evidence.Transaction, evidence.Transaction]));
        evidence.Transaction.MaThanhToan = 999;
        Assert.Equal(TransferAssessment.Reconciliation, Assess(evidence));
    }

    [Theory]
    [InlineData("provider")] [InlineData("account")] [InlineData("currency")] [InlineData("direction")]
    [InlineData("transaction-id")] [InlineData("future-bank-time")]
    public void Untrusted_or_wrong_transfer_attributes_are_rejected(string changed)
    {
        var evidence = Evidence();
        switch (changed)
        {
            case "provider": evidence.Transaction.TenNhaCungCap = "OTHER"; break;
            case "account": evidence.Transaction.MaTaiKhoanNganHangQuan = "OTHER"; break;
            case "currency": evidence = evidence with { Currency = "USD" }; break;
            case "direction": evidence.Transaction.ChieuGiaoDich = "OUT"; break;
            case "transaction-id": evidence.Transaction.MaGiaoDichBenNgoai = ""; break;
            case "future-bank-time": evidence.Transaction.ThoiDiemNganHang = Now.AddMinutes(1); break;
        }
        Assert.Equal(TransferAssessment.Rejected, Assess(evidence));
    }

    [Theory]
    [InlineData("late")] [InlineData("expired")] [InlineData("missing-deadline")]
    [InlineData("wrong-reference")] [InlineData("cancelled")]
    public void Late_expired_unmatched_or_closed_order_requires_reconciliation_without_resurrection(string scenario)
    {
        var payment = Payment(); var evidence = Evidence(); var now = Now; var order = "PENDING_PAYMENT";
        switch (scenario)
        {
            case "late": now = Now.AddMinutes(15); break;
            case "expired": payment.TrangThai = "EXPIRED"; break;
            case "missing-deadline": payment.HetHanLuc = null; break;
            case "wrong-reference": evidence.Transaction.NoiDungChuyenKhoan = "WRONG"; break;
            case "cancelled": order = "CANCELLED"; break;
        }
        Assert.Equal(TransferAssessment.Reconciliation, Assess(evidence, payment, order, now));
        Assert.Equal(0, payment.SoTienDaXacMinh);
    }

    [Fact]
    public async Task Runtime_adapters_are_unavailable_and_do_not_accept_even_mock_valid_headers()
    {
        Assert.Null(await new UnavailablePaymentVerificationSource().AuthenticateAsync(new byte[] { 1 },
            new Dictionary<string, string> { ["TEST-ONLY-SIGNATURE"] = "mock-valid" }, default));
        Assert.Null(await new UnavailablePaymentQrSource().GetAsync(OrderReadTests.Snapshot(), default));
    }

    [Theory]
    [InlineData("PENDING", "Đang chờ")]
    [InlineData("PARTIAL", "Chưa xác minh đủ")]
    [InlineData("EXPIRED", "đã hết hạn")]
    [InlineData("RECONCILING", "Đang đối soát")]
    [InlineData("FAILED", "chưa thành công")]
    [InlineData("PAID", "chưa được hỗ trợ")]
    public void Payment_messages_preserve_model_states_without_inventing_paid(string status, string expected)
    {
        var payment = OrderReadTests.Snapshot().Payments[0] with { Status = status };
        Assert.Contains(expected, CustomerOrderPresentation.PaymentMessage(payment, Now));
    }

    [Fact]
    public void Elapsed_deadline_is_not_an_automatic_cancellation_or_stock_release()
    {
        var payment = OrderReadTests.Snapshot().Payments[0];
        Assert.Contains("chưa có xác nhận", CustomerOrderPresentation.PaymentMessage(payment, Now.AddMinutes(15)));
        Assert.Equal("PENDING", payment.Status);
        Assert.Contains("chưa nhất quán", CustomerOrderPresentation.PaymentMessage(payment with { Status = "VERIFIED" }, Now));
        Assert.Contains("Đã xác minh", CustomerOrderPresentation.PaymentMessage(payment with
            { Status = "VERIFIED", Verified = 60000, VerifiedUtc = Now }, Now));
    }

    [Fact]
    public async Task Reopening_payment_reads_new_snapshot_without_creating_payment_or_sending_paid()
    {
        var calls = 0;
        var stub = new OrderReadTests.Stub(request => {
            Assert.Equal(HttpMethod.Get, request.Method); calls++;
            var detail = OrderReadTests.Snapshot();
            if (calls > 1) detail = detail with { Order = detail.Order with { Status = "PENDING_CONFIRMATION" },
                Payments = [detail.Payments[0] with { Status = "VERIFIED", Verified = 60000, VerifiedUtc = Now }] };
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(detail) };
        });
        var first = OrderReadTests.Page(new PaymentPage(OrderReadTests.Client(stub), new UnavailablePaymentQrSource()));
        Assert.IsType<PageResult>(await first.OnGetAsync(7, default)); Assert.Equal(60000, first.Detail!.Remaining);
        var next = OrderReadTests.Page(new PaymentPage(OrderReadTests.Client(stub), new UnavailablePaymentQrSource()));
        await next.OnGetAsync(7, default); Assert.Equal(0, next.Detail!.Remaining);
        Assert.Equal("PENDING_CONFIRMATION", next.Detail.Order.Status); Assert.Equal(2, stub.Calls);
    }

    private static ValidatedPaymentQr Qr() => new(3, 60000, "REF-FAKE-7", Now.AddMinutes(15),
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aWQAAAABJRU5ErkJggg==");

    private sealed class FailedQrSource : IPaymentQrSource
    {
        public Task<ValidatedPaymentQr?> GetAsync(CustomerOrderDetail order, CancellationToken ct) => throw new TimeoutException();
    }

    [Fact]
    public async Task Qr_source_timeout_keeps_read_snapshot_and_never_creates_another_request()
    {
        var stub = new OrderReadTests.Stub(request => {
            Assert.Equal(HttpMethod.Get, request.Method);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(OrderReadTests.Snapshot()) };
        });
        var page = OrderReadTests.Page(new PaymentPage(OrderReadTests.Client(stub), new FailedQrSource()));
        await page.OnGetAsync(7, default);
        Assert.NotNull(page.Detail); Assert.Null(page.Qr); Assert.NotNull(page.Error); Assert.Equal(1, stub.Calls);
    }

    [Fact]
    public void Only_adapter_validated_matching_qr_image_can_reach_view()
    {
        // Fixture image is not a bank QR. This tests the presentation boundary, not VietQR encoding validity.
        Assert.True(PaymentPage.CanDisplay(Qr(), OrderReadTests.Snapshot(), Now));
        Assert.False(PaymentPage.CanDisplay(Qr() with { Reference = "WRONG" }, OrderReadTests.Snapshot(), Now));
        Assert.False(PaymentPage.CanDisplay(Qr() with { Amount = 1 }, OrderReadTests.Snapshot(), Now));
        Assert.False(PaymentPage.CanDisplay(Qr() with { PaymentId = 4 }, OrderReadTests.Snapshot(), Now));
        Assert.False(PaymentPage.CanDisplay(Qr(), OrderReadTests.Snapshot(), Now.AddMinutes(15)));
        Assert.False(PaymentPage.CanDisplay(Qr() with { PngDataUri = "javascript:alert(1)" }, OrderReadTests.Snapshot(), Now));
        Assert.False(PaymentPage.CanDisplay(Qr() with { PngDataUri = "data:image/png;base64,invalid" }, OrderReadTests.Snapshot(), Now));
        Assert.False(PaymentPage.CanDisplay(Qr(), OrderReadTests.Snapshot("CANCELLED"), Now));
    }
}
