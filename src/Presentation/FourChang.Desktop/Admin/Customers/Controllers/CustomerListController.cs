using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
namespace FourChang.Desktop.Admin.Customers.Controllers;

/// <summary>Điều phối tìm kiếm, phân trang và đọc hồ sơ cho UC-ADM-01; nghiệp vụ vẫn được API kiểm tra.</summary>
internal sealed class CustomerListController(CafeApiClient api)
{
    public int Page { get; private set; } = 1;
    public int Total { get; private set; }
    public string SearchTerm { get; private set; } = "";

    /// <summary>Đọc từ khóa hiện tại và về trang đầu để tải lại không giữ bộ lọc đã bị xóa.</summary>
    public void ApplySearch(string text)
    {
        SearchTerm = text.Trim();
        Page = 1;
    }

    /// <summary>Chuyển về trang trước; nút trên View chịu trách nhiệm bật/tắt theo trang hiện tại.</summary>
    public void PreviousPage() => Page--;

    /// <summary>Chuyển tới trang kế tiếp khi View xác nhận vẫn còn dữ liệu.</summary>
    public void NextPage() => Page++;

    /// <summary>Tải danh sách thật; nếu trang cuối đã hết dữ liệu thì quay về trang hợp lệ như hành vi cũ.</summary>
    public async Task<CustomerPage> LoadPageAsync()
    {
        var result = await api.GetCustomersAsync(SearchTerm, Page);
        if (result.Items.Count == 0 && result.Total > 0 && Page > 1)
        {
            Page = Math.Max(1, (result.Total + 19) / 20);
            result = await api.GetCustomersAsync(SearchTerm, Page);
        }
        Total = result.Total;
        return result;
    }

    /// <summary>Lấy phiên bản hồ sơ mới nhất trước khi mở form xem hoặc cập nhật để tránh dùng dữ liệu cũ.</summary>
    public Task<CustomerDetails> GetDetailsAsync(long id) => api.GetCustomerAsync(id);
}
