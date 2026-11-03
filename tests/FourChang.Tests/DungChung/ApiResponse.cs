using System.Text.Json.Nodes;

namespace FourChang.Tests.Shared;

public sealed record ApiResponse(int Status, string Content)
{
    public JsonNode Json => JsonNode.Parse(Content) ?? throw new InvalidOperationException("Thiếu JSON phản hồi.");

    /// <summary>
    /// Đối chiếu mã HTTP thực tế với mong đợi; không đưa body có thể chứa bí mật vào thông báo lỗi.
    /// </summary>
    public ApiResponse Expect(int expected, string purpose)
    {
        // Chỉ ghi mã HTTP và tên tình huống, không in body có thể chứa token/mật khẩu.
        Assert.True(Status == expected, $"{purpose}: cần HTTP {expected}, nhận HTTP {Status}.");
        return this;
    }
}
