// UC: UC-ADM-02
// Chức năng: Khai báo API quản lý thực đơn và tải hình ảnh món.
// Cần nhớ: Đọc hàm từ bước kiểm tra dữ liệu đến bước trả kết quả; các quyết định nghiệp vụ do Backend kiểm tra.

using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
namespace FourChang.Api.Features.Admin.Menu;
/// <summary>Biên API ADM-02; quyền Admin được kiểm tra trước mọi truy vấn và lệnh ghi.</summary>
public static class MenuAdminEndpoints
{
    /// <summary>Đăng ký CRUD đúng nhóm thực đơn; không dùng tên người dùng/tên nhân viên để cấp quyền.</summary>
    public static void MapAdminMenu(this WebApplication app)
    {
        app.MapGet("/menu-images/{file}", MenuImages.Read);
        var g = app.MapGroup("/api/admin/menu").RequireAuthorization("Admin");
        g.MapGet("/", (MenuAdminService s) => s.Read());
        g.MapGet("/operations/{id:guid}", (Guid id, CafeDbContext db, HttpContext c) => AdminOperation.Receipt(db, id, "ThucDon", c));
        g.MapPost("/categories", (MenuCategoryInput i, HttpContext c, MenuAdminService s) => s.Category(null, i, c));
        g.MapPut("/categories/{id:long}", (long id, MenuCategoryInput i, HttpContext c, MenuAdminService s) => s.Category(id, i, c));
        g.MapPost("/images", MenuImages.Upload).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(8 * 1024 * 1024));
        g.MapPost("/dishes", (MenuDishInput i, HttpContext c, MenuAdminService s) => s.Dish(null, i, c));
        g.MapPut("/dishes/{id:long}", (long id, MenuDishInput i, HttpContext c, MenuAdminService s) => s.Dish(id, i, c));
        g.MapPost("/variants/batch", (MenuVariantBatchInput i, HttpContext c, MenuAdminService s) => s.VariantBatch(i, c));
        g.MapPost("/variants", (MenuVariantInput i, HttpContext c, MenuAdminService s) => s.Variant(null, i, c));
        g.MapPut("/variants/{id:long}", (long id, MenuVariantInput i, HttpContext c, MenuAdminService s) => s.Variant(id, i, c));
        g.MapPost("/toppings", (MenuToppingInput i, HttpContext c, MenuAdminService s) => s.Topping(null, i, c));
        g.MapPut("/toppings/{id:long}", (long id, MenuToppingInput i, HttpContext c, MenuAdminService s) => s.Topping(id, i, c));
        g.MapPost("/banners", Retired);
        g.MapPut("/banners/{id:long}", Retired);
        g.MapPost("/{kind}/{id:long}/delete", Retired);
    }
    /// <summary>Client cũ nhận thông báo chức năng đã bỏ; vẫn qua kiểm quyền của nhóm Admin.</summary>
    private static IResult Retired() => Results.Problem(statusCode: 410, detail: "Chức năng đã bỏ khỏi thực đơn tinh gọn. Hãy sửa hoặc ẩn/ngừng dữ liệu.");
}
