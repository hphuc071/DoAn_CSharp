using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Customer;

public sealed class MenuCatalogTests
{
    [Fact, Trait("Actor", "KhachHang"), Trait("UseCase", "UC-WEB-04"), Trait("Layer", "API")]
    public async Task DuyetThucDon_TimKiemLocVaXemChiTietBienThe_ThanhCong()
    {
        await using var api = await TestApi.Start("UC-WEB-04");

        // Bước 1-2: Lấy danh sách thực đơn công bố
        var res = await api.Send("GET", "/api/menu");
        res.Expect(200, "Lấy danh sách thực đơn thành công");

        var products = res.Json.AsArray();
        Assert.NotNull(products);
        if (products.Count > 0)
        {
            var first = products[0]!;
            long maMon = first.Number("maMon") != 0 ? first.Number("maMon") : first.Number("id");
            Check(maMon > 0, "Mã món phải lớn hơn 0");
            Check(!string.IsNullOrWhiteSpace(first.Text("name")), "Tên món không được để trống");

            // Bước 3-4: Lọc món còn hàng và sắp xếp theo giá tăng dần
            var filterRes = await api.Send("GET", "/api/menu?availableOnly=true&sort=price_asc");
            filterRes.Expect(200, "Tra cứu thực đơn kèm bộ lọc và sắp xếp thành công");

            // Bước 5: Xem chi tiết biến thể Size/Nhiệt độ, mức Đường/Đá và Topping của món
            var variantRes = await api.Send("GET", $"/api/menu/{maMon}/variants");
            variantRes.Expect(200, "Xem chi tiết biến thể món đang bán thành công");
        }
    }

    [Fact, Trait("Actor", "KhachHang"), Trait("UseCase", "UC-WEB-04"), Trait("Layer", "API")]
    public async Task DuyetThucDon_BoLocGiaSaiHoacMonNgungBan_XuLyNgoaiLeChinhXac()
    {
        await using var api = await TestApi.Start("UC-WEB-04-Ex");

        // Ngoại lệ E2: Khoảng giá từ lớn hơn giá đến -> 400 BadRequest
        var badFilterRes = await api.Send("GET", "/api/menu?minPrice=100000&maxPrice=20000");
        badFilterRes.Expect(400, "Khoảng giá lọc không hợp lệ phải trả về 400 BadRequest");

        // Ngoại lệ E3(b): Truy cập trực tiếp URL chi tiết của món không tồn tại / ngừng bán -> 404 NotFound
        var notFoundRes = await api.Send("GET", "/api/menu/999999/variants");
        notFoundRes.Expect(404, "Món ngừng phục vụ hoặc không tồn tại phải trả về 404 NotFound");
    }
}
