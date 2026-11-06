using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Admin.Access.Views;
using FourChang.Desktop.Admin.Customers.Views;
using FourChang.Desktop.Admin.Dashboard;
using FourChang.Desktop.Admin.Ingredients;
using FourChang.Desktop.Admin.Inventory;
using FourChang.Desktop.Admin.Invoices;
using FourChang.Desktop.Admin.Menu;
using FourChang.Desktop.Admin.Orders;
using FourChang.Desktop.Admin.Recipes;
using FourChang.Desktop.Admin.Refunds;
using FourChang.Desktop.Admin.Reports;
using FourChang.Desktop.Admin.StockDocuments;
using FourChang.Desktop.Staff.Invoices;
using FourChang.Desktop.Staff.Orders;
using FourChang.Desktop.Staff.Sales;
using FourChang.Desktop.Staff.WorkSessions;
using FourChang.Presentation.Shared.Contracts.Common;
namespace FourChang.Desktop.Shell;
public sealed class MainForm : Form
{
    private readonly string role;
    private readonly string sessionName;
    private readonly Panel content = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16), BackColor = CafeTheme.Cream };
    private readonly Label pageTitle = Ui.Label("", 18, true);
    private readonly Dictionary<string, Button> navigation = [];
    // Chỉ giữ màn hình có nháp POS trong vòng đời một phiên; không ghi dữ liệu nhạy cảm xuống đĩa.
    private readonly Dictionary<string, Control> draftPages = [];
    private readonly HashSet<string> draftRoutes = ["sale", "shift"];

    public MainForm(string role, string name)
    {
        CafeVisuals.Initialize();
        var windowIcon = BrandMark.CreateWindowIcon(); Icon = windowIcon; Disposed += (_, _) => windowIcon.Dispose();
        this.role = role; sessionName = name; Text = $"4CHANG LINH NGU LAM · {(role == "Admin" ? "Quản lý" : "POS nhân viên")}";
        ClientSize = new Size(1440, 960); MinimumSize = new Size(1100, 740); StartPosition = FormStartPosition.CenterScreen; BackColor = CafeTheme.Cream; Font = CafeTheme.Body(); AutoScaleMode = AutoScaleMode.Dpi;
        var staffLayout = role == "Staff";
        var side = new Panel { Dock = DockStyle.Left, Width = staffLayout ? 108 : 212, BackColor = CafeTheme.Dark, Padding = new Padding(8, 14, 8, 8) };
        var brand = new Panel { Dock = DockStyle.Top, Height = staffLayout ? 86 : 96 };
        var mark = new BrandMark { Location = new Point(staffLayout ? 24 : 8, 0), Size = new Size(38, 38), BackColor = CafeTheme.Dark };
        var brandName = Ui.Label(staffLayout ? "4C" : "4CHANG", 15, true, Color.White); brandName.Location = new Point(staffLayout ? 28 : 52, 0); brandName.Visible = !staffLayout;
        var brandSub = Ui.Label("LINH NGU LAM", 8, true, Color.FromArgb(225, 205, 181)); brandSub.Location = new Point(53, 33); brandSub.Visible = !staffLayout;
        var roleName = Ui.Label(role == "Staff" ? "POS / NHÂN VIÊN" : "DASHBOARD / QUẢN LÝ", 8, false, Color.FromArgb(192, 171, 150)); roleName.Location = new Point(staffLayout ? 18 : 9, staffLayout ? 48 : 64); roleName.Text = staffLayout ? "POS" : "QUẢN LÝ";
        brand.Controls.AddRange([mark, brandName, brandSub, roleName]);
        var links = new FlowLayoutPanel { Name = "MainNavigation", Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 8, 0, 0) };
        // Khi thanh cuộn dọc xuất hiện ở cửa sổ thấp, nút phải vừa phần client còn lại.
        // Chỉ điều chỉnh vài pixel trong sidebar hiện có, không đổi route/thứ tự hoặc độ rộng sidebar.
        var fittingNavigation = false;
        void FitNavigation()
        {
            if (fittingNavigation) return;
            fittingNavigation = true;
            try
            {
                var reserve = !staffLayout && (links.VerticalScroll.Visible || links.Height < 700) ? SystemInformation.VerticalScrollBarWidth : 0;
                var width = Math.Max(80, links.ClientSize.Width - links.Padding.Horizontal - reserve - 4);
                foreach (var button in links.Controls.OfType<Button>()) button.Width = Math.Min(staffLayout ? 88 : 188, width);
                foreach (var label in links.Controls.OfType<Label>())
                {
                    var caption = label.Tag as string ?? label.Text; label.Tag = caption;
                    var available = Math.Max(60, width - label.Margin.Left);
                    var wrap = TextRenderer.MeasureText(caption, label.Font).Width > available;
                    label.AutoSize = false; label.MaximumSize = Size.Empty; label.Width = available;
                    label.Text = wrap ? caption.Replace(" & ", " &\n") : caption; label.Height = wrap ? 34 : 18;
                }
            }
            finally { fittingNavigation = false; }
        }
        links.SizeChanged += (_, _) => FitNavigation();
        links.Layout += (_, _) => FitNavigation();
        void Add(string route, string caption)
        {
            var navigationCaption = staffLayout ? route switch { "sale" => "Bán hàng", "orders" => "Đơn hàng", "cancel" => "Hủy món", "payment" => "Hóa đơn", "shift" => "Ca làm", _ => caption } : caption;
            var b = new NavigationButton(navigationCaption, route, staffLayout) { Width = staffLayout ? 88 : 188, Height = staffLayout ? 78 : 44, Margin = new Padding(0, 0, 0, 4) }; b.Click += (_, _) => Navigate(route); navigation[route] = b; links.Controls.Add(b);
        }
        // Nhóm chỉ tác động thứ tự nhìn; quyền của từng route vẫn được API kiểm tra như trước.
        void Group(string title)
        {
            var label = Ui.Label(title, 8, true, Color.FromArgb(202, 181, 157));
            label.Margin = new Padding(12, 14, 0, 8); links.Controls.Add(label);
        }
        if (role == "Admin")
        {
            Add("dashboard", "Tổng quan");
            foreach (var group in new[] {
                ("CON NGƯỜI & TRUY CẬP", new[] {"customers", "employees", "work-sessions", "access"}),
                ("THỰC ĐƠN & KHO", new[] {"menu", "ingredients", "recipes", "inventory", "stock"}),
                ("ĐƠN HÀNG & TÀI CHÍNH", new[] {"manage-orders", "invoices", "refunds", "reports"}) })
            {
                Group(group.Item1);
                foreach (var route in group.Item2) { var screen = ScreenCatalog.Find(role, route); Add(screen.Route, screen.Name); }
            }
        }
        else foreach (var screen in ScreenCatalog.All.Where(s => s.Role == role).OrderBy(s => s.Route switch { "sale" => 0, "orders" => 1, "cancel" => 2, "payment" => 3, _ => 4 })) Add(screen.Route, screen.Name);
        var foot = new Label { Text = role == "Staff" ? "POS" : "4CHANG", Dock = DockStyle.Bottom, Height = 38, Padding = new Padding(4, 8, 0, 0), Font = CafeTheme.Body(8), ForeColor = Color.FromArgb(192, 171, 150) };
        side.Controls.Add(links); side.Controls.Add(foot); side.Controls.Add(brand);
        var main = new Panel { Dock = DockStyle.Fill };
        var top = new Panel { Dock = DockStyle.Top, Height = 66, Padding = new Padding(20, 10, 20, 10), BackColor = staffLayout ? CafeTheme.Coffee : CafeTheme.Surface };
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pageTitle.AutoSize = false; pageTitle.AutoEllipsis = true; pageTitle.Dock = DockStyle.Fill; pageTitle.TextAlign = ContentAlignment.MiddleLeft; pageTitle.Margin = Padding.Empty;
        pageTitle.ForeColor = staffLayout ? Color.White : CafeTheme.Ink;
        var account = Ui.Label(role == "Admin" ? "Quản lý" : name, 10, true); account.ForeColor = staffLayout ? Color.FromArgb(255, 241, 218) : CafeTheme.Ink; account.AutoSize = false; account.AutoEllipsis = true; account.Width = staffLayout ? 210 : 80; account.Height = 44; account.TextAlign = ContentAlignment.MiddleRight; account.Margin = new Padding(0, 0, 16, 0);
        var action = Ui.Button(role == "Admin" ? "Tài khoản" : "Kết thúc ca", () => { if (role == "Staff") Navigate("shift"); else { if (FourChang.Desktop.Shared.Session.LiveSession.Preview) AccessView.ShowLogin(this); else ShowAccount(); } }); action.Margin = Padding.Empty;
        var topRight = Ui.Row(account, action); topRight.Dock = DockStyle.Fill; topRight.WrapContents = false; topRight.Anchor = AnchorStyles.Right;
        foreach (Control item in topRight.Controls) item.Margin = item == account ? new Padding(0, 0, 16, 0) : Padding.Empty;
        header.Controls.Add(pageTitle, 0, 0); header.Controls.Add(topRight, 1, 0); top.Controls.Add(header);
        main.Controls.Add(content); main.Controls.Add(top); Controls.Add(main); Controls.Add(side);
        Disposed += (_, _) => { foreach (var draft in draftPages.Values) draft.Dispose(); draftPages.Clear(); };
        Navigate(role == "Admin" ? "dashboard" : "sale");
    }
    // Tài khoản mở đúng UC-ADM-02; đăng xuất/đổi mật khẩu thành công quay lại màn hình xác thực.
    public event EventHandler? ReauthenticationRequested;
    public void RequestReauthentication()
    {
        if (ReauthenticationRequested is not null) ReauthenticationRequested(this, EventArgs.Empty); else Close();
    }
    private void ShowAccount() => Navigate("access");
    public void Navigate(string route)
    {
        foreach (var pair in navigation) pair.Value.BackColor = pair.Key == route ? CafeTheme.Coffee : CafeTheme.Dark;
        var definition = ScreenCatalog.All.FirstOrDefault(s => s.Role == role && s.Route == route);
        pageTitle.Text = definition?.Name ?? "Tổng quan hôm nay";
        Control body = draftPages.TryGetValue(route, out var remembered) ? remembered : !FourChang.Desktop.Shared.Session.LiveSession.Preview ? FourChang.Desktop.Shared.Database.DatabaseViews.Create(role, route) : route switch
        {
            "shift" => ShiftView.Create(sessionName),
            "sale" => SaleView.Create(),
            "orders" => OrdersView.Create(),
            "cancel" => CancelView.Create(),
            "payment" => PaymentView.Create(),
            "customers" => CustomersView.Create(),
            "access" => AccessView.Create(),
            "menu" => MenuManagementView.Create(),
            "ingredients" => IngredientsView.Create(),
            "recipes" => RecipesView.Create(),
            "inventory" => InventoryView.Create(),
            "stock" => StockDocumentsView.Create(),
            "manage-orders" => OrdersManagementView.Create(),
            "invoices" => InvoicesView.Create(),
            "refunds" => RefundsView.Create(),
            "reports" => ReportsView.Create(),
            "dashboard" => DashboardView.Create(),
            _ => Ui.Label("Chưa có màn hình này")
        };
        // Giữ trang nháp trong cây Form nhưng ẩn đi: FindForm/owner và callback đang chạy
        // vẫn hợp lệ, đồng thời không mất ô nhập khi chuyển giữa bán hàng và ca làm.
        if (role == "Staff" && draftRoutes.Contains(route)) draftPages[route] = body;
        content.SuspendLayout();
        foreach (Control container in content.Controls.Cast<Control>().ToArray())
        {
            var containsDraft = container.Controls.Cast<Control>().Any(child => draftPages.Values.Contains(child));
            if (containsDraft) container.Visible = false;
            else container.Dispose();
        }
        if (body.Parent is { } rememberedLayout && rememberedLayout.Parent == content)
        {
            rememberedLayout.Visible = true; rememberedLayout.BringToFront();
        }
        else
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.Dock = DockStyle.Top; layout.Controls.Add(body, 0, 0); content.Controls.Add(layout); layout.BringToFront();
        }
        content.ResumeLayout(true); content.AutoScrollPosition = Point.Empty;
        // Áp dụng theme cho trang mới, không tạo lại dữ liệu hay gọi lại API vì đổi màu.
        CafeVisuals.Apply(this);
    }
}
