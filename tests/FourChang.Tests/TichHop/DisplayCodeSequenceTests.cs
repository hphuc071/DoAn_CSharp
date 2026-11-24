using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using FourChang.Infrastructure.Database.Context;
using FourChang.Domain.Entities.Orders;
using FourChang.Domain.Entities.Employees;
using FourChang.Domain.Entities.Payments;
using FourChang.Domain.Entities.Inventory;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Tests.Integration;

/// <summary>Kiểm tra SQL thật trong DB GUID riêng: mã từ 1 độc lập khóa, nâng dữ liệu và cạnh tranh.</summary>
public sealed class DisplayCodeSequenceTests
{
    [Fact, Trait("Feature", "DisplayCodeSequence")]
    public async Task Renumber_KeepsKeysMoneyAndSnapshots_ContinuesPerPrefix_ConcurrentSafe()
    {
        await using var api = await TestApi.Start("DisplayCodeSequence");
        var customer = await api.NewCustomer();
        var connection = new SqlConnectionStringBuilder {
            DataSource = api.Settings.GetValueOrDefault("FOURCHANG_SQL_SERVER", "127.0.0.1,14330"),
            InitialCatalog = api.Database, UserID = "sa", Password = api.Settings["SQL_SA_PASSWORD"],
            TrustServerCertificate = true, Encrypt = true };
        var options = new DbContextOptionsBuilder<CafeDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new CafeDbContext(options);
        var employee = await db.NhanVien.FirstAsync();
        var shift = new CaLamViec { MaNhanVien = employee.MaNhanVien, TenCa = "Sáng", MaMay = "code-test", TrangThai = "OPEN", BatDauLuc = DateTime.UtcNow, TienMatDauCa = 500000 };
        db.CaLamViec.Add(shift); await db.SaveChangesAsync();
        Assert.Equal("CA-1", shift.MaCaHienThi);
        await api.Sql("DBCC CHECKIDENT ('dbo.DonHang', RESEED, 60000) WITH NO_INFOMSGS;");
        var pos = new DonHang { KenhDatHang = "POS", MaCaLamViec = shift.MaCaLamViec, HinhThucNhanMon = "TAKEAWAY", TrangThai = "ACCEPTED" };
        var web = new DonHang { KenhDatHang = "WEB", MaKhachHang = customer.Id, HinhThucNhanMon = "TAKEAWAY", TrangThai = "PENDING_PAYMENT" };
        db.DonHang.AddRange(pos, web); await db.SaveChangesAsync();
        Assert.True(pos.MaDonHang >= 60000); Assert.Equal("POS-1", pos.MaDonHienThi); Assert.Equal("WEB-1", web.MaDonHienThi);
        var adminId = await db.TaiKhoanQuanLy.Select(x => x.MaQuanLy).FirstAsync();
        var invoice = new HoaDon { MaDonHang = pos.MaDonHang, LoaiHoaDon = "ORIGINAL", TrangThai = "ISSUED", SoTien = 39000,
            NoiDungHoaDonDaChotJson = "{\"MaDonHienThi\":\"OLD\",\"Total\":39000}", PhatHanhLuc = DateTime.UtcNow };
        var receipt = new PhieuKho { LoaiPhieu = "RECEIPT", TrangThai = "DRAFT", MaQuanLyLap = adminId, MaThaoTac = Guid.NewGuid() };
        var issue = new PhieuKho { LoaiPhieu = "ISSUE", TrangThai = "DRAFT", MaQuanLyLap = adminId, MaThaoTac = Guid.NewGuid() };
        db.AddRange(invoice, receipt, issue); await db.SaveChangesAsync();
        Assert.Equal("HD-1", invoice.SoHoaDon); Assert.StartsWith("PN-", receipt.SoPhieuKho); Assert.Equal("PX-1", issue.SoPhieuKho);
        receipt.LoaiPhieu = "ISSUE"; await db.SaveChangesAsync(); Assert.Equal("PX-2", receipt.SoPhieuKho);
        var payment = new ThanhToan { MaDonHang = web.MaDonHang, MaThanhToanHienThi = "PAY-OLD", PhuongThuc = "BANK_TRANSFER", TrangThai = "PENDING",
            SoTienYeuCau = 39000, MaThaoTac = Guid.NewGuid(), MaThamChieuChuyenKhoan = "BANK-DO-NOT-CHANGE" };
        db.ThanhToan.Add(payment); await db.SaveChangesAsync();
        var refund = new HoSoHoanTien { MaDonHang = web.MaDonHang, MaThanhToan = payment.MaThanhToan, MaQuanLyYeuCau = adminId, LoaiNguon = "CANCELLATION", PhuongThucHoan = "CASH", TrangThai = "REQUESTED", SoTienYeuCau = 39000 };
        db.HoSoHoanTien.Add(refund); await db.SaveChangesAsync(); Assert.Equal("HT-1", refund.MaHoanTienHienThi);
        // Mô phỏng database cũ; bắt buộc migration đánh lại mà không đổi khóa/tài chính.
        pos.MaDonHienThi = "ORD-LEGACY-LONG"; await db.SaveChangesAsync();
        var before = await api.Sql("SELECT MaDonHang,SoTien,NoiDungHoaDonDaChotJson FROM dbo.HoaDon; SELECT MaThanhToan,MaThamChieuChuyenKhoan,SoTienYeuCau FROM dbo.ThanhToan; SELECT MaNguyenLieu,TonThucTe FROM dbo.TonKho ORDER BY MaNguyenLieu;");
        foreach (var prefix in new[] { "WEB", "POS", "HD", "DC", "PN", "PX", "PK", "CA", "HT" }) await api.Sql("DROP SEQUENCE dbo.DisplayCode_" + prefix);
        var migration = await File.ReadAllTextAsync(Path.Combine(TestApi.Root, "docs/database/30-bang/013-short-display-codes.sql"));
        await api.Sql(migration); await api.Sql(migration); // Idempotent, không đánh lại lần hai.
        Assert.Equal(before, await api.Sql("SELECT MaDonHang,SoTien,NoiDungHoaDonDaChotJson FROM dbo.HoaDon; SELECT MaThanhToan,MaThamChieuChuyenKhoan,SoTienYeuCau FROM dbo.ThanhToan; SELECT MaNguyenLieu,TonThucTe FROM dbo.TonKho ORDER BY MaNguyenLieu;"));
        Assert.Equal("POS-1", await api.Sql($"SELECT MaDonHienThi FROM dbo.DonHang WHERE MaDonHang={pos.MaDonHang}"));
        Assert.Equal("0", await api.Sql("SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=1"));
        var admin = await api.AdminLogin();
        var found = (await api.Send("GET", "/api/admin/orders?TimKiem=POS-1", headers: admin)).Expect(200, "Tìm theo mã hiển thị").Json;
        Assert.Contains(found["duLieu"]!.AsArray(), x => x!.Number("maDonHang") == pos.MaDonHang && x!.Text("maDon") == "POS-1");
        var next = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ => {
            await using var writer = new CafeDbContext(options);
            var order = new DonHang { KenhDatHang = "POS", MaCaLamViec = shift.MaCaLamViec, HinhThucNhanMon = "TAKEAWAY", TrangThai = "ACCEPTED" };
            writer.DonHang.Add(order); await writer.SaveChangesAsync(); return order.MaDonHienThi;
        }));
        Assert.Equal(8, next.Distinct().Count()); Assert.Contains("POS-2", next); Assert.Contains("POS-9", next);
        await Assert.ThrowsAsync<SqlException>(() => api.Sql($"UPDATE dbo.HoaDon SET SoTien=0 WHERE MaHoaDon={invoice.MaHoaDon}"));
    }
}