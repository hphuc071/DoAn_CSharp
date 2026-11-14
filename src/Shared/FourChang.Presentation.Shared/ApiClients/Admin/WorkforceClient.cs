using System.Globalization;
using System.Text.Json;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Staff;

namespace FourChang.Presentation.Shared.ApiClients;

/// <summary>Hợp đồng tích hợp ADM-11/12 dùng chung; UI không tự truy cập SQL hoặc tự tính số liệu đã chốt.</summary>
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

    /// <summary>Xóa có điều kiện, không gửi DELETE thô từ giao diện tới SQL.</summary>
    public Task<JsonElement> DeleteEmployeeAsync(long id, DeleteEmployeeInput input) => SendAsync<JsonElement>(HttpMethod.Post, $"/api/admin/employees/{id}/delete", input);
    /// <summary>Receipt chung cho cả xóa hồ sơ, không ép kết quả xóa thành hồ sơ Employee.</summary>
    public Task<JsonElement> EmployeeOperationReceiptAsync(Guid id) => SendAsync<JsonElement>(HttpMethod.Get, $"/api/admin/employees/operations/{id}");
    /// <summary>Đọc đúng kiểu kết quả của lệnh ca đã commit khi mất phản hồi.</summary>
    public Task<T> ReadWorkSessionReceiptAsync<T>(Guid id) => SendAsync<T>(HttpMethod.Get, $"/api/admin/work-sessions/operations/{id}");
    /// <summary>Chỉ trả mã phục hồi ở lần cấp đầu; receipt không chứa mã bí mật.</summary>
    public Task<ShiftRecoveryReceipt> IssueShiftRecoveryAsync(long id, IssueShiftRecoveryInput input) => SendAsync<ShiftRecoveryReceipt>(HttpMethod.Post, $"/api/admin/work-sessions/{id}/recovery", input);
    /// <summary>Nhân viên đổi mã một lần thành phiên chỉ gửi đối chiếu, không phải đăng nhập tài khoản.</summary>
    public Task<FourChang.Presentation.Shared.Contracts.Common.SessionResult> RedeemShiftRecoveryAsync(RedeemShiftRecoveryInput input) => SendAsync<FourChang.Presentation.Shared.Contracts.Common.SessionResult>(HttpMethod.Post, "/api/pos/shifts/recovery", input);
    /// <summary>Xác minh token đã lưu với API trước khi tiếp tục sau khởi động lại.</summary>
    public Task<ShiftSessionContext> CurrentShiftSessionAsync() => SendAsync<ShiftSessionContext>(HttpMethod.Get, "/api/pos/shifts/current/session");
}
