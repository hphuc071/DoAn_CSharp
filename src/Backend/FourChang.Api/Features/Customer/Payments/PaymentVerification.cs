using FourChang.Domain.Entities.Payments;

namespace FourChang.Api.Features.Customer.Payments;

// Only an authenticated bank adapter may produce this evidence. It is not a callback request DTO.
public sealed record VerifiedBankTransfer(GiaoDichNganHang Transaction, string Currency);
public interface IPaymentVerificationSource
{
    Task<VerifiedBankTransfer?> AuthenticateAsync(ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, string> headers, CancellationToken ct);
}

// Runtime deliberately has no fake adapter, callback endpoint or financial writer.
public sealed class UnavailablePaymentVerificationSource : IPaymentVerificationSource
{
    public Task<VerifiedBankTransfer?> AuthenticateAsync(ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, string> headers, CancellationToken ct) => Task.FromResult<VerifiedBankTransfer?>(null);
}

public enum TransferAssessment { Rejected, Duplicate, Conflict, Partial, Exact, Excess, Reconciliation }

// Planning/validation only: never credits money, creates refunds, changes orders or saves entities.
// A future writer must recheck version/expiry and durable bank transaction uniqueness inside its transaction.
public static class PaymentVerificationPolicy
{
    public static TransferAssessment Assess(VerifiedBankTransfer? evidence, ThanhToan payment,
        string orderStatus, string expectedProvider, string expectedAccount, DateTime nowUtc,
        IReadOnlyCollection<GiaoDichNganHang> recorded)
    {
        if (evidence is null) return TransferAssessment.Rejected;
        var transfer = evidence.Transaction;
        if (string.IsNullOrWhiteSpace(expectedProvider) || string.IsNullOrWhiteSpace(expectedAccount) ||
            transfer.TenNhaCungCap != expectedProvider || transfer.MaTaiKhoanNganHangQuan != expectedAccount ||
            string.IsNullOrWhiteSpace(transfer.MaGiaoDichBenNgoai) || transfer.ChieuGiaoDich != "IN" ||
            transfer.SoTien <= 0 || transfer.SoTien != decimal.Truncate(transfer.SoTien) || evidence.Currency != "VND" ||
            transfer.ThoiDiemNganHang > nowUtc || payment.PhuongThuc != "BANK_TRANSFER")
            return TransferAssessment.Rejected;
        if (transfer.MaThanhToan is { } association && association != payment.MaThanhToan)
            return TransferAssessment.Reconciliation;
        var matches = recorded.Where(x => x.TenNhaCungCap == transfer.TenNhaCungCap &&
            x.MaTaiKhoanNganHangQuan == transfer.MaTaiKhoanNganHangQuan &&
            x.MaGiaoDichBenNgoai == transfer.MaGiaoDichBenNgoai).Take(2).ToArray();
        if (matches.Length > 1) return TransferAssessment.Conflict;
        var previous = matches.SingleOrDefault();
        if (previous is not null)
            return previous.SoTien == transfer.SoTien && previous.ChieuGiaoDich == transfer.ChieuGiaoDich &&
                previous.NoiDungChuyenKhoan == transfer.NoiDungChuyenKhoan &&
                previous.ThoiDiemNganHang == transfer.ThoiDiemNganHang && previous.MaThanhToan == payment.MaThanhToan
                ? TransferAssessment.Duplicate : TransferAssessment.Conflict;
        if (string.IsNullOrWhiteSpace(payment.MaThamChieuChuyenKhoan) ||
            transfer.NoiDungChuyenKhoan != payment.MaThamChieuChuyenKhoan ||
            payment.HetHanLuc is not { } deadline || nowUtc >= deadline || transfer.ThoiDiemNganHang >= deadline ||
            orderStatus != "PENDING_PAYMENT" || payment.TrangThai is not ("PENDING" or "PARTIAL") ||
            payment.SoTienYeuCau <= 0 || payment.SoTienDaXacMinh < 0)
            return TransferAssessment.Reconciliation;
        if (payment.SoTienDaXacMinh > decimal.MaxValue - transfer.SoTien)
            return TransferAssessment.Reconciliation;
        var total = payment.SoTienDaXacMinh + transfer.SoTien;
        return total < payment.SoTienYeuCau ? TransferAssessment.Partial
            : total == payment.SoTienYeuCau ? TransferAssessment.Exact : TransferAssessment.Excess;
    }
}
