using FourChang.Presentation.Shared.Contracts.Admin;
namespace FourChang.Api.Features.Admin.Access;
/// <summary>Route cũ trả 410 để client biết chức năng cấp thiết bị đã bỏ, không ghi dữ liệu.</summary>
public sealed class PosDeviceService
{
    /// <summary>Chuẩn hóa mã máy lịch sử cho client cũ, không cấp quyền truy cập.</summary>
    public static string Machine(string? text)=>text?.Trim().ToUpperInvariant()??"";
    /// <summary>Trả410 thống nhất, không ghi dữ liệu hoặc tạo receipt thành công giả.</summary>
    private static Task<IResult> Removed()=>Task.FromResult<IResult>(Results.Problem(statusCode:410,detail:"Đã bỏ quản lý thiết bị POS; mở ca bằng hồ sơ nhân viên."));
    /// <summary>Thông báo danh sách thiết bị đã được bỏ khỏi nghiệp vụ.</summary>
    public Task<IResult> List()=>Removed();
    /// <summary>Từ chối cấp thiết bị; POS mới mở phiên theo hồ sơ và ca.</summary>
    public Task<IResult> Register(RegisterPosDevice input,HttpContext context)=>Removed();
    /// <summary>Từ chối thu hồi thiết bị đã bỏ; không tác động ca hoặc giao dịch.</summary>
    public Task<IResult> Revoke(long id,RevokePosDevice input,HttpContext context)=>Removed();
    /// <summary>Không tra ca theo thiết bị; quản trị ca dùng phân hệ ca làm việc.</summary>
    public Task<IResult> Shifts(long id)=>Removed();
    /// <summary>Không chốt ca qua thiết bị; giữ kiểm soát đối chiếu ở API ca hiện hành.</summary>
    public Task<IResult> CloseRelated(long deviceId,long shiftId,CloseDeviceShift input,HttpContext context)=>Removed();
    /// <summary>Thông báo route tra thao tác thiết bị cũ đã hết hỗ trợ.</summary>
    public Task<IResult> Operation(Guid operation)=>Removed();
}
