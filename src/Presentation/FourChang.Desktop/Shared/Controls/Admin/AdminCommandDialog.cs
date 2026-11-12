using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;

namespace FourChang.Desktop.Shared.Controls.Admin;

/// <summary>Giữ request chưa rõ kết quả trong trang cho lần mở lại, không lưu thông tin nhạy cảm ra ổ đĩa.</summary>
internal sealed class CommandDraft<T> where T : class
{
    internal T? Request;
    internal Guid Operation = Guid.NewGuid();
    /// <summary>Chỉ tạo mã mới sau thành công hoặc lỗi chắc chắn chưa commit.</summary>
    internal void Reset() { Request = null; Operation = Guid.NewGuid(); }

}

/// <summary>Dialog nhập lệnh có nhãn, footer cố định và giữ nháp khi mất mạng; không chứa quy tắc tiền/kho.</summary>
internal sealed class AdminCommandDialog<T> : Form where T : class
{
    private readonly FlowLayoutPanel fields = new() { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(20) };
    private readonly Label feedback = Ui.Label("", 9, false, CafeTheme.Danger);
    private readonly Button save;
    private readonly CommandDraft<T> draft;
    private bool sending;

    /// <summary>Dựng form theo phong cách chung, không tạo thêm project hoặc thay navigation.</summary>
    internal AdminCommandDialog(string title, CommandDraft<T> draft)
    {
        this.draft = draft; Text = title; AutoScaleMode = AutoScaleMode.Dpi; Font = CafeTheme.Body(); BackColor = CafeTheme.Cream;
        ClientSize = new(730, 700); MinimumSize = new(600, 450); StartPosition = FormStartPosition.CenterParent;
        save = Ui.Button("Xác nhận", () => { }, true); save.Name = "CommandConfirm";
        fields.Controls.Add(Ui.Label(title, 17, true)); feedback.MaximumSize = new(640, 0); fields.Controls.Add(feedback);
        AdminUi.Compose(this, fields, save, Ui.Button("Hủy", () => { if (!sending) Close(); }));
        FormClosing += (_, e) => { if (sending) e.Cancel = true; };
    }
    /// <summary>Thêm ô text có giới hạn rõ, giữ control thật để nhập tiếng Việt và dùng bàn phím.</summary>
    internal TextBox TextField(string label, string? value = null, int max = 500)
    {
        var input = Ui.Input(label, value ?? "", 620); input.MaxLength = max;
        fields.Controls.Add(Ui.Field(label, input)); return input;
    }
    /// <summary>Thêm số decimal chính xác, không dùng float hoặc parse text theo vùng.</summary>
    internal NumericUpDown NumberField(string label, decimal value = 0, int decimals = 0)
    {
        var input = new NumericUpDown { Width = 620, DecimalPlaces = decimals, Maximum = 999999999999m, Value = Math.Clamp(value, 0, 999999999999m), Font = CafeTheme.Body(11), ThousandsSeparator = true };
        fields.Controls.Add(Ui.Field(label, new AdminInputFrame(input) { Width = 640, Height = 48 })); return input;
    }
    /// <summary>Yêu cầu tích xác nhận riêng cho việc người dùng thực sự đã kiểm tra chứng cứ.</summary>
    internal CheckBox CheckField(string label)
    {
        var input = new CheckBox { Text = label, AutoSize = true, MaximumSize = new(620, 0), Margin = new Padding(0, 8, 0, 12), Font = CafeTheme.Body() };
        fields.Controls.Add(input); return input;
    }
    /// <summary>Danh sách lựa chọn chỉ gồm các giá trị nghiệp vụ được service chấp nhận.</summary>
    internal ComboBox ChoiceField(string label, params string[] values)
    {
        var input = Ui.Select(values); input.Width = 620; fields.Controls.Add(Ui.Field(label, input)); return input;
    }
    /// <summary>Gắn lệnh typed và receipt; timeout giữ đúng request/mã cũ, không đóng hoặc báo lưu thành công.</summary>
    internal void Submit(Func<Guid, T> build, Func<T, Task> send, Func<Guid, Task> receipt)
    {
        save.Click += async (_, _) =>
        {
            if (sending) return; sending = true; save.Enabled = false; feedback.Text = "Đang xử lý…";
            try
            {
                draft.Request ??= build(draft.Operation);
                await AdminUi.Recover(async () => { await send(draft.Request); return true; }, async () => { await receipt(draft.Operation); return true; });
                draft.Reset(); sending = false; DialogResult = DialogResult.OK; Close();
            }
            catch (CafeApiException ex)
            {
                feedback.Text = ex.Message;
                if (ex.StatusCode is not null && (int)ex.StatusCode < 500) draft.Reset();
            }
            catch (FormatException) { feedback.Text = "Định dạng thông tin chưa hợp lệ. Kiểm tra lại các trường."; draft.Reset(); }
            finally { sending = false; if (!save.IsDisposed) { save.Enabled = true; SetFieldsEnabled(draft.Request is null); } }
        };
        if (draft.Request is not null) { SetFieldsEnabled(false); feedback.Text = "Có lệnh chưa rõ kết quả. Xác nhận sẽ tra/gửi lại đúng nội dung cũ; không tạo lệnh mới."; }
    }

    /// <summary>Khóa các trường khi chưa biết lệnh cũ đã lưu chưa, tránh gửi nội dung khác với nội dung đang hiển thị.</summary>
    private void SetFieldsEnabled(bool enabled)
    {
        foreach (Control field in fields.Controls)
            if (field != feedback && field is not Label) field.Enabled = enabled;
    }
}
