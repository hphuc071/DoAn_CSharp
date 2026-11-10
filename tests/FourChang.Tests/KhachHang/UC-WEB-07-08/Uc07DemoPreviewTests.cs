using FourChang.Web.Pages.Checkout;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class Uc07DemoPreviewTests
{
    private sealed class EnvironmentStub(string name) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "FourChang.Web";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static DemoQrModel Page(string? flag, string environment)
    {
        var values = new Dictionary<string, string?> { ["Demo:PaymentQrEnabled"] = flag };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new DemoQrModel(configuration, new EnvironmentStub(environment))
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Theory]
    [InlineData(null, "Development")]
    [InlineData("false", "Development")]
    [InlineData("true", "Production")]
    [InlineData("true", "Staging")]
    public void Preview_route_is_hidden_unless_explicitly_enabled_in_development(string? flag, string environment)
    {
        var page = Page(flag, environment);
        Assert.IsType<NotFoundResult>(page.OnGet());
        Assert.Equal("no-store", page.Response.Headers.CacheControl.ToString());
        Assert.Equal("noindex, nofollow", page.Response.Headers["X-Robots-Tag"].ToString());
    }

    [Theory]
    [InlineData("true", "Development")]
    [InlineData("TRUE", "development")]
    public void Explicit_development_opt_in_returns_preview_without_api_or_database(string flag, string environment)
    {
        var page = Page(flag, environment);
        Assert.IsType<PageResult>(page.OnGet());
        Assert.Equal("no-store", page.Response.Headers.CacheControl.ToString());
        Assert.Equal("noindex, nofollow", page.Response.Headers["X-Robots-Tag"].ToString());
    }
}
