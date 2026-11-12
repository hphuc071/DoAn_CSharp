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
        var g = app.MapGroup("/api/admin/menu").RequireAuthorization("Admin");
        g.MapGet("/", (MenuAdminService s) => s.Read());
        g.MapGet("/operations/{id:guid}", (Guid id, CafeDbContext db, HttpContext c) => AdminOperation.Receipt(db, id, "ThucDon", c));
        g.MapPost("/categories", (MenuCategoryInput i, HttpContext c, MenuAdminService s) => s.Category(null, i, c));
        g.MapPut("/categories/{id:long}", (long id, MenuCategoryInput i, HttpContext c, MenuAdminService s) => s.Category(id, i, c));
        g.MapPost("/dishes", (MenuDishInput i, HttpContext c, MenuAdminService s) => s.Dish(null, i, c));
        g.MapPut("/dishes/{id:long}", (long id, MenuDishInput i, HttpContext c, MenuAdminService s) => s.Dish(id, i, c));
        g.MapPost("/variants", (MenuVariantInput i, HttpContext c, MenuAdminService s) => s.Variant(null, i, c));
        g.MapPut("/variants/{id:long}", (long id, MenuVariantInput i, HttpContext c, MenuAdminService s) => s.Variant(id, i, c));
        g.MapPost("/toppings", (MenuToppingInput i, HttpContext c, MenuAdminService s) => s.Topping(null, i, c));
        g.MapPut("/toppings/{id:long}", (long id, MenuToppingInput i, HttpContext c, MenuAdminService s) => s.Topping(id, i, c));
        g.MapPost("/banners", (MenuBannerInput i, HttpContext c, MenuAdminService s) => s.Banner(null, i, c));
        g.MapPut("/banners/{id:long}", (long id, MenuBannerInput i, HttpContext c, MenuAdminService s) => s.Banner(id, i, c));
        g.MapPost("/{kind}/{id:long}/delete", (string kind, long id, MenuDeleteInput i, HttpContext c, MenuAdminService s) => s.Delete(kind, id, i, c));
    }
}
