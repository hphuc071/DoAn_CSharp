using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FourChang.Domain.Entities.Audit;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FourChang.Api.Features.Customer.Orders;

public static class OrderIntegrationSafety
{
    public static bool Enabled(IConfiguration config, string capability) =>
        bool.TryParse(config["Orders:SqlIntegrationValidated"], out var validated) && validated &&
        bool.TryParse(config["Orders:" + capability], out var enabled) && enabled;
    public static bool ValidVersion(string? version, out byte[] bytes)
    {
        bytes = [];
        try { bytes = Convert.FromBase64String(version ?? ""); return bytes.Length == 8; }
        catch (FormatException) { return false; }
    }
    internal static byte[] Hash<T>(T value) => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value));

    // Shares the global operation namespace with UC06's unique audit key. Never a session-owned lock.
    internal static async Task<bool> LockAsync(CafeDbContext db, IDbContextTransaction tx, string resource, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = tx.GetDbTransaction();
        command.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; SELECT @r;";
        var parameter = command.CreateParameter(); parameter.ParameterName = "@resource";
        parameter.DbType = DbType.String; parameter.Size = 255; parameter.Value = resource;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync(ct);
        return result is not null and not DBNull && Convert.ToInt32(result, CultureInfo.InvariantCulture) >= 0;
    }
    internal static NhatKyHeThong SystemAudit(Guid operation, string action, string? orderId,
        byte[] hash, object receipt, DateTime now) => new()
    {
        MaThaoTac = operation, MaLienKetXuLy = operation, LoaiTacNhan = "SYSTEM",
        TenNguoiThucHienDaChot = "ORDER_INTEGRATION", HanhDong = action, LoaiDoiTuong = "DonHang",
        MaDoiTuong = orderId, MaBamYeuCau = hash, KetQuaDaChotJson = JsonSerializer.Serialize(receipt),
        DuLieuSauJson = JsonSerializer.Serialize(receipt), KetQua = "SUCCESS", PhatSinhLuc = now
    };
}
