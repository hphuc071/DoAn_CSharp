using FourChang.Domain.Entities.Payments;

namespace FourChang.Api.Features.Customer.Payments;

// Provider-specific signature/certificate/raw-body/replay-window authentication belongs here.
// There is deliberately no invented HMAC/header protocol or default financial provider.
public interface IBankCallbackProtocol
{
    Task<VerifiedBankTransfer?> AuthenticateAndDecodeAsync(ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, string> headers, CancellationToken ct);
}
public sealed class UnavailableBankCallbackProtocol : IBankCallbackProtocol
{
    public Task<VerifiedBankTransfer?> AuthenticateAndDecodeAsync(ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, string> headers, CancellationToken ct) => Task.FromResult<VerifiedBankTransfer?>(null);
}
public sealed record BankReceiverProfile(string Provider, string Account);
public sealed class BankCallbackAdapter(IBankCallbackProtocol protocol, BankReceiverProfile receiver) : IPaymentVerificationSource
{
    public async Task<VerifiedBankTransfer?> AuthenticateAsync(ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, string> headers, CancellationToken ct)
    {
        if (body.Length is 0 or > 65536 || string.IsNullOrWhiteSpace(receiver.Provider) || string.IsNullOrWhiteSpace(receiver.Account)) return null;
        var evidence = await protocol.AuthenticateAndDecodeAsync(body, headers, ct);
        if (!Valid(evidence, receiver)) return null;
        var bank = evidence!.Transaction;
        // Copy authenticated fields; never accept callback-provided SQL links, IDs or verification states.
        return new(new GiaoDichNganHang
        {
            TenNhaCungCap = bank.TenNhaCungCap, MaTaiKhoanNganHangQuan = bank.MaTaiKhoanNganHangQuan,
            MaGiaoDichBenNgoai = bank.MaGiaoDichBenNgoai, ChieuGiaoDich = "IN", SoTien = bank.SoTien,
            NoiDungChuyenKhoan = bank.NoiDungChuyenKhoan, ThoiDiemNganHang = bank.ThoiDiemNganHang
        }, "VND");
    }
    public static bool Valid(VerifiedBankTransfer? evidence, BankReceiverProfile receiver) => evidence is not null &&
        evidence.Currency == "VND" && evidence.Transaction is { } b &&
        b.TenNhaCungCap == receiver.Provider && b.TenNhaCungCap.Length is > 0 and <= 50 &&
        b.MaTaiKhoanNganHangQuan == receiver.Account && b.MaTaiKhoanNganHangQuan.Length is > 0 and <= 100 &&
        !string.IsNullOrWhiteSpace(b.MaGiaoDichBenNgoai) && b.MaGiaoDichBenNgoai.Length <= 150 && !b.MaGiaoDichBenNgoai.Any(char.IsControl) &&
        b.ChieuGiaoDich == "IN" && b.SoTien > 0 && b.SoTien <= 999999999999999999m &&
        b.SoTien == decimal.Truncate(b.SoTien) && (b.NoiDungChuyenKhoan?.Length ?? 0) <= 500 &&
        b.ThoiDiemNganHang.Kind == DateTimeKind.Utc && b.ThoiDiemNganHang > DateTime.UnixEpoch;
}
