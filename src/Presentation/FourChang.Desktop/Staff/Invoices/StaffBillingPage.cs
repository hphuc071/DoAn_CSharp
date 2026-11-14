using FourChang.Desktop.Shared.Controls;

namespace FourChang.Desktop.Staff.Invoices;

/// <summary>Giữ hóa đơn hiện có và đặt chi hoàn tại quầy trong tab thu ngân riêng.</summary>
public sealed class StaffBillingPage : UserControl
{
    private readonly TabControl tabs;
    private Form? host;
    /// <summary>Tái sử dụng trang hóa đơn/hoàn, không tạo logic thanh toán mới hoặc bỏ chức năng đã có.</summary>
    public StaffBillingPage()
    {
        Dock = DockStyle.Top; Height = 830;
        tabs = Ui.Tabs(("Hóa đơn", new StaffInvoicesPage()), ("Hoàn tiền tại quầy", new StaffCounterRefundPage()));
        tabs.Name = "StaffBillingTabs"; tabs.Dock = DockStyle.Fill; Controls.Add(tabs);
        HandleCreated += (_, _) => { host = FindForm(); if (host is not null) host.Resize += Fit; Fit(this, EventArgs.Empty); };
    }
    /// <summary>Tab tận dụng chiều cao cửa sổ, cho phép cuộn nội dung khi thu nhỏ.</summary>
    private void Fit(object? sender, EventArgs e) { if (host is not null) Height = Math.Max(700, host.ClientSize.Height - 110); }
    /// <summary>Gỡ sự kiện của cửa sổ khi chuyển trang để không giữ lại control hoặc timer cũ.</summary>
    protected override void Dispose(bool disposing) { if (disposing && host is not null) host.Resize -= Fit; base.Dispose(disposing); }
}
