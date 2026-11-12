using FourChang.Desktop.Shared.Theme;

namespace FourChang.Desktop.Shared.Controls.Admin;

/// <summary>Chỉ trình bày các trang vận hành: nhãn Việt, số/ngày rõ và flow tự tăng chiều cao khi xuống hàng.</summary>
internal static class OperationsPresentation
{
    /// <summary>Flow nhiều bộ lọc phải tính chiều cao sau wrap; không để card chỉ cao một dòng rồi cắt dòng dưới.</summary>
    internal static void FitRows(FlowLayoutPanel flow)
    {
        // Chiều cao phải được tính từ nội dung bởi layout engine; đo lại tọa độ child.Bottom
        // trong Layout gây vòng lặp tăng chiều cao khi đổi DPI, wrap hoặc reparent vào thẻ.
        flow.AutoSize = true;
        flow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        flow.WrapContents = true;
        flow.MinimumSize = Size.Empty;
    }

    /// <summary>Giữ giá trị code trong binding; chỉ đổi cách vẽ nhãn và định dạng tiền/ngày.</summary>
    internal static void Grid(DataGridView grid)
    {
        foreach (DataGridViewColumn column in grid.Columns)
        {
            column.MinimumWidth = Math.Max(65, TextRenderer.MeasureText(column.HeaderText, CafeTheme.Strong(9)).Width + 28);
            if (column.DataPropertyName is "TrangThai" or "HinhThuc" or "PhuongThuc" or "LoaiNguon") column.MinimumWidth = 140;
            if (column.DataPropertyName is "SoTien" or "TienHang" or "PhiGiaoHang" or "YeuCau" or "DuocDuyet")
            { column.DefaultCellStyle.Format = "#,##0.####"; column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; }
            if (column.DataPropertyName is "TaoLuc" or "PhatHanhLuc" or "GhiSoLuc" or "Luc") { column.MinimumWidth = 155; column.DefaultCellStyle.Format = "dd/MM/yyyy HH:mm"; }
        }
        grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex < 0 || e.Value is not string code) return;
            if (grid.Columns[e.ColumnIndex].DataPropertyName is "TrangThai" or "HinhThuc" or "PhuongThuc" or "LoaiNguon" or "LoaiPhieu" or "Loai")
                e.Value = Label(code);
        };
    }
    /// <summary>Đặt nhãn tiếng Việt cho bảng chi tiết tự sinh, giữ nguyên tên thuộc tính dùng để binding và lấy khóa.</summary>
    internal static void DetailColumns(DataGridView grid)
    {
        var names = new Dictionary<string, string>
        {
            ["MaNguyenLieu"]="Mã nguyên liệu", ["NguyenLieu"]="Nguyên liệu", ["DonVi"]="Đơn vị", ["SoLuong"]="Số lượng",
            ["DonGiaNhap"]="Đơn giá nhập", ["TonSoDaChot"]="Tồn sổ đã chốt", ["MaDong"]="Mã dòng", ["Ten"]="Nguyên liệu",
            ["DinhMucMoiLy"]="Định mức / ly", ["DaGiu"]="Đã giữ", ["TieuHao"]="Đã tiêu hao", ["GiaiPhong"]="Đã giải phóng",
            ["TenMon"]="Tên món", ["KichCo"]="Kích cỡ", ["NhietDo"]="Nóng / lạnh", ["Duong"]="Đường (%)", ["Da"]="Đá (%)",
            ["GhiChu"]="Ghi chú", ["DaPha"]="Bắt đầu pha", ["PhaXong"]="Pha xong", ["DaHuy"]="Đã hủy", ["DonGia"]="Đơn giá",
            ["MonThem"]="Topping", ["TrangThai"]="Trạng thái", ["HanhDong"]="Thao tác", ["LyDo"]="Lý do", ["Nguoi"]="Người thực hiện",
            ["Luc"]="Thời điểm", ["MaCa"]="Mã ca", ["MaMay"]="Mã máy", ["KetQua"]="Kết quả", ["Truoc"]="Trước", ["Sau"]="Sau"
        };
        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (column.DataPropertyName == "PhienBanDuLieu") column.Visible = false;
            if (names.TryGetValue(column.DataPropertyName, out var caption)) column.HeaderText = caption;
        }
    }

    /// <summary>Thống nhất từ ngữ giữa đơn, kho, hoàn và hóa đơn, không đổi mã lưu trong database.</summary>
    internal static string Label(string code) => code switch
    {
        "DRAFT" => "Nháp", "POSTED" => "Đã ghi sổ", "RECEIPT" => "Nhập kho", "ISSUE" => "Xuất kho", "COUNT" => "Kiểm kê", "ADJUSTMENT" => "Điều chỉnh", "REVERSAL" => "Đảo phiếu",
        "PENDING_PAYMENT" => "Chờ thanh toán", "PENDING_CONFIRMATION" => "Chờ xác nhận", "ACCEPTED" => "Đã tiếp nhận", "PREPARING" => "Đang pha", "READY" => "Sẵn sàng",
        "DELIVERING" => "Đang giao", "COMPLETED" => "Hoàn tất", "CANCELLED" => "Đã hủy", "EXPIRED" => "Hết hạn", "DELIVERY_FAILED" => "Giao thất bại",
        "CASH" => "Tiền mặt", "BANK_TRANSFER" => "Chuyển khoản", "TAKEAWAY" => "Mang đi", "DINE_IN" => "Tại bàn", "DELIVERY" => "Giao tận nơi",
        "REQUESTED" => "Chờ duyệt", "APPROVED" => "Đã duyệt", "REJECTED" => "Từ chối", "PROCESSING" => "Đang chi", "RECONCILING" => "Đang đối soát", "PARTIALLY_REFUNDED" => "Đã hoàn một phần", "REFUNDED" => "Đã hoàn",
        "CANCELLATION" => "Hủy món", "OVERPAYMENT" => "Tiền thừa", "LATE_PAYMENT" => "Tiền đến muộn", "COMPLAINT" => "Khiếu nại", "LEGACY" => "Cũ: cần đối soát", "ORIGINAL" => "Hóa đơn gốc", "ISSUED" => "Đã phát hành", _ => code
    };
}
