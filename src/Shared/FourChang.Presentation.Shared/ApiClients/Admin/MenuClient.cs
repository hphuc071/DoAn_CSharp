using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Customer;
namespace FourChang.Presentation.Shared.ApiClients;
public sealed partial class CafeApiClient
{
    /// <summary>Snapshot giá/tùy chọn chung, không lấy giá từ dữ liệu mẫu của UI.</summary>
    public Task<CatalogSnapshot> GetCatalogAsync(CancellationToken ct=default)=>SendAsync<CatalogSnapshot>(HttpMethod.Get,"/api/catalog",ct:ct);
    /// <summary>Snapshot quản trị có rowversion để form lưu an toàn.</summary>
    public Task<MenuAdminSnapshot> GetAdminMenuAsync()=>SendAsync<MenuAdminSnapshot>(HttpMethod.Get,"/api/admin/menu");
    /// <summary>Gửi đúng payload và mã thao tác đã giữ trên form; không tự tạo mã mới khi timeout.</summary>
    public Task<MenuSaveReceipt> SaveMenuAsync(string kind,long? id,object input)=>SendAsync<MenuSaveReceipt>(id is null?HttpMethod.Post:HttpMethod.Put,"/api/admin/menu/"+kind+(id is null?"":"/"+id),input);
    /// <summary>Xóa có điều kiện được kiểm tra tại API, không xóa cục bộ trước khi server xác nhận.</summary>
    public Task<MenuSaveReceipt> DeleteMenuAsync(string kind,long id,MenuDeleteInput input)=>SendAsync<MenuSaveReceipt>(HttpMethod.Post,$"/api/admin/menu/{kind}/{id}/delete",input);
    /// <summary>Tra kết quả commit khi rớt mạng để tránh tạo món/topping lần hai.</summary>
    public Task<MenuSaveReceipt> MenuReceiptAsync(Guid operation)=>SendAsync<MenuSaveReceipt>(HttpMethod.Get,"/api/admin/menu/operations/"+operation);
    /// <summary>Nghe SSE; reconnect luôn được server yêu cầu tải lại snapshot, không mất thay đổi khi offline.</summary>
    public async Task WatchCatalogAsync(Func<Task> changed,CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try
            {
                using var request=new HttpRequestMessage(HttpMethod.Get,"/api/catalog/events");
                using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();
                using var stream=await response.Content.ReadAsStreamAsync(ct);using var reader=new StreamReader(stream);
                while(await reader.ReadLineAsync(ct) is {} line)if(line=="data: changed")await changed();
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){return;}
            catch(Exception ex)when(ex is HttpRequestException or IOException or TaskCanceledException or CafeApiException){}
            try{await Task.Delay(2000,ct);}catch(OperationCanceledException){return;}
        }
    }
}
