using FourChang.Presentation.Shared.Contracts.Staff;
namespace FourChang.Presentation.Shared.ApiClients;
public sealed partial class CafeApiClient
{
    /// <summary>Lấy bàn đang dùng từ nguồn chung, không tự khai báo số bàn ở POS.</summary>
    public Task<List<PosDiningTable>> GetPosTablesAsync() => SendAsync<List<PosDiningTable>>(HttpMethod.Get, "/api/pos/tables");
    /// <summary>Tạo đơn/thu tiền nguyên tử; thử lại luôn giữ cùng mã thao tác và nội dung.</summary>
    public Task<PosCheckoutReceipt> CheckoutPosAsync(PosCheckoutInput input) => SendAsync<PosCheckoutReceipt>(HttpMethod.Post, "/api/pos/checkout", input);
    /// <summary>Chỉ đọc trạng thái thanh toán của đơn thuộc ca hiện tại.</summary>
    public Task<PosCheckoutReceipt> GetPosCheckoutAsync(long id) => SendAsync<PosCheckoutReceipt>(HttpMethod.Get, $"/api/pos/checkout/{id}");
    /// <summary>Lấy QR thử nghiệm nếu server bật chế độ Development và đơn còn chờ tiền.</summary>
    public Task<SimulationQr> GetPosSimulationQrAsync(long id) => SendAsync<SimulationQr>(HttpMethod.Get, $"/api/pos/checkout/{id}/simulation");
    /// <summary>Lấy QR thử nghiệm cho đơn Web thuộc khách hiện tại; server không cho xem đơn người khác.</summary>
    public Task<SimulationQr> GetCustomerSimulationQrAsync(long id) => SendAsync<SimulationQr>(HttpMethod.Get, $"/api/customer/orders/{id}/simulation");
}
