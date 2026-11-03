using FourChang.Desktop.Shared.Database;
using System.Data;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Staff;
using FourChang.Presentation.Shared.Preview;

namespace FourChang.Desktop.Admin.Employees;
public static class EmployeesPage
{
    public static Control Create()
    {
        var grid = Ui.Grid(new PreviewTable(["Mã nhân viên", "Họ tên", "Điện thoại", "Ngày vào làm", "Trạng thái"], []), 400);
        var error = Ui.Label("", 9, false, CafeTheme.Danger);
        error.Visible = false;
        List<Employee> records = [];
        var add = Ui.Button("Thêm nhân viên", () =>
        {
        }, true);
        var edit = Ui.Button("Sửa / ngừng làm", () =>
        {
        });
        var reload = Ui.Button("Tải lại", () =>
        {
        });
        var box = Ui.Panel("Hồ sơ nhân viên", Ui.Row(add, edit, reload), error, grid);
        async Task Load()
        {
            try
            {
                records = await LiveSession.Api.GetEmployeesAsync(true);
                if (grid.IsDisposed)
                    return;
                DatabaseViews.SetGrid(grid, new(["Mã nhân viên", "Họ tên", "Điện thoại", "Ngày vào làm", "Trạng thái"], records.Select(x => new[] { x.MaNhanVien.ToString(), x.HoTen, x.SoDienThoai ?? "", x.NgayVaoLam.ToString("dd/MM/yyyy"), x.TrangThai }).ToArray()));
                error.Visible = false;
            }
            catch (CafeApiException ex)
            {
                if (!error.IsDisposed)
                {
                    error.Text = ex.Message;
                    error.Visible = true;
                }
            }
        }

        async Task Save(Employee? current)
        {
            using var form = new Form
            {
                Text = current is null ? "Thêm nhân viên" : "Sửa nhân viên",
                ClientSize = new Size(480, 420),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = CafeTheme.Cream,
                Font = CafeTheme.Body()
            };
            var name = Ui.Input("Họ tên", current?.HoTen ?? "", 400);
            var phone = Ui.Input("Điện thoại", current?.SoDienThoai ?? "", 400);
            var date = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = current?.NgayVaoLam ?? DateTime.Today,
                Width = 400
            };
            var status = Ui.Select("ACTIVE", "INACTIVE");
            status.SelectedItem = current?.TrangThai ?? "ACTIVE";
            var save = Ui.Button("Lưu", () =>
            {
                if (string.IsNullOrWhiteSpace(name.Text))
                {
                    MessageBox.Show(form, "Nhập họ tên.");
                    return;
                }

                form.DialogResult = DialogResult.OK;
            }, true);
            var fields = Ui.Stack(Ui.Field("Họ tên", name), Ui.Field("Điện thoại", phone), Ui.Field("Ngày vào làm", date), Ui.Field("Trạng thái", status), save);
            fields.Padding = new Padding(24);
            form.Controls.Add(fields);
            if (form.ShowDialog(box.FindForm()) != DialogResult.OK)
                return;
            try
            {
                await LiveSession.Api.SaveEmployeeAsync(new(name.Text, phone.Text, date.Value, status.Text, current?.PhienBanDuLieu), current?.MaNhanVien);
                await Load();
            }
            catch (CafeApiException ex)
            {
                error.Text = ex.Message;
                error.Visible = true;
            }
        }

        box.HandleCreated += async (_, _) => await Load();
        reload.Click += async (_, _) => await Load();
        add.Click += async (_, _) => await Save(null);
        edit.Click += async (_, _) =>
        {
            if (grid.CurrentRow is null)
                return;
            var id = Convert.ToInt64(grid.CurrentRow.Cells[0].Value);
            await Save(records.First(x => x.MaNhanVien == id));
        };
        return box;
    }
}
