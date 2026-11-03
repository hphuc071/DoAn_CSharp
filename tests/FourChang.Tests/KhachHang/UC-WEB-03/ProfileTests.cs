using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Customer;
// UC-WEB-03: mỗi thay đổi kiểm tra chủ sở hữu, rowversion, audit và snapshot đơn đã chốt.
public sealed class ProfileTests
{

    /// <summary>
    /// Kiểm tra hồ sơ, quyền sở hữu địa chỉ, rowversion, snapshot đơn, đổi mật khẩu và biên nhận phục hồi sau timeout.
    /// </summary>
    [Fact, Trait("Actor", "KhachHang"), Trait("UseCase", "UC-WEB-03"), Trait("Layer", "API")]
    public async Task HoSo_DiaChi_MatKhau_SoHuu_VaKhoiPhucReceipt()
    {
        await using var api = await TestApi.Start("UC-WEB-03");
        var a = await api.NewCustomer("selfa");
        var b = await api.NewCustomer("selfb");
        var ha = await api.Login(a);
        var hb = await api.Login(b);
        var p = await api.Profile(ha);
        (await api.Send("GET", "/api/customer/profile")).Expect(401, "Ẩn danh không đọc hồ sơ");
        var save = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", p.Text("phienBanDuLieu")), ("HoTen", "Tên khách đã sửa"), ("Email", a.Input.Text("Email")), ("SoDienThoai", "0912345678"));
        (await api.Send("PUT", "/api/customer/profile", save, ha)).Expect(200, "Lưu hồ sơ");
        (await api.Send("PUT", "/api/customer/profile", save, ha)).Expect(200, "Lưu lặp");
        var stale = save.Copy();
        stale["MaThaoTac"] = NewId();
        (await api.Send("PUT", "/api/customer/profile", stale, ha)).Expect(409, "Rowversion cũ");
        p = await api.Profile(ha);
        save["MaThaoTac"] = NewId();
        save["PhienBanDuLieu"] = p.Text("phienBanDuLieu");
        save["Email"] = b.Input.Text("Email");
        (await api.Send("PUT", "/api/customer/profile", save, ha)).Expect(409, "Email khách khác");
        save["Email"] = a.Input.Text("Email");
        save["MaThaoTac"] = NewId();
        save["HoTen"] = "";
        (await api.Send("PUT", "/api/customer/profile", save, ha)).Expect(400, "Tên rỗng");
        var address = Body(("MaThaoTac", NewId()), ("PhienBanHoSo", p.Text("phienBanDuLieu")), ("NhanDiaChi", "Nhà"), ("TenNguoiNhan", "Người nhận"), ("DienThoaiNguoiNhan", "0912345678"), ("DiaChiChiTiet", "Địa chỉ thử nghiệm"), ("ViDo", 10.7), ("KinhDo", 106.6), ("GhiChuGiaoHang", "Gọi trước"), ("LaDiaChiMacDinh", true));
        (await api.Send("POST", "/api/customer/addresses", address, ha)).Expect(200, "Thêm địa chỉ mặc định");
        (await api.Send("POST", "/api/customer/addresses", address, ha)).Expect(200, "Thêm lặp");
        p = await api.Profile(ha);
        Assert.Single(p["diaChi"]!.AsArray());
        var id = p["diaChi"]![0]!.Number("maDiaChi");
        var version = p["diaChi"]![0]!.Text("phienBanDuLieu");
        await api.Sql($"INSERT dbo.DonHang(MaDonHienThi,MaKhachHang,KenhDatHang,HinhThucNhanMon,TenNguoiNhan,DienThoaiNguoiNhan,DiaChiGiaoDaChot,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai) SELECT 'ACCOUNT-SNAPSHOT',{a.Id},'WEB','DELIVERY',TenNguoiNhan,DienThoaiNguoiNhan,DiaChiChiTiet,15000,15000,'PENDING_CONFIRMATION' FROM dbo.DiaChiKhachHang WHERE MaDiaChi={id}");
        var other = address.Copy();
        other["MaThaoTac"] = NewId();
        other["PhienBanHoSo"] = (await api.Profile(hb)).Text("phienBanDuLieu");
        other["PhienBanDiaChi"] = version;
        (await api.Send("PUT", $"/api/customer/addresses/{id}", other, hb)).Expect(404, "Không sửa địa chỉ người khác");
        address["MaThaoTac"] = NewId();
        address["PhienBanHoSo"] = p.Text("phienBanDuLieu");
        address["NhanDiaChi"] = "Công ty";
        address["DiaChiChiTiet"] = "Địa chỉ công ty thử nghiệm";
        (await api.Send("POST", "/api/customer/addresses", address, ha)).Expect(200, "Thêm mặc định thứ hai");
        p = await api.Profile(ha);
        Assert.Single(p["diaChi"]!.AsArray(), x => x!.Flag("laDiaChiMacDinh"));
        var edit = address.Copy();
        edit["MaThaoTac"] = NewId();
        edit["PhienBanHoSo"] = p.Text("phienBanDuLieu");
        edit["PhienBanDiaChi"] = p["diaChi"]!.AsArray().Single(x => x!.Number("maDiaChi") == id)!.Text("phienBanDuLieu");
        edit["LaDiaChiMacDinh"] = false;
        edit["DiaChiChiTiet"] = "Địa chỉ nhà đã sửa";
        (await api.Send("PUT", $"/api/customer/addresses/{id}", edit, ha)).Expect(200, "Sửa địa chỉ");
        p = await api.Profile(ha);
        var remove = Body(("MaThaoTac", NewId()), ("PhienBanHoSo", p.Text("phienBanDuLieu")), ("PhienBanDiaChi", p["diaChi"]!.AsArray().Single(x => x!.Number("maDiaChi") == id)!.Text("phienBanDuLieu")));
        (await api.Send("POST", $"/api/customer/addresses/{id}/remove", remove, ha)).Expect(200, "Bỏ địa chỉ");
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.DiaChiKhachHang WHERE MaDiaChi={id} AND DangSuDung=0"));
        // Xác nhận địa chỉ và trạng thái của đơn đã chốt vẫn nguyên vẹn sau thay đổi hồ sơ, địa chỉ hoặc mật khẩu.
        async Task CheckSnapshot() => Assert.Equal("1", await api.Sql("SELECT COUNT(*) FROM dbo.DonHang WHERE MaDonHienThi='ACCOUNT-SNAPSHOT' AND DiaChiGiaoDaChot=N'Địa chỉ thử nghiệm' AND TrangThai='PENDING_CONFIRMATION'"));
        await CheckSnapshot();
        p = await api.Profile(ha);
        var atomic = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", p.Text("phienBanDuLieu")), ("HoTen", "Tên không được commit"), ("Email", a.Input.Text("Email")), ("SoDienThoai", "0912345678"));
        await api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try
        {
            Check((await api.Send("PUT", "/api/customer/profile", atomic, ha)).Status >= 400, "Audit lỗi");
        }
        finally
        {
            await api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app");
        }

        Assert.NotEqual("Tên không được commit", (await api.Profile(ha)).Text("hoTen"));
        (await api.Send("GET", "/api/customer/account/operations/" + atomic.Text("MaThaoTac"), headers: ha)).Expect(404, "Không receipt giả");
        var second = await api.Login(a);
        p = await api.Profile(ha);
        var newPassword = Password();
        var password = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", p.Text("phienBanDuLieu")), ("MatKhauHienTai", "Sai"), ("MatKhauMoi", newPassword), ("XacNhanMatKhau", newPassword));
        (await api.Send("POST", "/api/customer/password", password, ha)).Expect(400, "Mật khẩu hiện tại sai");
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE MaKhachHang={a.Id} AND HanhDong='CUSTOMER_PASSWORD_DENIED'"));
        password["MaThaoTac"] = NewId();
        password["MatKhauHienTai"] = a.Input.Text("MatKhau");
        (await api.Send("POST", "/api/customer/password", password, ha)).Expect(200, "Đổi mật khẩu");
        (await api.Send("GET", "/api/customer/profile", headers: ha)).Expect(401, "Thu hồi phiên nguồn");
        (await api.Send("GET", "/api/customer/profile", headers: second)).Expect(401, "Thu hồi phiên thứ hai");
        (await api.Send("GET", "/api/customer/account/operations/" + password.Text("MaThaoTac"), headers: ha)).Expect(200, "Nguồn đọc receipt riêng");
        (await api.Send("GET", "/api/customer/account/operations/" + password.Text("MaThaoTac"), headers: hb)).Expect(404, "Người khác không đọc receipt");
        a.Input["MatKhau"] = newPassword;
        (await api.Send("GET", "/api/customer/profile", headers: await api.Login(a))).Expect(200, "Mật khẩu mới hoạt động");
        var audit = await api.Sql($"SELECT COALESCE(DuLieuTruocJson,'')+COALESCE(DuLieuSauJson,'')+COALESCE(KetQuaDaChotJson,'') FROM dbo.NhatKyHeThong WHERE MaKhachHang={a.Id}");
        Check(!audit.Contains(newPassword) && !audit.Contains("MatKhauBam") && !audit.Contains("MaLamMoiDaBam"), "Audit không chứa bí mật");
        await CheckSnapshot();
    }
}
