using System.Globalization;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Common;
using FourChang.Presentation.Shared.Contracts.Customer;

namespace FourChang.Desktop.Admin.Dashboard;

/// <summary>Dashboard chỉ trình bày số liệu thật từ API; không tạo biểu đồ hoặc xu hướng giả.</summary>
public static class DashboardPage
{
    /// <summary>Giữ bốn KPI và các vùng doanh thu/thực đơn/kho/nhật ký theo bố cục đang có.</summary>
    public static Control Create() => new DashboardWorkspace();

    /// <summary>Quản lý trạng thái tải và vùng nội dung; refresh thất bại vẫn giữ dữ liệu đã tải có nhãn thời điểm.</summary>
    private sealed class DashboardWorkspace : UserControl
    {
        private readonly Label sync = Ui.Label("Đang lấy số liệu hoạt động…", 9, false, CafeTheme.Muted);
        private readonly Button reload = Ui.Button("Làm mới", () => { });
        private readonly Panel content = new() { Dock = DockStyle.Top, AutoSize = true, Margin = Padding.Empty };
        private bool loading;
        private bool started;
        private DateTime? loadedAt;

        /// <summary>Tạo thanh cập nhật và vùng nội dung co giãn; không đổi sidebar hay header của MainForm.</summary>
        internal DashboardWorkspace()
        {
            Name = "DashboardWorkspace"; Dock = DockStyle.Top; AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink; BackColor = CafeTheme.Cream; Padding = new Padding(16);
            var toolbar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 54, ColumnCount = 2, Margin = Padding.Empty };
            toolbar.ColumnStyles.Add(new(SizeType.Percent, 100)); toolbar.ColumnStyles.Add(new(SizeType.Absolute, 128));
            sync.Dock = DockStyle.Fill; sync.AutoSize = false; sync.TextAlign = ContentAlignment.MiddleLeft;
            reload.Dock = DockStyle.Top; reload.Height = 44; reload.Name = "DashboardReload";
            toolbar.Controls.Add(sync, 0, 0); toolbar.Controls.Add(reload, 1, 0);
            Controls.Add(content); Controls.Add(toolbar);
            reload.Click += async (_, _) => await LoadSnapshot();
            HandleCreated += async (_, _) => { if (started) return; started = true; await LoadSnapshot(); };
        }

        /// <summary>Chỉ thay nội dung khi cả bốn nguồn đọc thành công, tránh trộn số mới và dữ liệu cũ trên cùng dashboard.</summary>
        private async Task LoadSnapshot()
        {
            if (loading || IsDisposed) return;
            loading = true; reload.Enabled = false; sync.Text = "Đang cập nhật số liệu…";
            try
            {
                var totals = LiveSession.Api.DashboardAsync();
                var menu = LiveSession.Api.GetMenuAsync();
                var stock = LiveSession.Api.GetInventoryAsync();
                var audit = LiveSession.Api.ReadTableAsync("NhatKyHeThong");
                await Task.WhenAll(totals, menu, stock, audit);
                if (IsDisposed) return;
                Render(await totals, await menu, await stock, await audit);
                loadedAt = DateTime.Now;
                sync.Text = "Toàn bộ dữ liệu hiện tại · Cập nhật " + loadedAt.Value.ToString("HH:mm:ss");
                sync.ForeColor = CafeTheme.Muted;
            }
            catch (CafeApiException error)
            {
                if (IsDisposed) return;
                sync.Text = error.Message + (loadedAt is null ? " Chưa tải được số liệu." : " · Đang giữ dữ liệu lúc " + loadedAt.Value.ToString("HH:mm:ss"));
                sync.ForeColor = CafeTheme.Danger;
            }
            finally { loading = false; if (!reload.IsDisposed) reload.Enabled = true; }
        }

