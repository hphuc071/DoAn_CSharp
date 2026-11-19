using FourChang.Api.Shared.Operations;
using System.Text.Json;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Shared.WorkSessions;

/// <summary>Liên kết bàn giao dùng audit bền vững và MaCaBanGiaoTruoc đã có; không tạo ca nhận thay nhân viên.</summary>
public static class HandoverLink
{
    /// <summary>Đọc biên bản bàn giao chưa có ca nhận; truy vấn này chạy trong khóa nghiệp vụ khi ghi.</summary>
    public static async Task<List<HandoverInfo>> Pending(CafeDbContext db)
    {
        var claimed = await db.CaLamViec.Where(x => x.MaCaBanGiaoTruoc != null).Select(x => x.MaCaBanGiaoTruoc!.Value).ToListAsync();
        var rows = await db.NhatKyHeThong.AsNoTracking().Where(x => x.HanhDong == "SHIFT_HANDED_OVER" && x.DuLieuSauJson != null)
            .Select(x => x.DuLieuSauJson!).ToListAsync();
        return rows.Select(Read)
            .Where(x => !claimed.Contains(x.MaCaNguon)).ToList();
    }

    /// <summary>Đọc cả biên bản cũ và envelope audit chung; không dựa vào mô tả tự do hoặc tên nhân viên.</summary>
    public static HandoverInfo Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("KetQua", out var result)) root = result;
        var value = root.Deserialize<HandoverInfo>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return value is { MaCaNguon: > 0, MaNhanVienNhan: > 0 } ? value : throw new AdminRule(409, "Biên bản bàn giao chưa đủ thông tin. Cần kiểm tra lịch sử.");
    }

    /// <summary>Ngăn vô hiệu hồ sơ đang được chỉ định nhận ca nhưng chưa tiếp nhận tại POS.</summary>
    public static async Task<bool> HasPendingReceiver(CafeDbContext db, long employee) =>
        (await Pending(db)).Any(x => x.MaNhanVienNhan == employee);

    /// <summary>Nhận đúng biên bản chỉ định nhân viên; máy là thông tin lịch sử, không khóa máy dùng chung của quán.</summary>
    public static async Task<long?> SourceForOpen(CafeDbContext db, long employee, string machine)
    {
        var pending = (await Pending(db)).Where(x => x.MaNhanVienNhan == employee).ToList();
        if (pending.Count > 1) throw new AdminRule(409, "Nhân viên có nhiều bàn giao chờ nhận. Liên hệ Quản lý kiểm tra.");
        return pending.SingleOrDefault()?.MaCaNguon;
    }

    /// <summary>Dùng chung cho chi tiết ca và báo cáo: đơn thuộc ca thực sự hoàn thành; dữ liệu cũ thiếu audit dùng ca gốc.</summary>
    public static IQueryable<FourChang.Domain.Entities.Orders.DonHang> CompletedByShift(CafeDbContext db,
        IQueryable<FourChang.Domain.Entities.Orders.DonHang> orders, long shift)
    {
        var completions = db.NhatKyHeThong.AsNoTracking().Where(x => x.LoaiDoiTuong == "DonHang" && x.HanhDong == "ORDER_COMPLETE" && x.MaCaLamViec != null);
        return orders.Where(x => x.TrangThai == "COMPLETED" &&
            (completions.Any(a => a.MaDoiTuong == x.MaDonHang.ToString() && a.MaCaLamViec == shift) ||
             x.MaCaLamViec == shift && !completions.Any(a => a.MaDoiTuong == x.MaDonHang.ToString())));
    }
    /// <summary>Lấy chuỗi ca nguồn để truy vết đơn/khoản chưa rõ; không chuyển MaCaLamViec của giao dịch tài chính.</summary>
    public static async Task<List<long>> Ancestors(CafeDbContext db, long id)
    {
        var result = new List<long>();
        long? current = id;
        while (current is not null)
        {
            if (result.Contains(current.Value)) throw new AdminRule(409, "Liên kết ca bị vòng lặp. Cần kiểm tra dữ liệu.");
            result.Add(current.Value);
            current = await db.CaLamViec.Where(x => x.MaCaLamViec == current).Select(x => x.MaCaBanGiaoTruoc).SingleAsync();
        }
        return result;
    }
    /// <summary>Chỉ những đơn nằm trong biên bản ca trước giao cho ca này mới thuộc nhóm nhận bàn giao.</summary>
    public static async Task<HandoverInfo?> ForReceiver(CafeDbContext db,long shift)
    {
        var source=await db.CaLamViec.AsNoTracking().Where(x=>x.MaCaLamViec==shift).Select(x=>x.MaCaBanGiaoTruoc).SingleAsync();
        if(source is null)return null;
        var key=source.Value.ToString();
        var json=await db.NhatKyHeThong.AsNoTracking().Where(x=>x.LoaiDoiTuong=="CaLamViec"&&x.MaDoiTuong==key&&x.HanhDong=="SHIFT_HANDED_OVER").Select(x=>x.DuLieuSauJson).SingleOrDefaultAsync();
        return json is null?null:Read(json);
    }
}
