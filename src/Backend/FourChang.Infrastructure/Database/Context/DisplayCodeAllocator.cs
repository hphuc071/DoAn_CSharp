// UC: Dùng chung toàn hệ thống
// Chức năng: Cấp mã hiển thị dễ đọc như WEB-1, POS-1, HD-1.
// Cần nhớ: Số hiển thị độc lập khóa database; khi hủy giao dịch có thể có khoảng trống trong dãy số.

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using FourChang.Domain.Entities.Orders;
using FourChang.Domain.Entities.Payments;
using FourChang.Domain.Entities.Employees;
using FourChang.Domain.Entities.Inventory;

namespace FourChang.Infrastructure.Database.Context;

/// <summary>Cấp số hiển thị theo loại chứng từ, không thay khóa chính/khóa ngoại.
/// Sequence SQL bảo vệ nhiều máy tạo đồng thời; rollback có thể để trống một số đã cấp.</summary>
internal static class DisplayCodeAllocator
{
    internal static readonly string[] Prefixes = ["WEB", "POS", "HD", "DC", "PN", "PX", "PK", "CA", "HT"];

    /// <summary>
    /// Dùng chung toàn hệ thống — Cấp mã hiển thị ngắn trước khi lưu bản ghi.
    ///
    /// Hàm này được dùng để:
    /// 1. Tìm các bản ghi cần được cấp mã hiển thị.
    /// 2. Chọn tiền tố đúng loại, ví dụ WEB, POS, HD, PN hoặc PX.
    /// 3. Lấy số tiếp theo do SQL Server cấp và ghép thành mã ngắn.
    ///
    /// Thông tin cần truyền vào:
    /// - db: Đối tượng dùng để đọc và ghi dữ liệu của hệ thống.
    /// - ct: Tín hiệu yêu cầu dừng tác vụ nếu không cần xử lý tiếp.
    ///
    /// Cần nhớ: Chỉ thay mã hiển thị, không đổi khóa liên kết trong database. Hủy một thao tác có thể làm dãy số bị trống một số.
    /// </summary>
    internal static async Task AssignAsync(CafeDbContext db, CancellationToken ct)
    {
        var entries = db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified && e.Entity is PhieuKho).ToArray();
        foreach (var entry in entries)
        {
            var target = entry.Entity switch
            {
                DonHang o => (o.KenhDatHang, "MaDonHienThi"),
                HoaDon i => (i.LoaiHoaDon == "ADJUSTMENT" ? "DC" : "HD", "SoHoaDon"),
                PhieuKho p => (p.LoaiPhieu == "RECEIPT" ? "PN" : p.LoaiPhieu == "ISSUE" ? "PX" : "PK", "SoPhieuKho"),
                CaLamViec => ("CA", "MaCaHienThi"),
                HoSoHoanTien => ("HT", "MaHoanTienHienThi"),
                _ => ("", "")
            };
            if (target.Item1.Length == 0) continue;
            if (!Prefixes.Contains(target.Item1)) throw new InvalidOperationException("Loại mã hiển thị không hợp lệ.");
            // Đổi loại phiếu nháp thì cấp đúng tiền tố mới; sửa nội dung vẫn giữ mã cũ.
            if (entry.State == EntityState.Modified && entry.Property(target.Item2).CurrentValue is string code &&
                code.StartsWith(target.Item1 + "-", StringComparison.Ordinal)) continue;
            // Mọi bản ghi mới đều lấy sequence; mã tạm từ caller không phải số đã cấp.
            await db.Database.OpenConnectionAsync(ct);
            try
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandText = "SELECT NEXT VALUE FOR dbo.DisplayCode_" + target.Item1;
                var value = Convert.ToInt64(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
                entry.Property(target.Item2).CurrentValue = target.Item1 + "-" + value.ToString(CultureInfo.InvariantCulture);
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }
}