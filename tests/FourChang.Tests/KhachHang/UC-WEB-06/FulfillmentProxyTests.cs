using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Checkout = FourChang.Web.Pages.Checkout.IndexModel;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class FulfillmentProxyTests
{
    private sealed class Backend(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Assert.Equal("/api/customer/orders/fulfillment-preview", request.RequestUri!.AbsolutePath);
            Assert.Equal("server-only-test-token", request.Headers.Authorization?.Parameter);
            return Task.FromResult(send(request));
        }
    }
    private static readonly FulfillmentPreviewRequest Input = new("DINE_IN", "test-token", null, null, null, null, null,
        [new(1, 1, 100, 100, null, [])]);
    private static Checkout Page(Backend backend, string? flag, bool cookie = true)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(Input));
        if (cookie) context.Request.Headers.Cookie = "fourchang_session=server-only-test-token";
        var client = new CafeApiClient(new HttpClient(backend) { BaseAddress = new Uri("http://backend.invalid") });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Checkout:FulfillmentPreviewEnabled"] = flag }).Build();
        return new(client, config, new EphemeralDataProtectionProvider()) { PageContext = new PageContext { HttpContext = context } };
    }
    [Theory]
    [InlineData(null)] [InlineData("false")] [InlineData("Development")]
    public async Task Preview_default_off_in_web_even_with_manual_post(string? flag)
    {
        var backend = new Backend(_ => throw new InvalidOperationException("No HTTP permitted"));
        var result = Assert.IsType<ObjectResult>(await Page(backend, flag).OnPostFulfillmentPreviewAsync(default));
        Assert.Equal(503, result.StatusCode); Assert.Equal(0, backend.Calls);
    }
    [Fact]
    public async Task Preview_requires_customer_cookie_before_backend_call()
    {
        var backend = new Backend(_ => throw new InvalidOperationException());
        var result = Assert.IsType<ObjectResult>(await Page(backend, "true", false).OnPostFulfillmentPreviewAsync(default));
        Assert.Equal(401, result.StatusCode); Assert.Equal(0, backend.Calls);
    }
    [Theory]
    [InlineData(502, "text/html", "<html>proxy error</html>")]
    [InlineData(503, "application/octet-stream", "")]
    [InlineData(200, "text/html", "<html>not a preview</html>")]
    public async Task Non_json_or_unavailable_backend_never_appears_as_a_successful_preview(int status, string type, string body)
    {
        var backend = new Backend(_ => new((HttpStatusCode)status) { Content = new StringContent(body, System.Text.Encoding.UTF8, type) });
        var result = Assert.IsType<ObjectResult>(await Page(backend, "true").OnPostFulfillmentPreviewAsync(default));
        Assert.Equal(status == 200 ? 502 : status, result.StatusCode); Assert.Equal(1, backend.Calls);
    }
    [Fact]
    public async Task A_response_that_claims_create_enabled_is_rejected()
    {
        var backend = new Backend(_ => new(HttpStatusCode.OK) { Content = JsonContent.Create(
            new FulfillmentPreview("DINE_IN", "TEST", new(45000, 0, 45000), DateTimeOffset.UtcNow.AddMinutes(1), "proof", true)) });
        var result = Assert.IsType<ObjectResult>(await Page(backend, "true").OnPostFulfillmentPreviewAsync(default));
        Assert.Equal(502, result.StatusCode);
    }

    [Theory]
    [InlineData("expired")] [InlineData("missing-table")]
    [InlineData("dine-in-shipping")] [InlineData("unexpected-201")]
    public async Task Stale_or_inconsistent_preview_is_not_presented_as_valid(string failure)
    {
        var preview = new FulfillmentPreview("DINE_IN", "TEST", new(45000, 0, 45000),
            DateTimeOffset.UtcNow.AddMinutes(1), "test-proof");
        preview = failure switch {
            "expired" => preview with { ExpiresAt = DateTimeOffset.MinValue },
            "missing-table" => preview with { TableCode = null },
            "dine-in-shipping" => preview with { Price = new(45000, 15000, 60000) },
            _ => preview
        };
        var backend = new Backend(_ => new(failure == "unexpected-201" ? HttpStatusCode.Created : HttpStatusCode.OK)
            { Content = JsonContent.Create(preview) });
        var result = Assert.IsType<ObjectResult>(await Page(backend, "true").OnPostFulfillmentPreviewAsync(default));
        Assert.Equal(502, result.StatusCode); Assert.Equal(1, backend.Calls);
    }

    [Fact]
    public async Task Valid_read_only_preview_is_returned_without_create_or_profile_request()
    {
        var preview = new FulfillmentPreview("DINE_IN", "TEST", new(45000, 0, 45000),
            DateTimeOffset.UtcNow.AddMinutes(1), "test-proof");
        var backend = new Backend(_ => new(HttpStatusCode.OK) { Content = JsonContent.Create(preview) });
        var result = Assert.IsType<JsonResult>(await Page(backend, "true").OnPostFulfillmentPreviewAsync(default));
        Assert.Equal(preview, result.Value); Assert.Equal(1, backend.Calls);
    }
}
