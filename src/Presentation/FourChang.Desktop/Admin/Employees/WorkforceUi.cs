using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.ApiClients;

namespace FourChang.Desktop.Admin.Employees;

/// <summary>Tiện ích UI cho hai UC hồ sơ/ca: lỗi, khôi phục receipt và hộp thoại; nghiệp vụ ở API.</summary>
internal static class WorkforceUi
{
    /// <summary>Chỉ tra receipt khi kết quả có thể đã commit; lỗi dữ liệu/quyền được trả nguyên để người dùng sửa.</summary>
    internal static async Task<T> Recover<T>(Func<Task<T>> send, Func<Task<T>> receipt)
    {
        try { return await send(); }
        catch (CafeApiException error) when (error.StatusCode is null || (int)error.StatusCode >= 500)
        {
            try { return await receipt(); }
            catch (CafeApiException) { throw new CafeApiException("Chưa xác nhận được kết quả. Giữ nguyên dữ liệu và mã thao tác; kết nối lại rồi bấm thử lại."); }
        }
    }

    /// <summary>Thông báo lỗi và đưa phiên hết hạn về xác thực; dữ liệu nháp không chứa mật khẩu.</summary>
    internal static void Error(Control owner, CafeApiException error)
    {
        MessageBox.Show(owner.FindForm(), error.Message, "Không thể hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        if (error.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            var form = owner.FindForm();
            while (form is not null && form is not MainForm) form = form.Owner;
            (form as MainForm)?.RequestReauthentication();
        }
    }

    /// <summary>Hộp thoại theo cùng bảng màu Admin; nội dung cuộn khi cửa sổ nhỏ.</summary>
    internal static Form Dialog(string title, int width = 580, int height = 530) => new()
    {
        Text = title,
        ClientSize = new Size(width, height),
        MinimumSize = new Size(480, 400),
        StartPosition = FormStartPosition.CenterParent,
        BackColor = CafeTheme.Cream,
        Font = CafeTheme.Body(),
        AutoScroll = true
    };

    /// <summary>Đổi nhãn trạng thái cho người dùng mà giữ nguyên mã API.</summary>
    internal static string Status(string value) => value switch
    {
        "ACTIVE" => "Đang làm",
        "INACTIVE" => "Ngừng làm",
        "OPEN" => "Đang mở",
        "LOCKED" => "Đang khóa",
        "CLOSED" => "Đã chốt",
        "HANDED_OVER" => "Đã bàn giao",
        _ => value
    };
}
