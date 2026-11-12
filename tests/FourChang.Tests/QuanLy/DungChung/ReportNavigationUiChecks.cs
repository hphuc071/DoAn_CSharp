using FourChang.Desktop.Shell;

namespace FourChang.Tests.Admin;

/// <summary>Kịch bản STA đi qua router thật; không gọi dịch vụ điều hướng giả hoặc viết dữ liệu nghiệp vụ.</summary>
internal static class ReportNavigationUiChecks
{
    /// <summary>Chọn lần lượt sáu tab, bấm nút thật rồi kiểm tra trang đích xuất hiện trong cùng cửa sổ.</summary>
    internal static async Task Run(MainForm shell, string output)
    {
        var targets = new[]
        {
            (Tab: "Món đã phục vụ", Caption: "Đến Thực đơn", Page: "MenuManagementPage"),
            (Tab: "Tỷ lệ đơn theo ngày tạo", Caption: "Đến Đơn hàng", Page: "OrdersManagementPage"),
            (Tab: "Nguyên liệu cảnh báo", Caption: "Đến Tồn kho", Page: "InventoryPage"),
            (Tab: "Nguồn thu trong kỳ", Caption: "Đến Đơn hàng", Page: "OrdersManagementPage"),
            (Tab: "Hoàn thành công trong kỳ", Caption: "Đến Hoàn tiền", Page: "RefundsManagementPage"),
            (Tab: "Hủy món trong kỳ", Caption: "Đến Đơn hàng", Page: "OrdersManagementPage")
        };
        for (var i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            if (i > 0) shell.Navigate("reports");
            await AdminFeaturesUiTests.WaitUntil(() => AdminFeaturesUiTests.Descendants(shell).OfType<TabControl>()
                .Any(t => t.Name == "ReportTables" && t.TabPages.Count == 6));
            var tabs = AdminFeaturesUiTests.Descendants(shell).OfType<TabControl>().Single(t => t.Name == "ReportTables");
            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().Single(t => t.Text == target.Tab);
            // Không chọn dòng: chứng minh nút vẫn dùng được ở bảng báo cáo trống hoặc không có selection.
            foreach (var grid in AdminFeaturesUiTests.Descendants(tabs).OfType<DataGridView>())
            {
                grid.ClearSelection();
                grid.CurrentCell = null;
            }
            var button = AdminFeaturesUiTests.Descendants(shell).OfType<Button>().Single(b => b.Name == "ReportRelatedPage");
            Assert.True(button.Enabled);
            Assert.Equal(target.Caption, button.Text);
            Assert.DoesNotContain(AdminFeaturesUiTests.Descendants(shell).OfType<Button>(), b => b.Text == "Mở nguồn đang chọn");
            button.PerformClick();
            await AdminFeaturesUiTests.WaitUntil(() => AdminFeaturesUiTests.Descendants(shell).Any(c => c.GetType().Name == target.Page));
            Assert.DoesNotContain(AdminFeaturesUiTests.Descendants(shell), c => c.GetType().Name == "ReportsManagementPage");
            Assert.Single(Application.OpenForms.Cast<Form>().OfType<MainForm>());
            if (i == 0) AdminFeaturesUiTests.Capture(shell, Path.Combine(output, "ReportNavigation-menu.png"));
        }
    }
}
