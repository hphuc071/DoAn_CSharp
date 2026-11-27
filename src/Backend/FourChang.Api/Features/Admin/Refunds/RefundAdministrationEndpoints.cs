// UC: UC-ADM-06
// Chức năng: Khai báo API quản lý và theo dõi hồ sơ hoàn tiền.
// Cần nhớ: Đọc hàm từ bước kiểm tra dữ liệu đến bước trả kết quả; các quyết định nghiệp vụ do Backend kiểm tra.

using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Refunds;

/// <summary>UC-ADM-06: theo dõi hóa đơn/hoàn; bảo vệ khoản hoàn tại quầy khỏi thao tác quản lý trùng.</summary>
public static class RefundAdministrationEndpoints
{
    /// <summary>Giữ các route này riêng Admin. Staff chi tiền mặt đã duyệt qua endpoint counter-refunds, không được tự tăng thu/hạn mức.</summary>
    public static void MapRefundAdministration(this WebApplication app)
    {
        var g = app.MapGroup("/api/admin/refunds").RequireAuthorization("Admin");
        g.MapGet("", (string? search, DateTime? from, DateTime? to, string? status, RefundAdministrationService s) => s.Read(search, from, to, status));
        g.MapGet("/{id:long}", (long id, RefundAdministrationService s) => s.Details(id));
        g.MapPost("", (RefundRequestInput i, HttpContext c, RefundAdministrationService s, CafeDbContext db) =>
            GuardCounter(db, null, i.MaHuyMon, () => s.Save(null, i, c), i.LoaiNguon == "CANCELLATION" ? i.MaThanhToan : null, i.MaDonHang));
        g.MapPut("/{id:long}", (long id, RefundRequestInput i, HttpContext c, RefundAdministrationService s, CafeDbContext db) => GuardCounter(db, id, null, () => s.Save(id, i, c)));
        g.MapPost("/{id:long}/decision", (long id, RefundDecisionInput i, HttpContext c, RefundAdministrationService s, CafeDbContext db) => GuardCounter(db, id, null, () => s.Decide(id, i, c)));
        g.MapPost("/{id:long}/attempts", (long id, RefundAttemptInput i, HttpContext c, RefundAdministrationService s, CafeDbContext db) => GuardCounter(db, id, null, () => s.Begin(id, i, c)));
        g.MapPost("/attempts/{id:long}/verify", async (long id, RefundVerificationInput i, HttpContext c, RefundAdministrationService s, CafeDbContext db) =>
        {
            var refundId = await db.LanChiHoanTien.Where(x => x.MaLanChiHoan == id).Select(x => (long?)x.MaHoanTien).SingleOrDefaultAsync();
            return await GuardCounter(db, refundId, null, () => s.Verify(id, i, c));
        });
        g.MapGet("/operations/{id:guid}", (Guid id, CafeDbContext db, HttpContext c) => AdminOperation.Receipt(db, id, "HoSoHoanTien", c));
        g.MapPost("/incoming/reconcile", (IncomingReconciliationInput i, HttpContext c, IncomingReconciliationService s) => s.Match(i, c));
        g.MapGet("/incoming/{id:long}", async (long id, CafeDbContext db) =>
        {
            var payment = await db.ThanhToan.Where(x => x.MaThanhToan == id && x.PhuongThuc == "BANK_TRANSFER")
                .Select(x => new IncomingPaymentView(x.MaThanhToan, x.MaDonHang, x.SoTienYeuCau, x.SoTienDaXacMinh, x.TrangThai, x.PhienBanDuLieu)).SingleOrDefaultAsync();
            return payment is null ? Results.NotFound() : Results.Ok(payment);
        });
    }
    /// <summary>Khoản hoàn do duyệt hủy đã có hạn mức: không sửa/duyệt/chi lại bằng API quản lý cũ, kể cả replay receipt.</summary>
    private static async Task<IResult> GuardCounter(CafeDbContext db, long? id, long? cancellation, Func<Task<IResult>> action, long? payment = null, long? order = null)
    {
        if (await db.HoSoHoanTien.AnyAsync(x => x.LoaiNguon == "CANCELLATION" && x.PhuongThucHoan == "CASH" &&
            (id != null && x.MaHoanTien == id || cancellation != null && x.MaHuyMon == cancellation || payment != null && x.MaThanhToan == payment && x.MaDonHang == order)))
            return Results.Problem(statusCode: 409, detail: "Khoản hoàn đã được duyệt cùng yêu cầu hủy. Nhân viên trả tiền và xác nhận tại quầy; Quản lý chỉ theo dõi kết quả.");
        return await action();
    }

}
