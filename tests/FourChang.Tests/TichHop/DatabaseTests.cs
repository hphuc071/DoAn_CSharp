using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Integration;
// CLI --test-db là mã C# DatabaseChecks hiện có, được chạy với môi trường database riêng.
// xUnit nhận exit code và kiểm tra manifest thật, không gọi bất kỳ file .ps1 nào.
public sealed class DatabaseTests
{

    /// <summary>
    /// Kiểm tra đúng 35 bảng, 73 khóa ngoại và chạy 17 nhóm ràng buộc SQL bằng mã C# hiện có.
    /// </summary>
    [Fact, Trait("Actor", "TichHop"), Trait("UseCase", "Database"), Trait("Layer", "Database")]
    public async Task RangBuocDuLieu_35Bang_73KhoaNgoai()
    {
        await using var api = await TestApi.Start("Database");
        Assert.Equal("35", await api.Sql("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0"));
        Assert.Equal("73", await api.Sql("SELECT COUNT(*) FROM sys.foreign_keys"));
        await api.RunApiCommand("--test-db");
    }

    /// <summary>
    /// Nâng cấp database test từ 34 lên 35 bảng, xác nhận giữ nguyên sơ đồ SSMS và các ràng buộc nghiệp vụ.
    /// </summary>
    [Fact, Trait("Actor", "TichHop"), Trait("UseCase", "DatabaseUpgrade"), Trait("Layer", "Database")]
    public async Task NangCap34Thanh35_BaoToanSoDoSSMS()
    {
        await using var api = await TestApi.Start("DatabaseUpgrade", legacy: true);
        Assert.Equal("35", await api.Sql("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0 AND name<>'sysdiagrams'"));
        Assert.Equal("73", await api.Sql("SELECT COUNT(*) FROM sys.foreign_keys"));
        Assert.Equal("ERD đã lưu", await api.Sql("SELECT name FROM dbo.sysdiagrams WHERE diagram_id=1"));
        await api.RunApiCommand("--check-db");
        await api.RunApiCommand("--test-db");
    }
}
