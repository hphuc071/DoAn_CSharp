using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using FourChang.Infrastructure.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Infrastructure.Persistence;

public static class DatabaseBootstrap
{
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
                schema.CommandText = await ReadResourceAsync("FourChang.Schema34.sql");
                await schema.ExecuteNonQueryAsync();
            }
            else await ValidateAsync(connection);
        }
        await CreateApplicationLoginAsync();
        await using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>().UseSqlServer(DatabaseSettings.ConnectionString()).Options);
        await DemoSeed.RunAsync(db);
        await using var check = new SqlConnection(DatabaseSettings.ConnectionString());
        await check.OpenAsync();
        await ValidateAsync(check);
    }
    public static async Task ValidateAsync(SqlConnection connection)
    {
        using var manifest = JsonDocument.Parse(await ReadResourceAsync("FourChang.Model34.json"));
        var expected = manifest.RootElement.GetProperty("tables").EnumerateArray().Select(t => t.GetProperty("name").GetString()!).Order().ToArray();
        var actual = (await ReadTableNamesAsync(connection)).Order().ToArray();
        if (!expected.SequenceEqual(actual)) throw new InvalidOperationException("Database không đúng 34 bảng đã chốt. Dừng để bảo vệ dữ liệu; không tự xóa hoặc sửa schema cũ.");
        foreach (var table in manifest.RootElement.GetProperty("tables").EnumerateArray())
        {
            await using var query = connection.CreateCommand();
            query.CommandText = "SELECT c.name FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='dbo' AND t.name=@table";
            query.Parameters.AddWithValue("@table", table.GetProperty("name").GetString());
            var columns = new List<string>();
            await using (var reader = await query.ExecuteReaderAsync()) while(await reader.ReadAsync()) columns.Add(reader.GetString(0));
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
        await using(var reader = await foreignKeys.ExecuteReaderAsync()) while(await reader.ReadAsync())actualKeys.Add(string.Join('|',reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3)));
        var expectedKeys = manifest.RootElement.GetProperty("relations").EnumerateArray().Select(r=>string.Join('|',r.GetProperty("child").GetString(),string.Join(',',r.GetProperty("fields").EnumerateArray().Select(x=>x.GetString())),r.GetProperty("parent").GetString(),string.Join(',',r.GetProperty("target").EnumerateArray().Select(x=>x.GetString())))).ToHashSet(StringComparer.Ordinal);
        if(!actualKeys.SetEquals(expectedKeys))throw new InvalidOperationException("FK không nối đúng thuộc tính đã chốt.");
        await using var integrity = connection.CreateCommand();
        integrity.CommandText = "SELECT (SELECT COUNT(*) FROM sys.key_constraints WHERE type='PK'), (SELECT COUNT(*) FROM sys.check_constraints WHERE is_disabled=0 AND is_not_trusted=0), (SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1 AND is_disabled=0)";
        await using(var reader = await integrity.ExecuteReaderAsync())
        {
            await reader.ReadAsync();
            var checkCount = manifest.RootElement.GetProperty("tables").EnumerateArray().Sum(t=>t.GetProperty("checks").GetArrayLength());
            if(reader.GetInt32(0)!=34||reader.GetInt32(1)!=checkCount||reader.GetInt32(2)!=11)throw new InvalidOperationException("PK, CHECK hoặc trigger chưa đúng bộ thiết kế.");
        }
    }
    private static async Task<List<string>> ReadTableNamesAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY t.name";
        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result;
    }
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
        grant.CommandText = "IF USER_ID(N'fourchang_app') IS NULL CREATE USER [fourchang_app] FOR LOGIN [fourchang_app]; GRANT SELECT, INSERT, UPDATE ON SCHEMA::dbo TO [fourchang_app]; DENY DELETE ON SCHEMA::dbo TO [fourchang_app];";
        await grant.ExecuteNonQueryAsync();
    }
    private static async Task<string> ReadResourceAsync(string name)
    {
        await using var stream = typeof(DatabaseBootstrap).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException("Thiếu tài nguyên schema.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
