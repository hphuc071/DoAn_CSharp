using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Orders;

/// <summary>UC-ADM-07: giám sát đa kênh, truy vết và xử lý bằng lệnh server chung với POS.</summary>
public sealed class OrdersManagementPage : AdminWorkspacePage<AdminOrderRow>
{
    private readonly ComboBox channel = Ui.Select("Tất cả", "WEB", "POS");
    private readonly ComboBox fulfillment = Ui.Select("Tất cả", "DINE_IN", "TAKEAWAY", "DELIVERY");
    private readonly ComboBox state = Ui.Select("Tất cả", "PENDING_PAYMENT", "PENDING_CONFIRMATION", "ACCEPTED", "PREPARING", "READY", "DELIVERING", "COMPLETED", "CANCELLED", "EXPIRED", "DELIVERY_FAILED");
    private readonly DateTimePicker from = new() { Value = DateTime.Today.AddDays(-30), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 130 };
    private readonly DateTimePicker to = new() { Value = DateTime.Today, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 130 };
    private readonly NumericUpDown shift = new() { Minimum = 0, Maximum = 999999999, Width = 110 };
    private readonly Label pagination = Ui.Label("", 9);
    private readonly Dictionary<string, CommandDraft<OrderActionInput>> drafts = [];
    private readonly Dictionary<long, CommandDraft<OrderAddressInput>> addressDrafts = [];
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 3000 };
    private int page = 1;
    private readonly Button nextAction;
    private readonly Button otherActions;
    private readonly ContextMenuStrip actionMenu = new();
    private readonly List<OrderUiAction> orderActions;
    private bool commandRunning;
    private sealed record OrderUiAction(string Key, string Caption, Func<Task> Execute, Func<AdminOrderRow,bool> Available);

    /// <summary>Giữ bố cục header/sidebar; lọc ngày không dùng checkbox, phân trang ở máy chủ.</summary>
    public OrdersManagementPage() : base("Giám sát đơn hàng", "Đa kênh • Tiền và nguyên liệu đã chốt • Mọi quyết định có lý do", true, "ĐƠN HÀNG")
    {
        Filters.Controls.Clear();
        Filters.Controls.AddRange([Ui.Field("Tìm kiếm", SearchBox), Ui.Field("Nguồn", channel), Ui.Field("Nhận món", fulfillment), Ui.Field("Trạng thái", state),
            Ui.Field("Từ ngày", from), Ui.Field("Đến ngày", to), Ui.Field("Mã ca (0: tất cả)", shift)]);
        AddAction(Filters, "Tìm kiếm", async () => { page = 1; await Reload(); });
        AddAction(Filters, "Tải lại", async () => { SearchBox.Clear(); page = 1; await Reload(); });
        Column("MaDon", "Mã đơn", 110); Column("Kenh", "Nguồn", 50); Column("HinhThuc", "Nhận món", 90);
        Column("TrangThai", "Trạng thái", 100); Column("NhanVien", "Nhân viên / ca", 110); Column("CanhBao", "Cảnh báo", 130);
        Column("TienHang", "Tiền hàng", 90); Column("PhiGiaoHang", "Phí giao", 75); Column("TaoLuc", "Đặt lúc", 110);
        // Chỉ giữ một hành động tiến trình trên thanh chính. Toàn bộ handler cũ được dùng lại trong menu.
        orderActions = [
            new("ACCEPT","Tiếp nhận",()=>Act("ACCEPT"),r=>r.TrangThai=="PENDING_CONFIRMATION"),
            new("CANCEL","Hủy phần chưa pha",()=>Act("CANCEL"),r=>r.TrangThai is "PENDING_CONFIRMATION" or "ACCEPTED" or "PREPARING"),
            new("START","Bắt đầu pha",()=>Act("START"),r=>r.TrangThai is "ACCEPTED" or "PREPARING"),
            new("FINISH","Pha xong",()=>Act("FINISH"),r=>r.TrangThai is "ACCEPTED" or "PREPARING"),
            new("HANDOVER","Bàn giao / hoàn tất",()=>Act(Selected()?.HinhThuc=="DELIVERY" && Selected()?.TrangThai=="READY"?"DELIVER":"COMPLETE"),r=>r.TrangThai=="READY" || r.HinhThuc=="DELIVERY" && r.TrangThai=="DELIVERING"),
            new("ADDRESS","Đổi địa chỉ",Address,r=>r.HinhThuc=="DELIVERY" && r.TrangThai is not ("COMPLETED" or "CANCELLED" or "REJECTED" or "EXPIRED")),
            new("RETRY_DELIVERY","Giao lại",()=>Act("RETRY_DELIVERY"),r=>r.HinhThuc=="DELIVERY" && r.TrangThai=="DELIVERING"),
            new("FAIL_DELIVERY","Giao thất bại",()=>Act("FAIL_DELIVERY"),r=>r.HinhThuc=="DELIVERY" && r.TrangThai=="DELIVERING"),
            new("EXPIRE","Chốt hết hạn chờ tiền",()=>Act("EXPIRE"),r=>r.Kenh=="WEB" && r.TrangThai=="PENDING_PAYMENT")
        ];
        AddAction(Actions,"Chi tiết",Details);
        nextAction=AddAction(Actions,"Bước tiếp theo",RunNextAction,true); nextAction.Name="OrderNextAction";
        otherActions=Ui.Button("Thao tác khác",ShowOtherActions); otherActions.Name="OrderOtherActions"; otherActions.ContextMenuStrip=actionMenu; Actions.Controls.Add(otherActions);
        Grid.SelectionChanged+=(_,_)=>UpdateActionBar();
        Grid.DataBindingComplete+=(_,_)=>UpdateActionBar();
        AddAction(Actions, "Trang trước", async () => { if (page > 1) { page--; await Reload(); } });
        AddAction(Actions, "Trang sau", async () => { page++; await Reload(); }); Actions.Controls.Add(pagination);
        refresh.Tick += async (_, _) => { if (FindForm()?.OwnedForms.Length == 0) await Reload(); }; refresh.Start();
        OperationsPresentation.FitRows(Filters); OperationsPresentation.FitRows(Actions); OperationsPresentation.Grid(Grid);
        CommerceWorkspaceVisuals.Apply(Actions, Counter, pagination, Grid);
        Grid.Columns.Cast<DataGridViewColumn>().First(x=>x.DataPropertyName=="MaDon").MinimumWidth=175;
        Grid.CellFormatting+=(_,e)=>
        {
            if(e.ColumnIndex<0 || Grid.Columns[e.ColumnIndex].DataPropertyName!="MaDon" || e.Value is not string code)return;
            if(e.RowIndex>=0)Grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText=code;
            if(code.Length>23)e.Value=code[..4]+"…"+code[^10..];
        };
        UpdateActionBar();
    }

    /// <summary>Chọn bước chính theo trạng thái đang xem; đây chỉ là gợi ý UI, server vẫn xác thực đủ điều kiện.</summary>
    private OrderUiAction? NextAction(AdminOrderRow? row)
    {
        if(row is null)return null;
        var key=row.TrangThai switch {"PENDING_CONFIRMATION"=>"ACCEPT","ACCEPTED"=>"START","PREPARING"=>"FINISH","READY"=>"HANDOVER","DELIVERING" when row.HinhThuc=="DELIVERY"=>"HANDOVER",_=>null};
        return orderActions.FirstOrDefault(x=>x.Key==key);
    }
    /// <summary>Đơn đã kết thúc không hiện bước xử lý mới; thanh công cụ luôn tối đa ba nút.</summary>
    private void UpdateActionBar()
    {
        var row=Selected();var action=NextAction(row);
        nextAction.Visible=action is not null;
        nextAction.Text=action?.Caption??"Bước tiếp theo";
        nextAction.Enabled=action is not null&&!commandRunning;
        otherActions.Enabled=!commandRunning&&row is not null&&orderActions.Any(x=>x.Available(row)&&x.Key!=action?.Key);
    }
    /// <summary>Gọi lại handler nghiệp vụ hiện có, không tự bỏ qua dialog xác nhận hoặc điều kiện máy chủ.</summary>
    private async Task RunNextAction()
    {
        if(NextAction(Selected()) is { } action)await RunOrderAction(action);
    }
    /// <summary>Menu chỉ hiện khi cần dùng, gom thao tác phụ mà không xóa chức năng cũ.</summary>
    private void ShowOtherActions()
    {
        if(Selected() is not { } row||commandRunning)return;
        foreach(ToolStripItem item in actionMenu.Items.Cast<ToolStripItem>().ToArray())item.Dispose();
        actionMenu.Items.Clear();var primary=NextAction(row);
        foreach(var action in orderActions.Where(x=>x.Available(row)&&x.Key!=primary?.Key))
        {
            var item=new ToolStripMenuItem(action.Caption);
            item.Click+=async(_,_)=>
            {
                try{await RunOrderAction(action);}
                catch(FourChang.Presentation.Shared.ApiClients.CafeApiException error){AdminUi.Error(this,error);}
            };
            actionMenu.Items.Add(item);
        }
        actionMenu.Font=FourChang.Desktop.Shared.Theme.CafeTheme.Body();
        actionMenu.Show(otherActions,new Point(0,otherActions.Height));
    }
    /// <summary>Chặn bấm lặp trong lúc tải/dialog; không thay mã chống trùng hoặc payload của luồng hiện có.</summary>
    private async Task RunOrderAction(OrderUiAction action)
    {
        if(commandRunning)return;commandRunning=true;UpdateActionBar();
        try{await action.Execute();}
        finally{commandRunning=false;if(!IsDisposed)UpdateActionBar();}
    }

    /// <summary>Chuyển cận ngày địa phương sang UTC; đến ngày bao gồm cả ngày bằng cận cuối loại trừ.</summary>
    protected override async Task<IReadOnlyList<AdminOrderRow>> Fetch()
    {
        var result = await LiveSession.Api.GetAdminOrdersAsync(new(channel.SelectedIndex == 0 ? null : channel.Text, fulfillment.SelectedIndex == 0 ? null : fulfillment.Text,
            state.SelectedIndex == 0 ? null : state.Text, from.Value.Date.ToUniversalTime(), to.Value.Date.AddDays(1).ToUniversalTime(), shift.Value == 0 ? null : (long)shift.Value, SearchBox.Text.Trim(), page));
        pagination.Text = $"Trang {page} · {result.TongSo} đơn · {result.DocLuc.ToLocalTime():HH:mm:ss}"; return result.DuLieu;
    }
    /// <summary>Tìm tại server; không lọc lại gây khác tổng số phân trang.</summary>
    protected override bool Match(AdminOrderRow item, string query) => true;

    /// <summary>Hiển thị tùy chọn và nguyên liệu snapshot, thu/hoàn và lịch sử, không lấy giá menu mới.</summary>
    private async Task Details()
    {
        var selected = Selected(); if (selected is null) return;
        var data = await LiveSession.Api.GetAdminOrderAsync(selected.MaDonHang);
        using var form = AdminUi.Dialog("Đơn " + selected.MaDon, 1140, 760);
        form.Controls.Add(Ui.Panel("Đơn " + selected.MaDon, Ui.Label($"Nghĩa vụ: {data.Don.TaiChinh.NghiaVu:N0}đ · Đã thu: {data.Don.TaiChinh.DaThu:N0}đ · Đã hoàn: {data.Don.TaiChinh.DaHoan:N0}đ"),
            Ui.Label("Địa chỉ: " + (data.DiaChi ?? "Không giao tận nơi")), Ui.Tabs(("Món và tùy chọn", DataGrid(data.Mon)), ("Định mức và giữ chỗ", DataGrid(data.DinhMuc ?? [])),
                ("Tiêu hao", DataGrid(data.BienDong)), ("Lịch sử", DataGrid(data.LichSu)))));
        form.ShowDialog(FindForm());
    }

    /// <summary>Hành động có lý do/version/mã lệnh; hủy hoặc pha cần chỉ rõ dòng và số ly.</summary>
    private async Task Act(string action)
    {
        var selected = Selected(); if (selected is null) return;
        var data = await LiveSession.Api.GetAdminOrderAsync(selected.MaDonHang); var key = selected.MaDonHang + ":" + action;
        if (data.Mon.Count == 0) { Message.Text = "Đơn chưa có dòng món để xử lý."; return; }
        if (!drafts.TryGetValue(key, out var draft)) drafts[key] = draft = new();
        using var form = new AdminCommandDialog<OrderActionInput>("Xử lý đơn " + selected.MaDon + " · " + action, draft);
        var line = form.ChoiceField("Dòng món", data.Mon.Select(x => $"{x.MaDong} · {x.TenMon} · Chưa pha: {x.SoLuong - x.DaPha - x.DaHuy}").ToArray());
        var count = form.NumberField("Số ly", 1); var reason = form.TextField("Lý do bắt buộc");
        var evidence = form.TextField("Chứng cứ / xác nhận của khách (ngoại lệ giao)"); var resolution = form.TextField("Mã xử lý khi giao thất bại", max: 30);
        var reconciled = form.CheckField("Đã đối chiếu sao kê tiền đến hạn (bắt buộc khi chốt hết hạn)");
        line.Enabled = count.Enabled = action is "CANCEL" or "START" or "FINISH";
        form.Submit(op => new(action, reason.Text, data.Don.PhienBanDuLieu, op, data.Mon.Count == 0 ? null : data.Mon[line.SelectedIndex].MaDong,
            (int)count.Value, evidence.Text, resolution.Text, reconciled.Checked), async input => { await LiveSession.Api.AdminOrderActionAsync(selected.MaDonHang, input); }, async op => { await LiveSession.Api.AdminOrderReceiptAsync(op); });
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }

    /// <summary>Đổi địa chỉ chỉ với chấp thuận rõ và chứng cứ, không thu thêm hoặc tự lùi trạng thái.</summary>
    private async Task Address()
    {
        var selected = Selected(); if (selected is null) return;
        var data = await LiveSession.Api.GetAdminOrderAsync(selected.MaDonHang);
        if (!addressDrafts.TryGetValue(selected.MaDonHang, out var addressDraft)) addressDrafts[selected.MaDonHang] = addressDraft = new();
        using var form = new AdminCommandDialog<OrderAddressInput>("Điều chỉnh địa chỉ giao", addressDraft);
        var address = form.TextField("Địa chỉ đề nghị", data.DiaChi, 500);
        var latitude = form.TextField("Vĩ độ (dấu chấm thập phân)"); var longitude = form.TextField("Kinh độ (dấu chấm thập phân)");
        var distance = form.NumberField("Khoảng cách đã đối chiếu (km)", decimals: 3);
        var customer = form.CheckField("Khách đã đồng ý địa chỉ/phương án giao"); var admin = form.CheckField("Quản lý xác nhận quán có thể giao, giữ phí đã chốt");
        var reason = form.TextField("Lý do"); var evidence = form.TextField("Tham chiếu chứng cứ xác nhận");
        form.Submit(op => new(address.Text, decimal.Parse(latitude.Text, System.Globalization.CultureInfo.InvariantCulture), decimal.Parse(longitude.Text, System.Globalization.CultureInfo.InvariantCulture),
            distance.Value, customer.Checked, admin.Checked, reason.Text, evidence.Text, data.Don.PhienBanDuLieu, op),
            async input => { await LiveSession.Api.AdminOrderAddressAsync(selected.MaDonHang, input); }, async op => { await LiveSession.Api.AdminOrderReceiptAsync(op); });
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }

    /// <summary>Bảng chi tiết chỉ đọc; không hiển thị byte phiên bản như một cột dữ liệu nghiệp vụ.</summary>
    internal static Control DataGrid<T>(IEnumerable<T> data)
    {
        var grid = Ui.Grid(new([], []), 460); grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        grid.DataBindingComplete += (_, _) => OperationsPresentation.DetailColumns(grid);
        OperationsPresentation.Grid(grid);
        grid.DataSource = data.ToList();
        return grid;
    }
    /// <summary>Dừng polling khi rời màn hình, không để timer giữ trang đã dispose.</summary>
    protected override void Dispose(bool disposing) { if (disposing) { refresh.Stop(); refresh.Dispose(); actionMenu.Dispose(); } base.Dispose(disposing); }
}
