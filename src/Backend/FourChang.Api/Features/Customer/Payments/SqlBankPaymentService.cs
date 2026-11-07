using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Domain.Entities.Payments;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Customer.Payments;

// Not a public HTTP endpoint. Only the authenticated source feeds this transactional writer.
// No provider calls inside a SQL transaction; no retry after ambiguous commit.
public sealed class SqlBankPaymentService(CafeDbContext db, IConfiguration config,
    IPaymentVerificationSource source, TimeProvider clock)
{
    public async Task<IResult> ProcessAsync(ReadOnlyMemory<byte> rawBody,
        IReadOnlyDictionary<string, string> headers, CancellationToken ct)
    {
        if (!OrderIntegrationSafety.Enabled(config, "PaymentProcessingEnabled"))
            return Results.Json(new { code = "PAYMENT_PROCESSING_DISABLED" }, statusCode: 503);
        var receiver = new BankReceiverProfile(config["Bank:Provider"] ?? "", config["Bank:ReceiverAccount"] ?? "");
        if (string.IsNullOrWhiteSpace(receiver.Provider) || string.IsNullOrWhiteSpace(receiver.Account) || rawBody.Length is 0 or > 65536)
            return Results.BadRequest(new { code = "BANK_PROFILE_OR_BODY_INVALID" });
        var evidence = await source.AuthenticateAsync(rawBody, headers, ct);
        if (!BankCallbackAdapter.Valid(evidence, receiver)) return Results.Unauthorized();
        // Own a frozen copy across awaits; never let a provider implementation mutate authenticated
        // fields between hashing, policy assessment and persistence.
        var input = evidence!.Transaction;
        var bank = new GiaoDichNganHang
        {
            TenNhaCungCap = input.TenNhaCungCap, MaTaiKhoanNganHangQuan = input.MaTaiKhoanNganHangQuan,
            MaGiaoDichBenNgoai = input.MaGiaoDichBenNgoai, ChieuGiaoDich = input.ChieuGiaoDich,
            SoTien = input.SoTien, NoiDungChuyenKhoan = input.NoiDungChuyenKhoan, ThoiDiemNganHang = input.ThoiDiemNganHang
        };
        evidence = new(bank, evidence.Currency);
        var now = clock.GetUtcNow().UtcDateTime;
        if (bank.ThoiDiemNganHang > now) return Results.BadRequest(new { code = "FUTURE_BANK_TIMESTAMP" });
        // Canonical hash excludes SQL IDs and links; provider transaction identity is global/durable.
        var identity = OrderIntegrationSafety.Hash(new { bank.TenNhaCungCap, bank.MaTaiKhoanNganHangQuan, bank.MaGiaoDichBenNgoai });
        var operation = new Guid(identity.AsSpan(0, 16));
        var hash = OrderIntegrationSafety.Hash(new { bank.TenNhaCungCap, bank.MaTaiKhoanNganHangQuan,
            bank.MaGiaoDichBenNgoai, bank.ChieuGiaoDich, bank.SoTien, bank.NoiDungChuyenKhoan, bank.ThoiDiemNganHang, evidence.Currency });
        // Resolve reference outside the transaction; do not retain a payment S-lock while waiting
        // for the per-order lock (two different callbacks must not deadlock each other that way).
        var candidate = string.IsNullOrWhiteSpace(bank.NoiDungChuyenKhoan) ? null :
            await db.ThanhToan.AsNoTracking().SingleOrDefaultAsync(x => x.MaThamChieuChuyenKhoan == bank.NoiDungChuyenKhoan, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await OrderIntegrationSafety.LockAsync(db, tx, "4chang.order." + operation.ToString("N"), ct))
            return Results.Conflict(new { code = "BANK_OPERATION_BUSY" });
        var previous = await db.GiaoDichNganHang.AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenNhaCungCap == bank.TenNhaCungCap && x.MaTaiKhoanNganHangQuan == bank.MaTaiKhoanNganHangQuan &&
            x.MaGiaoDichBenNgoai == bank.MaGiaoDichBenNgoai, ct);
        var audit = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation, ct);
        if (previous is not null || audit is not null)
        {
            if (previous?.MaBamNoiDung is null || audit?.MaBamYeuCau is null || audit.HanhDong != "RECORD_BANK_TRANSFER" ||
                !CryptographicOperations.FixedTimeEquals(previous.MaBamNoiDung, hash) ||
                !CryptographicOperations.FixedTimeEquals(audit.MaBamYeuCau, hash) || string.IsNullOrWhiteSpace(audit.KetQuaDaChotJson))
                return Results.Conflict(new { code = "BANK_REPLAY_CONFLICT" });
            return Results.Content(audit.KetQuaDaChotJson, "application/json", statusCode: 200);
        }
        if (await db.ThanhToan.AnyAsync(x => x.MaThaoTac == operation, ct))
            return Results.Conflict(new { code = "BANK_OPERATION_KEY_CONFLICT" });
        var code = "RECONCILIATION_REQUIRED";
        var confirmed = false;
        long? linkedOrder = null;
        long? linkedPayment = null;
        string? orderStatusBefore = null, paymentStatusBefore = null, orderStatusAfter = null, paymentStatusAfter = null;
        decimal? verifiedBefore = null, verifiedAfter = null;
        if (candidate is not null && candidate.MaThamChieuChuyenKhoan == bank.NoiDungChuyenKhoan)
        {
            if (!await OrderIntegrationSafety.LockAsync(db, tx, "4chang.orderstate." + candidate.MaDonHang, ct))
                return Results.Conflict(new { code = "ORDER_BUSY" });
            var order = await db.DonHang.SingleOrDefaultAsync(x => x.MaDonHang == candidate.MaDonHang && x.KenhDatHang == "WEB", ct);
            var payment = await db.ThanhToan.SingleAsync(x => x.MaThanhToan == candidate.MaThanhToan, ct);
            if (order is not null)
            {
                orderStatusBefore = order.TrangThai; paymentStatusBefore = payment.TrangThai; verifiedBefore = payment.SoTienDaXacMinh;
                orderStatusAfter = order.TrangThai; paymentStatusAfter = payment.TrangThai; verifiedAfter = payment.SoTienDaXacMinh;
                var lines = await db.ChiTietDonHang.AsNoTracking().Where(x => x.MaDonHang == order.MaDonHang).ToListAsync(ct);
                var ids = lines.Select(x => x.MaChiTietDonHang).ToArray();
                var toppings = await db.MonThemChiTietDonHang.AsNoTracking().Where(x => ids.Contains(x.MaChiTietDonHang)).ToListAsync(ct);
                var requests = await db.ThanhToan.AsNoTracking().Where(x => x.MaDonHang == order.MaDonHang).ToListAsync(ct);
                var hasRefund = await db.HoSoHoanTien.AnyAsync(x => x.MaDonHang == order.MaDonHang, ct);
                var ledgerTotal = await db.GiaoDichNganHang.Where(x => x.MaThanhToan == payment.MaThanhToan &&
                    x.TrangThaiXacMinh == "VERIFIED" && x.ChieuGiaoDich == "IN").SumAsync(x => (decimal?)x.SoTien, ct) ?? 0;
                if (!hasRefund && requests.Count == 1 && ledgerTotal == payment.SoTienDaXacMinh)
                {
                    var snapshot = new CustomerOrderDetail(new(order.MaDonHang, order.MaDonHienThi, order.TrangThai,
                        order.HinhThucNhanMon, order.ThoiDiemTao, order.ThoiDiemCapNhat), Convert.ToBase64String(order.PhienBanDuLieu),
                        order.HetHanLuc, order.TiepNhanLuc, order.HoanTatLuc, order.PhiGiaoHangBanDau, order.PhiGiaoHangHienTai,
                        lines.Select(x => new CustomerOrderLine(x.MaChiTietDonHang, x.TenMonDaChot, x.KichCoDaChot, x.NhietDoDaChot,
                            x.PhanTramDuong, x.PhanTramDa, x.SoLuongDat, x.SoLuongDaHuy, x.SoLuongBatDauPha, x.SoLuongPhaXong, x.GiaMonDaChot,
                            x.GhiChu, toppings.Where(t => t.MaChiTietDonHang == x.MaChiTietDonHang).Select(t =>
                                new CustomerOrderTopping(t.TenMonThemDaChot, t.SoPhanMoiLy, t.GiaMoiPhanDaChot)).ToArray())).ToArray(),
                        [new(payment.MaThanhToan, payment.MaThanhToanHienThi, payment.PhuongThuc, payment.TrangThai, payment.SoTienYeuCau,
                            payment.SoTienDaXacMinh, payment.MaThamChieuChuyenKhoan, payment.HetHanLuc, payment.XacMinhLuc)], [], [], now);
                    // Locks/reads may have waited across the deadline. Never assess with the old
                    // authentication/start timestamp; recheck server time at the decision boundary.
                    now = clock.GetUtcNow().UtcDateTime;
                    var plan = PaymentConfirmationPlanning.Plan(evidence, payment, snapshot, receiver.Provider, receiver.Account, now, []);
                    if (plan.Assessment is TransferAssessment.Partial or TransferAssessment.Exact or TransferAssessment.Excess &&
                        plan.VerifiedTotalAfter <= 999999999999999999m)
                    {
                        payment.SoTienDaXacMinh = plan.VerifiedTotalAfter;
                        payment.TrangThai = plan.CanConfirmOrder ? "VERIFIED" : "PARTIAL";
                        payment.XacMinhLuc = now;
                        if (plan.CanConfirmOrder) order.TrangThai = "PENDING_CONFIRMATION";
                        orderStatusAfter = order.TrangThai; paymentStatusAfter = payment.TrangThai; verifiedAfter = payment.SoTienDaXacMinh;
                        linkedOrder = order.MaDonHang; linkedPayment = payment.MaThanhToan;
                        bank.MaThanhToan = payment.MaThanhToan; confirmed = plan.CanConfirmOrder;
                        code = plan.ExcessToReconcile > 0 ? "EXCESS_REQUIRES_RECONCILIATION" : plan.Code;
                    }
                }
            }
        }
        // Even unmatched/late money is recorded once as authenticated bank truth, but not silently
        // allocated/credited to an order. Admin reconciliation must resolve linkage/refund with a real actor.
        var row = new GiaoDichNganHang
        {
            TenNhaCungCap = bank.TenNhaCungCap, MaTaiKhoanNganHangQuan = bank.MaTaiKhoanNganHangQuan,
            MaGiaoDichBenNgoai = bank.MaGiaoDichBenNgoai, ChieuGiaoDich = "IN", SoTien = bank.SoTien,
            NoiDungChuyenKhoan = bank.NoiDungChuyenKhoan, ThoiDiemNganHang = bank.ThoiDiemNganHang,
            TrangThaiXacMinh = "VERIFIED", XacMinhLuc = now, MaThanhToan = linkedPayment, MaBamNoiDung = hash
        };
        db.GiaoDichNganHang.Add(row);
        var receipt = new { orderId = linkedOrder, paymentId = linkedPayment, code, orderConfirmed = confirmed, amount = bank.SoTien };
        var entry = OrderIntegrationSafety.SystemAudit(operation, "RECORD_BANK_TRANSFER",
            linkedOrder?.ToString(System.Globalization.CultureInfo.InvariantCulture), hash, receipt, now);
        entry.DuLieuTruocJson = JsonSerializer.Serialize(new { orderStatusBefore, paymentStatusBefore, verifiedBefore });
        entry.DuLieuSauJson = JsonSerializer.Serialize(new { receipt, orderStatusAfter, paymentStatusAfter, verifiedAfter });
        db.NhatKyHeThong.Add(OrderAuditOutbox.Stamp(entry));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Ok(receipt);
    }
}
