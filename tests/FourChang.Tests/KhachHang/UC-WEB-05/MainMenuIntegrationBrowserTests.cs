using System.Net;
using FourChang.Api.Features.Customer.Menu;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.ApiClients;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FourChang.Tests.Shared;
using Microsoft.Playwright;

namespace FourChang.Tests;

// Rehearsal regression: actual integrated site.js, all navigation intercepted; no API/SQL.
[Trait("Safety", "NoDatabase")]
public sealed class MainMenuIntegrationBrowserTests
{
    [Theory]
    [InlineData("minPrice=-1")]
    [InlineData("maxPrice=-1")]
    [InlineData("minPrice=20&maxPrice=10")]
    public async Task Main_catalog_invalid_filters_reject_before_database_access(string query)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<CafeDbContext>(o => o.UseSqlServer(
            "Server=forbidden.invalid;Database=NoDatabase;Integrated Security=true"));
        await using var app = builder.Build();
        app.MapCatalog(); await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/menu?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await app.StopAsync();
    }
    private sealed class CatalogSource : HttpMessageHandler
    {
        public Uri? Requested;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get, request.Method); Requested = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json") });
        }
    }
    [Fact]
    public async Task Sorted_menu_uses_backend_sort_query_without_write_or_local_approximation()
    {
        var source = new CatalogSource();
        var model = new FourChang.Web.Pages.Menu.IndexModel(new CafeApiClient(
            new HttpClient(source) { BaseAddress = new Uri("http://catalog.invalid") })) {
            PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } };
        await model.OnGetAsync("bestseller");
        Assert.Equal("/api/menu?sort=bestseller", source.Requested?.PathAndQuery);
        Assert.Null(model.Error);
    }
    [Theory]
    [InlineData("newest")]
    [InlineData("bestseller")]
    public async Task Menu_sort_requests_server_order_and_preserves_both_storage_formats(string sort)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var requests = new List<string>();
        await context.RouteAsync("**/*", async route => {
            Assert.Equal("GET", route.Request.Method);
            requests.Add(route.Request.Url);
            await route.FulfillAsync(new() { ContentType = "text/html; charset=utf-8", Body = """
                <main><input id="menu-search"><select id="price-filter"><option value="all">all</option></select>
                <select id="stock-filter"><option value="all">all</option></select>
                <select id="menu-sort"><option value="default">default</option><option value="newest">new</option><option value="bestseller">best</option></select>
                <div id="categories"><button data-category="Tất cả">all</button></div><button id="reset-filter">reset</button>
                <div id="product-grid"><article class="product-card" data-name="Cà phê" data-category="Cà phê" data-price="45000" data-available="true">coffee</article></div>
                <div id="menu-empty" hidden></div><div id="scenario-message" hidden></div></main>
                """ });
        });
        await page.GotoAsync("http://localhost:18796/");
        await page.EvaluateAsync("() => { localStorage.setItem('fourchang_cart_v1','legacy-original'); localStorage.setItem('fourchang.cart-draft.v1','current-original'); localStorage.setItem('fourchang.checkout-intent.v1','pending-original'); }");
        var script = await File.ReadAllTextAsync(Path.Combine(TestApi.FindRoot(), "src", "Presentation", "FourChang.Web", "wwwroot", "js", "site.js"));
        await page.AddScriptTagAsync(new() { Content = script });
        await Assertions.Expect(page.Locator("[role=status]")).ToContainTextAsync("định dạng cũ");
        await page.Locator("#menu-search").FillAsync("ca phe");
        await Assertions.Expect(page.Locator(".product-card")).ToBeVisibleAsync();
        await page.Locator("#menu-search").FillAsync("missing");
        await Assertions.Expect(page.Locator(".product-card")).ToBeHiddenAsync();
        await page.Locator("#menu-sort").SelectOptionAsync(sort);
        await page.WaitForURLAsync("**/?sort=" + sort);
        await page.AddScriptTagAsync(new() { Content = script });
        await Assertions.Expect(page.Locator("#menu-sort")).ToHaveValueAsync(sort);
        Assert.Equal("legacy-original", await page.EvaluateAsync<string>("localStorage.getItem('fourchang_cart_v1')"));
        Assert.Equal("current-original", await page.EvaluateAsync<string>("localStorage.getItem('fourchang.cart-draft.v1')"));
        Assert.Equal("pending-original", await page.EvaluateAsync<string>("localStorage.getItem('fourchang.checkout-intent.v1')"));
        Assert.Equal(2, requests.Count);
    }
}
