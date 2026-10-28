using MocCafe.Desktop.DesignSystem;
using System.Drawing.Drawing2D;
namespace MocCafe.Desktop.Controls;

public sealed class RevenueChart : Control
{
    public RevenueChart() { Height=220;Dock=DockStyle.Top;DoubleBuffered=true;BackColor=CafeTheme.Surface; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        int left=44, top=15, bottom=Height-34, right=Width-15;
        using var grid=new Pen(CafeTheme.Line);using var muted=new SolidBrush(CafeTheme.Muted);using var coffee=new SolidBrush(CafeTheme.Coffee);using var cream=new SolidBrush(Color.FromArgb(212,190,164));using var font=CafeTheme.Body(8);
        for(int i=0;i<4;i++) {int y=top+(bottom-top)*i/3;e.Graphics.DrawLine(grid,left,y,right,y);e.Graphics.DrawString((3-i)*500+"K",font,muted,0,y-5);}
        int[] cash=[20,32,48,40,68,82,110,96,70];int[] bank=[12,25,33,40,48,38,50,42,30];
        float slot=(right-left)/9f;for(int i=0;i<9;i++){float x=left+i*slot+slot*.2f,w=slot*.58f,h1=cash[i]*(bottom-top)/170f,h2=bank[i]*(bottom-top)/170f;e.Graphics.FillRectangle(coffee,x,bottom-h1,w,h1);e.Graphics.FillRectangle(cream,x,bottom-h1-h2,w,h2);e.Graphics.DrawString((7+i*2)+"h",font,muted,x,bottom+10);}
    }
}
