using FourChang.Tests.Shared;
using Microsoft.Data.SqlClient;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Text.Json;

namespace FourChang.Tests.Integration;

/// <summary>Nâng cấp thuộc tính trên database GUID; không truy cập database quán.</summary>
public sealed class ColumnSimplificationTests
{
    [Fact, Trait("Layer", "Database"), Trait("UseCase", "Schema30")]
    public async Task EfMappings_MatchAll402ColumnsInManifest()
    {
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(TestApi.Root, "docs/database/30-bang/model.json")));
        await using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true").Options);
        var entities = db.Model.GetEntityTypes().ToArray();
        Assert.Equal(30, entities.Length);
        Assert.Equal(402, entities.Sum(e => e.GetProperties().Count()));
        Assert.Equal(63, entities.Sum(e => e.GetForeignKeys().Count()));
        foreach (var table in manifest.RootElement.GetProperty("tables").EnumerateArray())
        {
            var name = table.GetProperty("name").GetString()!;
            var entity = Assert.Single(entities, e => e.GetTableName() == name);
            var store = StoreObjectIdentifier.Table(name, "dbo");
            Assert.Equal(table.GetProperty("fields").EnumerateArray().Select(f => f.GetProperty("name").GetString()).Order(),
                entity.GetProperties().Select(p => p.GetColumnName(store)).Order());
        }
    }

    private static Task<string> Script() => File.ReadAllTextAsync(Path.Combine(TestApi.Root, "docs/database/30-bang/012-simplify-columns.sql"));

    private static async Task Prepare(TestApi api)
    {
        await api.Sql($"CREATE DATABASE [{api.Database}]", "master");
        await api.Sql(await File.ReadAllTextAsync(Path.Combine(TestApi.Root, "docs/database/migrations/30-bang/schema-before-column-simplification.sql")));
        await api.Sql("""
            INSERT dbo.Ban(MaBanHienThi,TenBan,TenKhuVuc,SoChoNgoi,DangSuDung) VALUES('TEST-01',N'Bàn thử',N'Tầng cũ',6,1);
            INSERT dbo.NguyenLieu(TenNguyenLieu,DonViCoSo,TonAnToan,NguongCanhBao,DangSuDung) VALUES(N'Nguyên liệu thử','g',123.5,200,1);
            INSERT dbo.TonKho(MaNguyenLieu,TonThucTe,TongGiuCho) VALUES(1,1000,0);
            """);
    }

    [Fact, Trait("Layer", "Database"), Trait("UseCase", "Schema30")]
    public async Task Upgrade_ArchivesRemovedValues_PreservesRemainingRows_AndIsIdempotent()
    {
        string? before = null;
        await using var api = await TestApi.Start("Columns-Archive", beforeInitialize: async f =>
        {
            await Prepare(f);
            before = await f.Sql("SELECT MaNguyenLieu,TenNguyenLieu,DonViCoSo,NguongCanhBao,DangSuDung,ThoiDiemTao,ThoiDiemCapNhat,PhienBanDuLieu FROM dbo.NguyenLieu FOR JSON PATH");
            await f.Sql(await Script());
        });
        Assert.Equal(before, await api.Sql("SELECT MaNguyenLieu,TenNguyenLieu,DonViCoSo,NguongCanhBao,DangSuDung,ThoiDiemTao,ThoiDiemCapNhat,PhienBanDuLieu FROM dbo.NguyenLieu WHERE MaNguyenLieu=1 FOR JSON PATH"));
        Assert.Equal("123.5000", await api.Sql("SELECT CONVERT(decimal(18,4),JSON_VALUE(DuLieuTruocJson,'$[0].TonAnToan')) FROM dbo.NhatKyHeThong WHERE HanhDong='SCHEMA_COLUMNS_ARCHIVE' AND LoaiDoiTuong='NguyenLieu'"));
        Assert.Equal("Tầng cũ|6", await api.Sql("SELECT CONCAT(JSON_VALUE(DuLieuTruocJson,'$[0].TenKhuVuc'),'|',JSON_VALUE(DuLieuTruocJson,'$[0].SoChoNgoi')) FROM dbo.NhatKyHeThong WHERE HanhDong='SCHEMA_COLUMNS_ARCHIVE' AND LoaiDoiTuong='Ban'"));
        Assert.Equal("402", await api.Sql("SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id WHERE t.is_ms_shipped=0 AND t.name<>'sysdiagrams'"));
        Assert.Equal("63", await api.Sql("SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=0 AND is_not_trusted=0"));
        var archives = await api.Sql("SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE HanhDong='SCHEMA_COLUMNS_ARCHIVE'");
        await api.Sql(await Script());
        await api.RunApiCommand("--init-db");
        Assert.Equal(archives, await api.Sql("SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE HanhDong='SCHEMA_COLUMNS_ARCHIVE'"));
        Assert.Equal("1000.0000|0.0000", await api.Sql("SELECT CONCAT(TonThucTe,'|',TongGiuCho) FROM dbo.TonKho WHERE MaNguyenLieu=1"));
        await api.RunApiCommand("--check-db");
    }

    [Fact, Trait("Layer", "Database"), Trait("UseCase", "Schema30")]
    public async Task LateFailure_RollsBackColumnsAndArchives_ThenRetriesSuccessfully()
    {
        await using var api = await TestApi.Start("Columns-Rollback", beforeInitialize: async f =>
        {
            await Prepare(f);
            // Dependency on a late column forces failure after earlier ALTER statements.
            await f.Sql("ALTER TABLE dbo.BienDongKho ADD CONSTRAINT CK_Test_KeepCreated CHECK(ThoiDiemTao>'2000-01-01')");
            var script = await Script();
            var error = await Assert.ThrowsAsync<SqlException>(() => f.Sql(script));
            Assert.Contains(error.Errors.Cast<SqlError>(), e => e.Number == 5074);
            Assert.Equal("423", await f.Sql("SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id WHERE t.is_ms_shipped=0"));
            Assert.Equal("0", await f.Sql("SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE HanhDong='SCHEMA_COLUMNS_ARCHIVE'"));
            Assert.Equal("123.5000", await f.Sql("SELECT TonAnToan FROM dbo.NguyenLieu WHERE MaNguyenLieu=1"));
            await f.Sql("ALTER TABLE dbo.BienDongKho DROP CONSTRAINT CK_Test_KeepCreated");
        });
        await api.RunApiCommand("--check-db");
    }

    [Fact, Trait("Layer", "Database"), Trait("UseCase", "Schema30")]
    public async Task PartialSchema_IsRejectedWithoutFurtherChanges()
    {
        await using var api = await TestApi.Start("Columns-Partial", beforeInitialize: async f =>
        {
            await Prepare(f);
            await f.Sql("ALTER TABLE dbo.PhienDangNhap DROP COLUMN ThongTinThietBi");
            var script = await Script();
            var error = await Assert.ThrowsAsync<SqlException>(() => f.Sql(script));
            Assert.Equal(51201, error.Number);
            Assert.Equal("422", await f.Sql("SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id WHERE t.is_ms_shipped=0"));
            await f.Sql("ALTER TABLE dbo.PhienDangNhap ADD ThongTinThietBi nvarchar(300) NULL");
        });
        await api.RunApiCommand("--check-db");
    }
}
