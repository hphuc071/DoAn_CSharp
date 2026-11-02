using FourChang.Presentation.Shared.Integration;
namespace FourChang.Api.Features.Access;
public static class PosDeviceEndpoints
{
 public static void MapPosDevices(this WebApplication app)
 {
  var group=app.MapGroup("/api/admin/access/devices").RequireAuthorization("Admin");
  group.MapGet("",(PosDeviceService service)=>service.List());
  group.MapPost("",(RegisterPosDevice input,HttpContext context,PosDeviceService service)=>service.Register(input,context));
  group.MapPost("/{id:long}/revoke",(long id,RevokePosDevice input,HttpContext context,PosDeviceService service)=>service.Revoke(id,input,context));
  group.MapGet("/{id:long}/shifts",(long id,PosDeviceService service)=>service.Shifts(id));
  group.MapPost("/{id:long}/shifts/{shift:long}/close",(long id,long shift,CloseDeviceShift input,HttpContext context,PosDeviceService service)=>service.CloseRelated(id,shift,input,context));
  group.MapGet("/operations/{operation:guid}",(Guid operation,PosDeviceService service)=>service.Operation(operation));
 }
}
