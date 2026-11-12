using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.ApiClients;

namespace FourChang.Desktop.Shared.Controls.Admin;

/// <summary>Hộp thoại và phục hồi kết quả cho các trang Admin; không chứa nghiệp vụ tính giá/kho.</summary>
internal static class AdminUi
{
    /// <summary>Tra receipt sau lỗi kết nối/5xx, giữ nguyên mã lệnh để không ghi hai lần.</summary>
    internal static async Task<T> Recover<T>(Func<Task<T>> send, Func<Task<T>> receipt)
    {
        try { return await send(); }
        catch (CafeApiException error) when (error.StatusCode is null || (int)error.StatusCode >= 500)
        {
            try { return await receipt(); }
            catch (CafeApiException) { throw new CafeApiException("Chưa rõ kết quả lưu. Giữ dữ liệu và mã thao tác; kết nối lại rồi thử lại."); }
        }
    }

    /// <summary>Hiển thị lỗi và yêu cầu đăng nhập lại khi phiên Admin không còn hợp lệ.</summary>
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

    /// <summary>Giữ nút lưu/hủy ngoài vùng cuộn để form dài hoặc cửa sổ nhỏ vẫn luôn thao tác được.</summary>
    internal static void Compose(Form form, Control body, params Control[] buttons)
    {
        form.AutoScroll = false;
        var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        viewport.Controls.Add(body);
        var footer = Ui.Row(buttons);
        footer.Dock = DockStyle.Bottom;
        footer.Padding = new Padding(20, 8, 20, 0);
        // Dock xử lý từ control cuối lên: dành chỗ footer trước rồi mới cho viewport lấp phần còn lại.
        form.Controls.Add(viewport);
        form.Controls.Add(footer);
    }

    /// <summary>Tạo khung dialog theo màu nâu kem đang có và hỗ trợ thay đổi kích thước.</summary>
    internal static Form Dialog(string title, int width = 760, int height = 650) => new()
    {
        AutoScaleMode = AutoScaleMode.Dpi,
        Text = title,
        ClientSize = new Size(width, height),
        MinimumSize = new Size(580, 480),
        StartPosition = FormStartPosition.CenterParent,
        BackColor = CafeTheme.Cream,
        Font = CafeTheme.Body(),
        AutoScroll = true
    };
}
