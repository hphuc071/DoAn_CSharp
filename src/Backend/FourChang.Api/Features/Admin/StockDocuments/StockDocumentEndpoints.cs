using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Api.Features.Admin.StockDocuments;

/// <summary>Điểm vào UC-ADM-06; quyền Admin được kiểm tra trước mọi truy vấn/lệnh kho.</summary>
public static class StockDocumentEndpoints
{
    /// <summary>Đăng ký danh sách, nháp, ghi sổ, đảo và tra kết quả sau timeout; không có API sửa phiếu đã ghi sổ.</summary>
    public static void MapStockDocuments(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/stock-documents").RequireAuthorization("Admin");
        group.MapGet("", (StockDocumentService service) => service.Read());
        group.MapPost("", (StockDocumentInput input, HttpContext context, StockDocumentService service) => service.Save(null, input, context));
        group.MapPut("/{id:long}", (long id, StockDocumentInput input, HttpContext context, StockDocumentService service) => service.Save(id, input, context));
        group.MapPost("/{id:long}/post", (long id, StockDocumentAction input, HttpContext context, StockDocumentService service) => service.Post(id, input, context));
        group.MapPost("/{id:long}/reverse", (long id, StockDocumentAction input, HttpContext context, StockDocumentService service) => service.Reverse(id, input, context));
        group.MapPost("/{id:long}/delete", (long id, AdminDeleteInput input, HttpContext context, StockDocumentService service) => service.Delete(id, input, context));
        group.MapGet("/operations/{id:guid}", (Guid id, CafeDbContext db, HttpContext context) => AdminOperation.Receipt(db, id, "PhieuKho", context));
    }
}
