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
    /// <summary>Chọn trang theo vai trò và route hiện có; năm UC Admin dùng trang nghiệp vụ riêng thay bảng thô.</summary>
    public static Control Create(string role, string route)
    {
        if (role == "Staff")
            return route switch
            {
                "shift" => FourChang.Desktop.Staff.WorkSessions.ShiftPage.Create(),
                "sale" => FourChang.Desktop.Staff.Sales.SalePage.Create(),
                "orders" => new FourChang.Desktop.Staff.Orders.StaffOrdersPage(),
                "cancel" => new FourChang.Desktop.Staff.Orders.StaffCancelledOrdersPage(),
                "payment" => new FourChang.Desktop.Staff.Invoices.StaffBillingPage(),
                _ => Ui.Label("Chưa có màn hình")
            };
        // UC-ADM-01 dùng màn hình nghiệp vụ riêng, thay bảng đọc thô ở chế độ chạy thật.
        if (route == "customers")
            return new FourChang.Desktop.Admin.Customers.Views.CustomersPage();
        if (route == "access")
            return new FourChang.Desktop.Admin.Access.Views.AccessPage();
        if (route == "employees")
            return FourChang.Desktop.Admin.Employees.EmployeesPage.Create();
        if (route == "work-sessions") return new FourChang.Desktop.Admin.WorkSessions.WorkSessionsPage();
        if (route == "dashboard")
            return FourChang.Desktop.Admin.Dashboard.DashboardPage.Create();
        if (route == "menu") return new FourChang.Desktop.Admin.Menu.MenuManagementPage();
        if (route == "ingredients") return new FourChang.Desktop.Admin.Ingredients.IngredientsPage();
        if (route == "recipes") return new FourChang.Desktop.Admin.Recipes.RecipesPage();
        if (route == "inventory") return new FourChang.Desktop.Admin.Inventory.InventoryPage();
        if (route == "stock") return new FourChang.Desktop.Admin.StockDocuments.StockDocumentsPage();
        if (route == "cancelled-orders") return new FourChang.Desktop.Admin.Orders.CancelledOrdersPage();
        if (route == "manage-orders") return new FourChang.Desktop.Admin.Orders.OrdersManagementPage();
        if (route == "invoices") return new FourChang.Desktop.Admin.Invoices.InvoicesManagementPage();
        if (route == "refunds") return new FourChang.Desktop.Admin.Refunds.RefundsManagementPage();
        if (route == "reports") return new FourChang.Desktop.Admin.Reports.ReportsManagementPage();
        if (!AdminTables.TryGetValue(route, out var tables))
            return Ui.Label("Chưa có màn hình");
        return Ui.Tabs(tables.Select(name => (DatabaseLabels.Table(name), Table(name, true))).ToArray());
    }

    /// <summary>Tạo bảng tra cứu chỉ đọc và nút tải lại; dữ liệu lấy từ API phù hợp vai trò.</summary>
    internal static Control Table(string name, bool admin)
    {
        var grid = Ui.Grid(new PreviewTable([], []), 440);
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        var error = Ui.Label("", 9, false, CafeTheme.Danger);
        error.Visible = false;
        var reload = Ui.Button("Tải lại", () =>
        {
        });
        var box = Ui.Panel(DatabaseLabels.Table(name), Ui.Row(reload), error, grid);
        async Task Load()
        {
            if (box.IsDisposed)
                return;
            reload.Enabled = false;
            error.Visible = false;
            try
            {
                var data = admin ? await LiveSession.Api.ReadTableAsync(name) : await LiveSession.Api.ReadPosTableAsync(name);
                if (!grid.IsDisposed)
                    SetGrid(grid, data);
            }
            catch (CafeApiException exception)
            {
                if (!error.IsDisposed)
                {
                    error.Text = exception.Message;
                    error.Visible = true;
                }
            }
            finally
            {
                if (!reload.IsDisposed)
                    reload.Enabled = true;
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

    /// <summary>Gắn cột/dữ liệu trả từ API vào grid; không thực thi lệnh ghi database từ control.</summary>
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
