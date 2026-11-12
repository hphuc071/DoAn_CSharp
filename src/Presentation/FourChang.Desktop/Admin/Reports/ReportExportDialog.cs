using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Reports;

/// <summary>Tiến độ thật của job phía server; đóng dialog không phát hành lại hoặc làm file dở thành thành công.</summary>
public sealed class ReportExportDialog : Form
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly Label status = Ui.Label("", 10);
    private readonly ProgressBar progress = new() { Width = 600, Height = 18 };
    private readonly Button save;
    private ReportExportJob job;

    /// <summary>Tách tạo file, tải file và lưu file; hủy SaveDialog không ghi thành công giả.</summary>
    public ReportExportDialog(ReportExportJob job)
    {
        this.job = job; Text = "Xuất " + job.DinhDang; ClientSize = new(700, 330); MinimumSize = new(620, 300); Font = CafeTheme.Body(); BackColor = CafeTheme.Cream; AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        save = Ui.Button("Lưu tệp hoàn chỉnh", () => { }, true); save.Enabled = false; save.Click += async (_, _) => await Save();
        var cancel = Ui.Button("Hủy tác vụ", () => { }); cancel.Click += async (_, _) =>
        { try { this.job = await LiveSession.Api.CancelReportExportAsync(job.MaCongViec); status.Text = "Đã yêu cầu hủy tác vụ."; save.Enabled = false; lifetime.Cancel(); } catch (CafeApiException ex) { status.Text = ex.Message; } };
        Controls.Add(Ui.Panel("Xuất theo mốc " + job.MocDuLieu.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"), status, progress, Ui.Row(save, cancel, Ui.Button("Đóng", () => Close()))));
        Shown += async (_, _) => await Poll(); FormClosed += (_, _) => lifetime.Cancel();
    }
    /// <summary>Chỉ bật lưu khi READY; mất mạng giữ job cũ, không tạo tác vụ khác.</summary>
    private async Task Poll()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                try
                {
                    job = await LiveSession.Api.ReportExportStatusAsync(job.MaCongViec); if (IsDisposed) return;
                    progress.Value = Math.Clamp(job.TienDo, 0, 100); status.Text = job.TrangThai + (job.Loi is null ? "" : " · " + job.Loi);
                    save.Enabled = job.TrangThai == "READY";
                    if (job.TrangThai is "READY" or "FAILED" or "CANCELLED") return;
                }
                catch (CafeApiException ex) { if (IsDisposed) return; status.Text = ex.Message + " · Đang giữ mã job để nối lại."; }
                await Task.Delay(1000, lifetime.Token);
            }
        }
        catch (OperationCanceledException) { }
    }
    /// <summary>Server kiểm tra lại quyền khi tải, file được ghi temp rồi đổi tên nguyên tử.</summary>
    private async Task Save()
    {
        using var dialog = new SaveFileDialog { Filter = job.DinhDang == "PDF" ? "PDF (*.pdf)|*.pdf" : "Excel (*.xlsx)|*.xlsx", FileName = "4CHANG-" + job.MaCongViec.ToString("N") + (job.DinhDang == "PDF" ? ".pdf" : ".xlsx") };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        save.Enabled = false;
        try
        {
            var bytes = await LiveSession.Api.GetReportFileAsync(job.MaCongViec, job.DinhDang == "PDF", lifetime.Token);
            var temp = dialog.FileName + ".tmp"; await File.WriteAllBytesAsync(temp, bytes, lifetime.Token); File.Move(temp, dialog.FileName, true);
            status.Text = "Đã lưu tệp hoàn chỉnh đúng snapshot.";
        }
        catch (CafeApiException ex) { status.Text = ex.Message; }
        catch (IOException) { status.Text = "Không ghi được tệp. Chọn nơi lưu khác và thử lại tải cùng job."; }
        catch (OperationCanceledException) { }
        finally { if (!save.IsDisposed) save.Enabled = job.TrangThai == "READY"; }
    }
    /// <summary>Dừng polling khi cửa sổ đóng, không xóa tệp hoặc nghiệp vụ của job khác.</summary>
    protected override void Dispose(bool disposing) { if (disposing) { lifetime.Cancel(); lifetime.Dispose(); } base.Dispose(disposing); }
}
