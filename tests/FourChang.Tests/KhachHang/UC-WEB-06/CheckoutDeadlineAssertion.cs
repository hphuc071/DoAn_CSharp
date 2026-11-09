namespace FourChang.Tests;

// SQL datetime2(3) rounds timestamps; JSON receipts preserve DateTime ticks.
internal static class CheckoutDeadlineAssertion
{
    internal static void Verify(DateTime operationAuditUtc, int timeoutMinutes,
        DateTime? orderDeadline, DateTime? paymentDeadline,
        IEnumerable<DateTime?> reservationDeadlines, DateTime? apiReceiptDeadline,
        DateTime? auditReceiptDeadline)
    {
        if (timeoutMinutes <= 0 || orderDeadline is null || paymentDeadline is null ||
            apiReceiptDeadline is null || auditReceiptDeadline is null)
            throw new InvalidOperationException("Missing or invalid deadline evidence.");

        // Both audit event and SQL deadlines have the same millisecond precision.
        if (orderDeadline.Value != operationAuditUtc.AddMinutes(timeoutMinutes) ||
            paymentDeadline != orderDeadline)
            throw new InvalidOperationException("Deadline differs from operation time plus timeout.");
        var reservations = reservationDeadlines.ToArray();
        if (reservations.Length == 0 || reservations.Any(x => x != orderDeadline))
            throw new InvalidOperationException("Reservation deadline mismatch.");
        if (apiReceiptDeadline != auditReceiptDeadline ||
            Math.Abs((apiReceiptDeadline.Value - orderDeadline.Value).Ticks) > TimeSpan.TicksPerMillisecond / 2)
            throw new InvalidOperationException("Receipt deadline mismatch beyond SQL rounding precision.");
    }
}
