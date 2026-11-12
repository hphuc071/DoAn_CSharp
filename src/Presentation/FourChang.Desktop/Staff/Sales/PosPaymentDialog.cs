using System.Diagnostics;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Staff;
namespace FourChang.Desktop.Staff.Sales;

/// <summary>Hiển thị QR thử nghiệm và đọc trạng thái server; timer không tự ghi thanh toán thành công.</summary>
public sealed class PosPaymentDialog : Form
{
    private readonly System.Windows.Forms.Timer timer=new(){Interval=2000};
    private readonly long orderId;
    private readonly Label status=Ui.Label("Đang chờ xác nhận mô phỏng…",10);
    private bool reading;
    public PosCheckoutReceipt? Receipt { get;private set; }
    /// <summary>QR chỉ được tạo từ server; trang xác nhận ghi rõ mô phỏng không thu tiền thật.</summary>
    public PosPaymentDialog(long id,SimulationQr qr)
    {
        orderId=id;Name="PosSimulationPayment";Text="Thanh toán mô phỏng · 4CHANG";ClientSize=new(620,650);Font=CafeTheme.Body();BackColor=CafeTheme.Cream;StartPosition=FormStartPosition.CenterParent;
        var bytes=Convert.FromBase64String(qr.PngDataUri[(qr.PngDataUri.IndexOf(',')+1)..]);using var stream=new MemoryStream(bytes);using var image=Image.FromStream(stream);
        var picture=new PictureBox{Image=new Bitmap(image),SizeMode=PictureBoxSizeMode.Zoom,Width=300,Height=300};
        var open=Ui.Button("Mở trang xác nhận mô phỏng",()=>Process.Start(new ProcessStartInfo(qr.Url){UseShellExecute=true}));
        Controls.Add(Ui.Panel("MÔ PHỎNG — KHÔNG CHUYỂN TIỀN THẬT",Ui.Label($"Số tiền: {qr.SoTien:N0}đ",16,true),picture,
            Ui.Label("Quét QR hoặc mở trang xác nhận thử nghiệm. Thành công được đọc lại từ máy chủ.",9),status,
            Ui.Row(open,Ui.Button("Đóng",()=>Close()))));
        timer.Tick+=async(_,_)=>await Poll();Shown+=async(_,_)=>{timer.Start();await Poll();};
        FormClosed+=(_,_)=>{timer.Stop();timer.Dispose();picture.Image?.Dispose();};
    }
    /// <summary>Đọc trạng thái đơn đúng ca; mất mạng không đổi thành đã thu và vẫn giữ đơn chờ.</summary>
    private async Task Poll()
    {
        if(reading||IsDisposed)return;reading=true;
        try
        {
            var result=await LiveSession.Api.GetPosCheckoutAsync(orderId);if(IsDisposed)return;
            if(result.TrangThaiThanhToan=="VERIFIED"){Receipt=result;timer.Stop();DialogResult=DialogResult.OK;Close();}
        }
        catch(FourChang.Presentation.Shared.ApiClients.CafeApiException ex){if(!IsDisposed)status.Text=ex.Message;}
        finally{reading=false;}
    }
}
