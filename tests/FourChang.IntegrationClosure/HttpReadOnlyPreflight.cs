using System.Data.Common;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Api.Security;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.ApiClients;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Independent negative pipeline checks after the SQL attempt stopped: no SQL, fake auth or network.
static class HttpReadOnlyPreflight
{
    sealed class NoSql : DbConnectionInterceptor
    {
        public int Attempts;
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData data, InterceptionResult result, CancellationToken ct = default)
        { Attempts++; throw new InvalidOperationException("SQL forbidden."); }
    }
    sealed class NoBackend : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; throw new InvalidOperationException("Backend/network forbidden in readonly preflight."); }
    }
    public static async Task RunAsync()
    {
        var results = new List<object>(); var monitor = new NoSql(); var backend = new NoBackend();
        void Check(HttpStatusCode actual, HttpStatusCode expected, string scenario)
        { results.Add(new { scenario, actual = (int)actual, expected = (int)expected }); if (actual != expected) throw new InvalidOperationException(scenario); }
        var b = WebApplication.CreateBuilder(); b.Configuration.Sources.Clear(); b.Logging.ClearProviders(); b.WebHost.UseTestServer();
        b.Services.AddDbContext<CafeDbContext>(o => o.UseSqlServer("Server=forbidden.invalid;Database=NoDatabase;Integrated Security=true").AddInterceptors(monitor));
        b.Services.AddScoped<ICustomerOrderReader, CustomerOrderReader>();
        b.Services.AddAuthentication("Session").AddScheme<AuthenticationSchemeOptions, SessionAuthentication>("Session", _ => { });
        b.Services.AddAuthorization(o => o.AddPolicy("Customer", p => p.RequireRole("CUSTOMER")));
        await using var api = b.Build(); api.UseAuthentication(); api.UseAuthorization(); api.MapCustomerOrders(); api.MapCustomerOrderDetails();
        await api.StartAsync(); using var http = api.GetTestClient();
        foreach (var path in new[] { "/api/customer/orders", "/api/customer/orders/7/cancel" })
            Check((await http.PostAsync(path, new StringContent("{}", System.Text.Encoding.UTF8, "application/json"))).StatusCode,
                HttpStatusCode.Unauthorized, "Real SessionAuthentication rejects anonymous POST " + path);
        Check((await http.GetAsync("/api/customer/orders/7")).StatusCode, HttpStatusCode.Unauthorized, "Anonymous detail denied");

        var w = WebApplication.CreateBuilder(new WebApplicationOptions {
            ApplicationName = typeof(FourChang.Web.Pages.Orders.IndexModel).Assembly.GetName().Name,
            ContentRootPath = Path.GetFullPath("src/Presentation/FourChang.Web"), EnvironmentName = "ClosureReadOnly" });
        w.Configuration.Sources.Clear(); w.Logging.ClearProviders(); w.WebHost.UseTestServer(); w.Services.AddRazorPages();
        w.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        w.Services.AddHttpClient<CafeApiClient>(c => c.BaseAddress = new Uri("http://forbidden.invalid"))
            .ConfigurePrimaryHttpMessageHandler(() => backend);
        await using var web = w.Build(); web.MapRazorPages(); await web.StartAsync(); using var client = web.GetTestClient();
        var get = await client.GetAsync("/Checkout"); Check(get.StatusCode, HttpStatusCode.OK, "Checkout renders server-disabled");
        var html = await get.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!token.Success) throw new InvalidOperationException("No antiforgery token issued");
        foreach (var path in new[] { "/Checkout?handler=Create", "/Orders/7?handler=Cancel" })
            Check((await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode,
                HttpStatusCode.BadRequest, "Razor missing CSRF denied " + path);
        client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", get.Headers.GetValues("Set-Cookie").Select(x => x.Split(';')[0])));
        Check((await client.PostAsync("/Checkout?handler=Create", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value) }))).StatusCode,
            HttpStatusCode.ServiceUnavailable, "Valid CSRF Checkout remains server-disabled");
        if (monitor.Attempts != 0 || backend.Calls != 0) throw new InvalidOperationException("Unexpected SQL/backend access");
        await File.WriteAllTextAsync(Closure.Dir + "http-readonly.json", JsonSerializer.Serialize(new {
            status = "PASS_HTTP_INPROCESS_NEGATIVE_ONLY", sqlOpenAttempts = monitor.Attempts, backendCalls = backend.Calls,
            results, limitation = "No valid Customer session, ownership, enabled writes/replay or provider integration proven." },
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS_HTTP_INPROCESS: anonymous authorization + Razor CSRF/Checkout OFF; zero SQL/backend calls.");
    }
}
