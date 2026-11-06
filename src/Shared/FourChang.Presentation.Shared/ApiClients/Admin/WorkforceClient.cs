using System.Globalization;
using System.Text.Json;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Staff;

namespace FourChang.Presentation.Shared.ApiClients;

/// <summary>Hợp đồng tích hợp ADM-12/13 dùng chung; UI không tự truy cập SQL hoặc tự tính số liệu đã chốt.</summary>
public sealed partial class CafeApiClient
{
    /// <summary>Tìm và phân trang hồ sơ; trạng thái rỗng nghĩa là tất cả.</summary>
    public Task<EmployeePage> SearchEmployeesAsync(string search, string status, int page) => SendAsync<EmployeePage>(HttpMethod.Get,
        $"/api/admin/employees/search?search={Uri.EscapeDataString(search)}&status={status}&page={page}&pageSize=20");
    /// <summary>Lấy bản hồ sơ hiện tại trước khi sửa.</summary>
    public Task<Employee> GetEmployeeAsync(long id) => SendAsync<Employee>(HttpMethod.Get, $"/api/admin/employees/{id}");
    /// <summary>Đổi trạng thái có lý do và mã thao tác được giữ khi thử lại.</summary>
    public Task<Employee> SetEmployeeStatusAsync(long id, EmployeeStatusInput input) => SendAsync<Employee>(HttpMethod.Post, $"/api/admin/employees/{id}/status", input);
    /// <summary>Tra cứu receipt để phân biệt timeout với chưa lưu.</summary>
    public Task<Employee> EmployeeReceiptAsync(Guid id) => SendAsync<Employee>(HttpMethod.Get, $"/api/admin/employees/operations/{id}");
    /// <summary>Gửi các bộ lọc ca; ngày được đổi UTC trước khi gửi.</summary>
    public Task<WorkSessionPage> GetWorkSessionsAsync(long? employee, DateTime? from, DateTime? to, string name, string status, string machine, int page)
    {
        var args = new List<string> { $"page={page}", "pageSize=20", $"name={Uri.EscapeDataString(name)}", $"status={status}", $"machine={Uri.EscapeDataString(machine)}" };
        if (employee is not null) args.Add($"employee={employee}");
        if (from is not null) args.Add("from=" + Uri.EscapeDataString(from.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
        if (to is not null) args.Add("to=" + Uri.EscapeDataString(to.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
        return SendAsync<WorkSessionPage>(HttpMethod.Get, "/api/admin/work-sessions?" + string.Join("&", args));
    }
    /// <summary>Tải đối chiếu, giao dịch nguồn, audit và bàn giao từ máy chủ.</summary>
    public Task<WorkSessionDetails> GetWorkSessionAsync(long id) => SendAsync<WorkSessionDetails>(HttpMethod.Get, $"/api/admin/work-sessions/{id}");
    /// <summary>Cho tiếp tục ca LOCKED có lý do; ca đã kết thúc không được mở lại.</summary>
    public Task<WorkSessionInfo> ResumeWorkSessionAsync(long id, ResumeWorkSessionInput input) => SendAsync<WorkSessionInfo>(HttpMethod.Post, $"/api/admin/work-sessions/{id}/resume", input);
    /// <summary>Admin xác nhận từ số đếm POS đã gửi.</summary>
    public Task<WorkSessionInfo> ConfirmWorkSessionAsync(long id, CloseWorkSessionInput input) => SendAsync<WorkSessionInfo>(HttpMethod.Post, $"/api/admin/work-sessions/{id}/close", input);
    /// <summary>Bàn giao đúng tập đơn/khoản chưa rõ đã hiển thị.</summary>
    public Task<HandoverInfo> HandoverWorkSessionAsync(long id, HandoverWorkSessionInput input) => SendAsync<HandoverInfo>(HttpMethod.Post, $"/api/admin/work-sessions/{id}/handover", input);
    /// <summary>Receipt cho chốt/bàn giao; trả JSON vì hai thao tác có kết quả khác nhau.</summary>
    public Task<JsonElement> WorkSessionReceiptAsync(Guid id) => SendAsync<JsonElement>(HttpMethod.Get, $"/api/admin/work-sessions/operations/{id}");
    /// <summary>POS đọc đối chiếu ca mình, kể cả khi LOCKED chờ xác nhận.</summary>
    public Task<WorkSessionDetails> CurrentReconciliationAsync() => SendAsync<WorkSessionDetails>(HttpMethod.Get, "/api/pos/shifts/current/reconciliation");
    /// <summary>POS gửi số đếm và khóa ca; không cấp quyền chốt Admin.</summary>
    public Task<JsonElement> SubmitReconciliationAsync(SubmitReconciliation input) => SendAsync<JsonElement>(HttpMethod.Post, "/api/pos/shifts/current/reconciliation", input);
    /// <summary>POS tra kết quả gửi khi mất phản hồi sau commit.</summary>
    public Task<JsonElement> ReconciliationReceiptAsync(Guid id) => SendAsync<JsonElement>(HttpMethod.Get, $"/api/pos/shifts/current/reconciliation/operations/{id}");
}
