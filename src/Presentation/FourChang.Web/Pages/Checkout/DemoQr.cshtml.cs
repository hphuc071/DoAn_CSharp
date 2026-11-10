using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FourChang.Web.Pages.Checkout;

// Read-only, synthetic preview. It neither queries the API nor creates a payment/order.
// Do not use this page as evidence of a real transaction or bank integration.
public sealed class DemoQrModel(IConfiguration configuration, IWebHostEnvironment environment) : PageModel
{
    public static bool IsPreviewEnabled(string? flag, string? environmentName) =>
        string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);

    public IActionResult OnGet()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        if (!IsPreviewEnabled(configuration["Demo:PaymentQrEnabled"], environment.EnvironmentName))
            return NotFound();
        return Page();
    }
}
