using FourChang.Api.Features.Payments.Simulation;
using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
namespace FourChang.Tests;
/// <summary>Kiểm tra khóa môi trường trước mọi truy vấn SQL, không dựa vào việc ẩn nút trên UI.</summary>
public sealed class SimulationSafetyTests
{
    /// <summary>Production luôn chặn dù bật cờ; Development không bật cờ cũng không có chức năng giả thanh toán.</summary>
    [Theory, Trait("UseCase","UC-WEB-07")]
    [InlineData("Production","true")]
    [InlineData("Development","false")]
    [InlineData("Staging","true")]
    public void MoiTruongKhongChoPhep_ChanTruocSQL(string environment,string flag)
    {
        using var db=new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>().UseSqlServer("Server=forbidden.invalid;Database=NoDatabase;Integrated Security=true").Options);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Payments:SimulationEnabled"]=flag}).Build();
        var service=new PaymentSimulationService(db,config,new HostEnvironment{EnvironmentName=environment});
        Assert.False(service.Enabled);Assert.Equal(404,Assert.Throws<AdminRule>(()=>service.Landing("invalid")).Status);
    }
    private sealed class HostEnvironment:Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName {get;set;}="Production";
        public string ApplicationName {get;set;}="Test";
        public string WebRootPath {get;set;}="";
        public IFileProvider WebRootFileProvider {get;set;}=new NullFileProvider();
        public string ContentRootPath {get;set;}="";
        public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();
    }
}
