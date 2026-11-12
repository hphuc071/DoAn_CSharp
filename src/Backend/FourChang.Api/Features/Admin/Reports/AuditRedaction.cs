using System.Text.Json.Nodes;

namespace FourChang.Api.Features.Admin.Reports;

/// <summary>Che bí mật khi tra audit; không sửa nhật ký gốc, không trả rỗng giả nếu JSON hỏng.</summary>
public static class AuditRedaction
{
    /// <summary>Đệ quy theo tên trường, xử lý cả mảng và đối tượng lồng nhau.</summary>
    public static string? Clean(string? json)
    {
        if (json is null) return null;
        try { var node = JsonNode.Parse(json); Visit(node); return node?.ToJsonString(); }
        catch (System.Text.Json.JsonException) { return "[Không hiển thị dữ liệu không đúng JSON]"; }
    }
    /// <summary>Chặn token/password/khóa và dữ liệu tài khoản ngân hàng chưa che trong snapshot cũ.</summary>
    private static void Visit(JsonNode? node)
    {
        if (node is JsonArray array) foreach (var item in array) Visit(item);
        if (node is not JsonObject obj) return;
        foreach (var pair in obj.ToList())
        {
            var key = pair.Key.ToLowerInvariant();
            if (new[] { "password", "matkhau", "token", "secret", "khoathietbi", "mabamkhoa", "taikhoannhanmahoa" }.Any(key.Contains) || key == "taikhoan") obj[pair.Key] = "[ĐÃ CHE]";
            else Visit(pair.Value);
        }
    }
}
