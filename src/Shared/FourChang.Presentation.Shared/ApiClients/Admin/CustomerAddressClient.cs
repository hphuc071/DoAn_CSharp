using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Customer;

namespace FourChang.Presentation.Shared.ApiClients;

/// <summary>Client địa chỉ Admin tách riêng để nhóm không phải sửa chung một file client lớn.</summary>
public sealed partial class CafeApiClient
{
    /// <summary>Lấy sổ địa chỉ và version hồ sơ để đối chiếu với Web.</summary>
    public Task<CustomerAddressBook> GetAdminAddressesAsync(long owner) => SendAsync<CustomerAddressBook>(HttpMethod.Get, $"/api/admin/customers/{owner}/addresses");
    /// <summary>Thêm/sửa có GUID và version, không tự tạo mã mới khi thử lại.</summary>
    public Task<CustomerAddressBook> SaveAdminAddressAsync(long owner, long? id, SaveSelfAddress input) => SendAsync<CustomerAddressBook>(
        id is null ? HttpMethod.Post : HttpMethod.Put, $"/api/admin/customers/{owner}/addresses" + (id is null ? "" : $"/{id}"), input);
    /// <summary>Đặt mặc định/ngừng dùng và giữ bản ghi lịch sử.</summary>
    public Task<CustomerAddressBook> ActAdminAddressAsync(long owner, long id, string action, AdminAddressAction input) => SendAsync<CustomerAddressBook>(HttpMethod.Post, $"/api/admin/customers/{owner}/addresses/{id}/{action}", input);
    /// <summary>Tra receipt sau khi mất phản hồi.</summary>
    public Task<CustomerAddressBook> AdminAddressReceiptAsync(Guid id) => SendAsync<CustomerAddressBook>(HttpMethod.Get, $"/api/admin/customers/address-operations/{id}");
}
