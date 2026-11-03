using FourChang.Api.Shared.WorkSessions;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FourChang.Domain.Entities.Employees;
using FourChang.Domain.Entities.Payments;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Shared.WorkSessions;

// A3 của UC-ADM-02: tập quyền POS cố định. Không tạo vai trò/tài khoản nhân viên hoặc cho POS gọi Admin.
internal static class ShiftCash
{
    internal static async Task<decimal> Expected(CafeDbContext db, CaLamViec shift) => shift.TienMatDauCa +
        (await db.ThanhToan.Where(x => x.MaCaLamViec == shift.MaCaLamViec && x.PhuongThuc == "CASH" && x.TrangThai == "VERIFIED").SumAsync(x => (decimal?)x.SoTienDaXacMinh) ?? 0) -
        (await db.LanChiHoanTien.Where(x => x.MaCaLamViec == shift.MaCaLamViec && x.PhuongThuc == "CASH" && x.TrangThai == "SUCCEEDED").SumAsync(x => (decimal?)x.SoTien) ?? 0);
}
