using System.Data;
using FourChang.Desktop.Controls;
using FourChang.Desktop.Data;
using FourChang.Desktop.DesignSystem;
using FourChang.Presentation.Shared.Integration;
using FourChang.Presentation.Shared.Models;
namespace FourChang.Desktop.Features.Database;

public static class DatabaseViews
{
    private static readonly Dictionary<string,string[]> AdminTables=new()
    {
        ["customers"]=["TaiKhoanKhachHang","DiaChiKhachHang"],
        ["access"]=["TaiKhoanQuanLy","CaLamViec"],
        ["menu"]=["Mon","CauHinhMon","DanhMucMon","MonThem","CauHinhMonThem","TuyChonPhaCheHopLe"],
        ["ingredients"]=["NguyenLieu","DonViTinh","QuyDoiDonViNguyenLieu"],
        ["recipes"]=["CongThuc","ChiTietCongThuc"],
        ["inventory"]=["TonKho","GiuChoNguyenLieu","BienDongKho"],
        ["stock"]=["PhieuKho","ChiTietPhieuKho"],
        ["manage-orders"]=["DonHang","ChiTietDonHang","MonThemChiTietDonHang","NguyenLieuChiTietDonHang","HuyMon","Ban"],
        ["invoices"]=["HoaDon","ThanhToan","GiaoDichNganHang"],
        ["refunds"]=["HoSoHoanTien","LanChiHoanTien"],
        ["reports"]=["NhatKyHeThong","ThanhToan","LanChiHoanTien"]
    };
    public static Control Create(string role,string route)
    {
        if(role=="Staff")return route switch{"shift"=>Shift(),"sale"=>Sale(),"orders"=>Table("DonHang",false),"cancel"=>Ui.Panel("Hủy món",Ui.Label("Chọn đơn để tra cứu. Thao tác hủy sẽ được triển khai cùng luồng xử lý đơn."),Table("DonHang",false)),"payment"=>Table("HoaDon",false),_=>Ui.Label("Chưa có màn hình")};
        if(route=="employees")return Employees();
        if(route=="dashboard")return Dashboard();
        if(!AdminTables.TryGetValue(route,out var tables))return Ui.Label("Chưa có màn hình");
        return Ui.Tabs(tables.Select(name=>(DatabaseLabels.Table(name),Table(name,true))).ToArray());
    }
    private static Control Table(string name,bool admin)
    {
        var grid=Ui.Grid(new PreviewTable([],[]),440);grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.AllCells;
        var error=Ui.Label("",9,false,CafeTheme.Danger);error.Visible=false;
        var reload=Ui.Button("Tải lại",()=>{});
        var box=Ui.Panel(DatabaseLabels.Table(name),Ui.Row(reload),error,grid);
        async Task Load()
        {
            if(box.IsDisposed)return;reload.Enabled=false;error.Visible=false;
            try{var data=admin?await LiveSession.Api.ReadTableAsync(name):await LiveSession.Api.ReadPosTableAsync(name);if(!grid.IsDisposed)SetGrid(grid,data);}
            catch(CafeApiException exception){if(!error.IsDisposed){error.Text=exception.Message;error.Visible=true;}}
            finally{if(!reload.IsDisposed)reload.Enabled=true;}
        }
        var loaded=false;box.HandleCreated+=async(_,_)=>{if(loaded)return;loaded=true;await Load();};reload.Click+=async(_,_)=>await Load();return box;
    }
    private static void SetGrid(DataGridView grid,TableData data)
    {
        var table=new DataTable();foreach(var column in data.Columns)table.Columns.Add(DatabaseLabels.Column(column));foreach(var row in data.Rows)table.Rows.Add(row.Cast<object>().ToArray());grid.DataSource=table;
    }
    private static Control Dashboard()
    {
        var panel=new Panel{AutoSize=true,Dock=DockStyle.Top};
        panel.HandleCreated+=async(_,_)=>
        {
            try{var data=await LiveSession.Api.DashboardAsync();if(panel.IsDisposed)return;var content=Ui.Stack(Ui.Kpis(("Thực thu sau hoàn",MoneyDisplay.Format(data.Revenue),"Thu đã xác minh trừ chi hoàn thành công"),("Đơn hoàn tất",data.CompletedOrders.ToString(),"Theo dữ liệu database"),("Hồ sơ hoàn đang mở",data.PendingRefunds.ToString(),"Chưa hoàn tất hoặc từ chối"),("Nguyên liệu cảnh báo",data.LowIngredients.ToString(),"Theo tồn thực tế và lượng giữ")),Table("DonHang",true));panel.Controls.Add(content);}
            catch(CafeApiException exception){if(!panel.IsDisposed)panel.Controls.Add(Ui.Label(exception.Message,10,false,CafeTheme.Danger));}
        };return panel;
    }
    private static Control Employees()
    {
        var grid=Ui.Grid(new PreviewTable(["Mã nhân viên","Họ tên","Điện thoại","Ngày vào làm","Trạng thái"],[]),400);
        var error=Ui.Label("",9,false,CafeTheme.Danger);error.Visible=false;List<Employee> records=[];
        var add=Ui.Button("Thêm nhân viên",()=>{},true);var edit=Ui.Button("Sửa / ngừng làm",()=>{});var reload=Ui.Button("Tải lại",()=>{});
        var box=Ui.Panel("Hồ sơ nhân viên",Ui.Row(add,edit,reload),error,grid);
        async Task Load(){try{records=await LiveSession.Api.GetEmployeesAsync(true);if(grid.IsDisposed)return;SetGrid(grid,new(["Mã nhân viên","Họ tên","Điện thoại","Ngày vào làm","Trạng thái"],records.Select(x=>new[]{x.MaNhanVien.ToString(),x.HoTen,x.SoDienThoai??"",x.NgayVaoLam.ToString("dd/MM/yyyy"),x.TrangThai}).ToArray()));error.Visible=false;}catch(CafeApiException ex){if(!error.IsDisposed){error.Text=ex.Message;error.Visible=true;}}}
        async Task Save(Employee? current)
        {
            using var form=new Form{Text=current is null?"Thêm nhân viên":"Sửa nhân viên",ClientSize=new Size(480,420),StartPosition=FormStartPosition.CenterParent,BackColor=CafeTheme.Cream,Font=CafeTheme.Body()};
            var name=Ui.Input("Họ tên",current?.HoTen??"",400);var phone=Ui.Input("Điện thoại",current?.SoDienThoai??"",400);var date=new DateTimePicker{Format=DateTimePickerFormat.Short,Value=current?.NgayVaoLam??DateTime.Today,Width=400};var status=Ui.Select("ACTIVE","INACTIVE");status.SelectedItem=current?.TrangThai??"ACTIVE";
            var save=Ui.Button("Lưu",()=>{if(string.IsNullOrWhiteSpace(name.Text)){MessageBox.Show(form,"Nhập họ tên.");return;}form.DialogResult=DialogResult.OK;},true);
            var fields=Ui.Stack(Ui.Field("Họ tên",name),Ui.Field("Điện thoại",phone),Ui.Field("Ngày vào làm",date),Ui.Field("Trạng thái",status),save);fields.Padding=new Padding(24);form.Controls.Add(fields);
            if(form.ShowDialog(box.FindForm())!=DialogResult.OK)return;
            try{await LiveSession.Api.SaveEmployeeAsync(new(name.Text,phone.Text,date.Value,status.Text,current?.PhienBanDuLieu),current?.MaNhanVien);await Load();}
            catch(CafeApiException ex){error.Text=ex.Message;error.Visible=true;}
        }
        box.HandleCreated+=async(_,_)=>await Load();reload.Click+=async(_,_)=>await Load();add.Click+=async(_,_)=>await Save(null);edit.Click+=async(_,_)=>{if(grid.CurrentRow is null)return;var id=Convert.ToInt64(grid.CurrentRow.Cells[0].Value);await Save(records.First(x=>x.MaNhanVien==id));};return box;
    }
    private static Control Shift()
    {
        var session=LiveSession.Current;
        var amount=new NumericUpDown{Minimum=0,Maximum=1_000_000_000,ThousandsSeparator=true,Width=300,Font=CafeTheme.Body()};
        var reason=Ui.Input("Lý do nếu chênh lệch","",400);var error=Ui.Label("",9,false,CafeTheme.Danger);error.Visible=false;
        var close=Ui.Button("Kết thúc ca",()=>{},true);
        var panel=Ui.Panel("Ca làm việc",Ui.Label($"Nhân viên: {session?.Name}"),Ui.Label($"Mã ca: {session?.ShiftId}"),Ui.Field("Tiền mặt thực đếm",amount),Ui.Field("Lý do chênh lệch",reason),error,close);
        close.Click+=async(_,_)=>{close.Enabled=false;try{await LiveSession.Api.CloseShiftAsync(new(amount.Value,reason.Text));LiveSession.Api.Token=null;LiveSession.Current=null;panel.FindForm()?.Close();}catch(CafeApiException ex){error.Text=ex.Message;error.Visible=true;close.Enabled=true;}};return panel;
    }
    private static Control Sale()
    {
        var items=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,MaximumSize=new Size(800,0)};
        var error=Ui.Label("",9,false,CafeTheme.Danger);error.Visible=false;
        var panel=Ui.Columns(Ui.Panel("Thực đơn",error,items),Ui.Panel("Đơn hàng",Table("DonHang",false)),35);
        panel.HandleCreated+=async(_,_)=>
        {
            try
            {
                var products=await LiveSession.Api.GetMenuAsync();if(items.IsDisposed)return;
                foreach(var product in products)
                {
                    var card=new SurfacePanel{Width=210,Height=238,Padding=Padding.Empty,Margin=new Padding(0,0,12,12)};
                    var picture=new ProductImage{ImageIndex=product.ImageIndex,Dock=DockStyle.Top,Height=150};
                    var title=new Label{Text=product.Name+"\n"+MoneyDisplay.Format(product.Price)+(product.Available?"":" · Tạm hết"),Dock=DockStyle.Bottom,Height=88,Font=CafeTheme.Strong(10),Padding=new Padding(10),ForeColor=product.Available?CafeTheme.Ink:CafeTheme.Muted};card.Controls.Add(title);card.Controls.Add(picture);items.Controls.Add(card);
                }
            }
            catch(CafeApiException ex){if(!error.IsDisposed){error.Text=ex.Message;error.Visible=true;}}
        };return panel;
    }
}
