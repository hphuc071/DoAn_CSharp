using FourChang.Api.Shared.Data;
using System.Globalization;
using FourChang.Presentation.Shared.Contracts.Common;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Shared.Data;

// Một nơi duy nhất lọc các cột nhạy cảm trước khi trả bảng dữ liệu qua API.
public static class TableReader
{
    public static async Task<TableData> Read<T>(IQueryable<T> source) where T:class
    {
        var forbidden=new HashSet<string>(StringComparer.Ordinal){"MaBamKhoa","MatKhauBam","MaLamMoiDaBam","MaBamQR","TaiKhoanNhanMaHoa","NoiDungQR","MaBamYeuCau","DuLieuTruocJson","DuLieuSauJson","KetQuaDaChotJson"};
        var columns=typeof(T).GetProperties().Where(p=>!forbidden.Contains(p.Name)&&p.PropertyType!=typeof(byte[])).ToArray();
        var records=await source.AsNoTracking().Take(100).ToListAsync();
        var rows=records.Select(row=>columns.Select(p=>p.GetValue(row) switch{null=>"",DateTime time=>time.ToLocalTime().ToString("dd/MM/yyyy HH:mm",CultureInfo.GetCultureInfo("vi-VN")),decimal number=>number.ToString("0.####",CultureInfo.InvariantCulture),bool boolean=>boolean?"Có":"Không",var value=>Convert.ToString(value,CultureInfo.InvariantCulture)??""}).ToArray()).ToArray();
        return new(columns.Select(p=>p.Name).ToArray(),rows);
    }
}
