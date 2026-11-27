// UC: UC-POS-03, UC-POS-04, UC-ADM-05
// Chức năng: Dùng chung danh sách, bộ lọc, chi tiết và thao tác đơn cho nhân viên/quản lý.
// Cần nhớ: Đọc hàm từ bước kiểm tra dữ liệu đến bước trả kết quả; các quyết định nghiệp vụ do Backend kiểm tra.

using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Shared.Orders;

/// <summary>UC-ADM-05: theo dõi đơn và quyết định yêu cầu hủy; thao tác pha chế thuộc POS.</summary>
public partial class OrdersWorkspacePage : AdminWorkspacePage<AdminOrderRow>
{
    private readonly bool staff;
    private readonly bool cancellations;
    private readonly bool counterOnly;
    private readonly Label selectionStatus = Ui.Label("Chọn đơn để xem trạng thái", 10, true);
    private bool refreshingAutomatically;
    private AdminOrderFilter? appliedFilter;
    private string? boundSnapshot;
    private readonly ComboBox shiftGroup = Ui.Select("Ca này / đơn chờ nhận", "Đơn nhận từ ca trước", "Tất cả ca");
    private readonly ComboBox channel = Ui.Select("Tất cả", "WEB", "POS");
    private readonly ComboBox fulfillment = Ui.Select("Tất cả", "DINE_IN", "TAKEAWAY", "DELIVERY");
    private readonly ComboBox state = Ui.Select("Tất cả", "PENDING_PAYMENT", "PENDING_CONFIRMATION", "ACCEPTED", "PREPARING", "READY", "DELIVERING", "COMPLETED", "CANCELLED", "EXPIRED", "DELIVERY_FAILED");
    private readonly DateTimePicker from = new() { Value = DateTime.Today.AddDays(-30), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 130 };
    private readonly DateTimePicker to = new() { Value = DateTime.Today, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 130 };
    private readonly NumericUpDown shift = new() { Minimum = 0, Maximum = 999999999, Width = 110 };
    private readonly Label pagination = Ui.Label("", 9);
    private readonly Dictionary<string, CommandDraft<OrderActionInput>> drafts = [];
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 3000 };
    private int page = 1;
    private readonly Button nextAction;
    private readonly Button otherActions;
    private readonly Button? rejectCancellation;
    private readonly ContextMenuStrip actionMenu = new();
    private readonly List<OrderUiAction> orderActions;
    private bool commandRunning;
    private sealed record OrderUiAction(string Key, string Caption, Func<Task> Execute, Func<AdminOrderRow,bool> Available);

    /// <summary>Giữ bố cục header/sidebar; lọc ngày không dùng checkbox, phân trang ở máy chủ.</summary>
    protected OrdersWorkspacePage(bool staff, bool cancellations = false, bool counterOnly = false) : base(counterOnly ? "Hoàn tiền tại quầy" : cancellations ? staff ? "Yêu cầu hủy đơn" : "Duyệt yêu cầu hủy" : staff ? "Xử lý đơn hàng" : "Theo dõi đơn hàng", counterOnly ? "Đối chiếu khách nhận • Chi theo hạn mức Quản lý đã duyệt" : cancellations ? staff ? "Gửi yêu cầu và theo dõi kết quả phê duyệt của Quản lý" : "Kiểm tra yêu cầu • Duyệt hoặc từ chối • Theo dõi kết quả" : staff ? "Tiếp nhận đơn • Theo dõi pha chế • Bàn giao cho khách" : "Tra cứu đơn • Duyệt yêu cầu hủy • Theo dõi kết quả", true, counterOnly ? "THU NGÂN / HOÀN TIỀN" : cancellations ? "ĐƠN HÀNG HỦY" : "ĐƠN HÀNG", fitTableToRows: staff)
    {
        this.staff = staff; this.cancellations = cancellations; this.counterOnly = counterOnly;
        Name = cancellations ? "CancelledOrdersWorkspace" : "OrdersWorkspace";
        if (cancellations) { state.Items.Clear(); state.Items.AddRange(["Tất cả", "PENDING_CONFIRMATION", "ACCEPTED", "PREPARING", "CANCELLED"]); state.SelectedIndex = 0; }
        else state.Items.Remove("CANCELLED");
        if (!staff && cancellations) { state.Items.Clear(); state.Items.AddRange(["Tất cả", "PENDING", "APPROVED", "REJECTED"]); state.SelectedIndex = 0; }
        state.Name = "OrderStatusFilter";
        state.Format += (_, e) => e.Value = e.ListItem?.ToString() switch { "PENDING" => "Chờ xử lý", "APPROVED" => "Đã duyệt", "REJECTED" => "Đã từ chối", var code => OperationsPresentation.Label(code ?? "") };
        state.FormattingEnabled = true;
        // Double buffering chỉ tác động vẽ, không can thiệp dữ liệu hoặc cơ chế chọn dòng.
        typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(Grid, true);
        Filters.Controls.Clear();
        BuildOrderFilters();
        AddAction(Filters, "Tìm kiếm", async () => { page = 1; await Reload(); });
        AddAction(Filters, "Tải lại", async () => { SearchBox.Clear(); page = 1; await Reload(); });
        Column("MaDon", "Mã đơn", 75); Column("Kenh", "Nguồn", 50); Column("HinhThuc", "Nhận món", 90);
        Column("TrangThai", cancellations ? "Kết quả xử lý" : "Trạng thái", !staff && cancellations ? 185 : 140);
        if (!staff) { Column("TenKhachHang", "Khách hàng", 130); if (!cancellations) Column("NhanVien", "Nhân viên / ca", 110); }
        else if(!cancellations)Column("NguonCa","Nguồn ca",150);
        if (!cancellations) Column("TienHang", staff ? "Tiền hàng" : "Tiền món", 90);
        else Column("TaiChinh", counterOnly ? "Khoản còn hoàn" : "Đã thanh toán", 100);
        if (!cancellations) Column("PhiGiaoHang", "Phí giao", 75);
        Column("TaoLuc", "Đặt lúc", 140);
        // Chỉ giữ một hành động tiến trình trên thanh chính. Nhân viên dùng menu phụ; Quản lý chỉ có lệnh đúng phạm vi.
        orderActions = [
            new("ACCEPT","Tiếp nhận",()=>Act("ACCEPT"),r=>r.TrangThai=="PENDING_CONFIRMATION"),
            new("CANCEL",staff ? "Gửi yêu cầu hủy" : "Hủy phần chưa pha",()=>Act("CANCEL"),r=>r.TrangThai is "PENDING_CONFIRMATION" or "ACCEPTED" or "PREPARING"),
            new("START","Bắt đầu pha",()=>Act("START"),r=>r.TrangThai is "ACCEPTED" or "PREPARING"),
            new("FINISH","Pha xong",()=>Act("FINISH"),r=>r.TrangThai is "ACCEPTED" or "PREPARING"),
            new("HANDOVER","Bàn giao / hoàn tất",()=>Act(Selected()?.HinhThuc=="DELIVERY" && Selected()?.TrangThai=="READY"?"DELIVER":"COMPLETE"),r=>r.TrangThai=="READY" || r.HinhThuc=="DELIVERY" && r.TrangThai=="DELIVERING"),
            new("RETRY_DELIVERY","Giao lại",()=>Act("RETRY_DELIVERY"),r=>r.HinhThuc=="DELIVERY" && r.TrangThai=="DELIVERING"),
            new("FAIL_DELIVERY","Giao thất bại",()=>Act("FAIL_DELIVERY"),r=>r.HinhThuc=="DELIVERY" && r.TrangThai=="DELIVERING"),
            new("EXPIRE","Chốt hết hạn chờ tiền",()=>Act("EXPIRE"),r=>r.Kenh=="WEB" && r.TrangThai=="PENDING_PAYMENT")
        ];
        // Chỉ hiển thị hành động POS được cấp quyền. API vẫn kiểm tra độc lập, không dựa vào nút ẩn.
        if (staff) orderActions.RemoveAll(x => x.Key is "ADDRESS" or "RETRY_DELIVERY" or "FAIL_DELIVERY" or "EXPIRE");
        if (!staff) orderActions.RemoveAll(x => x.Key != "FAIL_DELIVERY");
        AddCancellationActions();
        if (!(staff && cancellations && !counterOnly)) AddAction(Actions,"Chi tiết",Details);
        else Grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0 && !Busy && !commandRunning) await Details(); };
        nextAction=AddAction(Actions,"Bước tiếp theo",RunNextAction,true); nextAction.Name="OrderNextAction";
        otherActions=Ui.Button("Thao tác khác",ShowOtherActions); otherActions.Name="OrderOtherActions"; otherActions.ContextMenuStrip=actionMenu; Actions.Controls.Add(otherActions);
        if (!staff && cancellations)
        {
            rejectCancellation = AddAction(Actions, "Từ chối yêu cầu hủy", async () =>
            {
                var reject = orderActions.First(x => x.Key == "REJECT");
                if (Selected() is { } row && reject.Available(row)) await RunOrderAction(reject);
            });
            rejectCancellation.Name = "OrderRejectCancellation";
        }
        selectionStatus.Name = "OrderSelectionStatus"; selectionStatus.Margin = new Padding(8, 10, 0, 8);
        selectionStatus.AutoSize = false; selectionStatus.AutoEllipsis = true; selectionStatus.Width = 470; selectionStatus.Height = 28;
        Actions.Controls.Add(selectionStatus);

        Grid.CurrentCellChanged+=(_,_)=>UpdateActionBar();
        Grid.DataBindingComplete+=(_,_)=>UpdateActionBar();
        AddAction(Actions, "Trang trước", async () => { if (page > 1) { page--; await Reload(); } });
        AddAction(Actions, "Trang sau", async () => { page++; await Reload(); }); Actions.Controls.Add(pagination);
        refresh.Tick += async (_, _) => await RefreshOrdersInBackground(); refresh.Start();
        OperationsPresentation.FitRows(Filters); OperationsPresentation.FitRows(Actions); OperationsPresentation.Grid(Grid);
        CommerceWorkspaceVisuals.Apply(Actions, Counter, pagination, Grid);
        ConfigureOrderAppearance();

        if (!staff)
        {
            CommerceWorkspaceVisuals.AdminStyle(Filters, Actions, Grid);
            foreach (DataGridViewColumn column in Grid.Columns)
            {
                // Mã WEB/POS đã chỉ nguồn. Thông tin nhân viên nằm trong tooltip của khách, tránh ép tên dài vào hàng bảng.
                if (column.DataPropertyName is "Kenh" or "NhanVien") column.Visible = false;
                (column.MinimumWidth, column.FillWeight) = column.DataPropertyName switch
                {
                    "MaDon" => (115, 115), "HinhThuc" => (110, 110), "TrangThai" => (150, 150),
                    "TenKhachHang" => (150, 175), "TienHang" => (95, 95), "PhiGiaoHang" => (90, 90),
                    "TaiChinh" => (125, 125), "TaoLuc" => (145, 155), _ => (column.MinimumWidth, column.FillWeight)
                };
                if (column.DataPropertyName == "TenKhachHang") column.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            }
            SearchBox.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter && !Busy) { e.SuppressKeyPress = true; page = 1; await Reload(); } };
            Grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0 && !Busy && !commandRunning) await Details(); };
        }
        Grid.Columns.Cast<DataGridViewColumn>().First(x=>x.DataPropertyName=="MaDon").MinimumWidth=125;
        Grid.CellFormatting+=(_,e)=>
        {
            if(e.ColumnIndex<0 || Grid.Columns[e.ColumnIndex].DataPropertyName!="MaDon" || e.Value is not string code)return;
            if(e.RowIndex>=0)Grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText=code;
            e.Value = code; // Mã ngắn hiển thị đầy đủ, không cắt bằng dấu ba chấm.
        };
        UpdateActionBar();
    }

    /// <summary>Chọn bước chính theo trạng thái đang xem; đây chỉ là gợi ý UI, server vẫn xác thực đủ điều kiện.</summary>
    private OrderUiAction? NextAction(AdminOrderRow? row)
    {
        if(row is null)return null;
        if (row.YeuCauHuy is not null)
            return orderActions.FirstOrDefault(x => x.Key == (staff ? "FORWARD" : "APPROVE") && x.Available(row));
        if (staff && counterOnly && row.TrangThai == "CANCELLED") return orderActions.FirstOrDefault(x => x.Key == "COUNTER_REFUND" && x.Available(row));
        if (!staff) return cancellations ? null : orderActions.FirstOrDefault(x => x.Key == "FAIL_DELIVERY" && x.Available(row));
        var key=row.TrangThai switch {"PENDING_CONFIRMATION"=>"ACCEPT","ACCEPTED"=>"START","PREPARING"=>"FINISH","READY"=>"HANDOVER","DELIVERING" when row.HinhThuc=="DELIVERY"=>"HANDOVER",_=>null};
        return orderActions.FirstOrDefault(x=>x.Key==key && x.Available(row));
    }
    /// <summary>Đơn đã kết thúc không hiện bước xử lý mới; thanh công cụ luôn tối đa ba nút.</summary>
    private void UpdateActionBar()
    {
        var row=Selected();var action=NextAction(row);
        var requestOnly = staff && cancellations && !counterOnly;
        nextAction.Visible=requestOnly || action is not null;
        nextAction.Text=requestOnly ? "Gửi yêu cầu duyệt" : action?.Caption??"Bước tiếp theo";
        otherActions.Visible = staff && !requestOnly && !counterOnly;
        if (rejectCancellation is not null)
        {
            rejectCancellation.Visible = row?.YeuCauHuy?.TrangThai == "FORWARDED";
            rejectCancellation.Enabled = !commandRunning && rejectCancellation.Visible;
        }
        selectionStatus.Text = row is null ? "Chọn một đơn hàng trong danh sách" : OrderStatusCaption(row);
        selectionStatus.ForeColor = row?.TrangThai == "CANCELLED" ? Color.FromArgb(48, 105, 66) : FourChang.Desktop.Shared.Theme.CafeTheme.Muted;
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

    /// <summary>Đọc cùng snapshot đơn qua endpoint đúng vai trò; POS không gọi đường dẫn Admin.</summary>
    private Task<AdminOrderDetail> ReadOrder(long id) => staff ? LiveSession.Api.GetStaffOrderAsync(id) : LiveSession.Api.GetAdminOrderAsync(id);

    /// <summary>Chuyển cận ngày địa phương sang UTC; đến ngày bao gồm cả ngày bằng cận cuối loại trừ.</summary>
    protected override async Task<IReadOnlyList<AdminOrderRow>> Fetch()
    {
        var filter = refreshingAutomatically && appliedFilter is not null ? appliedFilter : new AdminOrderFilter(channel.SelectedIndex == 0 ? null : channel.Text, fulfillment.SelectedIndex == 0 ? null : fulfillment.Text,
            !staff && cancellations || state.SelectedIndex == 0 ? null : state.SelectedItem?.ToString(), from.Value.Date.ToUniversalTime(), to.Value.Date.AddDays(1).ToUniversalTime(), shift.Value == 0 ? null : (long)shift.Value, SearchBox.Text.Trim(), page, NhomDon: counterOnly ? "COUNTER_REFUND" : cancellations ? "CANCELLATION" : "NORMAL", PhamViCa: staff&&!cancellations ? shiftGroup.SelectedIndex switch {0=>"CURRENT",1=>"PREVIOUS",_=>"ALL"}:null, KetQuaHuy: !staff && cancellations && state.SelectedIndex > 0 ? state.SelectedItem?.ToString() : null);
        appliedFilter = filter;
        var result = staff ? await LiveSession.Api.GetStaffOrdersAsync(filter) : await LiveSession.Api.GetAdminOrdersAsync(filter);
        pagination.Text = $"Trang {page} · {result.TongSo} đơn"; return result.DuLieu;
    }
    /// <summary>Tìm tại server; không lọc lại gây khác tổng số phân trang.</summary>
    protected override bool Match(AdminOrderRow item, string query) => true;

    /// <summary>Chỉ hiển thị bốn phần cơ bản; dữ liệu kho và nhật ký vẫn được lưu, không đưa vào hộp thoại này.</summary>
    private async Task Details()
    {
        var selected = Selected(); if (selected is null) return;
        var data = await ReadOrder(selected.MaDonHang);
        using var form = new OrderDetailsForm(data, !staff && cancellations);
        form.ShowDialog(FindForm());
    }

    /// <summary>Xử lý đúng dòng và số ly còn lại; bước pha/hoàn tất không cần gõ lý do, giao thất bại yêu cầu lý do.</summary>
    private async Task Act(string action)
    {
        var selected = Selected(); if (selected is null) return;
        var data = await ReadOrder(selected.MaDonHang); var key = selected.MaDonHang + ":" + action;
        var lineAction = action is "START" or "FINISH" or "CANCEL";
        int Remaining(AdminOrderLine row) => action == "FINISH" ? row.DaPha - row.PhaXong : row.SoLuong - row.DaHuy - row.DaPha;
        var eligible = data.Mon.Where(x => !lineAction || Remaining(x) > 0).ToList();
        if (eligible.Count == 0) { Message.Text = "Không còn số ly phù hợp để xử lý bước này."; return; }
        if (!drafts.TryGetValue(key, out var draft)) drafts[key] = draft = new();
        var title = action switch { "START" => "Bắt đầu pha", "FINISH" => "Xác nhận pha xong", "COMPLETE" => "Hoàn tất đơn hàng", "ACCEPT" => "Tiếp nhận đơn", "DELIVER" => "Bắt đầu giao hàng", "CANCEL" => staff ? "Gửi yêu cầu hủy cho Quản lý" : "Hủy phần chưa pha", _ => OperationsPresentation.Label(action) };
        using var form = new AdminCommandDialog<OrderActionInput>(title + " · " + selected.MaDon, draft);
        ComboBox? line = null; NumericUpDown? count = null; TextBox? reason = null; TextBox? evidence = null; TextBox? resolution = null; CheckBox? reconciled = null;
        if (lineAction)
        {
            line = form.ChoiceField("Món cần xử lý", eligible.Select(x => $"{x.TenMon} · {x.KichCo} · {(x.NhietDo == "HOT" ? "Nóng" : "Lạnh")} · Còn {Remaining(x)} ly").ToArray());
            line.Name = "OrderActionLine";
            count = form.NumberField(action == "START" ? "Số ly bắt đầu pha" : action == "FINISH" ? "Số ly xác nhận pha xong" : "Số ly hủy", action == "CANCEL" ? 1 : Remaining(eligible[0]));
            count.Name = "OrderActionQuantity"; count.Minimum = 1; count.Maximum = Remaining(eligible[0]);
            line.SelectedIndexChanged += (_, _) => { if (line.SelectedIndex < 0) return; count.Maximum = Remaining(eligible[line.SelectedIndex]); count.Value = action == "CANCEL" ? 1 : count.Maximum; };
        }
        if (action is not ("ACCEPT" or "START" or "FINISH" or "DELIVER" or "COMPLETE")) reason = form.TextField("Lý do bắt buộc");
        if (action is "RETRY_DELIVERY" or "EXPIRE") evidence = form.TextField("Chứng cứ / xác nhận của khách");

        if (action == "EXPIRE") reconciled = form.CheckField("Đã đối chiếu sao kê tiền đến hạn");
        if (action is "ACCEPT" or "START" or "FINISH" or "DELIVER" or "COMPLETE")
        {
            form.MinimumSize = new Size(640, 320); form.ClientSize = new Size(730, lineAction ? 440 : 320);
            form.Info(action == "ACCEPT" ? "Xác nhận tiếp nhận đơn hàng để chuẩn bị món cho khách." : action == "DELIVER" ? "Xác nhận đã bàn giao đơn để bắt đầu giao hàng cho khách." : action == "COMPLETE" ? "Xác nhận đã bàn giao đủ món cho khách và hoàn tất đơn hàng." : "Mặc định chọn toàn bộ số ly còn cần xử lý của món đã chọn.");
        }
        form.Submit(op => new(action, reason?.Text ?? "", data.Don.PhienBanDuLieu, op, lineAction ? eligible[line!.SelectedIndex].MaDong : null,
            (int)(count?.Value ?? 0), evidence?.Text, resolution?.Text, reconciled?.Checked ?? false), async input => { if (staff) await LiveSession.Api.StaffOrderActionAsync(selected.MaDonHang, input);
            else await LiveSession.Api.AdminOrderActionAsync(selected.MaDonHang, input); }, async op => { if (staff) await LiveSession.Api.StaffOrderReceiptAsync(op);
            else await LiveSession.Api.AdminOrderReceiptAsync(op); });
        if (form.ShowDialog(FindForm()) == DialogResult.OK)
        {
            await Reload();
            if(staff&&action=="CANCEL")Message.Text="Đã gửi yêu cầu hủy. Chờ Quản lý duyệt; đơn chưa bị hủy.";
        }
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
