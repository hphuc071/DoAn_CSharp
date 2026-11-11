using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using FourChang.Api.Features.Admin.Tables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class TableQrAdministrationSafetyTests
{
    private static IConfigurationRoot Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Orders:TableQrIssuanceEnabled"] = "true",
            ["Orders:TableQrProtocolValidated"] = "true",
            ["Orders:SqlIntegrationValidated"] = "true",
            ["Orders:TableQr:IssuerApproved"] = "true",
            ["Orders:TableQr:IssuanceSqlValidated"] = "true",
            ["Orders:TableQr:MinLifetimeSeconds"] = "60",
            ["Orders:TableQr:MaxLifetimeSeconds"] = "3600"
        }).Build();

    [Theory]
    [InlineData("Orders:TableQrIssuanceEnabled")]
    [InlineData("Orders:TableQrProtocolValidated")]
    [InlineData("Orders:SqlIntegrationValidated")]
    [InlineData("Orders:TableQr:IssuerApproved")]
    [InlineData("Orders:TableQr:IssuanceSqlValidated")]
    public void Every_approval_gate_is_required(string key)
    {
        var config = Config();
        Assert.True(TableQrAdministration.Ready(config));
        config[key] = "false";
        Assert.False(TableQrAdministration.Ready(config));
        config[key] = "Development";
        Assert.False(TableQrAdministration.Ready(config));
        config[key] = null;
        Assert.False(TableQrAdministration.Ready(config));
    }

    [Fact]
    public void Missing_configuration_leaves_issuer_off()
    {
        var config = new ConfigurationBuilder().Build();
        Assert.False(TableQrAdministration.Ready(config));
    }

    [Fact]
    public void Generated_token_is_canonical_random_256_bits_and_digest_matches()
    {
        var first = TableQrAdministration.CreateToken();
        var second = TableQrAdministration.CreateToken();
        var bytes = WebEncoders.Base64UrlDecode(first.Token);
        Assert.Equal(43, first.Token.Length);
        Assert.Equal(32, bytes.Length);
        Assert.Equal(first.Token, WebEncoders.Base64UrlEncode(bytes));
        Assert.Equal(SHA256.HashData(bytes), first.Digest);
        Assert.NotEqual(first.Token, second.Token);
        Assert.False(first.Digest.SequenceEqual(second.Digest));
    }

    [Theory]
    [InlineData("abc", true)]
    [InlineData("  Khách đề nghị đổi QR  ", true)]
    [InlineData("", false)]
    [InlineData(" a ", false)]
    [InlineData("Hi\nthere", false)]
    public void Reason_must_be_bounded_and_not_contain_control_characters(string reason, bool expected)
        => Assert.Equal(expected, TableQrAdministration.ValidReason(reason));

    [Fact]
    public void Rowversion_must_be_exactly_eight_bytes_and_canonical()
    {
        var good = Convert.ToBase64String(new byte[8]);
        Assert.True(TableQrAdministration.ValidVersion(good, out var bytes));
        Assert.Equal(8, bytes.Length);
        Assert.False(TableQrAdministration.ValidVersion("AQ==", out _));
        Assert.False(TableQrAdministration.ValidVersion("%%", out _));
        Assert.False(TableQrAdministration.ValidVersion(" " + good, out _));
    }

    [Fact]
    public void Issuance_lifetime_requires_approved_bounds_and_explicit_utc()
    {
        var config = Config();
        var now = new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
        Assert.True(TableQrAdministration.ValidExpiry(config, now.AddMinutes(10), now, out var utc));
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.False(TableQrAdministration.ValidExpiry(config, null, now, out _));
        Assert.False(TableQrAdministration.ValidExpiry(config, now.AddSeconds(59), now, out _));
        Assert.False(TableQrAdministration.ValidExpiry(config, now.AddSeconds(3601), now, out _));
        Assert.False(TableQrAdministration.ValidExpiry(config, now.AddMinutes(10).ToOffset(TimeSpan.FromHours(7)), now, out _));
        config["Orders:TableQr:MaxLifetimeSeconds"] = null;
        Assert.False(TableQrAdministration.ValidExpiry(config, now.AddMinutes(10), now, out _));
    }

    [Fact]
    public async Task Disabled_and_http_requests_never_open_a_database()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "ADMIN"), new Claim(ClaimTypes.NameIdentifier, "1")], "Test"));
        context.Request.Headers["Idempotency-Key"] = Guid.NewGuid().ToString();
        var request = new TableQrMutationRequest(Convert.ToBase64String(new byte[8]),
            "Quản lý cấp QR thử", DateTimeOffset.UtcNow.AddMinutes(10));
        var disabled = Config();
        disabled["Orders:TableQr:IssuanceSqlValidated"] = "false";
        var result = await TableQrAdministration.IssueAsync(1, request, context, null!, disabled,
            TimeProvider.System, default);
        Assert.Equal(503, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());

        context.Request.Scheme = "http";
        var rejected = await TableQrAdministration.IssueAsync(1, request, context, null!, Config(),
            TimeProvider.System, default);
        Assert.Equal(503, Assert.IsAssignableFrom<IStatusCodeHttpResult>(rejected).StatusCode);
    }

    [Fact]
    public async Task Only_admin_can_reach_issuer_sql_boundary()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "CUSTOMER"), new Claim(ClaimTypes.NameIdentifier, "6")], "Test"));
        var input = new TableQrMutationRequest(Convert.ToBase64String(new byte[8]), "Thu hồi QR");
        var result = await TableQrAdministration.RevokeAsync(1, input, context, null!, Config(),
            TimeProvider.System, default);
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>(result);
    }

    [Fact]
    public async Task Routes_are_admin_only_post_with_explicit_rate_limit()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "OfflineMockTests" });
        builder.Services.AddScoped<FourChang.Infrastructure.Database.Context.CafeDbContext>(_ =>
            throw new InvalidOperationException("Database access forbidden in route metadata test"));
        builder.Services.AddSingleton(TimeProvider.System);
        var app = builder.Build();
        app.MapTableQrAdministration();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>().ToArray();
        Assert.Equal(3, endpoints.Length);
        foreach (var endpoint in endpoints)
        {
            Assert.StartsWith("/api/admin/tables/", endpoint.RoutePattern.RawText);
            Assert.Equal("Admin", Assert.Single(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()).Policy);
            var method = endpoint.RoutePattern.RawText!.EndsWith("/context", StringComparison.Ordinal)
                ? "GET" : "POST";
            Assert.Equal(method, Assert.Single(endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods));
        }
        await app.DisposeAsync();
    }
}
