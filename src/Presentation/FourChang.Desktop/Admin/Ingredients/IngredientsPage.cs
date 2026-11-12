using System.Globalization;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Ingredients;

/// <summary>Danh mục nguyên liệu theo hai đơn vị cố định, không còn tác vụ quản lý đơn vị/quy đổi.</summary>
public sealed class IngredientsPage : AdminWorkspacePage<IngredientView>
{
    private IngredientSnapshot snapshot = new([], []);

    /// <summary>Giữ nhóm tìm kiếm và ba hành động; hiển thị đơn vị ngay cạnh tên để đọc ngưỡng đúng.</summary>
    public IngredientsPage() : base("Danh sách nguyên liệu", "Đơn vị cố định Gam (g) / Mililit (ml) · Quản lý ngưỡng và trạng thái", polished: true)
    {
        Column("MaNguyenLieu", "Mã", 38); Column("TenNguyenLieu", "Nguyên liệu", 155);
        Column("MaDonViCoSo", "Đơn vị", 70); Column("TonAnToan", "Tồn an toàn", 90);
        Column("NguongCanhBao", "Ngưỡng cảnh báo", 100); Column("DangSuDung", "Trạng thái", 90);
        Column("SoThamChieu", "Liên kết", 60);
        foreach (var column in Grid.Columns.Cast<DataGridViewColumn>().Where(x => x.DataPropertyName is "TonAnToan" or "NguongCanhBao"))
        { column.DefaultCellStyle.Format = "#,##0.####"; column.DefaultCellStyle.FormatProvider = CultureInfo.GetCultureInfo("vi-VN"); }
        SearchBox.Name = "IngredientSearch";
        AddAction(Actions, "Thêm nguyên liệu", () => Edit(null), true);
        AddAction(Actions, "Sửa nguyên liệu", async () => { if (Selected() is { } item) await Edit(item); });
        AddAction(Actions, "Xóa có điều kiện", Delete);
        Grid.CellFormatting += FormatCell;
    }

    /// <summary>Đọc danh mục và nhãn đơn vị từ API, không khai báo ID riêng trong giao diện.</summary>
    protected override async Task<IReadOnlyList<IngredientView>> Fetch()
    { snapshot = await LiveSession.Api.GetIngredientsAsync(); return snapshot.NguyenLieu; }

    /// <summary>Tìm theo mã hoặc tên, không lấy tên làm khóa ghi dữ liệu.</summary>
    protected override bool Match(IngredientView item, string query) => query.Length == 0 || item.MaNguyenLieu.ToString() == query || item.TenNguyenLieu.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>Chuyển mã đơn vị/trạng thái sang chữ dễ đọc; không thay giá trị gốc của DTO.</summary>
    private void FormatCell(object? sender, DataGridViewCellFormattingEventArgs args)
    {
        if (args.RowIndex < 0 || args.ColumnIndex < 0) return;
        var property = Grid.Columns[args.ColumnIndex].DataPropertyName;
        if (property == "DangSuDung") { args.Value = args.Value is true ? "Đang dùng" : "Ngừng dùng"; args.FormattingApplied = true; }
        if (property == "MaDonViCoSo" && args.Value is long id)
        {
            var unit = snapshot.DonVi.SingleOrDefault(x => x.MaDonVi == id);
            args.Value = unit is null ? "Chưa xác định" : IngredientUnits.IsSupported(unit.KyHieu) ? IngredientUnits.Label(unit.KyHieu) : unit.TenDonVi;
            args.FormattingApplied = true;
        }
    }

    /// <summary>Mở form tạo/sửa; trạng thái ngừng vẫn chỉnh trong form, không bỏ chức năng này.</summary>
    private async Task Edit(IngredientView? item)
    {
        using var form = new IngredientEditorForm(snapshot, item);
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }

    /// <summary>Xác nhận xóa rồi để API kiểm tra lại tham chiếu/version, không tự xóa công thức hoặc số dư.</summary>
    private async Task Delete()
    {
        var item = Selected(); if (item is null) return;
        if (MessageBox.Show(this, $"Xóa nguyên liệu {item.TenNguyenLieu}? Nguyên liệu đã phát sinh chỉ được ngừng khi hợp lệ.", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var operation = Guid.NewGuid(); var input = new AdminDeleteInput(item.PhienBanDuLieu, operation);
        await AdminUi.Recover(() => LiveSession.Api.DeleteIngredientAsync(item.MaNguyenLieu, input), () => LiveSession.Api.AdminReceiptAsync("ingredients", operation));
        await Reload();
    }
}
