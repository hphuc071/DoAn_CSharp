using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Presentation.Shared.ApiClients;

/// <summary>Client Admin dùng hợp đồng chung; giao diện không tự tạo giá, định mức hoặc số dư.</summary>
public sealed partial class CafeApiClient
{
    /// <summary>Đọc danh mục nguyên liệu và đơn vị để tìm kiếm/chọn thông tin trong biểu mẫu.</summary>
    public Task<IngredientSnapshot> GetIngredientsAsync() => SendAsync<IngredientSnapshot>(HttpMethod.Get, "/api/admin/ingredients");
    /// <summary>Gửi lệnh nguyên liệu có cùng mã thao tác khi thử lại sau timeout.</summary>
    public Task<AdminSaveReceipt> SaveIngredientAsync(long? id, IngredientInput input) => SendAsync<AdminSaveReceipt>(id is null ? HttpMethod.Post : HttpMethod.Put, "/api/admin/ingredients" + (id is null ? "" : "/" + id), input);
    /// <summary>Yêu cầu server kiểm tra tham chiếu trước xóa nguyên liệu.</summary>
    public Task<AdminSaveReceipt> DeleteIngredientAsync(long id, AdminDeleteInput input) => SendAsync<AdminSaveReceipt>(HttpMethod.Post, $"/api/admin/ingredients/{id}/delete", input);
    /// <summary>Thêm/sửa đơn vị chung, giữ nguyên khóa đơn vị trong lịch sử.</summary>
    public Task<AdminSaveReceipt> SaveUnitAsync(long? id, UnitInput input) => SendAsync<AdminSaveReceipt>(id is null ? HttpMethod.Post : HttpMethod.Put, "/api/admin/ingredients/units" + (id is null ? "" : "/" + id), input);
    /// <summary>Xóa đơn vị chưa tham chiếu sau khi người dùng xác nhận.</summary>
    public Task<AdminSaveReceipt> DeleteUnitAsync(long id, AdminDeleteInput input) => SendAsync<AdminSaveReceipt>(HttpMethod.Post, $"/api/admin/ingredients/units/{id}/delete", input);
    /// <summary>Đọc công thức hiện hành để thêm/sửa; không lấy bản lịch sử làm dữ liệu chỉnh sửa.</summary>
    public Task<IReadOnlyList<RecipeView>> GetRecipesAsync() => SendAsync<IReadOnlyList<RecipeView>>(HttpMethod.Get, "/api/admin/recipes");
    /// <summary>Lưu công thức và áp dụng ngay; server tự giữ các định mức lịch sử.</summary>
    public Task<AdminSaveReceipt> SaveRecipeAsync(long? id, RecipeInput input) => SendAsync<AdminSaveReceipt>(id is null ? HttpMethod.Post : HttpMethod.Put, "/api/admin/recipes" + (id is null ? "" : "/" + id), input);
    /// <summary>Lưu nguyên tử bảng công thức tất cả size của một món.</summary>
    public Task<AdminSaveReceipt> SaveDishRecipesAsync(RecipeDishInput input) => SendAsync<AdminSaveReceipt>(HttpMethod.Post, "/api/admin/recipes/dish", input);
    /// <summary>Đọc tồn tại thời điểm xác nhận; bộ lọc không sửa số dư.</summary>
    public Task<StockSnapshot> GetInventoryAsync(string? search = null, string? status = null) => SendAsync<StockSnapshot>(HttpMethod.Get, $"/api/admin/inventory?search={Uri.EscapeDataString(search ?? "")}&status={Uri.EscapeDataString(status ?? "")}");
    /// <summary>Truy vết nguyên liệu về biến động, đơn/phiếu và giữ chỗ.</summary>
    public Task<StockDetail> GetStockDetailsAsync(long id) => SendAsync<StockDetail>(HttpMethod.Get, $"/api/admin/inventory/{id}");
    /// <summary>Tra receipt của lệnh nguyên liệu/BOM khi chưa biết yêu cầu trước đã commit hay chưa.</summary>
    public Task<AdminSaveReceipt> AdminReceiptAsync(string area, Guid operation) => SendAsync<AdminSaveReceipt>(HttpMethod.Get, $"/api/admin/{area}/operations/{operation}");
}
