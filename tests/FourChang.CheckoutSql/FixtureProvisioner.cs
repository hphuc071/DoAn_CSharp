// Reuses verified V2 schema/seed recipe on a NEW independently guarded instance. V2 source/data untouched.
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using FourChang.Domain.Entities.Accounts;
using FourChang.Domain.Entities.Inventory;
using FourChang.Domain.Entities.Menu;
using FourChang.Domain.Entities.Orders;
using FourChang.Domain.Entities.Payments;
using FourChang.Infrastructure.Database.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

static class FixtureProvisioner
{
    public static readonly string[] Slots=[];
    public static async Task<Fixture> CreateAsync(string schemaQuery,Dictionary<string,object?> evidence)
    {
        var schemaPath="docs/database/35-bang/schema.sql";
        var schemaBytes=await File.ReadAllBytesAsync(schemaPath);
        FixtureSqlGuard.Check(Convert.ToHexString(SHA256.HashData(schemaBytes))=="A58EA9C2779C4554DC47134DD7CEF41F5A1DA3838D240F5DCC3D54F264AB65F1","Canonical schema source hash mismatch");
        var schema=System.Text.Encoding.UTF8.GetString(schemaBytes);
        // The approved source has no USE/database/login/seed statements; EXPIRED is only a payment/
        // inventory status. Do not search/replace or extend the DonHang constraint to make tests pass.
        FixtureSqlGuard.Check(!Regex.IsMatch(schema,@"(?im)^\s*(USE\s|CREATE\s+DATABASE|INSERT\s|MERGE\s|GO\s*$)"),"Unexpected schema bootstrap scope");
        await using(var master=new SqlConnection(FixtureSqlGuard.Connection(admin:true,master:true)))
        {
            await master.OpenAsync(); await FixtureSqlGuard.VerifyAsync(master,requireMarker:false,master:true);
            await using var read=master.CreateCommand(); read.CommandText="SELECT COUNT(*) FROM sys.databases WHERE database_id>4";
            FixtureSqlGuard.Check(Convert.ToInt32(await read.ExecuteScalarAsync())==0,"New container contains unexpected user databases");
            await Execute(master,"CREATE DATABASE [FourChangUc06ConsentFixtureV4]",master:true,marker:false);
            var password=Environment.GetEnvironmentVariable("UC06_FIXTURE_APP_PASSWORD")??throw new SafetyStop("App credential missing");
            await Execute(master,"DECLARE @s nvarchar(max)=N'CREATE LOGIN [uc06_consent] WITH PASSWORD='+QUOTENAME(@pwd,'''')+N',DEFAULT_DATABASE=[FourChangUc06ConsentFixtureV4],CHECK_POLICY=ON'; EXEC sys.sp_executesql @s; GRANT VIEW SERVER STATE TO [uc06_consent]; GRANT VIEW SERVER PERFORMANCE STATE TO [uc06_consent];",master:true,marker:false,parameters:[new SqlParameter("@pwd",password)]);
        }
        await using(var sql=new SqlConnection(FixtureSqlGuard.Connection(admin:true)))
        {
            await sql.OpenAsync(); await FixtureSqlGuard.VerifyAsync(sql,requireMarker:false);
            await using var empty=sql.CreateCommand(); empty.CommandText="SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0";
            FixtureSqlGuard.Check(Convert.ToInt32(await empty.ExecuteScalarAsync())==0,"Fixture schema is not empty");
            evidence["schemaInitializationStarted"]=true;
            await Execute(sql,schema,marker:false);
            await Execute(sql,"EXEC sys.sp_addextendedproperty @name=N'FourChang.Uc06Consent.FixtureRunId',@value=@run",marker:false,parameters:[new SqlParameter("@run",FixtureSqlGuard.RunId.ToString())]);
            await Execute(sql,"CREATE USER [uc06_consent] FOR LOGIN [uc06_consent]; GRANT SELECT,INSERT,UPDATE ON SCHEMA::dbo TO [uc06_consent]; GRANT VIEW DEFINITION TO [uc06_consent];");
            await using var counts=sql.CreateCommand(); counts.CommandText="SELECT (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0),(SELECT COUNT(*) FROM sys.foreign_keys),(SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1),(SELECT definition FROM sys.check_constraints WHERE name='CK_DonHang_7')";
            await using var r=await counts.ExecuteReaderAsync();
            FixtureSqlGuard.Check(await r.ReadAsync() && r.GetInt32(0)==35 && r.GetInt32(1)==73 && r.GetInt32(2)==11 && !r.GetString(3).Contains("EXPIRED",StringComparison.Ordinal),"BLOCKED_SCHEMA: canonical schema verification failed");
            evidence["schema"]=new {path=schemaPath,sha256=Convert.ToHexString(SHA256.HashData(schemaBytes)),tables=35,foreignKeys=73,triggers=11,expiredOrderMigration=false};
        }
        evidence["F0"]=await CountsAsync();
        var fixture=await SeedAsync();
        await using(var sql=new SqlConnection(FixtureSqlGuard.Connection()))
        {
            await sql.OpenAsync(); await FixtureSqlGuard.VerifyAsync(sql);
            var shape=await ReadRows(sql,schemaQuery);
            fixture=fixture with { SchemaSha256=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(shape))) };
            evidence["schemaMetadata"]=shape;
        }
        var bytes=JsonSerializer.SerializeToUtf8Bytes(fixture,new JsonSerializerOptions{WriteIndented=true});
        await File.WriteAllBytesAsync("artifacts/uc-web-06/sql-consent-v4/manifest.json",bytes);
        evidence["manifestSha256"]=Convert.ToHexString(SHA256.HashData(bytes));
        evidence["fixture"]=fixture;
        evidence["F1"]=await CountsAsync();
        await ConsentRun.ValidateSeedAsync(fixture);
        evidence["F1Validated"]=true;
        return fixture;
    }
    public static async Task<object> CountsAsync()
    {
        await using var sql=new SqlConnection(FixtureSqlGuard.Connection());await sql.OpenAsync();
        return await ReadRows(sql,"SELECT (SELECT COUNT_BIG(*) FROM dbo.TaiKhoanKhachHang) AS customers,(SELECT COUNT_BIG(*) FROM dbo.TaiKhoanQuanLy) AS admins,(SELECT COUNT_BIG(*) FROM dbo.DonHang) AS orders,(SELECT COUNT_BIG(*) FROM dbo.ThanhToan) AS payments,(SELECT COUNT_BIG(*) FROM dbo.NhatKyHeThong) AS audits,(SELECT COUNT_BIG(*) FROM dbo.GiaoDichNganHang) AS banks,(SELECT COUNT_BIG(*) FROM dbo.HoSoHoanTien) AS refunds,(SELECT COUNT_BIG(*) FROM dbo.GiuChoNguyenLieu) AS holds,(SELECT COUNT_BIG(*) FROM dbo.NguyenLieuChiTietDonHang) AS bom,(SELECT COUNT_BIG(*) FROM dbo.TonKho) AS stock");
    }
    static async Task<Fixture> SeedAsync()
    {
        await using var db=new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>().UseSqlServer(FixtureSqlGuard.Connection(admin:true))
            .AddInterceptors(new FixtureConnectionGuard(),new FixtureCommandGuard()).Options);
        await using var tx=await db.Database.BeginTransactionAsync();
        FixtureSqlGuard.Check(!await db.TaiKhoanKhachHang.AnyAsync() && !await db.DonHang.AnyAsync() && !await db.NhatKyHeThong.AnyAsync(),"Fixture data is not empty");
        var now=DateTime.UtcNow; var deadline=new DateTime(now.AddHours(2).Ticks/TimeSpan.TicksPerMillisecond*TimeSpan.TicksPerMillisecond,DateTimeKind.Utc);
        var admin=new TaiKhoanQuanLy {TenDangNhapChuanHoa="UC06_FIXTURE_ADMIN",HoTen="Fixture administrator",MatKhauBam=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),TrangThai="LOCKED"};
        // Canonical CK_TaiKhoanKhachHang_3 requires a contact. Reserved .invalid addresses are
        // synthetic fixture data, never real customer contacts. Do not disable the CHECK.
        var a=new TaiKhoanKhachHang {TenDangNhapChuanHoa="UC06_CUSTOMER_A",HoTen="Synthetic A",EmailChuanHoa="ph5-a@example.invalid",MatKhauBam=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),TrangThai="LOCKED"};
        var b=new TaiKhoanKhachHang {TenDangNhapChuanHoa="UC06_CUSTOMER_B",HoTen="Synthetic B",EmailChuanHoa="ph5-b@example.invalid",MatKhauBam=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),TrangThai="LOCKED"};
        var unit=new DonViTinh {KyHieu="g",TenDonVi="Fixture gram",DangSuDung=true};
        var category=new DanhMucMon {TenDanhMuc="Fixture only",DaCongBo=true,DangSuDung=true};
        db.AddRange(admin,a,b,unit,category); await db.SaveChangesAsync();
        var ingredient=new NguyenLieu {TenNguyenLieu="Fixture ingredient",MaDonViCoSo=unit.MaDonVi,DangSuDung=true};
        var item=new Mon {MaDanhMuc=category.MaDanhMuc,TenMon="Fixture beverage",DaCongBo=true,DangBan=true};
        db.AddRange(ingredient,item); await db.SaveChangesAsync();
        var option=new CauHinhMon {MaMon=item.MaMon,KichCo="M",NhietDo="ICED",GiaBan=10000,DangBan=true};
        db.Add(option); await db.SaveChangesAsync();
        db.Add(new TuyChonPhaCheHopLe {MaCauHinhMon=option.MaCauHinhMon,PhanTramDuong=100,PhanTramDa=100,DangSuDung=true}); await db.SaveChangesAsync();
        var recipe=new CongThuc {MaCauHinhMon=option.MaCauHinhMon,SoPhienBan=1,TenCongThuc="Fixture recipe",TrangThai="DRAFT",MaQuanLyTao=admin.MaQuanLy};
        db.Add(recipe); await db.SaveChangesAsync();
        db.Add(new ChiTietCongThuc {MaCongThuc=recipe.MaCongThuc,MaNguyenLieu=ingredient.MaNguyenLieu,LoaiDinhMuc="FIXED",DinhMucCoSo=10});
        await db.SaveChangesAsync();
        recipe.TrangThai="ACTIVE"; recipe.MaQuanLyDuyet=admin.MaQuanLy; recipe.HieuLucTu=now;
        db.Add(new TonKho {MaNguyenLieu=ingredient.MaNguyenLieu,TonThucTe=1000,TongGiuCho=10*Slots.Length}); await db.SaveChangesAsync();
        var orders=new Dictionary<string,long>();
        foreach(var slot in Slots)
        {
            var order=new DonHang {MaDonHienThi="UC06-"+slot,MaKhachHang=a.MaKhachHang,KenhDatHang="WEB",HinhThucNhanMon="TAKEAWAY",TrangThai="PENDING_PAYMENT",HetHanLuc=deadline};
            db.Add(order); await db.SaveChangesAsync(); orders[slot]=order.MaDonHang;
            var line=new ChiTietDonHang {MaDonHang=order.MaDonHang,MaCauHinhMon=option.MaCauHinhMon,MaCongThuc=recipe.MaCongThuc,TenMonDaChot=item.TenMon,KichCoDaChot="M",NhietDoDaChot="ICED",PhanTramDa=100,PhanTramDuong=100,GiaMonDaChot=10000,SoLuongDat=1};
            db.Add(line); db.Add(new ThanhToan {MaDonHang=order.MaDonHang,MaThanhToanHienThi="UC06-P-"+slot,PhuongThuc="BANK_TRANSFER",SoTienYeuCau=10000,MaThamChieuChuyenKhoan="UC06-R-"+slot,TrangThai="PENDING",HetHanLuc=deadline,MaThaoTac=Guid.NewGuid()});
            await db.SaveChangesAsync();
            db.Add(new NguyenLieuChiTietDonHang {MaChiTietDonHang=line.MaChiTietDonHang,MaNguyenLieu=ingredient.MaNguyenLieu,TenNguyenLieuDaChot=ingredient.TenNguyenLieu,DonViCoSoDaChot="g",DinhMucMoiLy=10}); await db.SaveChangesAsync();
            db.Add(new GiuChoNguyenLieu {MaChiTietDonHang=line.MaChiTietDonHang,MaNguyenLieu=ingredient.MaNguyenLieu,SoLuongGiuCho=10,TrangThai="RESERVED",HetHanLuc=deadline}); await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return new(FixtureSqlGuard.Database,FixtureSqlGuard.RunId,"",a.MaKhachHang,b.MaKhachHang,orders);
    }
    static async Task Execute(SqlConnection sql,string text,bool master=false,bool marker=true,SqlParameter[]? parameters=null)
    {
        await FixtureSqlGuard.VerifyAsync(sql,requireMarker:marker,master:master);
        await using var cmd=sql.CreateCommand(); cmd.CommandText=text; cmd.CommandTimeout=60;
        if(parameters is not null) cmd.Parameters.AddRange(parameters);
        await cmd.ExecuteNonQueryAsync();
    }
    public static async Task<List<Dictionary<string,object?>>> ReadRows(SqlConnection sql,string query)
    {
        await FixtureSqlGuard.VerifyAsync(sql);
        await using var cmd=sql.CreateCommand();cmd.CommandText=query;cmd.CommandTimeout=15;
        await using var r=await cmd.ExecuteReaderAsync(); var rows=new List<Dictionary<string,object?>>();
        while(await r.ReadAsync()) {var row=new Dictionary<string,object?>(); for(var i=0;i<r.FieldCount;i++) row[r.GetName(i)]=r.IsDBNull(i)?null:r.GetValue(i); rows.Add(row);}
        return rows;
    }
}
