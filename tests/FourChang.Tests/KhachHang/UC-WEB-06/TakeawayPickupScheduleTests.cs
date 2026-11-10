using System.Text.Json;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.Extensions.Configuration;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class TakeawayPickupScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Deadline = Now.AddMinutes(20);

    private static IConfiguration Policy(params (string Key, string? Value)[] changes)
    {
        var values = new Dictionary<string, string?>
        {
            ["Orders:TakeawaySchedulingEnabled"] = "true",
            ["Orders:TakeawaySchedulingPolicyApproved"] = "true",
            ["Orders:TakeawayMinLeadMinutes"] = "10",
            ["Orders:TakeawayMaxAdvanceMinutes"] = "1440",
            ["Orders:TakeawayTimeZoneId"] = "UTC",
            ["Orders:TakeawayOpenLocal"] = "08:00",
            ["Orders:TakeawayCloseLocal"] = "20:00"
        };
        foreach (var (key, value) in changes) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void Old_takeaway_hash_shape_is_identical_when_pickup_absent()
    {
        var req = new CreateCustomerOrderRequest("TAKEAWAY", null, "Customer", "0900000000",
            null, null, null, []);
        var oldShape = new { req.HinhThucNhanMon, req.MaBanHienThi, req.TenNguoiNhan,
            req.DienThoaiNguoiNhan, req.DiaChiGiao, req.KhoangCachKm, req.GhiChu, req.Items };
        Assert.Equal(JsonSerializer.Serialize(oldShape), JsonSerializer.Serialize(req));
        Assert.DoesNotContain("ThoiDiemHenNhan", JsonSerializer.Serialize(req));
        Assert.Null(TakeawayPickupSchedule.Validate(null, Policy(
            ("Orders:TakeawaySchedulingEnabled", "false")), Now, Deadline));
    }

    [Fact]
    public void Scheduled_time_is_in_the_fingerprint_and_can_be_validated_without_sql()
    {
        var req = new CreateCustomerOrderRequest("TAKEAWAY", null, "Customer", "0900000000",
            null, null, null, [], Now.AddHours(1));
        Assert.Contains("ThoiDiemHenNhan", JsonSerializer.Serialize(req));
        Assert.Null(TakeawayPickupSchedule.Validate(req.ThoiDiemHenNhan, Policy(), Now, Deadline));
    }

    [Theory]
    [InlineData("Orders:TakeawaySchedulingEnabled", "false", "TAKEAWAY_SCHEDULING_DISABLED")]
    [InlineData("Orders:TakeawaySchedulingPolicyApproved", "false", "TAKEAWAY_SCHEDULING_POLICY_REQUIRED")]
    [InlineData("Orders:TakeawayTimeZoneId", "Invalid/Nowhere", "TAKEAWAY_SCHEDULING_POLICY_INVALID")]
    [InlineData("Orders:TakeawayOpenLocal", "19:00", "TAKEAWAY_OUTSIDE_APPROVED_HOURS")]
    [InlineData("Orders:TakeawayMaxAdvanceMinutes", "bad", "TAKEAWAY_SCHEDULING_POLICY_INVALID")]
    public void Scheduling_rejects_unapproved_or_bad_policy(string key, string value, string code) =>
        Assert.Equal(code, TakeawayPickupSchedule.Validate(Now.AddHours(1), Policy((key, value)), Now, Deadline));

    [Fact]
    public void Timing_and_payment_deadline_are_checked_before_any_stock_operation()
    {
        Assert.Equal("TAKEAWAY_PICKUP_TOO_SOON",
            TakeawayPickupSchedule.Validate(Now.AddMinutes(15), Policy(), Now, Deadline));
        Assert.Equal("TAKEAWAY_PICKUP_TOO_FAR",
            TakeawayPickupSchedule.Validate(Now.AddDays(2), Policy(), Now, Deadline));
        Assert.Equal("TAKEAWAY_OUTSIDE_APPROVED_HOURS",
            TakeawayPickupSchedule.Validate(Now.AddHours(11), Policy(), Now, Deadline));
        Assert.Equal("TAKEAWAY_PICKUP_TOO_SOON",
            TakeawayPickupSchedule.Validate(Now.AddMinutes(25), Policy(), Now, Now.AddMinutes(30)));
    }
}
