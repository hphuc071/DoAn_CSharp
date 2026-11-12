using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Admin;

/// <summary>Quyền quản lý địa chỉ mới đã được chấp thuận; API/SQL thật và Web đọc cùng một dữ liệu.</summary>
public sealed class CustomerAddressAdministrationTests
{
    /// <summary>Thêm/sửa/mặc định/ngừng, ownership, Web/Admin conflict, chống trùng và rollback audit.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-01"), Trait("Layer", "API")]
    public async Task SoDiaChi_QuanLyVaWeb_DungQuyen_Version_VaRollback()
    {
        await using var api = await TestApi.Start("AdminAddressBook");
        var admin = await api.AdminLogin(); var customer = await api.NewCustomer(); var user = await api.Login(customer);
        var url = $"/api/admin/customers/{customer.Id}/addresses";
        (await api.Send("GET", url)).Expect(401, "Ẩn danh");
        (await api.Send("GET", url, headers: user)).Expect(403, "Khách không dùng quyền Admin");
        var book = (await api.Send("GET", url, headers: admin)).Expect(200, "Sổ rỗng").Json;
        var request = Body(("MaThaoTac", NewId()), ("PhienBanHoSo", book["account"]!.Text("phienBanDuLieu")),
            ("NhanDiaChi", "Nhà riêng"), ("TenNguoiNhan", "Nguyễn Minh Anh"), ("DienThoaiNguoiNhan", "0912345678"),
            ("DiaChiChiTiet", "12 Đường Hoa, Phường An Bình, TP Hồ Chí Minh"), ("LaDiaChiMacDinh", true));
        var bad = request.Copy(); bad["DienThoaiNguoiNhan"] = "abc";
        (await api.Send("POST", url, bad, admin)).Expect(400, "Số điện thoại sai");
        book = (await api.Send("POST", url, request, admin)).Expect(200, "Thêm địa chỉ").Json;
        var first = book["diaChi"]![0]!; var firstId = first.Number("maDiaChi");
        (await api.Send("POST", url, request, admin)).Expect(200, "Gửi lặp không thêm trùng");
        var web = (await api.Send("GET", "/api/customer/profile", headers: user)).Expect(200, "Web thấy địa chỉ Admin vừa lưu").Json;
        Assert.Equal(firstId, web["diaChi"]![0]!.Number("maDiaChi"));
        var other = await api.NewCustomer();
        var wrong = request.Copy(); wrong["MaThaoTac"] = NewId();
        var otherBook = (await api.Send("GET", $"/api/admin/customers/{other.Id}/addresses", headers: admin)).Expect(200, "Khách khác").Json;
        wrong["PhienBanHoSo"] = otherBook["account"]!.Text("phienBanDuLieu"); wrong["PhienBanDiaChi"] = first.Text("phienBanDuLieu");
        (await api.Send("PUT", $"/api/admin/customers/{other.Id}/addresses/{firstId}", wrong, admin)).Expect(404, "Địa chỉ không thuộc khách");
        var second = request.Copy(); second["MaThaoTac"] = NewId(); second["PhienBanHoSo"] = book["account"]!.Text("phienBanDuLieu");
        second["NhanDiaChi"] = "Văn phòng"; second["DiaChiChiTiet"] = "24 Đường Trà, Phường An Bình, TP Hồ Chí Minh";
        book = (await api.Send("POST", url, second, admin)).Expect(200, "Địa chỉ thứ hai mặc định").Json;
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.DiaChiKhachHang WHERE MaKhachHang={customer.Id} AND LaDiaChiMacDinh=1 AND DangSuDung=1"));
        var a = book["diaChi"]!.AsArray().Single(x => x!.Number("maDiaChi") == firstId)!;
        var select = Body(("MaThaoTac", NewId()), ("PhienBanHoSo", book["account"]!.Text("phienBanDuLieu")), ("PhienBanDiaChi", a.Text("phienBanDuLieu")));
        book = (await api.Send("POST", url + $"/{firstId}/default", select, admin)).Expect(200, "Chọn lại mặc định").Json;
        (await api.Send("POST", url + $"/{firstId}/default", select, admin)).Expect(200, "Lặp mặc định");
        var edit = request.Copy(); edit["MaThaoTac"] = NewId(); edit["PhienBanHoSo"] = book["account"]!.Text("phienBanDuLieu");
        a = book["diaChi"]!.AsArray().Single(x => x!.Number("maDiaChi") == firstId)!;
        edit["PhienBanDiaChi"] = a.Text("phienBanDuLieu"); edit["DiaChiChiTiet"] = "36 Đường Cà Phê, Phường An Bình, TP Hồ Chí Minh";
        // Web sửa trước, request Admin với version cũ bị chặn.
        var self = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", book["account"]!.Text("phienBanDuLieu")), ("HoTen", "Khách cập nhật đồng thời"), ("Email", customer.Input.Text("Email")), ("SoDienThoai", customer.Input.Text("SoDienThoai")));
        (await api.Send("PUT", "/api/customer/profile", self, user)).Expect(200, "Web sửa hồ sơ");
        (await api.Send("PUT", url + $"/{firstId}", edit, admin)).Expect(409, "Admin dùng version cũ");
        book = (await api.Send("GET", url, headers: admin)).Expect(200, "Tải lại").Json;
        edit["PhienBanHoSo"] = book["account"]!.Text("phienBanDuLieu");
        await api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try { Check((await api.Send("PUT", url + $"/{firstId}", edit, admin)).Status >= 400, "Audit lỗi rollback"); }
        finally { await api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app"); }
        Assert.Equal(request.Text("DiaChiChiTiet"), await api.Sql($"SELECT DiaChiChiTiet FROM dbo.DiaChiKhachHang WHERE MaDiaChi={firstId}"));
        book = (await api.Send("GET", url, headers: admin)).Expect(200, "Sau rollback").Json;
        edit["PhienBanHoSo"] = book["account"]!.Text("phienBanDuLieu");
        a = book["diaChi"]!.AsArray().Single(x => x!.Number("maDiaChi") == firstId)!; edit["PhienBanDiaChi"] = a.Text("phienBanDuLieu");
        book = (await api.Send("PUT", url + $"/{firstId}", edit, admin)).Expect(200, "Sửa địa chỉ").Json;
        a = book["diaChi"]!.AsArray().Single(x => x!.Number("maDiaChi") == firstId)!;
        var remove = Body(("MaThaoTac", NewId()), ("PhienBanHoSo", book["account"]!.Text("phienBanDuLieu")), ("PhienBanDiaChi", a.Text("phienBanDuLieu")));
        book = (await api.Send("POST", url + $"/{firstId}/deactivate", remove, admin)).Expect(200, "Ngừng địa chỉ").Json;
        Assert.Equal("2", await api.Sql($"SELECT COUNT(*) FROM dbo.DiaChiKhachHang WHERE MaKhachHang={customer.Id}"));
        web = (await api.Send("GET", "/api/customer/profile", headers: user)).Expect(200, "Web bỏ địa chỉ ngừng").Json;
        Assert.Single(web["diaChi"]!.AsArray());
        (await api.Send("GET", "/api/admin/customers/address-operations/" + remove.Text("MaThaoTac"), headers: admin)).Expect(200, "Receipt");
    }
}
