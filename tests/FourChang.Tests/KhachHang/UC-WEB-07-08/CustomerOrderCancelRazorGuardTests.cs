
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Web.Pages.Orders;
using FourChang.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class CustomerOrderCancelRazorGuardTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct)
        {
            Calls++;

            throw new InvalidOperationException(
                "Guard test must never contact an API.");
        }
    }

    private static DetailsModel CreatePage(
        RecordingHandler handler,
        bool cancellationEnabled,
        bool authenticated)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Orders:CustomerCancellationWebEnabled"] =
                    cancellationEnabled.ToString()
            })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = services
        };

        if (authenticated)
        {
            context.Request.Headers.Cookie =
                $"{CustomerCookie.Name}=synthetic-test-token";
        }

        var client = new CafeApiClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri(
                    "https://fourchang-test.invalid/")
            });

        return new DetailsModel(client)
        {
            PageContext = new PageContext
            {
                HttpContext = context
            }
        };
    }

    private static string ValidVersion =>
        Convert.ToBase64String(new byte[8]);

    [Fact]
    public async Task Disabled_flag_returns_503_without_API_call()
    {
        var handler = new RecordingHandler();
        var page = CreatePage(
            handler,
            cancellationEnabled: false,
            authenticated: true);

        var result = await page.OnPostCancelAsync(
            7,
            "Khách đổi ý",
            ValidVersion,
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Equal(
            503,
            Assert.IsType<ObjectResult>(result).StatusCode);

        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Disabled_flag_without_cookie_returns_401()
    {
        var handler = new RecordingHandler();
        var page = CreatePage(
            handler,
            cancellationEnabled: false,
            authenticated: false);

        var result = await page.OnPostCancelAsync(
            7,
            "Khách đổi ý",
            ValidVersion,
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Missing_version_never_calls_API()
    {
        var handler = new RecordingHandler();
        var page = CreatePage(
            handler,
            cancellationEnabled: true,
            authenticated: true);

        var result = await page.OnPostCancelAsync(
            7,
            "Khách đổi ý",
            null,
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Empty_operation_id_never_calls_API()
    {
        var handler = new RecordingHandler();
        var page = CreatePage(
            handler,
            cancellationEnabled: true,
            authenticated: true);

        var result = await page.OnPostCancelAsync(
            7,
            "Khách đổi ý",
            ValidVersion,
            Guid.Empty,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, handler.Calls);
    }
}
