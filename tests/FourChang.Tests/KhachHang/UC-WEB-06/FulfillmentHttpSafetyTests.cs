using System.Net;
using System.Net.Http.Json;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Api.Security;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.DataProtection;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Tests.Shared;
using System.Text.RegularExpressions;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class FulfillmentHttpSafetyTests
{
    private sealed class NoBackend : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; throw new InvalidOperationException("No outbound HTTP permitted"); }
    }
    [Fact]
    public async Task Real_Razor_antiforgery_and_server_off_gates_reject_manual_preview_and_create()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ApplicationName = typeof(FourChang.Web.Pages.Checkout.IndexModel).Assembly.GetName().Name,
            ContentRootPath = Path.Combine(TestApi.FindRoot(), "src", "Presentation", "FourChang.Web"),
            EnvironmentName = "OfflineMockTests" });
        builder.Configuration.Sources.Clear();
        builder.WebHost.UseTestServer();
        builder.Services.AddRazorPages();
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        var backend = new NoBackend();
        builder.Services.AddSingleton(new CafeApiClient(new HttpClient(backend) { BaseAddress = new Uri("http://forbidden.invalid") }));
        await using var app = builder.Build(); app.MapRazorPages(); await app.StartAsync();
        using var client = app.GetTestClient();
        using var get = await client.GetAsync("/Checkout"); get.EnsureSuccessStatusCode();
        var html = await get.Content.ReadAsStringAsync();
        var csrf = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(csrf); Assert.DoesNotContain("TABLE-05", html);
        var cookie = string.Join("; ", get.Headers.GetValues("Set-Cookie").Select(x => x.Split(';')[0]));
        foreach (var handler in new[] { "FulfillmentPreview", "Create" })
        {
            using var invalid = await client.PostAsJsonAsync("/Checkout?handler=" + handler, new { });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/Checkout?handler=" + handler) { Content = JsonContent.Create(new { }) };
            request.Headers.Add("Cookie", cookie); request.Headers.Add("RequestVerificationToken", csrf);
            using var disabled = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, disabled.StatusCode);
            Assert.Equal("no-store", disabled.Headers.CacheControl?.ToString());
        }
        Assert.Equal(0, backend.Calls); await app.StopAsync();
    }
    [Theory]
    [InlineData("/api/customer/orders/fulfillment-preview")]
    [InlineData("/api/customer/orders")]
    public async Task Real_authentication_and_authorization_pipeline_rejects_anonymous(string path)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        // No API Program, .env loader, TCP listener, fake authentication or SQL server.
        builder.Services.AddDbContext<CafeDbContext>(o => o.UseSqlServer(
            "Server=forbidden.invalid;Database=NoDatabase;Integrated Security=true"));
        builder.Services.AddScoped<ICustomerOrderReader, CustomerOrderReader>();
        builder.Services.AddAuthentication("Session").AddScheme<AuthenticationSchemeOptions, SessionAuthentication>("Session", _ => { });
        builder.Services.AddAuthorization(o => o.AddPolicy("Customer", p => p.RequireRole("CUSTOMER")));
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapCustomerOrders();
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync(path, new { method = "DINE_IN" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await app.StopAsync();
        // Positive session/ownership HTTP tests need a guarded SQL fixture, not fabricated claims.
    }
}
