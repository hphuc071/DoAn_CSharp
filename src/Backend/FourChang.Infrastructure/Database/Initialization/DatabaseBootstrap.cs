using FourChang.Infrastructure.Database.Context;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using FourChang.Infrastructure.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Infrastructure.Database.Initialization;

public static class DatabaseBootstrap
{
    /// <summary>Kiểm tra schema trước khi nâng cấp có kiểm soát, khởi tạo dữ liệu và cấp quyền ứng dụng tối thiểu.</summary>
    public static async Task InitializeAsync()
    {
        var database = DatabaseSettings.DatabaseName;
        if (!Regex.IsMatch(database, "^[A-Za-z][A-Za-z0-9_]{0,63}$")) throw new InvalidOperationException("Tên database không hợp lệ.");
        await using (var master = new SqlConnection(DatabaseSettings.ConnectionString(true, "master")))
        {
            await master.OpenAsync();
            await using var create = master.CreateCommand();
            create.CommandText = $"IF DB_ID(@database) IS NULL EXEC(N'CREATE DATABASE [{database}]');";
            create.Parameters.AddWithValue("@database", database);
            await create.ExecuteNonQueryAsync();
        }
        await using (var connection = new SqlConnection(DatabaseSettings.ConnectionString(true)))
        {
            await connection.OpenAsync();
            var existing = await ReadTableNamesAsync(connection);
            if (existing.Count == 0)
            {
                await using var schema = connection.CreateCommand();
                schema.CommandTimeout = 120;
                schema.CommandText = await ReadResourceAsync("FourChang.Schema35.sql");
                await schema.ExecuteNonQueryAsync();
            }
            else if (!existing.Contains("ThietBiPOS"))
            {
                // Chỉ nâng cấp từ schema 34 bảng đã xác minh; không tự sửa database lạ hoặc xóa sysdiagrams.
                await ValidateAsync(connection, "FourChang.Model34.json");
                await using var upgrade = connection.CreateCommand();
                upgrade.CommandText = await ReadResourceAsync("FourChang.Upgrade35.sql");
                await upgrade.ExecuteNonQueryAsync();
            }
            else await ValidateAsync(connection, existing.Contains("Banner") ? (await HasPhoneRules(connection) ? (await HasRequiredPhone(connection) ? (await HasAdminFinance(connection) ? "FourChang.Model37.json" : "FourChang.Model37BeforeFinance.json") : "FourChang.Model37BeforeRequiredPhone.json") : "FourChang.Model37BeforePhones.json") : await HasEmployeeAddress(connection) ? "FourChang.Model35.json" : "FourChang.Model35Legacy.json");
            if (!await HasEmployeeAddress(connection))
            {
                // Chỉ nâng cấp bộ 35 bảng cũ đã nhận diện chính xác, không tự sửa database lạ.
                await ValidateAsync(connection, "FourChang.Model35Legacy.json");
                await using var patch = connection.CreateCommand();
                patch.CommandText = await ReadResourceAsync("FourChang.EmployeeAddress.sql");
                await patch.ExecuteNonQueryAsync();
            }
        }
        // Chỉ nâng cấp khi đủ schema 35 đã chốt; lỗi DDL rollback, không tự sửa schema lạ.
        await using (var connection = new SqlConnection(DatabaseSettings.ConnectionString(true)))
        {
            await connection.OpenAsync();
            if (!(await ReadTableNamesAsync(connection)).Contains("Banner"))
            {
                await ValidateAsync(connection, "FourChang.Model35.json");
                await using var upgrade = connection.CreateCommand();
                upgrade.CommandTimeout = 120;
                upgrade.CommandText = await ReadResourceAsync("FourChang.Upgrade37.sql");
                await upgrade.ExecuteNonQueryAsync();
            }
            if (!await HasPhoneRules(connection))
            {
                await ValidateAsync(connection, "FourChang.Model37BeforePhones.json");
                await using var phonePatch = connection.CreateCommand();
                phonePatch.CommandText = await ReadResourceAsync("FourChang.CustomerPhones.sql");
                await phonePatch.ExecuteNonQueryAsync();
            }
            if (!await HasRequiredPhone(connection))
            {
                await ValidateAsync(connection, "FourChang.Model37BeforeRequiredPhone.json");
                await using var requiredPhone = connection.CreateCommand();
                requiredPhone.CommandText = await ReadResourceAsync("FourChang.RequiredCustomerPhone.sql");
                await requiredPhone.ExecuteNonQueryAsync();
            }
            if (!await HasAdminFinance(connection))
            {
                // Chỉ nâng cấp đúng schema cũ đã nhận diện; rollback DDL nếu dữ liệu trùng nguồn.
                await ValidateAsync(connection, "FourChang.Model37BeforeFinance.json");
                await using var finance = connection.CreateCommand();
                finance.CommandText = await ReadResourceAsync("FourChang.AdminFinance.sql");
                await finance.ExecuteNonQueryAsync();
            }
            await ValidateAsync(connection);
        }
        await CreateApplicationLoginAsync();
        await using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>().UseSqlServer(DatabaseSettings.ConnectionString()).Options);
        await DemoSeed.RunAsync(db);
        await using var check = new SqlConnection(DatabaseSettings.ConnectionString());
        await check.OpenAsync();
        await ValidateAsync(check);
    }
    /// <summary>Nhận diện bổ sung tài chính trước khi so khớp đầy đủ model, không suy từ số bảng.</summary>
    private static async Task<bool> HasAdminFinance(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN COL_LENGTH(N'dbo.HoaDon',N'MaHoanTien') IS NOT NULL AND COL_LENGTH(N'dbo.HoSoHoanTien',N'LoaiNguon') IS NOT NULL AND COL_LENGTH(N'dbo.DonHang',N'MaXuLyNgoaiLe') IS NOT NULL THEN 1 ELSE 0 END";
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    /// <summary>Nhận biết phiên bản schema trước/sau cột địa chỉ đã được chấp thuận, không dựa vào số bảng đơn thuần.</summary>
    private static async Task<bool> HasEmployeeAddress(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN COL_LENGTH(N'dbo.NhanVien',N'DiaChiLienHe') IS NULL THEN 0 ELSE 1 END";
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    /// <summary>Phân biệt phiên bản điện thoại nullable cũ với NOT NULL, sau đó vẫn đối chiếu toàn model.</summary>
    private static async Task<bool> HasRequiredPhone(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TaiKhoanKhachHang') AND name=N'SoDienThoaiChuanHoa' AND is_nullable=0";
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    /// <summary>Nhận diện cả hai ràng buộc mới; schema lạ vẫn bị bộ kiểm tra từ chối.</summary>
    private static async Task<bool> HasPhoneRules(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN OBJECT_ID(N'dbo.CK_KhachHang_DienThoai10',N'C') IS NOT NULL AND OBJECT_ID(N'dbo.CK_DiaChiKhachHang_DienThoai10',N'C') IS NOT NULL THEN 1 ELSE 0 END";
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    /// <summary>Đối chiếu cấu trúc SQL thực tế với model đã chốt; không bỏ qua bảng hoặc ràng buộc lạ.</summary>
    public static async Task ValidateAsync(SqlConnection connection, string manifestName = "FourChang.Model37.json")
    {
        using var manifest = JsonDocument.Parse(await ReadResourceAsync(manifestName));
        var expected = manifest.RootElement.GetProperty("tables").EnumerateArray().Select(t => t.GetProperty("name").GetString()!).Order().ToArray();
        var actual = (await ReadTableNamesAsync(connection)).Order().ToArray();
        if (!expected.SequenceEqual(actual)) throw new InvalidOperationException("Database không đúng bộ bảng nghiệp vụ đã chốt. Dừng để bảo vệ dữ liệu; không tự xóa hoặc sửa schema cũ.");
        foreach (var table in manifest.RootElement.GetProperty("tables").EnumerateArray())
        {
            await using var query = connection.CreateCommand();
            query.CommandText = "SELECT c.name FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='dbo' AND t.name=@table";
            query.Parameters.AddWithValue("@table", table.GetProperty("name").GetString());
            var columns = new List<string>();
            await using (var reader = await query.ExecuteReaderAsync()) while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
            var wanted = table.GetProperty("fields").EnumerateArray().Select(f => f.GetProperty("name").GetString()!).Order();
            if (!columns.Order().SequenceEqual(wanted)) throw new InvalidOperationException("Cột chưa khớp thiết kế: " + table.GetProperty("name").GetString());
        }
        await using var constraints = connection.CreateCommand();
        constraints.CommandText = "SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=0 AND is_not_trusted=0";
        var count = Convert.ToInt32(await constraints.ExecuteScalarAsync());

        if (count != manifest.RootElement.GetProperty("relations").GetArrayLength()) throw new InvalidOperationException("Số FK hoạt động chưa đúng mô hình.");
        await using var foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "SELECT tc.name,STRING_AGG(CAST(cc.name AS nvarchar(max)),',') WITHIN GROUP(ORDER BY c.constraint_column_id),tp.name,STRING_AGG(CAST(cp.name AS nvarchar(max)),',') WITHIN GROUP(ORDER BY c.constraint_column_id) FROM sys.foreign_keys f JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id JOIN sys.tables tc ON tc.object_id=f.parent_object_id JOIN sys.tables tp ON tp.object_id=f.referenced_object_id JOIN sys.columns cc ON cc.object_id=tc.object_id AND cc.column_id=c.parent_column_id JOIN sys.columns cp ON cp.object_id=tp.object_id AND cp.column_id=c.referenced_column_id GROUP BY f.object_id,tc.name,tp.name";
        var actualKeys = new HashSet<string>(StringComparer.Ordinal);
        await using (var reader = await foreignKeys.ExecuteReaderAsync()) while (await reader.ReadAsync()) actualKeys.Add(string.Join('|', reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        var expectedKeys = manifest.RootElement.GetProperty("relations").EnumerateArray().Select(r => string.Join('|', r.GetProperty("child").GetString(), string.Join(',', r.GetProperty("fields").EnumerateArray().Select(x => x.GetString())), r.GetProperty("parent").GetString(), string.Join(',', r.GetProperty("target").EnumerateArray().Select(x => x.GetString())))).ToHashSet(StringComparer.Ordinal);
        if (!actualKeys.SetEquals(expectedKeys)) throw new InvalidOperationException("FK không nối đúng thuộc tính đã chốt.");
        await using var integrity = connection.CreateCommand();
        integrity.CommandText = "SELECT (SELECT COUNT(*) FROM sys.key_constraints WHERE type='PK' AND parent_object_id<>ISNULL(OBJECT_ID(N'dbo.sysdiagrams'),-1)), (SELECT COUNT(*) FROM sys.check_constraints WHERE is_disabled=0 AND is_not_trusted=0), (SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1 AND is_disabled=0)";
        await using (var reader = await integrity.ExecuteReaderAsync())
        {
            await reader.ReadAsync();
            var checkCount = manifest.RootElement.GetProperty("tables").EnumerateArray().Sum(t => t.GetProperty("checks").GetArrayLength());
            if (reader.GetInt32(0) != manifest.RootElement.GetProperty("tables").GetArrayLength() || reader.GetInt32(1) != checkCount || reader.GetInt32(2) != 11) throw new InvalidOperationException("PK, CHECK hoặc trigger chưa đúng bộ thiết kế.");
        }
    }
    /// <summary>Đọc bảng thực tế, tách riêng sysdiagrams là bảng hỗ trợ SSMS để không làm sai số bảng nghiệp vụ.</summary>
    private static async Task<List<string>> ReadTableNamesAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        // Giữ nguyên bảng sơ đồ của SSMS theo xác nhận của người dùng. Chỉ bỏ qua khi đúng tên/schema và dấu hỗ trợ Microsoft.
        // Các bảng lạ khác vẫn bị chặn; PK của sysdiagrams không thuộc các bảng nghiệp vụ trong manifest.
        command.CommandText = "SELECT t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 AND NOT(s.name=N'dbo' AND t.name=N'sysdiagrams' AND EXISTS(SELECT 1 FROM sys.extended_properties ep WHERE ep.class=1 AND ep.major_id=t.object_id AND ep.minor_id=0 AND ep.name=N'microsoft_database_tools_support' AND CONVERT(nvarchar(10),ep.value)=N'1')) ORDER BY t.name";
        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result;
    }
    /// <summary>Cấp quyền SQL tối thiểu cho ứng dụng sau khi schema đã được kiểm tra.</summary>
    private static async Task CreateApplicationLoginAsync()
    {
        var password = Environment.GetEnvironmentVariable("SQL_APP_PASSWORD") ?? throw new InvalidOperationException("Thiếu SQL_APP_PASSWORD.");
        var escaped = password.Replace("'", "''");
        await using var master = new SqlConnection(DatabaseSettings.ConnectionString(true, "master"));
        await master.OpenAsync();
        await using var login = master.CreateCommand();
        login.CommandText = $"IF SUSER_ID(N'fourchang_app') IS NULL CREATE LOGIN [fourchang_app] WITH PASSWORD=N'{escaped}', CHECK_POLICY=ON; ELSE ALTER LOGIN [fourchang_app] WITH PASSWORD=N'{escaped}';";
        await login.ExecuteNonQueryAsync();
        await using var db = new SqlConnection(DatabaseSettings.ConnectionString(true));
        await db.OpenAsync();
        await using var grant = db.CreateCommand();
        grant.CommandText = "IF USER_ID(N'fourchang_app') IS NULL CREATE USER [fourchang_app] FOR LOGIN [fourchang_app]; GRANT SELECT, INSERT, UPDATE ON SCHEMA::dbo TO [fourchang_app]; REVOKE DELETE ON SCHEMA::dbo FROM [fourchang_app]; GRANT DELETE ON OBJECT::dbo.TaiKhoanKhachHang TO [fourchang_app];";
        await grant.ExecuteNonQueryAsync();
        // UC-ADM-01/11 chỉ xóa hồ sơ chưa tham chiếu; ca và audit vẫn không được xóa. DELETE phiếu kho chỉ dùng cho nháp; trigger vẫn chặn sửa/xóa POSTED. Không cấp DELETE đơn/tiền/nhật ký.
        using var manifest = JsonDocument.Parse(await ReadResourceAsync("FourChang.Model37.json"));
        foreach (var table in manifest.RootElement.GetProperty("tables").EnumerateArray())
        {
            var name = table.GetProperty("name").GetString()!;
            if (new[] { "TaiKhoanKhachHang", "NhanVien", "DanhMucMon", "Mon", "CauHinhMon", "MonThem", "CauHinhMonThem", "TuyChonPhaCheHopLe", "Banner", "NguyenLieu", "DonViTinh", "QuyDoiDonViNguyenLieu", "CongThuc", "ChiTietCongThuc", "PhieuKho", "ChiTietPhieuKho" }.Contains(name))
            {
                // Chỉ các đối tượng ADM-03 chưa tham chiếu được xóa qua service kiểm tra quyền/version.
                await using var allow = db.CreateCommand();
                allow.CommandText = $"REVOKE DELETE ON OBJECT::dbo.[{name}] FROM [fourchang_app]; GRANT DELETE ON OBJECT::dbo.[{name}] TO [fourchang_app]";
                await allow.ExecuteNonQueryAsync();
                continue;
            }
            await using var deny = db.CreateCommand();
            deny.CommandText = $"DENY DELETE ON OBJECT::dbo.[{name}] TO [fourchang_app]";
            await deny.ExecuteNonQueryAsync();
        }
    }
    /// <summary>Đọc model/script SQL nhúng trong assembly, bảo đảm bộ nâng cấp đi cùng phiên bản source.</summary>
    private static async Task<string> ReadResourceAsync(string name)
    {
        await using var stream = typeof(DatabaseBootstrap).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException("Thiếu tài nguyên schema.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
