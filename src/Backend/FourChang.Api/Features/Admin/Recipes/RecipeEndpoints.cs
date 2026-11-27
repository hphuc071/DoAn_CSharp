// UC: UC-ADM-02
// Chức năng: Khai báo API xem và lưu công thức pha chế.
// Cần nhớ: Đọc hàm từ bước kiểm tra dữ liệu đến bước trả kết quả; các quyết định nghiệp vụ do Backend kiểm tra.

using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Api.Features.Admin.Recipes;

/// <summary>API công thức thuộc UC-ADM-02; lưu trực tiếp nhưng giữ định mức lịch sử.</summary>
public static class RecipeEndpoints
{
    /// <summary>Đăng ký lưu/xem công thức và receipt; client cũ không còn quản trị vòng đời phiên bản.</summary>
    public static void MapRecipes(this WebApplication app)
    {
        var g = app.MapGroup("/api/admin/recipes").RequireAuthorization("Admin");
        g.MapGet("", (RecipeService s) => s.Read());
        g.MapPost("/dish", (RecipeDishInput i, HttpContext c, RecipeService s) => s.SaveDish(i, c));
        g.MapPost("", (RecipeInput i, HttpContext c, RecipeService s) => s.Save(null, i, c));
        g.MapPut("/{id:long}", (long id, RecipeInput i, HttpContext c, RecipeService s) => s.Save(id, i, c));
        g.MapPost("/{id:long}/clone", Retired);
        g.MapPost("/{id:long}/publish", Retired);
        g.MapPost("/{id:long}/stop", Retired);
        g.MapPost("/{id:long}/delete", Retired);
        g.MapPost("/simulate", Retired);
        g.MapGet("/operations/{id:guid}", (Guid id, CafeDbContext db, HttpContext c) => AdminOperation.Receipt(db, id, "CongThuc", c));
    }
    /// <summary>Các thao tác đời cũ bị chặn ở API sau quyền Admin, không chỉ ẩn trên giao diện.</summary>
    private static IResult Retired() => Results.Problem(statusCode: 410, detail: "Công thức được áp dụng ngay khi lưu; không còn phiên bản/lịch/mô phỏng thủ công.");
}
