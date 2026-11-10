using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Domain.Entities.Orders;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Web.Pages.Orders;
using FourChang.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PaymentPage = FourChang.Web.Pages.Checkout.PaymentModel;
using OrdersPage = FourChang.Web.Pages.Orders.IndexModel;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class OrderReadTests
{
    internal static readonly DateTime Now = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);
    internal static CustomerOrderDetail Snapshot(string status = "PENDING_PAYMENT") => new(
        new(7, "WEB-FAKE-7", status, "TAKEAWAY", Now, Now), "AQ==", Now.AddMinutes(15), null, null, 0, 0,
        [new(1, "Tên đã chốt", "M", "COLD", 50, 50, 2, 0, 0, 0, 20000, "", [new("Trân châu", 2, 5000)])],
        [new(3, "PAY-FAKE-3", "BANK_TRANSFER", "PENDING", 60000, 0, "REF-FAKE-7", Now.AddMinutes(15), null)],
        [], [], Now);

    internal sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; return Task.FromResult(send(request)); }
    }
    internal static T Page<T>(T page, string? token = "customer-a") where T : PageModel
    {
        var context = new DefaultHttpContext();
        if (token is not null) context.Request.Headers.Cookie = "fourchang_session=" + token;
        page.PageContext = new PageContext { HttpContext = context };
        return page;
    }
    internal static CafeApiClient Client(Stub stub) => new(new HttpClient(stub) { BaseAddress = new Uri("http://mock.invalid") });

    [Fact]
    public void Owner_scope_excludes_other_customers_pos_and_unowned_orders_before_filtering()
    {
        DonHang[] rows = [new() { MaDonHang = 7, MaKhachHang = 6, KenhDatHang = "WEB" },
            new() { MaDonHang = 8, MaKhachHang = 9, KenhDatHang = "WEB" },
            new() { MaDonHang = 9, MaKhachHang = 6, KenhDatHang = "POS" },
            new() { MaDonHang = 10, KenhDatHang = "WEB" }];
        Assert.Equal(7, Assert.Single(CustomerOrderReader.Owned(rows.AsQueryable(), 6)).MaDonHang);
        Assert.Equal(8, Assert.Single(CustomerOrderReader.Owned(rows.AsQueryable(), 9)).MaDonHang);
        Assert.Empty(CustomerOrderReader.Owned(rows.AsQueryable(), 0));
    }

    [Fact]
    public void Owner_and_filter_translate_to_sql_without_opening_connection()
    {
        using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>()
            .UseSqlServer("Server=must-not-connect.invalid;Database=OfflineOnly;Integrated Security=true").Options);
        var sql = CustomerOrderReader.Filter(CustomerOrderReader.Owned(db.DonHang.AsNoTracking(), 6),
            new(2, 10, "PENDING_PAYMENT", Now, Now.AddDays(1)))
            .OrderByDescending(x => x.ThoiDiemTao).ThenByDescending(x => x.MaDonHang).Skip(10).Take(10).ToQueryString();
        Assert.Contains("[MaKhachHang]", sql);
        Assert.Contains("'WEB'", sql);
        Assert.Contains("PENDING_PAYMENT", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("[ThoiDiemTao] >=", sql);
        Assert.Contains("[ThoiDiemTao] <", sql);
        Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    private sealed class Reader : ICustomerOrderReader
    {
        public int Calls;
        public Task<CustomerOrderPage> ListAsync(long customerId, CustomerOrderFilter filter, CancellationToken ct)
        { Calls++; return Task.FromResult(new CustomerOrderPage([], filter.Page, filter.PageSize, 0, Now)); }
        public Task<CustomerOrderDetail?> DetailAsync(long customerId, long orderId, CancellationToken ct)
        { Calls++; return Task.FromResult(customerId == 6 && orderId == 7 ? Snapshot() : null); }
    }
    private static DefaultHttpContext Actor(long? id)
    {
        var context = new DefaultHttpContext();
        if (id is not null) context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, id.Value.ToString()), new Claim(ClaimTypes.Role, "CUSTOMER")], "Mock"));
        return context;
    }

    [Fact]
    public async Task Detail_route_requires_customer_policy_and_is_get_only()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "OfflineMockTests" });
        builder.Services.AddSingleton<ICustomerOrderReader>(new Reader());
        await using var app = builder.Build(); // Never starts Kestrel or API Program.
        app.MapCustomerOrderDetails();
        var route = Assert.Single(((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>());
        Assert.Equal("/api/customer/orders/{orderId:long}", route.RoutePattern.RawText);
        Assert.Equal("Customer", Assert.Single(route.Metadata.GetOrderedMetadata<IAuthorizeData>()).Policy);
        Assert.Equal("GET", Assert.Single(route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods));
    }

    [Theory]
    [InlineData(6, 200)] [InlineData(9, 404)] [InlineData(0, 401)]
    public async Task Detail_handler_passes_authenticated_owner_and_does_not_disclose_other_orders(long actor, int status)
    {
        var reader = new Reader(); var context = Actor(actor);
        var result = await CustomerOrderReadEndpoints.DetailAsync(7, context, reader, default);
        Assert.Equal(status, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        if (status != 200) Assert.False(result is IValueHttpResult { Value: CustomerOrderDetail });
        if (actor == 0) Assert.Equal(0, reader.Calls);
    }

    [Theory]
    [InlineData(0, 10, null)] [InlineData(1, 51, null)] [InlineData(1, 10, "PAID")]
    public async Task Invalid_list_filter_rejected_before_reader(int page, int size, string? status)
    {
        var reader = new Reader();
        var result = await CustomerOrderReadEndpoints.ListAsync(Actor(6), reader, page, size, status, null, null, default);
        Assert.Equal(400, ((IStatusCodeHttpResult)result).StatusCode); Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public void Exclusive_date_filter_and_decimal_snapshot_totals_use_original_prices()
    {
        DonHang[] orders = [new() { ThoiDiemTao = Now }, new() { ThoiDiemTao = Now.AddDays(1) }];
        Assert.Single(CustomerOrderReader.Filter(orders.AsQueryable(), new(FromUtc: Now, ToUtc: Now.AddDays(1))));
        Assert.NotNull(new CustomerOrderFilter(FromUtc: Now.AddDays(1), ToUtc: Now).Validate());
        var detail = Snapshot();
        Assert.Equal(30000, detail.Lines[0].UnitPrice); Assert.Equal(60000, detail.CurrentTotal);
        var changed = detail with { Lines = [detail.Lines[0] with { Cancelled = 1 }], ShippingFee = 0 };
        Assert.Equal(60000, changed.OriginalTotal); Assert.Equal(30000, changed.CurrentTotal);
    }

    [Fact]
    public void Approved_refund_is_not_successful_disbursement_and_balance_separates_collected_and_refunded()
    {
        var detail = Snapshot();
        detail = detail with { Payments = [detail.Payments[0] with { Verified = 60000, Status = "VERIFIED", VerifiedUtc = Now }],
            Refunds = [new(1, "RF", "APPROVED", 60000, 60000, 0)], Lines = [detail.Lines[0] with { Cancelled = 2 }] };
        Assert.Equal(0, detail.Refunded); Assert.Equal(60000, detail.RefundObligation);
        var paidOut = detail with { Refunds = [detail.Refunds[0] with { SuccessfullyDisbursed = 20000 }] };
        Assert.Equal(20000, paidOut.Refunded); Assert.Equal(40000, paidOut.RefundObligation);
    }

    [Fact]
    public async Task Detail_proxy_uses_only_server_cookie_and_read_get_and_no_store()
    {
        var stub = new Stub(request => {
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal("/api/customer/orders/7", request.RequestUri!.AbsolutePath);
            Assert.Equal("customer-a", request.Headers.Authorization?.Parameter);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(Snapshot()) };
        });
        var page = Page(new DetailsModel(Client(stub)));
        Assert.IsType<PageResult>(await page.OnGetAsync(7, default));
        Assert.Equal(7, page.Detail!.Order.Id); Assert.Equal(1, stub.Calls);
        Assert.Equal("no-store", page.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task List_proxy_preserves_filters_pagination_and_empty_result()
    {
        var stub = new Stub(request => {
            Assert.Contains("page=2&pageSize=10&status=READY&fromUtc=", request.RequestUri!.Query);
            Assert.Equal(HttpMethod.Get, request.Method);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new CustomerOrderPage([], 2, 10, 0, Now)) };
        });
        var page = Page(new OrdersPage(Client(stub)) { PageNumber = 2, Status = "READY", FromUtc = Now });
        await page.OnGetAsync(default); Assert.NotNull(page.Snapshot); Assert.Empty(page.Snapshot.Items);
    }

    [Theory]
    [InlineData(401)] [InlineData(403)] [InlineData(404)] [InlineData(409)] [InlineData(502)] [InlineData(503)]
    public async Task Read_errors_are_controlled_even_with_html_body(int status)
    {
        var stub = new Stub(_ => new((HttpStatusCode)status) { Content = new StringContent("<html>upstream</html>", Encoding.UTF8, "text/html") });
        var page = Page(new PaymentPage(Client(stub), new UnavailablePaymentQrSource()));
        var result = await page.OnGetAsync(7, default);
        if (status is 401 or 403) Assert.IsType<RedirectToPageResult>(result);
        else { Assert.IsType<PageResult>(result); Assert.Equal(status, page.Response.StatusCode); Assert.NotNull(page.Error); }
        Assert.Null(page.Detail); Assert.Null(page.Qr); Assert.Equal(1, stub.Calls);
    }

    [Theory]
    [InlineData("text/html", "<html>bad</html>")]
    [InlineData("application/json", "")]
    [InlineData("application/json", "null")]
    [InlineData("application/json", "{}")]
    public async Task Invalid_success_body_never_displays_a_payment(string media, string body)
    {
        var stub = new Stub(_ => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, media) });
        var page = Page(new PaymentPage(Client(stub), new UnavailablePaymentQrSource()));
        await page.OnGetAsync(7, default); Assert.Equal(502, page.Response.StatusCode); Assert.Null(page.Detail);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Timeout_and_network_failure_do_not_retry_or_write(bool timeout)
    {
        var stub = new Stub(_ => throw (timeout ? new TaskCanceledException() : new HttpRequestException()));
        var page = Page(new DetailsModel(Client(stub)));
        await page.OnGetAsync(7, default);
        Assert.Equal(timeout ? 504 : 503, page.Response.StatusCode); Assert.Equal(1, stub.Calls); Assert.Null(page.Detail);
    }

    [Theory]
    [InlineData("customer-a")] [InlineData("customer-b")]
    public async Task No_customer_can_cancel_any_order_through_web_while_write_is_locked(string token)
    {
        var stub = new Stub(_ => throw new InvalidOperationException("No outbound request allowed"));
        var index = Page(new OrdersPage(Client(stub)), token);
        var detail = Page(new DetailsModel(Client(stub)), token);
       Assert.Equal(
    503,
    Assert.IsType<ObjectResult>(
        await detail.OnPostCancelAsync(
            7,
            "Đổi ý",
            Convert.ToBase64String(new byte[8]),
            Guid.NewGuid(),
            default
        )
    ).StatusCode
);
        Assert.Equal(0, stub.Calls);
    }

    [Fact]
    public async Task Missing_cookie_invalid_id_and_invalid_reason_never_reach_backend()
    {
        var stub = new Stub(_ => throw new InvalidOperationException());
        var page = Page(new DetailsModel(Client(stub)), null);
        Assert.IsType<RedirectToPageResult>(await page.OnGetAsync(7, default));
       Assert.IsType<UnauthorizedResult>(
    await page.OnPostCancelAsync(
        7,
        "Đổi ý",
        Convert.ToBase64String(new byte[8]),
        Guid.NewGuid(),
        default));
        Page(page); await page.OnGetAsync(-1, default); Assert.Equal(404, page.Response.StatusCode);
        Assert.IsType<BadRequestObjectResult>(
    await page.OnPostCancelAsync(
        7,
        " ",
        Convert.ToBase64String(new byte[8]),
        Guid.NewGuid(),
        default));

Assert.Equal(0, stub.Calls);
    }

    [Theory]
    [InlineData("PAID")] [InlineData("UNKNOWN")]
    public async Task Unknown_order_state_is_not_presented_as_success(string state)
    {
        var stub = new Stub(_ => new(HttpStatusCode.OK) { Content = JsonContent.Create(Snapshot(state)) });
        var page = Page(new DetailsModel(Client(stub))); await page.OnGetAsync(7, default);
        Assert.Equal(502, page.Response.StatusCode); Assert.Null(page.Detail);
    }

    [Theory]
    [InlineData("ADJUSTMENT", -10000, true)]
    [InlineData("ORIGINAL", -10000, false)]
    [InlineData("UNKNOWN", 10000, false)]
    public async Task Invoice_snapshot_follows_SQL_signed_adjustment_contract(string kind, int amount, bool valid)
    {
        var snapshot = Snapshot() with { Invoices = [new(11, "INV-TEST", kind, "ISSUED", amount, Now)] };
        var stub = new Stub(_ => new(HttpStatusCode.OK) { Content = JsonContent.Create(snapshot) });
        var page = Page(new DetailsModel(Client(stub)));
        await page.OnGetAsync(7, default);
        Assert.Equal(valid ? 200 : 502, page.Response.StatusCode);
        if (valid) Assert.Equal(amount, Assert.Single(page.Detail!.Invoices).Amount);
        else Assert.Null(page.Detail);
        Assert.Equal(1, stub.Calls);
    }

    [Theory]
    [InlineData("PENDING_CONFIRMATION", true)] [InlineData("ACCEPTED", false)] [InlineData("PREPARING", false)]
    [InlineData("PENDING_PAYMENT", false)] [InlineData("COMPLETED", false)]
    public void Cancellation_ui_eligibility_does_not_grant_write_permission(string state, bool eligible) =>
        Assert.Equal(eligible, Snapshot(state).CancellationEligible);
}
