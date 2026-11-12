using System.Drawing;

namespace FourChang.Desktop.Shared.Controls;

/// <summary>Flow đo theo chiều rộng vùng chứa để thẻ tự cao đúng số hàng sau khi wrap, không giữ khoảng trắng cũ.</summary>
internal sealed class ContentFlowPanel : FlowLayoutPanel
{
    /// <summary>Giới hạn chiều rộng phép đo; đề xuất vô hạn của Panel AutoSize không được làm mất hàng thứ hai.</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var available = Parent is null ? Width : Parent.ClientSize.Width - Parent.Padding.Horizontal - Margin.Horizontal;
        if (available <= 0) return base.GetPreferredSize(proposedSize);
        var requested = proposedSize.Width > 0 && proposedSize.Width < int.MaxValue ? Math.Min(proposedSize.Width, available) : available;
        return base.GetPreferredSize(new Size(Math.Max(1, requested), 0));
    }
}
