using System.Data;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Common;
using FourChang.Presentation.Shared.Preview;

namespace FourChang.Desktop.Shared.Database;
public static class DatabaseViews
{
    private static readonly Dictionary<string, string[]> AdminTables = new()
    {
        ["customers"] = ["TaiKhoanKhachHang", "DiaChiKhachHang"],
        ["access"] = ["TaiKhoanQuanLy", "CaLamViec"],
        ["menu"] = ["Mon", "CauHinhMon", "DanhMucMon", "MonThem", "CauHinhMonThem", "TuyChonPhaCheHopLe"],
        ["ingredients"] = ["NguyenLieu", "DonViTinh", "QuyDoiDonViNguyenLieu"],
        ["recipes"] = ["CongThuc", "ChiTietCongThuc"],
        ["inventory"] = ["TonKho", "GiuChoNguyenLieu", "BienDongKho"],
        ["stock"] = ["PhieuKho", "ChiTietPhieuKho"],
        ["manage-orders"] = ["DonHang", "ChiTietDonHang", "MonThemChiTietDonHang", "NguyenLieuChiTietDonHang", "HuyMon", "Ban"],
        ["invoices"] = ["HoaDon", "ThanhToan", "GiaoDichNganHang"],
        ["refunds"] = ["HoSoHoanTien", "LanChiHoanTien"],
        ["reports"] = ["NhatKyHeThong", "ThanhToan", "LanChiHoanTien"]
    };
    public static Control Create(string role, string route)
    {
        if (role == "Staff")
            return route switch
            {
                "shift" => FourChang.Desktop.Staff.WorkSessions.ShiftPage.Create(),
                "sale" => FourChang.Desktop.Staff.Sales.SalePage.Create(),
                "orders" => Table("DonHang", false),
                "cancel" => Ui.Panel("Hủy món", Ui.Label("Chọn đơn để tra cứu. Thao tác hủy sẽ được triển khai cùng luồng xử lý đơn."), Table("DonHang", false)),
                "payment" => Table("HoaDon", false),
                _ => Ui.Label("Chưa có màn hình")
            };
        // UC-ADM-01 dùng màn hình nghiệp vụ riêng, thay bảng đọc thô ở chế độ chạy thật.
        if (route == "menu")
            return new FourChang.Desktop.Admin.Menu.MenuManagementPage();
        if (route == "customers")
            return new FourChang.Desktop.Admin.Customers.Views.CustomersPage();
        if (route == "access")
            return new FourChang.Desktop.Admin.Access.Views.AccessPage();
        if (route == "work-sessions")
            return new FourChang.Desktop.Admin.WorkSessions.WorkSessionsPage();
        if (route == "employees")
            return FourChang.Desktop.Admin.Employees.EmployeesPage.Create();
        if (route == "dashboard")
            return FourChang.Desktop.Admin.Dashboard.DashboardPage.Create();
        if (!AdminTables.TryGetValue(route, out var tables))
            return Ui.Label("Chưa có màn hình");
        return Ui.Tabs(tables.Select(name => (DatabaseLabels.Table(name), Table(name, true))).ToArray());
    }

    internal static Control Table(string name, bool admin)
    {
        var grid = Ui.Grid(new PreviewTable([], []), 440);
        grid.Name = "ReadOnlyGrid_" + name;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        var error = Ui.Label("", 9, false, CafeTheme.Danger);
        error.Visible = false;
        var reload = Ui.Button("Tải lại", () =>
        {
        });
        var count = Ui.Label("Chưa tải dữ liệu", 9, false, CafeTheme.Muted);
        count.Name = "LoadedRowCount_" + name;
        var loading = Ui.Label("Đang tải dữ liệu…", 10, false, CafeTheme.Muted); loading.Name = "TableLoading_" + name;
        var progress = new FourChang.Desktop.Shared.Controls.Admin.AdminLoadingStrip();
        var box = Ui.Panel(DatabaseLabels.Table(name), Ui.Row(reload), error, loading, progress, grid, count);
        async Task Load()
        {
            if (box.IsDisposed)
                return;
            reload.Enabled = false;
            error.Visible = false;
            loading.Visible = true; progress.Running = CafeVisuals.MotionEnabled;
            if (grid.Rows.Count == 0) grid.Visible = false;
            try
            {
                var data = admin ? await LiveSession.Api.ReadTableAsync(name) : await LiveSession.Api.ReadPosTableAsync(name);
                if (!grid.IsDisposed)
                {
                    SetGrid(grid, data);
                    count.Text = $"{data.Rows.Length:N0} dòng đã tải · {DateTime.Now:HH:mm:ss}";
                    CafeVisuals.Apply(grid);
                }
            }
            catch (CafeApiException exception)
            {
                if (!error.IsDisposed)
                {
                    error.Text = exception.Message;
                    error.Visible = true;
                    count.Text = "Chưa xác nhận được dữ liệu mới";
                }
            }
            finally
            {
                if (!reload.IsDisposed)
                {
                    reload.Enabled = true; loading.Visible = false; progress.Running = false; grid.Visible = true;
                }
            }
        }

        var loaded = false;
        box.HandleCreated += async (_, _) =>
        {
            if (loaded)
                return;
            loaded = true;
            await Load();
        };
        reload.Click += async (_, _) => await Load();
        return box;
    }

    internal static void SetGrid(DataGridView grid, TableData data)
    {
        var table = new DataTable();
        foreach (var column in data.Columns)
            table.Columns.Add(DatabaseLabels.Column(column));
        foreach (var row in data.Rows)
            table.Rows.Add(row.Cast<object>().ToArray());
        grid.DataSource = table;
    }
}
