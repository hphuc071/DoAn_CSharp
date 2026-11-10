
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FourChang.Presentation.Shared.ApiClients;
using Xunit;

namespace FourChang.Tests;
[Trait("Safety", "NoDatabase")]
public sealed class CustomerOrderCancelApiClientTests
{
    // Giữ nguyên toàn bộ nội dung các bài test hiện tại.
    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken,
            Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return respond(request, ct);
        }
    }

    private static CafeApiClient CreateClient(StubHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://fourchang-test.invalid/")
        };

        return new CafeApiClient(http)
        {
            Token = "synthetic-customer-token"
        };
    }

    private static string Version =>
        Convert.ToBase64String(new byte[8]);

    [Fact]
    public async Task Cancel_sends_correct_contract()
    {
        var operation = Guid.NewGuid();

        var handler = new StubHandler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                "/api/customer/orders/7/cancel",
                request.RequestUri?.AbsolutePath);

            Assert.Equal(
                "Bearer",
                request.Headers.Authorization?.Scheme);

            Assert.Equal(
                "synthetic-customer-token",
                request.Headers.Authorization?.Parameter);

            Assert.Equal(
                operation.ToString("D"),
                Assert.Single(
                    request.Headers.GetValues("Idempotency-Key")));

            using var json = JsonDocument.Parse(
                await request.Content!.ReadAsStringAsync(ct));

            Assert.Equal(
                "Khách yêu cầu hủy",
                json.RootElement.GetProperty("reason").GetString());

            Assert.Equal(
                Version,
                json.RootElement
                    .GetProperty("expectedVersion").GetString());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    maDonHang = 7L,
                    trangThai = "CANCELLED",
                    soTienDeNghiHoan = 0m,
                    thongBao = "Đã ghi nhận hủy đơn."
                })
            };
        });

        var client = CreateClient(handler);

        var receipt = await client.CancelCustomerOrderAsync(
            7,
            "Khách yêu cầu hủy",
            Version,
            operation);

        Assert.Equal(7L, receipt.MaDonHang);
        Assert.Equal("CANCELLED", receipt.TrangThai);
        Assert.Equal(0m, receipt.SoTienDeNghiHoan);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "VERSION_CONFLICT")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "CANCELLATION_DISABLED")]
    public async Task Cancel_preserves_backend_errors(
        HttpStatusCode status, string code)
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = JsonContent.Create(new
                {
                    code,
                    message = "Yêu cầu bị từ chối."
                })
            }));

        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<CafeApiException>(
            () => client.CancelCustomerOrderAsync(
                7,
                "Khách yêu cầu hủy",
                Version,
                Guid.NewGuid()));

        Assert.Equal(status, error.StatusCode);
        Assert.Equal(code, error.Code);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Connection_failure_does_not_retry_post()
    {
        var handler = new StubHandler((_, _) =>
            throw new HttpRequestException("Synthetic connection failure"));

        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<CafeApiException>(
            () => client.CancelCustomerOrderAsync(
                7,
                "Khách yêu cầu hủy",
                Version,
                Guid.NewGuid()));

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            error.StatusCode);

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Invalid_version_never_sends_request()
    {
        var handler = new StubHandler((_, _) =>
            throw new InvalidOperationException(
                "Request must not be sent"));

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.CancelCustomerOrderAsync(
                7,
                "Khách yêu cầu hủy",
                "invalid-base64!",
                Guid.NewGuid()));

        Assert.Equal(0, handler.Calls);
    }
}
