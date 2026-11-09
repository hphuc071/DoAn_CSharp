using FourChang.Api.Features.Customer.Orders;
using FourChang.Api.Features.Customer.Payments;
using FourChang.Domain.Entities.Inventory;
using FourChang.Domain.Entities.Payments;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Web.Services;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class Phase3SafetyTests
{
    private static readonly DateTime Now = OrderReadTests.Now;
    private static ThanhToan Payment() => new() { MaThanhToan = 3, MaDonHang = 7, PhuongThuc = "BANK_TRANSFER",
        TrangThai = "PENDING", SoTienYeuCau = 60000, MaThamChieuChuyenKhoan = "REF-FAKE-7", HetHanLuc = Now.AddMinutes(15) };
    private static VerifiedBankTransfer Evidence(decimal amount = 60000) => new(new()
    { TenNhaCungCap = "MOCK", MaTaiKhoanNganHangQuan = "MOCK-ACCOUNT", MaGiaoDichBenNgoai = "TX-1", ChieuGiaoDich = "IN",
      SoTien = amount, NoiDungChuyenKhoan = "REF-FAKE-7", MaThanhToan = 3, ThoiDiemNganHang = Now }, "VND");
    private static PaymentConfirmationPlan Plan(VerifiedBankTransfer? evidence, ThanhToan? payment = null,
        CustomerOrderDetail? detail = null, DateTime? time = null, IReadOnlyCollection<GiaoDichNganHang>? recorded = null) =>
        PaymentConfirmationPlanning.Plan(evidence, payment ?? Payment(), detail ?? OrderReadTests.Snapshot(),
            "MOCK", "MOCK-ACCOUNT", time ?? Now, recorded ?? []);

    [Theory]
    [InlineData(50000, false, 0)] [InlineData(60000, true, 0)] [InlineData(70000, true, 10000)]
    public void Only_verified_matching_full_amount_can_propose_confirmation_and_excess_is_separate(decimal amount, bool confirm, decimal excess)
    {
        var payment = Payment(); var order = OrderReadTests.Snapshot();
        var plan = Plan(Evidence(amount), payment, order);
        Assert.Equal(confirm, plan.CanConfirmOrder); Assert.Equal(amount, plan.VerifiedTotalAfter); Assert.Equal(excess, plan.ExcessToReconcile);
        Assert.Equal(order.Version, plan.ExpectedVersion);
        Assert.Equal(0, payment.SoTienDaXacMinh); Assert.Equal("PENDING_PAYMENT", order.Order.Status); // No writes or PAID.
    }

    [Fact]
    public void Accumulated_partial_verified_receipts_can_plan_full_confirmation_without_crediting_again()
    {
        var payment = Payment(); payment.SoTienDaXacMinh = 40000; payment.TrangThai = "PARTIAL";
        var order = OrderReadTests.Snapshot(); order = order with { Payments = [order.Payments[0] with { Verified = 40000, Status = "PARTIAL" }] };
        var plan = Plan(Evidence(20000), payment, order);
        Assert.True(plan.CanConfirmOrder); Assert.Equal(60000, plan.VerifiedTotalAfter); Assert.Equal(40000, payment.SoTienDaXacMinh);
    }

    [Theory]
    [InlineData("unauthenticated")] [InlineData("wrong-reference")] [InlineData("wrong-account")]
    [InlineData("late")] [InlineData("duplicate")] [InlineData("wrong-link")]
    public void Untrusted_late_duplicate_or_mismatched_money_never_proposes_order_confirmation(string scenario)
    {
        VerifiedBankTransfer? evidence = Evidence(); var time = Now; GiaoDichNganHang[] recorded = [];
        switch (scenario)
        {
            case "unauthenticated": evidence = null; break;
            case "wrong-reference": evidence!.Transaction.NoiDungChuyenKhoan = "OTHER"; break;
            case "wrong-account": evidence!.Transaction.MaTaiKhoanNganHangQuan = "OTHER"; break;
            case "late": time = Now.AddMinutes(15); break;
            case "duplicate": recorded = [evidence!.Transaction]; break;
            case "wrong-link": evidence!.Transaction.MaThanhToan = 99; break;
        }
        var plan = Plan(evidence, time: time, recorded: recorded);
        Assert.False(plan.CanConfirmOrder); Assert.Equal(0, plan.VerifiedTotalAfter);
    }

    [Theory]
    [InlineData("wrong-order")] [InlineData("wrong-request")] [InlineData("wrong-total")]
    [InlineData("version")] [InlineData("deadline")] [InlineData("processed")]
    [InlineData("refund")] [InlineData("duplicate-payment")]
    public void Snapshot_conflicts_block_confirmation_before_any_financial_transition(string scenario)
    {
        var payment = Payment(); var detail = OrderReadTests.Snapshot();
        switch (scenario)
        {
            case "wrong-order": payment.MaDonHang = 8; break;
            case "wrong-request": detail = detail with { Payments = [detail.Payments[0] with { Id = 99 }] }; break;
            case "wrong-total": detail = detail with { ShippingFee = 1000 }; break;
            case "version": detail = detail with { Version = "" }; break;
            case "deadline": detail = detail with { ExpiresUtc = Now.AddHours(1) }; break;
            case "processed": detail = detail with { Lines = [detail.Lines[0] with { Started = 1 }] }; break;
            case "refund": detail = detail with { Refunds = [new(1, "REF", "REQUESTED", 1, null, 0)] }; break;
            case "duplicate-payment": detail = detail with { Payments = [detail.Payments[0], detail.Payments[0]] }; break;
        }
        Assert.False(Plan(Evidence(), payment, detail).CanConfirmOrder);
    }

    [Theory]
    [InlineData("CANCELLED")] [InlineData("PENDING_CONFIRMATION")] [InlineData("ACCEPTED")]
    public void Closed_or_already_confirmed_orders_cannot_be_resurrected(string status) =>
        Assert.False(Plan(Evidence(), detail: OrderReadTests.Snapshot(status)).CanConfirmOrder);

    [Fact]
    public void Callback_assessment_handles_decimal_overflow_and_mislinked_duplicate_without_throwing()
    {
        var payment = Payment(); payment.SoTienDaXacMinh = decimal.MaxValue;
        Assert.Equal(TransferAssessment.Reconciliation,
            PaymentVerificationPolicy.Assess(Evidence(), payment, "PENDING_PAYMENT", "MOCK", "MOCK-ACCOUNT", Now, []));
        var recorded = Evidence().Transaction; var wrong = Evidence(); wrong.Transaction.MaThanhToan = 99;
        Assert.Equal(TransferAssessment.Reconciliation,
            PaymentVerificationPolicy.Assess(wrong, Payment(), "PENDING_PAYMENT", "MOCK", "MOCK-ACCOUNT", Now, [recorded]));
    }

    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now.AddHours(1)); }
    [Theory]
    [InlineData("POS")] [InlineData("")] [InlineData("web")]
    public void Lifecycle_plans_cannot_treat_other_channels_as_web(string channel)
    {
        var detail = OrderReadTests.Snapshot("PENDING_CONFIRMATION");
        var snapshot = new OrderLifecycleSnapshot(6, channel, detail, [], [], true);
        var planner = new OrderLifecyclePlanner(new Clock());
        Assert.Equal("NOT_FOUND", planner.Cancel(snapshot, new(6, 7, Guid.NewGuid(), detail.Version, "Đổi ý")).Code);
        Assert.Equal("INVALID_COMMAND", planner.Expire(snapshot, new(7, Guid.NewGuid(), detail.Version)).Code);
    }

    [Theory]
    [InlineData("version")] [InlineData("lines")] [InlineData("payments")] [InlineData("invoices")]
    public void Malformed_or_duplicate_snapshot_is_not_displayed_as_current_customer_data(string corrupt)
    {
        var order = OrderReadTests.Snapshot();
        order = corrupt switch {
            "version" => order with { Version = "" },
            "lines" => order with { Lines = [order.Lines[0], order.Lines[0]] },
            "payments" => order with { Payments = [order.Payments[0], order.Payments[0]] },
            _ => order with { Invoices = [new(1, "INV", "ORIGINAL", "ISSUED", 60000, Now), new(1, "INV", "ORIGINAL", "ISSUED", 60000, Now)] }
        };
        Assert.False(CustomerOrderPresentation.Valid(order, 7));
    }
}
