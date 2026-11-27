// UC: UC-ADM-07
// Chức năng: Hiển thị số liệu tổng quan và lối đi đến các màn hình quản lý.
// Cần nhớ: Đọc hàm từ bước kiểm tra dữ liệu đến bước trả kết quả; các quyết định nghiệp vụ do Backend kiểm tra.

using System.Globalization;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Common;

namespace FourChang.Desktop.Admin.Dashboard;

/// <summary>Tổng quan chỉ đọc: cùng số liệu đơn hoàn thành với Báo cáo, không dùng tiền thu làm doanh thu.</summary>
public static class DashboardPage
{
    public static Control Create() => new DashboardWorkspace();

    private sealed class DashboardWorkspace : UserControl
    {
        private readonly Label status = Ui.Label("Đang tải tổng quan…", 9, false, CafeTheme.Muted);
        private readonly Button reload = Ui.Button("Tải lại", () => { });
        private readonly Panel content = new() { Dock = DockStyle.Top, AutoSize = true, Margin = Padding.Empty };
        private bool loading;
        private bool started;
        private bool hasSnapshot;
        private string? boundSnapshot;
        private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

        internal DashboardWorkspace()
        {
            Name = "DashboardWorkspace"; Dock = DockStyle.Top; AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink; BackColor = CafeTheme.Cream;
            Padding = new Padding(16); DoubleBuffered = true;
            var header = Ui.Stack(Ui.Label("4CHANG  /  TỔNG QUAN", 8, true, CafeTheme.Coffee),
                Ui.Label("Tổng quan hoạt động", 18, true),
                Ui.Label("Toàn bộ thời gian • Web và POS • Chỉ cộng tiền đơn đã hoàn thành", 9));
            reload.Name = "DashboardReload";
            var actions = Ui.Row(reload, Link("Đơn hàng", "manage-orders", "DashboardOrdersLink"),
                Link("Hoàn tiền", "refunds", "DashboardRefundsLink"), Link("Báo cáo", "reports", "DashboardReportsLink"));
            OperationsPresentation.FitRows(actions);
            status.Visible = true;
            status.TextChanged += (_, _) => status.Visible = !string.IsNullOrWhiteSpace(status.Text);
            Controls.Add(AdminListLayout.Column(header, actions, status, content));
            reload.Click += async (_, _) => await LoadSnapshot();
            HandleCreated += async (_, _) => { if (started) return; started = true; await LoadSnapshot(); };
        }

        /// <summary>Tái sử dụng báo cáo hiện hành để giá snapshot, topping, ly hủy và phí giao luôn khớp.</summary>
        private async Task LoadSnapshot()
        {
            if (loading || IsDisposed) return;
            loading = true; reload.Enabled = false; status.Text = "Đang tải tổng quan…";
            try
            {
                var reportTask = LiveSession.Api.GetReportAsync(new ReportFilter(
                    DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), DateTime.UtcNow));
                var totalsTask = LiveSession.Api.DashboardAsync();
                var stockTask = LiveSession.Api.GetInventoryAsync();
                await Task.WhenAll(reportTask, totalsTask, stockTask);
                if (IsDisposed) return;
                var report = await reportTask; var totals = await totalsTask; var stock = await stockTask;
                // Không đưa mã snapshot/thời điểm tải vào phép so sánh: tải lại cùng dữ liệu không dựng lại bảng.
                var signature = System.Text.Json.JsonSerializer.Serialize(new { report.ChiSo, report.Bang, totals.PendingRefunds, stock.NguyenLieu });
                if (signature != boundSnapshot)
                {
                    Render(report, totals, stock); boundSnapshot = signature;
                }
                hasSnapshot = true; status.Text = ""; status.ForeColor = CafeTheme.Muted;
            }
            catch (CafeApiException error)
            {
                if (IsDisposed) return;
                status.Text = error.Message + (hasSnapshot ? " Đang giữ dữ liệu đã tải trước đó." : " Chưa tải được tổng quan.");
                status.ForeColor = CafeTheme.Danger;
            }
            finally { loading = false; if (!reload.IsDisposed) reload.Enabled = true; }
        }

