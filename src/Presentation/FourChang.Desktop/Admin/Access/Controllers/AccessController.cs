using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
namespace FourChang.Desktop.Admin.Access.Controllers;

/// <summary>Điều phối các lời gọi API của trang quyền truy cập; không kiểm tra quyền thay cho backend.</summary>
internal sealed class AccessController(CafeApiClient api)
{
    /// <summary>Đọc hồ sơ tài khoản Quản lý duy nhất cho vùng thông tin tài khoản.</summary>
    public Task<AdminAccessProfile> LoadAccountAsync() => api.GetAdminAccountAsync();

    /// <summary>Đọc danh sách máy POS và trạng thái cấp/thu hồi để View cập nhật bảng.</summary>
    public Task<List<PosDeviceInfo>> LoadDevicesAsync() => api.GetPosDevicesAsync();

    /// <summary>Lấy rowversion mới nhất cho form đổi mật khẩu quản lý.</summary>
    public Task<AdminAccessProfile> LoadPasswordContextAsync() => api.GetAdminPasswordContextAsync();

    /// <summary>Thu hồi phiên Admin trên server trước khi View quay về màn hình đăng nhập.</summary>
    public Task<System.Text.Json.JsonElement> LogoutAsync() => api.LogoutAsync();

    /// <summary>Đọc ca liên quan của đúng thiết bị đã chọn để quản lý đối chiếu và chốt ca.</summary>
    public Task<List<DeviceShift>> LoadShiftsAsync(long deviceId) => api.GetDeviceShiftsAsync(deviceId);

    /// <summary>Tra biên nhận của thao tác chưa rõ kết quả trước khi thử lại để tránh ghi trùng.</summary>
    public Task<PosDeviceOperation?> FindOperationAsync(Guid operation) => api.GetDeviceOperationAsync(operation);

    /// <summary>Gửi lệnh chốt ca với cùng mã thao tác/rowversion đã chốt ở biểu mẫu.</summary>
    public Task<PosDeviceOperation> CloseShiftAsync(long deviceId, long shiftId, CloseDeviceShift input)
        => api.CloseDeviceShiftAsync(deviceId, shiftId, input);
}
