using System.Text.Json.Nodes;

namespace FourChang.Tests.Shared;

// Tiện ích dùng chung, không chứa kịch bản UC. Mọi kiểm tra HTTP/SQL đều chạy bằng C#.
public static class TestData
{
    private static int phoneSequence = Random.Shared.Next(10_000_000, 80_000_000);
    /// <summary>Tạo số giả lập 10 chữ số khác nhau trong cùng tiến trình để kiểm tra ràng buộc bắt buộc/duy nhất.</summary>
    public static string Phone() => "09" + Interlocked.Increment(ref phoneSequence).ToString("D8");

    /// <summary>
    /// Tạo mã thao tác mới cho một yêu cầu độc lập; khi kiểm tra gửi lặp phải giữ lại mã đã tạo.
    /// </summary>
    public static string NewId() => Guid.NewGuid().ToString();

    /// <summary>
    /// Sinh mật khẩu chỉ dùng trong dữ liệu kiểm thử để các lần chạy không dùng chung định danh bí mật.
    /// </summary>
    public static string Password() => "Test-" + Guid.NewGuid().ToString("N") + "!";

    /// <summary>
    /// Tạo JSON từ các cặp tên và giá trị; sao chép JsonNode để không dùng chung node giữa hai request.
    /// </summary>
    public static JsonObject Body(params (string Key, object? Value)[] values)
    {
        var result = new JsonObject();
        foreach (var (key, value) in values)
        {
            result[key] = value is JsonNode node ? node.DeepClone() : System.Text.Json.JsonSerializer.SerializeToNode(value);
        }
        return result;
    }

    /// <summary>
    /// Sao chép sâu nội dung JSON để tình huống lỗi có thể sửa dữ liệu mà không làm đổi request gốc.
    /// </summary>
    public static JsonObject Copy(this JsonNode node) => (JsonObject)node.DeepClone();

    /// <summary>
    /// Đọc giá trị chuỗi từ JSON; trả chuỗi rỗng khi trường tùy chọn không có giá trị.
    /// </summary>
    public static string Text(this JsonNode node, string key) => node[key]?.ToString() ?? "";

    /// <summary>
    /// Đọc trường số nguyên trong JSON để đối chiếu mã bản ghi hoặc giá trị mong đợi.
    /// </summary>
    public static long Number(this JsonNode node, string key) => node[key]!.GetValue<long>();

    /// <summary>
    /// Đọc cờ đúng hoặc sai trong JSON để kiểm tra trạng thái nghiệp vụ trả về.
    /// </summary>
    public static bool Flag(this JsonNode node, string key) => node[key]!.GetValue<bool>();

    /// <summary>
    /// Đưa điều kiện kiểm tra về assertion xUnit cùng thông báo rõ mục đích; sai điều kiện làm test thất bại.
    /// </summary>
    public static void Check(bool condition, string purpose) => Assert.True(condition, purpose);
}

public sealed record Customer(JsonObject Input, long Id);
public sealed record Device(JsonObject Input, JsonNode Record, Dictionary<string, string> Headers);
