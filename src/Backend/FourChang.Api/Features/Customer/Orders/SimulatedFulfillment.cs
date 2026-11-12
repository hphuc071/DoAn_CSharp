using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Customer.Orders;

/// <summary>Chính sách mô phỏng đã chốt: chọn bàn có thật trong CSDL và giao hàng phí cố định 15.000đ.</summary>
public static class SimulatedFulfillment
{
    public const decimal DeliveryFee = 15000m;

    /// <summary>Chỉ bật trong Development và khi có cờ riêng; không biến mô phỏng thành dịch vụ giao thật.</summary>
    public static bool Enabled(IConfiguration configuration) =>
        configuration.GetValue<bool>("Orders:FulfillmentSimulationEnabled") &&
        string.Equals(configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);

    /// <summary>Xác thực địa chỉ/bàn theo hình thức; phí và khoảng cách không do trình duyệt quyết định.</summary>
    public static async Task<string?> ValidateAsync(CafeDbContext db, CreateCustomerOrderRequest request, bool enabled, CancellationToken ct)
    {
        if (request.KhoangCachKm is not null) return "Mô phỏng không nhận khoảng cách do người dùng nhập.";
        if (request.HinhThucNhanMon == "TAKEAWAY")
            return request.MaBanHienThi is null && request.DiaChiGiao is null ? null : "Mang đi không kèm bàn hoặc địa chỉ giao.";
        if (!enabled) return "Hình thức nhận món chưa được bật.";
        if (request.HinhThucNhanMon == "DINE_IN")
        {
            if (request.DiaChiGiao is not null || string.IsNullOrWhiteSpace(request.MaBanHienThi)) return "Vui lòng chọn bàn.";
            return await db.Ban.AnyAsync(b => b.MaBanHienThi == request.MaBanHienThi && b.DangSuDung, ct)
                ? null : "Bàn đã ngừng sử dụng hoặc không tồn tại.";
        }
        if (request.HinhThucNhanMon == "DELIVERY")
            return request.MaBanHienThi is null && request.DiaChiGiao is { Length: >= 5 and <= 500 } && !request.DiaChiGiao.Any(char.IsControl)
                ? null : "Địa chỉ giao hàng cần từ 5 đến 500 ký tự, không kèm bàn.";
        return "Hình thức nhận món không hợp lệ.";
    }
}