        /// <summary>Ánh xạ DTO vào control; công thức doanh thu và số đơn vẫn do API hiện hữu cung cấp.</summary>
        private void Render(DashboardData data, List<MenuProduct> menu, StockSnapshot stock, TableData audit)
        {
            var summary = Ui.Kpis(("Thực thu sau hoàn", MoneyDisplay.Format(data.Revenue), "Thu đã xác minh trừ chi hoàn"),
                ("Đơn hoàn tất", data.CompletedOrders.ToString(), "Đã phục vụ / giao thành công"),
                ("Chờ hoàn tiền", data.PendingRefunds.ToString(), "Hồ sơ chưa hoàn tất"),
                ("Nguyên liệu cảnh báo", data.LowIngredients.ToString(), "Theo tồn kho và lượng giữ"));
            var top = Ui.Columns(Card("Tổng quan doanh thu", "Số liệu thực thu hiện tại", Revenue(data), 278),
                Card("Thực đơn đang bán", "Giá và trạng thái từ thực đơn chung", Products(menu), 278), 36);
            var lower = Ui.Columns(Card("Nguyên liệu và tồn kho", "Khả dụng sau giữ chỗ và tồn an toàn", StockGrid(stock), 300),
                Card("Nhật ký gần đây", "Các thao tác mới nhất đã được ghi nhận", AuditGrid(audit), 300), 50);
            var body = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = Padding.Empty };
            body.ColumnStyles.Add(new(SizeType.Percent, 100));
            foreach (var section in new Control[] { summary, top, lower })
            { section.Dock = DockStyle.Top; section.Margin = new Padding(0, 0, 0, 16); body.Controls.Add(section); }
            content.SuspendLayout();
            var old = content.Controls.Cast<Control>().ToArray();
            content.Controls.Clear(); content.Controls.Add(body);
            foreach (var control in old) control.Dispose();
            content.ResumeLayout(true);
        }

        /// <summary>Thẻ cùng chiều cao cho hai cột; tiêu đề/phụ đề tách khỏi bảng để không bị đè khi resize.</summary>
        private static Control Card(string title, string subtitle, Control body, int height)
        {
            var panel = new SurfacePanel { Dock = DockStyle.Top, Height = height, AutoSize = false, Padding = new Padding(20), Margin = Padding.Empty };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            layout.ColumnStyles.Add(new(SizeType.Percent, 100));
            layout.RowStyles.Add(new(SizeType.Absolute, 30)); layout.RowStyles.Add(new(SizeType.Absolute, 30)); layout.RowStyles.Add(new(SizeType.Percent, 100));
            var heading = Ui.Label(title, 12, true); heading.Dock = DockStyle.Fill; heading.AutoSize = false; heading.Margin = Padding.Empty; heading.TextAlign = ContentAlignment.MiddleLeft;
            var detail = Ui.Label(subtitle, 9, false, CafeTheme.Muted); detail.Dock = DockStyle.Fill; detail.AutoSize = false; detail.Margin = Padding.Empty; detail.TextAlign = ContentAlignment.MiddleLeft;
            body.Dock = DockStyle.Fill; body.Margin = Padding.Empty;
            layout.Controls.Add(heading, 0, 0); layout.Controls.Add(detail, 0, 1); layout.Controls.Add(body, 0, 2);
            panel.Controls.Add(layout); return panel;
        }

        /// <summary>Hiển thị tổng tiền rõ ràng thay các đường biểu đồ không có dữ liệu thời gian.</summary>
        private static Control Revenue(DashboardData data)
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = CafeTheme.Soft, Padding = new Padding(14) };
            var amount = Ui.Label(MoneyDisplay.Format(data.Revenue), 30, true, CafeTheme.Coffee);
            amount.Dock = DockStyle.Top; amount.AutoSize = false; amount.Height = 56; amount.Name = "DashboardRevenue";
            var description = Ui.Label("Thu đã xác minh, sau khi trừ các lần hoàn tiền thành công.", 10, false, CafeTheme.Muted);
            description.Dock = DockStyle.Top; description.AutoSize = false; description.Height = 40;
            var action = Ui.Button("Xem hóa đơn", () => (panel.FindForm() as MainForm)?.Navigate("invoices"));
            action.Dock = DockStyle.Bottom;
            panel.Controls.Add(description); panel.Controls.Add(amount); panel.Controls.Add(action); return panel;
        }

        /// <summary>Dùng ảnh, tên, giá và khả dụng từ catalog; không khai báo sản phẩm hay giá mẫu tại Dashboard.</summary>
        private static Control Products(List<MenuProduct> menu)
        {
            var items = menu.Where(x => x.Available).Take(3).ToList();
            if (items.Count == 0) return Ui.EmptyState("Chưa có món có thể đặt", "Kiểm tra thực đơn và nguyên liệu.", 170);
            var rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = items.Count, Margin = Padding.Empty };
            rows.ColumnStyles.Add(new(SizeType.Absolute, 68)); rows.ColumnStyles.Add(new(SizeType.Percent, 100));
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index]; rows.RowStyles.Add(new(SizeType.Percent, 100f / items.Count));
                var image = new ProductImage { ImageIndex = item.ImageIndex, Size = new Size(52, 52), Margin = Padding.Empty };
                var text = Ui.Stack(Ui.Label(item.Name, 10, true), Ui.Label(MoneyDisplay.Format(item.Price), 9, true, CafeTheme.Coffee));
                text.Dock = DockStyle.Fill; rows.Controls.Add(image, 0, index); rows.Controls.Add(text, 1, index);
            }
            return rows;
        }

        /// <summary>Tạo bảng vừa vùng chứa, tooltip giữ nội dung dài; mã đơn vị kỹ thuật không được dùng thay ký hiệu đơn vị.</summary>
        private static DataGridView Grid(params (string Property, string Title, float Weight)[] columns)
        {
            var grid = Ui.Grid(new([], []), 200); grid.AutoGenerateColumns = false; grid.Dock = DockStyle.Fill;
            grid.ColumnHeadersHeight = 38; grid.RowTemplate.Height = 42; grid.Font = CafeTheme.Body(9);
            foreach (var column in columns)
                grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = column.Property, HeaderText = column.Title, FillWeight = column.Weight, SortMode = DataGridViewColumnSortMode.NotSortable });
            return grid;
        }

        /// <summary>Tồn theo DTO ADM-05, ưu tiên cảnh báo; không tính lại hoặc ghi số dư tại Dashboard.</summary>
        private static Control StockGrid(StockSnapshot snapshot)
        {
            var grid = Grid(("Ten", "Nguyên liệu", 150), ("DonVi", "Đơn vị", 75), ("KhaDung", "Khả dụng", 90), ("TrangThai", "Trạng thái", 100));
            grid.Name = "DashboardStock";
            grid.DataSource = snapshot.NguyenLieu.OrderBy(x => x.CanhBao switch { "OUT" => 0, "LOW" => 1, "UNKNOWN" => 2, _ => 3 }).ThenBy(x => x.MaNguyenLieu)
                .Select(x => new
                {
                    Ten = x.TenNguyenLieu,
                    x.DonVi,
                    KhaDung = x.KhaDung?.ToString("#,##0.####", CultureInfo.GetCultureInfo("vi-VN")) ?? "Chưa xác định",
                    TrangThai = x.CanhBao switch { "OUT" => "Hết", "LOW" => "Sắp hết", "UNKNOWN" => "Chưa xác định", _ => "Còn" }
                }).ToList();
            return grid;
        }

        /// <summary>Lấy cột theo tên contract thay vì vị trí, tránh nhầm người thực hiện khi server bổ sung cột.</summary>
        private static string Cell(TableData data, string[] row, string key)
        {
            var index = Array.IndexOf(data.Columns, key);
            return index >= 0 && index < row.Length ? row[index] : "";
        }

        /// <summary>Hiển thị nhật ký mới nhất trong tập API trả về, không đưa JSON nhạy cảm vào tooltip hay bảng.</summary>
        private static Control AuditGrid(TableData data)
        {
            var grid = Grid(("ThoiDiem", "Thời điểm", 105), ("Nguoi", "Người thực hiện", 115), ("ThaoTac", "Thao tác", 150));
            grid.Name = "DashboardAudit";
            grid.DataSource = data.Rows.OrderByDescending(row => long.TryParse(Cell(data, row, "MaNhatKy"), out var id) ? id : 0).Take(12)
                .Select(row => new { ThoiDiem = Cell(data, row, "PhatSinhLuc"), Nguoi = Cell(data, row, "TenNguoiThucHienDaChot"), ThaoTac = Cell(data, row, "HanhDong") }).ToList();
            return grid;
        }
    }
}
