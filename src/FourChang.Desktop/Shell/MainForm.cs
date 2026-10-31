using FourChang.Desktop.Controls;
using FourChang.Desktop.DesignSystem;
using FourChang.Desktop.Features.Admin;
using FourChang.Desktop.Features.Pos;
using FourChang.Desktop.Preview;
using FourChang.Presentation.Shared.Catalog;
namespace FourChang.Desktop.Shell;
public sealed class MainForm:Form
{
    private readonly string role;
    private readonly string sessionName;
    private readonly Panel content=new(){Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(24),BackColor=CafeTheme.Cream};
    private readonly Label pageTitle=Ui.Label("",19,true);
    private readonly Dictionary<string,Button> navigation=[];
    public MainForm(string role,string name)
    {
        this.role=role;sessionName=name;Text=$"4CHANG LINH NGU LAM · {(role=="Admin"?"Quản lý":"POS nhân viên")}";
        ClientSize=new Size(1440,960);MinimumSize=new Size(1240,820);StartPosition=FormStartPosition.CenterScreen;BackColor=CafeTheme.Cream;Font=CafeTheme.Body();AutoScaleMode=AutoScaleMode.Dpi;
        var side=new Panel{Dock=DockStyle.Left,Width=246,BackColor=CafeTheme.Dark,Padding=new Padding(14,20,14,12)};
        var brand=new Panel{Dock=DockStyle.Top,Height=126};
        var brandName=Ui.Label("4CHANG",21,true,Color.White);brandName.Location=new Point(10,4);
        var brandSub=Ui.Label("LINH NGU LAM",11,true,Color.FromArgb(225,205,181));brandSub.Location=new Point(12,43);
        var roleName=Ui.Label(role=="Staff"?"POS / NHÂN VIÊN":"DASHBOARD / QUẢN LÝ",8,false,Color.FromArgb(192,171,150));roleName.Location=new Point(12,83);
        brand.Controls.AddRange([brandName,brandSub,roleName]);
        var links=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(0,8,0,0)};
        void Add(string route,string caption)
        {
            var b=Ui.Button(caption,()=>Navigate(route));b.AutoSize=false;b.Width=214;b.Height=44;b.ForeColor=Color.FromArgb(235,226,212);b.BackColor=CafeTheme.Dark;b.FlatAppearance.BorderSize=0;b.TextAlign=ContentAlignment.MiddleLeft;b.Margin=new Padding(0,0,0,4);navigation[route]=b;links.Controls.Add(b);
        }
        if(role=="Admin")Add("dashboard","Tổng quan");
        foreach(var s in ScreenCatalog.All.Where(s=>s.Role==role))Add(s.Route,s.Name);
        var foot=new Label{Text=role=="Staff"?"NHÂN VIÊN":"KHÔNG GIAN QUẢN LÝ",Dock=DockStyle.Bottom,Height=58,Padding=new Padding(12,8,0,0),Font=CafeTheme.Body(8),ForeColor=Color.FromArgb(192,171,150)};
        side.Controls.Add(links);side.Controls.Add(foot);side.Controls.Add(brand);
        var main=new Panel{Dock=DockStyle.Fill};
        var top=new Panel{Dock=DockStyle.Top,Height=88,Padding=new Padding(24,18,24,18),BackColor=CafeTheme.Surface};
        var header=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pageTitle.AutoSize=false;pageTitle.AutoEllipsis=true;pageTitle.Dock=DockStyle.Fill;pageTitle.TextAlign=ContentAlignment.MiddleLeft;pageTitle.Margin=Padding.Empty;
        var account=Ui.Label(role=="Admin"?"Quản lý":name,10,true);account.AutoSize=false;account.AutoEllipsis=true;account.Width=160;account.Height=44;account.TextAlign=ContentAlignment.MiddleRight;account.Margin=new Padding(0,0,16,0);
        var action=Ui.Button(role=="Admin"?"Tài khoản":"Kết thúc ca",()=>{if(role=="Staff")Navigate("shift");else { if(FourChang.Desktop.Data.LiveSession.Preview)AccessView.ShowLogin(this);else ShowAccount(); }});action.Margin=Padding.Empty;
        var topRight=Ui.Row(account,action);topRight.Dock=DockStyle.Fill;topRight.WrapContents=false;topRight.Anchor=AnchorStyles.Right;
        header.Controls.Add(pageTitle,0,0);header.Controls.Add(topRight,1,0);top.Controls.Add(header);
        main.Controls.Add(content);main.Controls.Add(top);Controls.Add(main);Controls.Add(side);
        Navigate(role=="Admin"?"dashboard":"sale");
    }
    private async void ShowAccount()
    {
        if(MessageBox.Show(this,"Đăng xuất tài khoản quản lý?","Tài khoản",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        try { await FourChang.Desktop.Data.LiveSession.Api.LogoutAsync(); FourChang.Desktop.Data.LiveSession.Api.Token=null; Close(); }
        catch(FourChang.Presentation.Shared.Integration.CafeApiException ex) { MessageBox.Show(this,ex.Message); }
    }
    public void Navigate(string route)
    {
        foreach(var pair in navigation)pair.Value.BackColor=pair.Key==route?CafeTheme.Coffee:CafeTheme.Dark;
        var definition=ScreenCatalog.All.FirstOrDefault(s=>s.Role==role&&s.Route==route);
        pageTitle.Text=definition?.Name??"Tổng quan hôm nay";
        Control body=!FourChang.Desktop.Data.LiveSession.Preview ? FourChang.Desktop.Features.Database.DatabaseViews.Create(role,route) : route switch
        {
            "shift"=>ShiftView.Create(sessionName),"sale"=>SaleView.Create(),"orders"=>OrdersView.Create(),"cancel"=>CancelView.Create(),"payment"=>PaymentView.Create(),
            "customers"=>CustomersView.Create(),"access"=>AccessView.Create(),"menu"=>MenuManagementView.Create(),"ingredients"=>IngredientsView.Create(),"recipes"=>RecipesView.Create(),"inventory"=>InventoryView.Create(),"stock"=>StockDocumentsView.Create(),"manage-orders"=>OrdersManagementView.Create(),"invoices"=>InvoicesView.Create(),"refunds"=>RefundsView.Create(),"reports"=>ReportsView.Create(),"dashboard"=>DashboardView.Create(),_=>Ui.Label("Chưa có màn hình này")
        };
        content.SuspendLayout();foreach(Control c in content.Controls)c.Dispose();content.Controls.Clear();
        var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,RowCount=2};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        body.Dock=DockStyle.Top;layout.RowCount=1;layout.Controls.Add(body,0,0);content.Controls.Add(layout);content.ResumeLayout(true);content.AutoScrollPosition=Point.Empty;
    }
}


