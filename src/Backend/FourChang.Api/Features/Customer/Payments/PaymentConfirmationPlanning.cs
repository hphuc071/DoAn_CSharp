using FourChang.Domain.Entities.Payments;
using FourChang.Presentation.Shared.Contracts.Customer;

namespace FourChang.Api.Features.Customer.Payments;

public sealed record PaymentConfirmationPlan(TransferAssessment Assessment, string Code,
    string ExpectedVersion, decimal VerifiedTotalAfter, decimal ExcessToReconcile, bool CanConfirmOrder);

// No writer/endpoint/DI registration. Future transaction must re-read this snapshot and durable bank key.
// Authenticating a callback and confirming an order are separate checks; an exact amount alone is insufficient.
public static class PaymentConfirmationPlanning
{
    public static PaymentConfirmationPlan Plan(VerifiedBankTransfer? evidence, ThanhToan payment,
        CustomerOrderDetail order, string provider, string account, DateTime nowUtc,
        IReadOnlyCollection<GiaoDichNganHang> recorded)
    {
        PaymentConfirmationPlan Reject(string code, TransferAssessment assessment = TransferAssessment.Reconciliation) =>
            new(assessment, code, order.Version, payment.SoTienDaXacMinh, 0, false);
        if (payment.MaDonHang != order.Order.Id || payment.MaDonHang <= 0 || payment.MaThanhToan <= 0 ||
            string.IsNullOrWhiteSpace(order.Version) || order.Payments.Count != 1 || order.Refunds.Count != 0 ||
            order.ExpiresUtc != payment.HetHanLuc || order.ShippingFee < 0 || order.Lines.Count == 0 ||
            order.Lines.Any(x => x.Id <= 0 || x.Quantity <= 0 || x.Cancelled != 0 || x.Started != 0 || x.Prepared != 0 || x.BasePrice < 0 ||
                x.Toppings.Any(t => t.Portions <= 0 || t.Price < 0))) return Reject("SNAPSHOT_CONFLICT");
        var request = order.Payments[0];
        if (request.Id != payment.MaThanhToan || request.Method != payment.PhuongThuc || request.Status != payment.TrangThai ||
            request.Requested != payment.SoTienYeuCau || request.Verified != payment.SoTienDaXacMinh ||
            request.Reference != payment.MaThamChieuChuyenKhoan || request.ExpiresUtc != payment.HetHanLuc)
            return Reject("SNAPSHOT_CONFLICT");
        decimal total;
        try { total = order.CurrentTotal; }
        catch (OverflowException) { return Reject("AMOUNT_OVERFLOW"); }
        if (total <= 0 || payment.SoTienYeuCau != total) return Reject("AMOUNT_CONFLICT");
        var assessment = PaymentVerificationPolicy.Assess(evidence, payment, order.Order.Status, provider, account, nowUtc, recorded);
        if (assessment is not (TransferAssessment.Exact or TransferAssessment.Partial or TransferAssessment.Excess))
            return Reject(assessment.ToString().ToUpperInvariant(), assessment);
        decimal verified;
        try { verified = checked(payment.SoTienDaXacMinh + evidence!.Transaction.SoTien); }
        catch (OverflowException) { return Reject("AMOUNT_OVERFLOW"); }
        return new(assessment, assessment == TransferAssessment.Partial ? "UNDERPAID" : "CONFIRMATION_PLANNED",
            order.Version, verified, Math.Max(0, verified - total), assessment is TransferAssessment.Exact or TransferAssessment.Excess);
    }
}
