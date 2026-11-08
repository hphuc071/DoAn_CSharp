using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Web.Services;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class Uc07DemoQrTests
{
    private static CustomerOrderDetail Current()
    {
        var order = OrderReadTests.Snapshot();
        var deadline = DateTime.UtcNow.AddMinutes(10);
        return order with
        {
            ExpiresUtc = deadline,
            Payments = [order.Payments[0] with { ExpiresUtc = deadline }]
        };
    }

    [Theory]
    [InlineData(null, "Development")]
    [InlineData("false", "Development")]
    [InlineData("true", "Production")]
    [InlineData("true", "Staging")]
    public void Demo_is_off_unless_explicitly_enabled_in_development(string? flag, string environment)
    {
        Assert.False(PaymentDemoQrPolicy.CanShow(Current(), flag, environment, DateTime.UtcNow));
    }

    [Fact]
    public void Owned_unpaid_pending_order_can_show_demo_without_bank_reference()
    {
        var current = Current();
        var order = current with { Payments = [current.Payments[0] with { Reference = null }] };
        Assert.True(PaymentDemoQrPolicy.CanShow(order, "true", "Development", DateTime.UtcNow));
        Assert.Equal("PENDING_PAYMENT", order.Order.Status);
        Assert.Equal(0, order.Payments[0].Verified);
    }

    [Theory]
    [InlineData("paid")]
    [InlineData("partial")]
    [InlineData("cancelled")]
    [InlineData("expired-order")]
    [InlineData("expired-payment")]
    [InlineData("mismatched-amount")]
    public void Demo_hides_for_non_payable_or_inconsistent_orders(string scenario)
    {
        var order = Current();
        switch (scenario)
        {
            case "paid":
                order = order with { Order = order.Order with { Status = "PENDING_CONFIRMATION" } };
                break;
            case "partial":
                order = order with { Payments = [order.Payments[0] with { Status = "PARTIAL", Verified = 1000 }] };
                break;
            case "cancelled":
                order = order with { Order = order.Order with { Status = "CANCELLED" } };
                break;
            case "expired-order":
                order = order with { ExpiresUtc = DateTime.UtcNow.AddMinutes(-1) };
                break;
            case "expired-payment":
                order = order with { Payments = [order.Payments[0] with { ExpiresUtc = DateTime.UtcNow.AddMinutes(-1) }] };
                break;
            default:
                order = order with { Payments = [order.Payments[0] with { Requested = 1 }] };
                break;
        }
        Assert.False(PaymentDemoQrPolicy.CanShow(order, "true", "Development", DateTime.UtcNow));
    }

    [Fact]
    public void Demo_deadline_is_the_earliest_server_utc_deadline()
    {
        var order = Current();
        var soon = DateTime.UtcNow.AddMinutes(2);
        order = order with { Payments = [order.Payments[0] with { ExpiresUtc = soon }] };
        Assert.Equal(soon, PaymentDemoQrPolicy.ExpiryUtc(order));
        Assert.Equal(DateTimeKind.Utc, PaymentDemoQrPolicy.ExpiryUtc(order).Kind);
    }
}
