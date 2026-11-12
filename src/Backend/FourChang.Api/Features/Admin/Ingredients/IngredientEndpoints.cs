using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Api.Features.Admin.Ingredients;

/// <summary>Biên API ADM-03; toàn nhóm yêu cầu Admin trước khi đọc hoặc ghi.</summary>
public static class IngredientEndpoints
{
    /// <summary>Đăng ký danh mục, đơn vị, xóa có điều kiện và tra receipt sau timeout.</summary>
    public static void MapIngredients(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/ingredients").RequireAuthorization("Admin");
        group.MapGet("", (IngredientService service) => service.Read());
        group.MapPost("", (IngredientInput input, HttpContext context, IngredientService service) => service.Save(null, input, context));
        group.MapPut("/{id:long}", (long id, IngredientInput input, HttpContext context, IngredientService service) => service.Save(id, input, context));
        group.MapPost("/{id:long}/delete", (long id, AdminDeleteInput input, HttpContext context, IngredientService service) => service.Delete(id, input, context));
        group.MapPost("/units", (UnitInput input, HttpContext context, IngredientService service) => service.Unit(null, input, context));
        group.MapPut("/units/{id:long}", (long id, UnitInput input, HttpContext context, IngredientService service) => service.Unit(id, input, context));
        group.MapPost("/units/{id:long}/delete", (long id, AdminDeleteInput input, HttpContext context, IngredientService service) => service.DeleteUnit(id, input, context));
        group.MapGet("/operations/{id:guid}", (Guid id, CafeDbContext db, HttpContext context) => AdminOperation.Receipt(db, id, "NguyenLieu", context));
    }
}
