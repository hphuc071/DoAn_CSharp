using FourChang.Api.Shared.Operations;
namespace FourChang.Api.Features.Payments.Simulation;
public static class PaymentSimulationEndpoints
{
    /// <summary>Lấy QR cần quyền chủ đơn; link đã ký chỉ dành xác nhận mô phỏng Development.</summary>
    public static void MapPaymentSimulation(this WebApplication app)
    {
        app.MapGet("/api/customer/orders/{id:long}/simulation", (long id, HttpContext c, PaymentSimulationService s) => Run(async () => Results.Ok(await s.Qr(id,c,false)))).RequireAuthorization("Customer");
        app.MapGet("/api/pos/checkout/{id:long}/simulation", (long id, HttpContext c, PaymentSimulationService s) => Run(async () => Results.Ok(await s.Qr(id,c,true)))).RequireAuthorization("Staff");
        app.MapGet("/simulation/pay/{token}", (string token, PaymentSimulationService s) => Run(() => Task.FromResult(s.Landing(token))));
        app.MapPost("/simulation/pay/{token}", (string token, PaymentSimulationService s) => Run(() => s.Confirm(token)));
    }
    /// <summary>Trả lỗi nghiệp vụ có nghĩa, không lộ SQL hoặc khóa ký.</summary>
    private static async Task<IResult> Run(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (AdminRule ex) { return Results.Problem(statusCode: ex.Status, detail: ex.Message); }
    }
}
