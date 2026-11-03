using FourChang.Presentation.Shared.ApiClients;
using FourChang.Web.Security;
namespace FourChang.Web.Services;

/// <summary>Đăng ký phần kết nối API và phiên khách hàng ở một nơi; Pages chỉ nhận các dịch vụ qua DI.</summary>
public static class CustomerServicesRegistration
{
    /// <summary>Giữ nguyên vòng đời DI, địa chỉ API và timeout; tách cấu hình dịch vụ khỏi Program.cs.</summary>
    public static IServiceCollection AddCustomerServices(this IServiceCollection services)
    {
        services.AddDataProtection();
        services.AddScoped<PendingCustomerLogout>();
        services.AddHttpClient<CafeApiClient>(client =>
        {
            client.BaseAddress = CafeApiClient.BaseAddress;
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        return services;
    }
}
