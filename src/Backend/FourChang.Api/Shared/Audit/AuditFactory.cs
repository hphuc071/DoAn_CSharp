using FourChang.Api.Shared.Audit;
using System.Security.Cryptography;
using System.Data;
using FourChang.Domain.Entities.Audit;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Shared.Audit;

// Dùng cùng cấu trúc audit cho Admin và Staff, giữ nguyên dữ liệu lịch sử.
public static class AuditFactory
{
    public static NhatKyHeThong Create(string role, long actor, string action, string entity, long id) => new() { MaLienKetXuLy = Guid.NewGuid(), LoaiTacNhan = role, MaKhachHang = role == "CUSTOMER" ? actor : null, MaQuanLy = role == "ADMIN" ? actor : null, MaCaLamViec = role == "STAFF" ? actor : null, TenNguoiThucHienDaChot = role, HanhDong = action, LoaiDoiTuong = entity, MaDoiTuong = id.ToString(System.Globalization.CultureInfo.InvariantCulture), KetQua = "SUCCEEDED", PhatSinhLuc = DateTime.UtcNow };
}
