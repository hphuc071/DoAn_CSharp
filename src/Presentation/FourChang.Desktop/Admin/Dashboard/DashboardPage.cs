using System.Drawing.Drawing2D;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Database;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Common;
namespace FourChang.Desktop.Admin.Dashboard;

public static class DashboardPage
{
    /// <summary>Bố cục dashboard theo mẫu; KPI lấy từ API thật, không dùng các con số minh họa để thay dữ liệu.</summary>
    public static Control Create()
    {
        var page=new Panel{Dock=DockStyle.Top,AutoSize=true};
        var loaded=false;
        page.HandleCreated+=async(_,_)=>
        {
            if(loaded)return;loaded=true;
            try
            {
                var data=await LiveSession.Api.DashboardAsync();if(page.IsDisposed)return;
                var menu=await LiveSession.Api.GetMenuAsync();if(page.IsDisposed)return;
                var products=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,ColumnCount=2};
                products.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,62));products.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
                foreach(var product in menu.Where(x=>x.Available).Take(3))
                {
                    var picture=new ProductImage {ImageIndex=product.ImageIndex,Size=new Size(54,54),Margin=new Padding(0,0,8,10)};
                    var caption=Ui.Stack(Ui.Label(product.Name,10,true),Ui.Label(MoneyDisplay.Format(product.Price),9,false,CafeTheme.Coffee));
                    products.Controls.Add(picture);products.Controls.Add(caption);
                }
                if(menu.Count==0)products.Controls.Add(Ui.Label("Chưa có món đang bán",9,false,CafeTheme.Muted));
                var summary=Ui.Kpis(("Thực thu sau hoàn",MoneyDisplay.Format(data.Revenue),"Thu đã xác minh trừ chi hoàn"),
                    ("Đơn hoàn tất",data.CompletedOrders.ToString(),"Đã phục vụ / giao thành công"),
                    ("Chờ hoàn tiền",data.PendingRefunds.ToString(),"Hồ sơ chưa hoàn tất"),
                    ("Nguyên liệu cảnh báo",data.LowIngredients.ToString(),"Theo tồn kho và lượng giữ"));
                var trend=new RevenueSummary(data.Revenue);
                var top=Ui.Columns(Ui.Panel("Tổng quan doanh thu",trend),Ui.Panel("Thực đơn đang bán",products),36);
                var inventory=DatabaseViews.Table("NguyenLieu",true);
                var audit=DatabaseViews.Table("NhatKyHeThong",true);
                foreach(var grid in All(inventory).Concat(All(audit)).OfType<DataGridView>())grid.Height=215;
                ArrangeColumns(inventory,["Nguyên liệu","Ngưỡng cảnh báo","Tồn an toàn","Đơn vị cơ sở"]);
                ArrangeColumns(audit,["Thời điểm","Người thực hiện","Thao tác","Kết quả"]);
                var lower=Ui.Columns(inventory,audit,50);
                var body=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Margin=Padding.Empty};
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
                foreach(var section in new Control[]{summary,top,lower}){section.Dock=DockStyle.Top;section.Margin=new Padding(0,0,0,12);body.Controls.Add(section);}
                page.Controls.Add(body);
            }
            catch(CafeApiException ex){if(!page.IsDisposed)page.Controls.Add(Ui.Label(ex.Message,10,false,CafeTheme.Danger));}
        };
        return page;
    }

    /// <summary>Ưu tiên cột nghiệp vụ dễ đọc trên dashboard; các cột kỹ thuật vẫn có thể cuộn ngang để xem.</summary>
    private static void ArrangeColumns(Control root, string[] captions)
    {
        foreach(var grid in All(root).OfType<DataGridView>())
        {
            grid.DataBindingComplete+=(_,_)=>
            {
                int index=0;
                foreach(var caption in captions)
                {
                    var column=grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(x=>x.HeaderText==caption);
                    if(column is not null)column.DisplayIndex=index++;
                }
            };
        }
    }

    /// <summary>Tìm bảng con để căn chiều cao các vùng dashboard, không thay dữ liệu hoặc quyền đọc.</summary>
    private static IEnumerable<Control> All(Control root)
    {
        foreach(Control child in root.Controls){yield return child;foreach(var nested in All(child))yield return nested;}
    }

    /// <summary>Chỉ vẽ tổng thực thu đã được API cung cấp; không tự tạo chuỗi doanh thu theo giờ.</summary>
    private sealed class RevenueSummary(decimal revenue) : Control
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using var line=new Pen(CafeTheme.Line);using var ink=new SolidBrush(CafeTheme.Muted);
            var bottom=Height-35;for(int i=0;i<4;i++){var y=18+(bottom-18)*i/3;e.Graphics.DrawLine(line,18,y,Width-20,y);}
            using var valueFont=CafeTheme.Strong(22);using var labelFont=CafeTheme.Body(9);
            TextRenderer.DrawText(e.Graphics,MoneyDisplay.Format(revenue),valueFont,new Rectangle(24,32,Width-48,50),CafeTheme.Coffee,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics,"Thực thu sau các khoản hoàn thành công",labelFont,new Rectangle(25,86,Width-50,30),CafeTheme.Muted,TextFormatFlags.Left|TextFormatFlags.WordBreak);
            TextRenderer.DrawText(e.Graphics,"Tổng quan hiện tại",labelFont,new Rectangle(24,bottom,Width-48,26),CafeTheme.Muted,TextFormatFlags.Left);
        }
        public RevenueSummary() : this(0) { }
        protected override void OnCreateControl(){base.OnCreateControl();Dock=DockStyle.Top;Height=210;DoubleBuffered=true;}
    }
}
