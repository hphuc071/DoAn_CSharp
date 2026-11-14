using FourChang.Api.Features.Admin.Orders;
using FourChang.Api.Shared.Orders;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Staff.Orders;

/// <summary>Điểm truy cập đơn của Nhân viên. Chính sách Staff xác minh phiên ca OPEN trước mọi thao tác.</summary>
public static class StaffOrderEndpoints
{
    /// <summary>Dùng chung truy vấn và service vòng đời để tiền, kho và chuyển trạng thái thống nhất giữa POS/Admin.</summary>
    public static void MapStaffOrders(this WebApplication app)
    {
        // Tra hóa đơn theo quyền ca Staff, không mở endpoint ghi/điều chỉnh của Admin.
        var invoices = app.MapGroup("/api/pos/invoices").RequireAuthorization("Staff");
        invoices.MapGet("", (string? search, DateTime? from, DateTime? to, int? page, FourChang.Api.Features.Admin.Invoices.InvoiceAdministrationService service) => service.Read(search, from, to, null, page ?? 1));
        invoices.MapGet("/{id:long}", (long id, FourChang.Api.Features.Admin.Invoices.InvoiceAdministrationService service) => service.StaffDetails(id));
        var group = app.MapGroup("/api/pos/orders").RequireAuthorization("Staff");
        group.MapGet("", ([AsParameters] AdminOrderFilter filter, HttpContext context, OrderAdministrationService service) => service.Read(filter, long.Parse(context.User.FindFirst("shift")!.Value)));
        group.MapGet("/{id:long}", (long id, OrderAdministrationService service) => service.Details(id));
        group.MapPost("/{id:long}/actions", (long id, OrderActionInput input, HttpContext context, OrderWorkflowService service) => service.Execute(id, input, context));
        group.MapPost("/{id:long}/cancellation", (long id, FourChang.Presentation.Shared.Contracts.Orders.CancellationCommand input, HttpContext c, CancellationWorkflow s) => s.Execute(id, input, c));
        group.MapGet("/{id:long}/counter-refunds", (long id, CancellationWorkflow s) => s.CounterRefunds(id));
        group.MapPost("/counter-refunds/{id:long}", (long id, FourChang.Presentation.Shared.Contracts.Orders.CounterRefundCommand input, HttpContext c, CancellationWorkflow s) => s.PayAtCounter(id, input, c));
        group.MapGet("/counter-refunds/operations/{operation:guid}", async (Guid operation, HttpContext c, CafeDbContext db) =>
        {
            var shift = long.Parse(c.User.FindFirst("shift")!.Value);
            var result = await db.NhatKyHeThong.AsNoTracking().Where(x => x.MaThaoTac == operation && x.MaCaLamViec == shift && x.HanhDong == "COUNTER_REFUND" && x.LoaiTacNhan == "STAFF")
                .Select(x => x.KetQuaDaChotJson).SingleOrDefaultAsync();
            return result is null ? Results.NotFound() : Results.Content(result, "application/json");
        });
        group.MapGet("/operations/{operation:guid}", Receipt);
    }

    /// <summary>Chỉ ca đã gửi lệnh được xem lại kết quả chống trùng, kể cả khi biết GUID của ca khác.</summary>
    private static async Task<IResult> Receipt(Guid operation, HttpContext context, CafeDbContext db)
    {
        var shift = long.Parse(context.User.FindFirst("shift")!.Value);
        var audit = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation &&
            x.MaCaLamViec == shift && x.LoaiTacNhan == "STAFF" && x.LoaiDoiTuong == "DonHang" && x.KetQua == "SUCCEEDED");
        return audit?.KetQuaDaChotJson is { } json ? Results.Content(json, "application/json") : Results.NotFound();
    }
}
