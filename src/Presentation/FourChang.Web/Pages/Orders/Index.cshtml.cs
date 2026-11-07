using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.Mvc;

namespace FourChang.Web.Pages.Orders;

public sealed class IndexModel(CafeApiClient api) : CustomerOrderPageModel(api)
{
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? FromUtc { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToUtc { get; set; }
    public CustomerOrderPage? Snapshot { get; private set; }
    public Task<IActionResult> OnPostCancelAsync(long orderId, string? reason, CancellationToken ct) =>
        RejectCancellationAsync(orderId, reason);

    public Task<IActionResult> OnGetAsync(CancellationToken ct) => ReadAsync(async () =>
    {
        var filter = new CustomerOrderFilter(PageNumber, 10, string.IsNullOrEmpty(Status) ? null : Status, FromUtc, ToUtc);
        if (!ModelState.IsValid || filter.Validate(ExpiryReadEnabled) is not null)
        {
            Response.StatusCode = 400;
            Error = filter.Validate(ExpiryReadEnabled) ?? "Bộ lọc không hợp lệ.";
            return;
        }
        var snapshot = await Api.GetCustomerOrdersAsync(filter, ct, ExpiryReadEnabled);
        if (snapshot.Items is null || snapshot.Page != PageNumber || snapshot.PageSize != 10 ||
            snapshot.TotalCount < 0 || snapshot.Items.Count > 10 || snapshot.Items.Any(x => x is null || x.Id <= 0 ||
                string.IsNullOrWhiteSpace(x.Code) || !CustomerOrderStates.IsReadable(x.Status, ExpiryReadEnabled)))
            throw new CafeApiException("Snapshot danh sách không hợp lệ.", System.Net.HttpStatusCode.BadGateway);
        Snapshot = snapshot;
    });
}
