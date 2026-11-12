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
public sealed class MainForm:Form
{
    private readonly string role;
    private readonly string sessionName;
    private readonly Panel content=new(){Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(16),BackColor=CafeTheme.Cream};
    private readonly Label pageTitle=Ui.Label("",18,true);
    private readonly Dictionary<string,Button> navigation=[];
    public MainForm(string role,string name)
    {
        var windowIcon=BrandMark.CreateWindowIcon();Icon=windowIcon;Disposed+=(_,_)=>windowIcon.Dispose();
        this.role=role;sessionName=name;Text=$"4CHANG LINH NGU LAM · {(role=="Admin"?"Quản lý":"POS nhân viên")}";
        ClientSize=new Size(1440,960);MinimumSize=new Size(1240,820);StartPosition=FormStartPosition.CenterScreen;BackColor=CafeTheme.Cream;Font=CafeTheme.Body();AutoScaleMode=AutoScaleMode.Dpi;
        var staffLayout = role == "Staff";
        var side=new Panel{Dock=DockStyle.Left,Width=staffLayout?108:212,BackColor=CafeTheme.Dark,Padding=new Padding(8,14,8,8)};
        var brand=new Panel{Dock=DockStyle.Top,Height=staffLayout?86:96};
        var mark=new BrandMark{Location=new Point(staffLayout?24:8,0),Size=new Size(38,38),BackColor=CafeTheme.Dark};
        var brandName=Ui.Label(staffLayout?"4C":"4CHANG",15,true,Color.White);brandName.Location=new Point(staffLayout?28:52,0);brandName.Visible=!staffLayout;
        var brandSub=Ui.Label("LINH NGU LAM",8,true,Color.FromArgb(225,205,181));brandSub.Location=new Point(53,32);brandSub.Visible=!staffLayout;
        var roleName=Ui.Label(role=="Staff"?"POS / NHÂN VIÊN":"DASHBOARD / QUẢN LÝ",8,false,Color.FromArgb(192,171,150));roleName.Location=new Point(staffLayout?18:9,staffLayout?48:64);roleName.Text=staffLayout?"POS":"QUẢN LÝ";
        brand.Controls.AddRange([mark,brandName,brandSub,roleName]);
        var links=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(0,8,0,0)};
        void Add(string route,string caption)
        {
            var navigationCaption=staffLayout?route switch {"sale"=>"Bán hàng","orders"=>"Đơn hàng","cancel"=>"Hủy món","payment"=>"Hóa đơn","shift"=>"Ca làm",_=>caption}:caption;
            var b=new NavigationButton(navigationCaption,route,staffLayout){Width=staffLayout?88:188,Height=staffLayout?78:40,Margin=new Padding(0,0,0,4)};b.Click+=(_,_)=>Navigate(route);navigation[route]=b;links.Controls.Add(b);
        }
        if(role=="Admin")Add("dashboard","Tổng quan");
        foreach(var s in ScreenCatalog.All.Where(s=>s.Role==role).OrderBy(s=>role=="Staff" ? s.Route switch { "sale"=>0,"orders"=>1,"cancel"=>2,"payment"=>3,_=>4 } : 0))Add(s.Route,s.Name);
        var foot=new Label{Text=role=="Staff"?"POS":"4CHANG",Dock=DockStyle.Bottom,Height=38,Padding=new Padding(4,8,0,0),Font=CafeTheme.Body(8),ForeColor=Color.FromArgb(192,171,150)};
        side.Controls.Add(links);side.Controls.Add(foot);side.Controls.Add(brand);
        var main=new Panel{Dock=DockStyle.Fill};
        var top=new Panel{Dock=DockStyle.Top,Height=66,Padding=new Padding(20,10,20,10),BackColor=staffLayout?CafeTheme.Coffee:CafeTheme.Surface};
        var header=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pageTitle.AutoSize=false;pageTitle.AutoEllipsis=true;pageTitle.Dock=DockStyle.Fill;pageTitle.TextAlign=ContentAlignment.MiddleLeft;pageTitle.Margin=Padding.Empty;
        pageTitle.ForeColor=staffLayout?Color.White:CafeTheme.Ink;
        var account=Ui.Label(role=="Admin"?"Quản lý":name,10,true);account.ForeColor=staffLayout?Color.FromArgb(255,241,218):CafeTheme.Ink;account.AutoSize=false;account.AutoEllipsis=true;account.Width=Math.Clamp(TextRenderer.MeasureText(account.Text,account.Font).Width+20,160,300);account.Height=44;account.TextAlign=ContentAlignment.MiddleRight;account.Margin=new Padding(0,0,16,0);
        var action=Ui.Button(role=="Admin"?"Tài khoản":"Kết thúc ca",()=>{if(role=="Staff")Navigate("shift");else { if(FourChang.Desktop.Shared.Session.LiveSession.Preview)AccessView.ShowLogin(this);else ShowAccount(); }});action.Margin=Padding.Empty;
        var topRight=Ui.Row(account,action);topRight.Dock=DockStyle.Fill;topRight.WrapContents=false;topRight.Anchor=AnchorStyles.Right;
        header.Controls.Add(pageTitle,0,0);header.Controls.Add(topRight,1,0);top.Controls.Add(header);
        main.Controls.Add(content);main.Controls.Add(top);Controls.Add(main);Controls.Add(side);
        CafeVisuals.Apply(this);
        Navigate(role=="Admin"?"dashboard":"sale");
    }
    // Tài khoản mở đúng UC-ADM-02; đăng xuất/đổi mật khẩu thành công quay lại màn hình xác thực.
    public event EventHandler? ReauthenticationRequested;
    public void RequestReauthentication()
    {
        if(ReauthenticationRequested is not null)ReauthenticationRequested(this,EventArgs.Empty);else Close();
    }
    private void ShowAccount()=>Navigate("access");
    public void Navigate(string route)
    {
        // POS khóa hoặc chỉ đối chiếu không chuyển sang luồng bán; server vẫn kiểm tra độc lập.
        if (role == "Staff" && !FourChang.Desktop.Shared.Session.LiveSession.Preview &&
            (FourChang.Desktop.Shared.Session.LiveSession.ShiftLocked || FourChang.Desktop.Shared.Session.LiveSession.Current?.Role == "STAFF_RECONCILIATION")) route = "shift";
        foreach(var pair in navigation)pair.Value.BackColor=pair.Key==route?CafeTheme.Coffee:CafeTheme.Dark;
        var definition=ScreenCatalog.All.FirstOrDefault(s=>s.Role==role&&s.Route==route);
        pageTitle.Text=definition?.Name??"Tổng quan hoạt động";
        Control body=!FourChang.Desktop.Shared.Session.LiveSession.Preview ? FourChang.Desktop.Shared.Database.DatabaseViews.Create(role,route) : route switch
        {
            "shift"=>ShiftView.Create(sessionName),"sale"=>SaleView.Create(),"orders"=>OrdersView.Create(),"cancel"=>CancelView.Create(),"payment"=>PaymentView.Create(),
            "customers"=>CustomersView.Create(),"access"=>AccessView.Create(),"menu"=>MenuManagementView.Create(),"ingredients"=>IngredientsView.Create(),"recipes"=>RecipesView.Create(),"inventory"=>InventoryView.Create(),"stock"=>StockDocumentsView.Create(),"manage-orders"=>OrdersManagementView.Create(),"invoices"=>InvoicesView.Create(),"refunds"=>RefundsView.Create(),"reports"=>ReportsView.Create(),"dashboard"=>DashboardView.Create(),_=>Ui.Label("Chưa có màn hình này")
        };
        content.SuspendLayout();foreach(Control c in content.Controls)c.Dispose();content.Controls.Clear();
        var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,RowCount=2};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        body.Dock=DockStyle.Top;layout.RowCount=1;layout.Controls.Add(body,0,0);content.Controls.Add(layout);content.ResumeLayout(true);content.AutoScrollPosition=Point.Empty;
    }
}
