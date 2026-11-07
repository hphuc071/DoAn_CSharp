namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class CheckoutDeadlineTests
{
    private static readonly DateTime Operation = new(2026, 11, 7, 6, 58, 19, 399, DateTimeKind.Utc);
    private static readonly DateTime Deadline = Operation.AddMinutes(15);
    private static readonly DateTime Receipt = Deadline.AddTicks(945);

    [Fact]
    public void RecordedOrder7_UsesAuditEventInsteadOfLaterSaveTimestamps()
    {
        var orderSaved = Operation.AddMilliseconds(272);
        var paymentSaved = Operation.AddMilliseconds(578);
        Assert.Equal(899728, (Deadline - orderSaved).TotalMilliseconds);
        Assert.Equal(899422, (Deadline - paymentSaved).TotalMilliseconds);
        CheckoutDeadlineAssertion.Verify(Operation, 15, Deadline, Deadline, [Deadline], Receipt, Receipt);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(16)]
    public void RejectsIncorrectBusinessTimeout(int minutes) => Assert.Throws<InvalidOperationException>(() =>
        CheckoutDeadlineAssertion.Verify(Operation, minutes, Deadline, Deadline, [Deadline], Receipt, Receipt));

    [Theory]
    [InlineData("payment")]
    [InlineData("reservation")]
    [InlineData("api")]
    [InlineData("audit")]
    public void RejectsInconsistentDeadlines(string evidence) => Assert.Throws<InvalidOperationException>(() =>
        CheckoutDeadlineAssertion.Verify(Operation, 15, Deadline,
            evidence == "payment" ? Deadline.AddMilliseconds(1) : Deadline,
            [evidence == "reservation" ? Deadline.AddMilliseconds(1) : Deadline],
            evidence == "api" ? Receipt.AddTicks(1) : Receipt,
            evidence == "audit" ? Receipt.AddTicks(1) : Receipt));

    [Fact]
    public void RejectsReceiptOutsideSqlRoundingPrecision() => Assert.Throws<InvalidOperationException>(() =>
        CheckoutDeadlineAssertion.Verify(Operation, 15, Deadline, Deadline, [Deadline],
            Deadline.AddTicks(5001), Deadline.AddTicks(5001)));

    [Fact]
    public void RejectsMissingEvidence() => Assert.Throws<InvalidOperationException>(() =>
        CheckoutDeadlineAssertion.Verify(Operation, 15, Deadline, Deadline, [], Receipt, null));
}
