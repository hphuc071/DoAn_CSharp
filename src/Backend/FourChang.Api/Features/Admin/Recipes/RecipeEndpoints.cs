using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Api.Features.Admin.Recipes;

/// <summary>API ADM-04, không cung cấp CRUD thô cho công thức đang được dùng.</summary>
public static class RecipeEndpoints
{
    /// <summary>Đăng ký nháp, sao chép, mô phỏng, công bố/ngừng và tra kết quả chống trùng.</summary>
    public static void MapRecipes(this WebApplication app)
    {
        var g = app.MapGroup("/api/admin/recipes").RequireAuthorization("Admin");
        g.MapGet("", (RecipeService s) => s.Read());
        g.MapPost("", (RecipeInput i, HttpContext c, RecipeService s) => s.Save(null, i, c));
        g.MapPut("/{id:long}", (long id, RecipeInput i, HttpContext c, RecipeService s) => s.Save(id, i, c));
        g.MapPost("/{id:long}/clone", (long id, AdminDeleteInput i, HttpContext c, RecipeService s) => s.Clone(id, i, c));
        g.MapPost("/{id:long}/publish", (long id, RecipePublishInput i, HttpContext c, RecipeService s) => s.Publish(id, i, c));
        g.MapPost("/{id:long}/stop", (long id, RecipeStopInput i, HttpContext c, RecipeService s) => s.Stop(id, i, c));
        g.MapPost("/{id:long}/delete", (long id, AdminDeleteInput i, HttpContext c, RecipeService s) => s.Delete(id, i, c));
        g.MapPost("/simulate", (RecipeSimulationInput i, RecipeService s) => s.Simulate(i));
        g.MapGet("/operations/{id:guid}", (Guid id, CafeDbContext db, HttpContext c) => AdminOperation.Receipt(db, id, "CongThuc", c));
    }
}
