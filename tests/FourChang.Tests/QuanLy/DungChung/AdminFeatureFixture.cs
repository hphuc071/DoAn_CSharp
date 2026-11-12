using System.Text.Json;
using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Tests.Admin;

/// <summary>Database GUID riêng cho nhóm test ADM-01..05, không chạy trên database đồ án.</summary>
public sealed class AdminFeatureFixture : IAsyncLifetime
{
    public TestApi Api { get; private set; } = null!;
    public Dictionary<string, string> Admin { get; private set; } = [];

    /// <summary>Khởi tạo SQL/API fixture và xác thực Admin qua endpoint thật.</summary>
    public async Task InitializeAsync() { Api = await TestApi.Start("ADM01-05"); Admin = await Api.AdminLogin(); }
    /// <summary>Dọn API và database test riêng sau khi cả collection hoàn tất.</summary>
    public async Task DisposeAsync() { if (Api is not null) await Api.DisposeAsync(); }
    /// <summary>Gửi HTTP thật và kiểm tra mã phản hồi, không gọi service trực tiếp để bỏ qua phân quyền.</summary>
    public async Task<JsonNode> Send(string method, string url, object? input = null, int expected = 200) =>
        (await Api.Send(method, url, input is null ? null : JsonSerializer.SerializeToNode(input), Admin)).Expect(expected, url).Json;
    /// <summary>Đọc DTO nguyên liệu bằng hợp đồng dùng chung với WinForms.</summary>
    public async Task<IngredientSnapshot> Ingredients() => (await Send("GET", "/api/admin/ingredients")).Deserialize<IngredientSnapshot>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    /// <summary>Đọc DTO công thức cùng phiên bản để kiểm thử đồng thời và snapshot.</summary>
    public async Task<IReadOnlyList<RecipeView>> Recipes() => (await Send("GET", "/api/admin/recipes")).Deserialize<List<RecipeView>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}

/// <summary>Tuần tự hóa các bài thay quyền SQL trong database fixture dùng chung.</summary>
[CollectionDefinition("AdminFeatures")]
public sealed class AdminFeatureCollection : ICollectionFixture<AdminFeatureFixture> { }