        private void Render(ReportSnapshot report, DashboardData totals, StockSnapshot stock)
        {
            var attention = stock.NguyenLieu.Count(x => x.CanhBao is "OUT" or "LOW" or "UNKNOWN");
            var summary = Ui.Kpis(("Tiền đơn hoàn tất", MoneyDisplay.Format(report.ChiSo.DoanhThuThuan), "Gồm topping và phí giao"),
                ("Đơn hoàn thành", report.ChiSo.HoanTat.ToString("N0", Vietnamese), "Chỉ đơn phục vụ xong"),
                ("Chờ hoàn tiền", totals.PendingRefunds.ToString("N0", Vietnamese), "Hồ sơ hoàn chưa kết thúc"),
                ("Cần kiểm tra kho", attention.ToString("N0", Vietnamese), "Hết / sắp hết / chưa có tồn"));
            summary.Name = "DashboardMetrics";
            // Tổng quan chỉ cần ba cột; chi tiết khách/món nằm ở trang Đơn hàng.
            var recent = new ReportTable("Đơn hoàn thành", [new("Mã đơn"), new("Tổng tiền", "number"), new("Hoàn thành lúc", "datetime")],
                report.Bang[0].Dong.Take(6).Select(x => (IReadOnlyList<string?>)new[] { x[0], x[2], x[3] }).ToList());
            var completed = Reports.ReportsManagementPage.Table(recent);
            completed.Name = "DashboardCompletedOrders";
            var stockGrid = StockGrid(stock);
            var tables = Ui.Columns(Card("Đơn hoàn thành gần đây", "6 đơn mới nhất • Tổng tiền theo đơn đã chốt", completed,
                    Link("Xem đơn hàng", "manage-orders", "DashboardCompletedLink")),
                Card("Theo dõi tồn kho", "Ưu tiên nguyên liệu cần kiểm tra", stockGrid,
                    Link("Xem tồn kho", "inventory", "DashboardInventoryLink")), 44);
            var definition = Ui.Label("Tiền đã thu của đơn chưa hoàn thành chưa được cộng. Tiền hoàn được theo dõi riêng tại Báo cáo và Hoàn tiền.", 9, false, CafeTheme.Muted);
            definition.MaximumSize = new Size(980, 0);
            var body = AdminListLayout.Column(summary, definition, tables);
            content.SuspendLayout();
            var old = content.Controls.Cast<Control>().ToArray();
            content.Controls.Clear(); content.Controls.Add(body);
            foreach (var control in old) control.Dispose();
            content.ResumeLayout(true);
        }

        /// <summary>Nút điều hướng chỉ mở trang liên quan, không ghi hay sửa dữ liệu.</summary>
        private static Button Link(string text, string route, string name)
        {
            var button = Ui.Button(text, () => { }); button.Name = name;
            button.Click += (_, _) => (button.FindForm() as MainForm)?.Navigate(route);
            return button;
        }

        private static Control Card(string title, string subtitle, DataGridView grid, Button link)
        {
            var panel = new AdminSurface { Dock = DockStyle.Top, Height = 480, Padding = new Padding(16), Margin = Padding.Empty };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
            layout.ColumnStyles.Add(new(SizeType.Percent, 100));
            layout.RowStyles.Add(new(SizeType.Absolute, 32)); layout.RowStyles.Add(new(SizeType.Absolute, 34));
            layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.Absolute, 52));
            var heading = Ui.Label(title, 12, true); heading.Dock = DockStyle.Fill;
            var detail = Ui.Label(subtitle, 9, false, CafeTheme.Muted); detail.Dock = DockStyle.Fill;
            grid.Dock = DockStyle.Fill; grid.Margin = Padding.Empty;
            layout.Controls.Add(heading, 0, 0); layout.Controls.Add(detail, 0, 1); layout.Controls.Add(grid, 0, 2);
            var actions = Ui.Row(link); actions.Dock = DockStyle.Fill; layout.Controls.Add(actions, 0, 3);
            panel.Controls.Add(layout); return panel;
        }

        /// <summary>Giữ đơn vị g/ml, số vi-VN và tồn chưa xác định; không giả tồn thiếu là 0.</summary>
        private static DataGridView StockGrid(StockSnapshot snapshot)
        {
            var grid = Ui.Grid(new([], []), 300); grid.AutoGenerateColumns = false; grid.Name = "DashboardStock";
            foreach (var column in new[] { ("Ten", "Nguyên liệu", 135f), ("DonVi", "Đơn vị", 50f), ("KhaDung", "Khả dụng", 90f), ("TrangThai", "Trạng thái", 110f) })
                grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = column.Item1, HeaderText = column.Item2, FillWeight = column.Item3, SortMode = DataGridViewColumnSortMode.NotSortable });
            CommerceWorkspaceVisuals.FormatGrid(grid);
            grid.RowTemplate.Height = 48; grid.ColumnHeadersHeight = 46; grid.ColumnHeadersDefaultCellStyle.Font = CafeTheme.Strong(8.5f);
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.MinimumWidth = column.DataPropertyName switch { "Ten" => 100, "DonVi" => 74, "KhaDung" => 100, _ => 90 };
                column.DefaultCellStyle.Padding = new Padding(6, 6, 6, 6);
                column.HeaderCell.Style.Padding = new Padding(3, 6, 3, 6);
                if (column.DataPropertyName == "KhaDung")
                { column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight; }
            }
            grid.DataSource = snapshot.NguyenLieu.OrderBy(x => x.CanhBao switch { "OUT" => 0, "LOW" => 1, "UNKNOWN" => 2, _ => 3 }).ThenBy(x => x.MaNguyenLieu).Take(6)
                .Select(x => new
                {
                    Ten = x.TenNguyenLieu, x.DonVi,
                    KhaDung = x.KhaDung?.ToString("#,##0.####", Vietnamese) ?? "Chưa có tồn",
                    TrangThai = x.CanhBao switch { "OUT" => "Hết", "LOW" => "Sắp hết", "UNKNOWN" => "Chưa có tồn", _ => "Còn" }
                }).ToList();
            return grid;
        }
    }
}
