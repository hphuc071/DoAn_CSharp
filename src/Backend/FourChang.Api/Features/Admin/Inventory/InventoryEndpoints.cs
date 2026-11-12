namespace FourChang.Api.Features.Admin.Inventory;

/// <summary>ADM-05: API tồn kho và truy vết có quyền Admin, không mở lệnh chỉnh số dư.</summary>
public static class InventoryEndpoints
{
    /// <summary>Đăng ký truy vấn theo bộ lọc và chi tiết nguyên liệu.</summary>
    public static void MapInventory(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/inventory").RequireAuthorization("Admin");
        group.MapGet("", (string? search, string? status, InventoryService service) => service.Read(search, status));
        group.MapGet("/{id:long}", (long id, InventoryService service) => service.Details(id));
    }
}
