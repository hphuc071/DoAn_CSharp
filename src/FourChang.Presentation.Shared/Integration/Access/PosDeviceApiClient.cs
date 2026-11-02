using System.Net;
namespace FourChang.Presentation.Shared.Integration;
public sealed partial class CafeApiClient
{
 public Task<List<PosDeviceInfo>> GetPosDevicesAsync()=>SendAsync<List<PosDeviceInfo>>(HttpMethod.Get,"/api/admin/access/devices");
 public Task<PosDeviceOperation> RegisterPosDeviceAsync(RegisterPosDevice input)=>SendAsync<PosDeviceOperation>(HttpMethod.Post,"/api/admin/access/devices",input);
 public Task<PosDeviceOperation> RevokePosDeviceAsync(long id,RevokePosDevice input)=>SendAsync<PosDeviceOperation>(HttpMethod.Post,$"/api/admin/access/devices/{id}/revoke",input);
 public Task<List<DeviceShift>> GetDeviceShiftsAsync(long id)=>SendAsync<List<DeviceShift>>(HttpMethod.Get,$"/api/admin/access/devices/{id}/shifts");
 public Task<PosDeviceOperation> CloseDeviceShiftAsync(long id,long shift,CloseDeviceShift input)=>SendAsync<PosDeviceOperation>(HttpMethod.Post,$"/api/admin/access/devices/{id}/shifts/{shift}/close",input);
 public async Task<PosDeviceOperation?> GetDeviceOperationAsync(Guid operation)
 {try{return await SendAsync<PosDeviceOperation>(HttpMethod.Get,$"/api/admin/access/devices/operations/{operation}");}catch(CafeApiException ex) when(ex.StatusCode==HttpStatusCode.NotFound){return null;}}
}
