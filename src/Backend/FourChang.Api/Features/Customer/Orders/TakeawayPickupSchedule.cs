using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace FourChang.Api.Features.Customer.Orders;

// Pure opt-in validation. Null = historical "as soon as possible" checkout.
// Policy and store opening hours require an operator-approved configuration.
// This validates time windows, NOT kitchen capacity, holiday closure or payment settlement.
public static class TakeawayPickupSchedule
{
    public static string? Validate(DateTimeOffset? requested, IConfiguration config,
        DateTimeOffset nowUtc, DateTimeOffset paymentDeadlineUtc)
    {
        if (requested is null) return null;
        if (!bool.TryParse(config["Orders:TakeawaySchedulingEnabled"], out var enabled) || !enabled)
            return "TAKEAWAY_SCHEDULING_DISABLED";
        if (!bool.TryParse(config["Orders:TakeawaySchedulingPolicyApproved"], out var approved) || !approved)
            return "TAKEAWAY_SCHEDULING_POLICY_REQUIRED";
        if (!int.TryParse(config["Orders:TakeawayMinLeadMinutes"], NumberStyles.None,
                CultureInfo.InvariantCulture, out var lead) || lead is < 1 or > 1440 ||
            !int.TryParse(config["Orders:TakeawayMaxAdvanceMinutes"], NumberStyles.None,
                CultureInfo.InvariantCulture, out var advance) || advance <= lead || advance > 43200 ||
            !TimeOnly.TryParseExact(config["Orders:TakeawayOpenLocal"], "HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var open) ||
            !TimeOnly.TryParseExact(config["Orders:TakeawayCloseLocal"], "HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var close) || open >= close ||
            string.IsNullOrWhiteSpace(config["Orders:TakeawayTimeZoneId"]))
            return "TAKEAWAY_SCHEDULING_POLICY_INVALID";
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(config["Orders:TakeawayTimeZoneId"]!); }
        catch (TimeZoneNotFoundException) { return "TAKEAWAY_SCHEDULING_POLICY_INVALID"; }
        catch (InvalidTimeZoneException) { return "TAKEAWAY_SCHEDULING_POLICY_INVALID"; }
        var utc = requested.Value.ToUniversalTime();
        if (utc <= nowUtc.AddMinutes(lead) || utc <= paymentDeadlineUtc)
            return "TAKEAWAY_PICKUP_TOO_SOON";
        if (utc > nowUtc.AddMinutes(advance))
            return "TAKEAWAY_PICKUP_TOO_FAR";
        var local = TimeZoneInfo.ConvertTime(utc, zone);
        var time = TimeOnly.FromDateTime(local.DateTime);
        if (time < open || time >= close)
            return "TAKEAWAY_OUTSIDE_APPROVED_HOURS";
        return null;
    }
}
