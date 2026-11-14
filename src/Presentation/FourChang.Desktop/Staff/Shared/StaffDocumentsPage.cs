using System.Data;
using System.Globalization;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Database;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Common;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Desktop.Shared.Invoices;
namespace FourChang.Desktop.Staff.Shared;

/// <summary>Danh sách đơn/hóa đơn POS chỉ đọc từ API hiện có, thay bảng thô bằng bố cục và nhãn thống nhất.</summary>
public sealed class StaffDocumentsPage : UserControl
{
    private readonly bool invoice;
    private readonly TextBox search=Ui.Input("Tìm trong danh sách đã tải…","",340);
    private readonly DataGridView grid=Ui.Grid(new([],[]),440);
    private readonly Label status=Ui.Label("",9,false,CafeTheme.Muted);
    private readonly Label count=Ui.Label("",9,false,CafeTheme.Muted);
    private readonly Button reload;
    private readonly Button details;
    private TableData? data;
    private bool busy;
    private readonly string[] columns;

    /// <summary>Không thêm nút xử lý chưa có backend. Mọi dòng giữ khóa gốc; chỉ định dạng và lọc danh sách đã tải.</summary>
    public StaffDocumentsPage(bool invoices)
    {
        status.Visible = false; status.TextChanged += (_, _) => status.Visible = !string.IsNullOrWhiteSpace(status.Text);
        invoice=invoices;Name=invoices?"StaffInvoicesPage":"StaffOrdersPage";AutoSize=true;Dock=DockStyle.Top;BackColor=CafeTheme.Cream;Padding=new Padding(16);
        search.Name="StaffDocumentSearch";grid.Name="StaffDocumentsGrid";grid.AutoGenerateColumns=false;
        columns=invoices?["SoHoaDon","MaDonHang","LoaiHoaDon","SoTien","TrangThai","PhatHanhLuc"]:
            ["MaDonHienThi","HinhThucNhanMon","MaBanDaChot","TrangThai","MaCaLamViec","ThoiDiemTao"];
        foreach(var name in columns)grid.Columns.Add(new DataGridViewTextBoxColumn{Name=name,DataPropertyName=name,HeaderText=ColumnCaption(name),FillWeight=name is "SoHoaDon" or "MaDonHienThi"?160:100,SortMode=DataGridViewColumnSortMode.NotSortable});
        reload=Ui.Button("Tải lại",()=>{});reload.Click+=async(_,_)=>await LoadData();
        details=Ui.Button(invoices?"Xem hóa đơn":"Xem đơn hàng",()=>{},true);details.Click+=async(_,_)=>await ShowDetails();
        search.TextChanged+=(_,_)=>Render();grid.CellDoubleClick+=async(_,e)=>{if(e.RowIndex>=0)await ShowDetails();};
        grid.CellFormatting+=(_,e)=>
        {
            if(e.ColumnIndex<0||e.Value is not string value)return;
            var property=grid.Columns[e.ColumnIndex].DataPropertyName;
            if(property is "TrangThai" or "HinhThucNhanMon" or "LoaiHoaDon")e.Value=OperationsPresentation.Label(value);
            if(property=="SoTien"&&decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out var amount))e.Value=amount.ToString("N0",CultureInfo.GetCultureInfo("vi-VN"))+"đ";
        };
        var filter=Ui.Row(Ui.Field("Tìm kiếm",search));var filterButtons=Ui.Row(reload);filterButtons.Dock=DockStyle.None;filterButtons.Margin=new Padding(0,30,0,8);filter.Controls.Add(filterButtons);
        var actions=Ui.Row(details);
        Controls.Add(new AdminListLayout(invoices?"Hóa đơn tại quầy":"Danh sách đơn hàng",
            invoices?"Tra cứu chứng từ đã phát hành và nội dung đã chốt.":"Tra cứu trạng thái, hình thức nhận món và thông tin đơn đã ghi nhận.",filter,actions,status,grid,count,"POS / "+(invoices?"HÓA ĐƠN":"ĐƠN HÀNG")));
        CommerceWorkspaceVisuals.FormatGrid(grid);
        HandleCreated+=async(_,_)=>await LoadData();
    }
    /// <summary>Tiêu đề tiếng Việt dùng chung ở bảng và chi tiết; khóa binding gốc không đổi.</summary>
    private static string ColumnCaption(string name)=>name switch
    {
        "SoHoaDon"=>"Số hóa đơn", "MaDonHang" or "MaDonHienThi"=>"Mã đơn", "LoaiHoaDon"=>"Loại hóa đơn",
        "SoTien"=>"Giá trị", "TrangThai"=>"Trạng thái", "PhatHanhLuc"=>"Phát hành", "ThoiDiemTao"=>"Ngày tạo",
        "HinhThucNhanMon"=>"Nhận món", "MaBanDaChot"=>"Bàn", "MaCaLamViec"=>"Mã ca", _=>DatabaseLabels.Column(name)
    };
    /// <summary>Lỗi kết nối giữ dữ liệu cũ và thông báo; không hiển thị dữ liệu mẫu hoặc số0 thay lỗi.</summary>
    private async Task LoadData()
    {
        if(busy||IsDisposed)return;busy=true;reload.Enabled=details.Enabled=false;
        try{var snapshot=await LiveSession.Api.ReadPosTableAsync(invoice?"HoaDon":"DonHang");if(IsDisposed)return;data=snapshot;status.Text="";Render();}
        catch(CafeApiException ex){if(!IsDisposed)status.Text=ex.Message+" · Chưa cập nhật được danh sách.";}
        finally{busy=false;if(!IsDisposed){reload.Enabled=true;details.Enabled=grid.Rows.Count>0;}}
    }
    /// <summary>Lọc nội dung hiển thị bằng LINQ, không dựng SQL/biểu thức từ từ khóa người dùng.</summary>
    private void Render()
    {
        if(data is null||grid.IsDisposed)return;
        var term=search.Text.Trim();var table=new DataTable();foreach(var name in data.Columns)table.Columns.Add(name);
        foreach(var row in data.Rows.Where(row=>term.Length==0||columns.Any(name=>Array.IndexOf(data.Columns,name) is var index && index>=0&&index<row.Length&&row[index].Contains(term,StringComparison.CurrentCultureIgnoreCase))))table.Rows.Add(row.Cast<object>().ToArray());
        grid.DataSource=table;count.Text=$"{table.Rows.Count} / {data.Rows.Length} bản ghi đã tải";details.Enabled=!busy&&grid.Rows.Count>0;
    }
    /// <summary>Đọc đúng dòng đang chọn; chi tiết chỉ xem, không phát hành/in/thu tiền ngầm.</summary>
    private async Task ShowDetails()
    {
        if(grid.CurrentRow?.DataBoundItem is not DataRowView row||data is null)return;
        if(invoice)
        {
            if(busy)return;busy=true;details.Enabled=false;
            try
            {
                // Giữ hóa đơn gốc, chỉ đọc bổ sung thông tin nhận món/thanh toán theo đúng quyền Staff.
                var order=await LiveSession.Api.GetStaffOrderAsync(Convert.ToInt64(row["MaDonHang"],CultureInfo.InvariantCulture));
                var issued=DateTime.TryParseExact(Convert.ToString(row["PhatHanhLuc"]),"dd/MM/yyyy HH:mm",CultureInfo.GetCultureInfo("vi-VN"),DateTimeStyles.AssumeLocal,out var instant)?instant.ToUniversalTime():(DateTime?)null;
                var view=new AdminInvoiceView(Convert.ToInt64(row["MaHoaDon"],CultureInfo.InvariantCulture),Convert.ToString(row["SoHoaDon"])??"",order.Don.MaDonHang,
                    Convert.ToString(row["LoaiHoaDon"])??"ORIGINAL",null,null,Convert.ToString(row["TenNguoiMua"]),
                    decimal.Parse(Convert.ToString(row["SoTien"])??"0",CultureInfo.InvariantCulture),Convert.ToString(row["TrangThai"])??"",issued,null,[]);
                using var invoiceForm=new InvoiceDetailsForm(view,Convert.ToString(row["NoiDungHoaDonDaChotJson"])??"",order);
                invoiceForm.ShowDialog(FindForm());
            }
            catch(CafeApiException error){status.Text=error.Message;}
            finally{busy=false;if(!details.IsDisposed)details.Enabled=grid.Rows.Count>0;}
            return;
        }
        var title=Convert.ToString(row[invoice?"SoHoaDon":"MaDonHienThi"])??"Chi tiết";
        using var form=AdminUi.Dialog((invoice?"Hóa đơn ":"Đơn hàng ")+title,940,700);
        var fields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(8)};
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,62));
        var index=0;
        foreach(var name in data.Columns.Where(x=>x!="NoiDungHoaDonDaChotJson"))
        {
            var value=Convert.ToString(row[name])??"";
            if(name is "TrangThai" or "HinhThucNhanMon" or "LoaiHoaDon")value=OperationsPresentation.Label(value);
            var label=Ui.Label(ColumnCaption(name),9,false,CafeTheme.Muted);var content=Ui.Label(string.IsNullOrEmpty(value)?"—":value,10,true);content.MaximumSize=new Size(510,0);
            label.Margin=content.Margin=new Padding(8,8,8,12);fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));fields.Controls.Add(label,0,index);fields.Controls.Add(content,1,index++);
        }
        Control body=fields;
        AdminUi.Compose(form,Ui.Panel(title,body),Ui.Button("Đóng",()=>form.Close()));form.ShowDialog(FindForm());
    }
}
