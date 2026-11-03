using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FourChang.Web.Pages.Account;

// UC-WEB-03: điều phối các form hồ sơ, địa chỉ và mật khẩu; API kiểm tra quyền và dữ liệu cuối cùng.
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProfileModel(CafeApiClient api) : PageModel
{
    // Các form chỉ gửi trường của thao tác đó; API kiểm tra bắt buộc, không bắt nhập lại form khác.
    public CustomerSelfProfile? Profile { get; private set; }
    public string? Error { get; private set; }
    [BindProperty] public Guid MaThaoTac { get; set; } = Guid.NewGuid();
    [BindProperty] public string PhienBan { get; set; } = "";
    [BindProperty] public string HoTen { get; set; } = "";
    [BindProperty] public string? Email { get; set; }
    [BindProperty] public string? SoDienThoai { get; set; }
    [BindProperty] public long? MaDiaChi { get; set; }
    [BindProperty] public string? PhienBanDiaChi { get; set; }
    [BindProperty] public string? NhanDiaChi { get; set; }
    [BindProperty] public string TenNguoiNhan { get; set; } = "";
    [BindProperty] public string DienThoaiNguoiNhan { get; set; } = "";
    [BindProperty] public string DiaChiChiTiet { get; set; } = "";
    [BindProperty] public decimal? ViDo { get; set; }
    [BindProperty] public decimal? KinhDo { get; set; }
    [BindProperty] public string? GhiChuGiaoHang { get; set; }
    [BindProperty] public bool LaDiaChiMacDinh { get; set; }
    [BindProperty] public string MatKhauHienTai { get; set; } = "";
    [BindProperty] public string MatKhauMoi { get; set; } = "";
    [BindProperty] public string XacNhanMatKhau { get; set; } = "";
    private bool Authenticate() { api.Token = Request.Cookies[CustomerCookie.Name]; return api.Token is not null; }
    public async Task<IActionResult> OnGetAsync(long? address = null)
    {
        if (!Authenticate()) return RedirectToPage("Login");
        try
        {
            Profile = await api.GetSelfProfileAsync(); PhienBan = Convert.ToBase64String(Profile.PhienBanDuLieu); HoTen = Profile.HoTen; Email = Profile.EmailChuanHoa; SoDienThoai = Profile.SoDienThoaiChuanHoa;
            if (address is not null)
            {
                var a = Profile.DiaChi.SingleOrDefault(x => x.MaDiaChi == address); if (a is null) return NotFound();
                MaDiaChi = a.MaDiaChi; PhienBanDiaChi = Convert.ToBase64String(a.PhienBanDuLieu); NhanDiaChi = a.NhanDiaChi; TenNguoiNhan = a.TenNguoiNhan; DienThoaiNguoiNhan = a.DienThoaiNguoiNhan; DiaChiChiTiet = a.DiaChiChiTiet; ViDo = a.ViDo; KinhDo = a.KinhDo; GhiChuGiaoHang = a.GhiChuGiaoHang; LaDiaChiMacDinh = a.LaDiaChiMacDinh;
            }
            return Page();
        }
        catch (CafeApiException ex) { return Failure(ex); }
    }
    public Task<IActionResult> OnPostSaveAsync() => Run(() => api.SaveSelfProfileAsync(new(MaThaoTac, Version(PhienBan), HoTen, Email, SoDienThoai)));
    public Task<IActionResult> OnPostAddressAsync() => Run(() => api.SaveSelfAddressAsync(MaDiaChi, new(MaThaoTac, Version(PhienBan), PhienBanDiaChi is null ? null : Version(PhienBanDiaChi), NhanDiaChi, TenNguoiNhan, DienThoaiNguoiNhan, DiaChiChiTiet, ViDo, KinhDo, GhiChuGiaoHang, LaDiaChiMacDinh)));
    public Task<IActionResult> OnPostRemoveAsync() => Run(() => MaDiaChi is { } id ? api.RemoveSelfAddressAsync(id, new(MaThaoTac, Version(PhienBan), Version(PhienBanDiaChi ?? ""))) : throw new FormatException());
    public Task<IActionResult> OnPostPasswordAsync() => Run(() => api.ChangeSelfPasswordAsync(new(MaThaoTac, Version(PhienBan), MatKhauHienTai, MatKhauMoi, XacNhanMatKhau)));
    private async Task<IActionResult> Run(Func<Task<CustomerSelfReceipt>> action)
    {
        if (!Authenticate()) return RedirectToPage("Login");
        // Mỗi form chỉ gửi trường của chính nó; không bắt nhập mật khẩu khi khách chỉ sửa tên.
        // API vẫn kiểm tra đầy đủ trường bắt buộc của lệnh, kể cả khi trình duyệt bỏ trường.
        foreach (var key in ModelState.Keys.Where(key => !Request.Form.ContainsKey(key)).ToArray()) ModelState.Remove(key);
        try
        {
            // Kiểm tra kết quả cũ trước POST lại, kể cả sau đổi mật khẩu đã thu hồi token nguồn.
            var result = await api.GetSelfOperationAsync(MaThaoTac);
            if (result is null) { if (!ModelState.IsValid) { Error = "Dữ liệu nhập không hợp lệ. Kiểm tra lại trường đang sửa."; return await Redisplay(); } result = await action(); }
            if (result.CanDangNhapLai) { Response.Cookies.Delete(CustomerCookie.Name); return RedirectToPage("Login"); }
            return RedirectToPage("Profile");
        }
        catch (FormatException) { Error = "Thiếu phiên bản dữ liệu hoặc mã địa chỉ. Tải lại để đối chiếu."; return await Redisplay(); }
        catch (CafeApiException ex)
        {
            if (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden) return Failure(ex);
            Error = ex.Message + " Nếu kết quả chưa rõ, giữ nguyên form để thử lại; nếu xung đột, tải lại và đối chiếu trước khi lưu."; return await Redisplay();
        }
    }
    private async Task<IActionResult> Redisplay()
    {
        // Chỉ tải phần hiển thị; giữ nguyên version và nháp đã gửi để không âm thầm ghi đè bản mới.
        foreach (var name in new[] { nameof(MatKhauHienTai), nameof(MatKhauMoi), nameof(XacNhanMatKhau) }) ModelState.Remove(name);
        MatKhauHienTai = MatKhauMoi = XacNhanMatKhau = "";
        try { Profile = await api.GetSelfProfileAsync(); } catch (CafeApiException) { }
        return Page();
    }
    private IActionResult Failure(CafeApiException ex)
    {
        if (ex.StatusCode == System.Net.HttpStatusCode.Forbidden) return RedirectToPage("ChangeTemporaryPassword");
        if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized) { Response.Cookies.Delete(CustomerCookie.Name); return RedirectToPage("Login"); }
        Error = ex.Message; return Page();
    }
    private static byte[] Version(string value) => Convert.FromBase64String(value);
}
