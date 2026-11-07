using System.Text.Json;
using Microsoft.Playwright;

namespace FourChang.Tests;

// Part of the Safety=NoDatabase fixture. Every request is routed to a mock,
// including persistent-profile browser restarts; no API or SQL is started.
public sealed partial class CheckoutBrowserTests
{
    private sealed class CommittedOrder
    {
        public string? OriginalCommand { get; private set; }
        public int Creations { get; private set; }
        public bool Accept(string command)
        {
            if (OriginalCommand is not null)
            {
                Assert.Equal(OriginalCommand, command);
                return false;
            }
            OriginalCommand = command;
            Creations++;
            return true;
        }
    }

    private static Task<string> Pending(IPage page) =>
        page.EvaluateAsync<string>("localStorage.getItem('fourchang.checkout-intent.v1')");

    private static Task RetryReady(IPage page) =>
        Assertions.Expect(page.Locator("#checkout-proceed")).ToHaveTextAsync("Thử lại thao tác đang chờ kết quả");

    private static async Task AssertRecovered(IPage page, CommittedOrder committed)
    {
        await Assertions.Expect(page.Locator("#checkout-receipt")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#receipt-code")).ToHaveTextAsync("ORD-TEST");
        using var stored = JsonDocument.Parse(await Pending(page));
        Assert.Equal(committed.OriginalCommand, stored.RootElement.GetProperty("command").GetRawText());
        Assert.Equal(12, stored.RootElement.GetProperty("receipt").GetProperty("maDonHang").GetInt32());
        Assert.Equal(1, committed.Creations);
        Assert.EndsWith("/Checkout", page.Url);
    }

    [Theory]
    [InlineData("network")]
    [InlineData("timeout")]
    public async Task LostResponseAfterMockCommitRetriesOriginalEvenIfQuoteChanges(string failure)
    {
        var committed = new CommittedOrder();
        create = async route =>
        {
            if (committed.Accept(route.Request.PostData!))
            {
                if (failure == "network") await route.AbortAsync("failed");
                // For timeout the mock has committed, but deliberately sends no response.
            }
            else await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = Receipt });
        };
        var page = await Open(true);
        await Confirm(page);
        if (failure == "timeout")
            await page.EvaluateAsync("() => { const original=AbortSignal.timeout.bind(AbortSignal); AbortSignal.timeout=()=>original(100); }");
        await page.Locator("#checkout-proceed").ClickAsync();
        await RetryReady(page);
        var pending = await Pending(page);
        await Assertions.Expect(page.Locator("#checkout-receipt")).ToBeHiddenAsync();
        Assert.NotNull(await page.EvaluateAsync<string?>("localStorage.getItem('" + CartKey + "')"));
        await page.ReloadAsync();
        await Mount(page);
        await page.WaitForFunctionAsync("!!window.fourchangCheckoutQuote");
        await page.EvaluateAsync("() => {window.fourchangCheckoutQuote.price.tongCong=99000; window.fourchangCheckoutQuote.payload.items[0].soLuong=2; window.dispatchEvent(new Event('checkout:quote'));}");
        Assert.Equal(pending, await Pending(page));
        await page.Locator("#checkout-proceed").ClickAsync();
        await AssertRecovered(page, committed);
        Assert.Equal(2, bodies.Count);
        Assert.Equal(bodies[0], bodies[1]);
        using var sent = JsonDocument.Parse(bodies[1]);
        Assert.Equal(45000, sent.RootElement.GetProperty("acceptedTotal").GetDecimal());
        Assert.Null(await page.EvaluateAsync<string?>("localStorage.getItem('" + CartKey + "')"));
    }

    [Fact]
    public async Task ReloadDuringInFlightCommittedRequestRecoversOriginalOperation()
    {
        var committed = new CommittedOrder();
        var firstCommit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        create = async route =>
        {
            if (committed.Accept(route.Request.PostData!)) firstCommit.TrySetResult();
            else await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = Receipt });
        };
        var page = await Open(true);
        await Confirm(page);
        await page.Locator("#checkout-proceed").ClickAsync();
        await firstCommit.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var pending = await Pending(page);
        await page.ReloadAsync(); // Destroys the sender and its Web Lock, not the committed mock order.
        await Mount(page);
        Assert.Equal(pending, await Pending(page));
        await page.Locator("#checkout-proceed").ClickAsync();
        await AssertRecovered(page, committed);
        Assert.Equal(2, bodies.Count);
    }

    [Fact]
    public async Task BrowserProcessRestartPreservesOriginalPendingIntentInSameProfile()
    {
        await context.DisposeAsync(); context = null!;
        await browser.DisposeAsync(); browser = null!;
        var profileRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "FourChang.Checkout.NoDatabase"));
        var profile = Path.GetFullPath(Path.Combine(profileRoot, Guid.NewGuid().ToString("N")));
        var committed = new CommittedOrder();
        try
        {
            context = await playwright.Chromium.LaunchPersistentContextAsync(profile, new() { Channel = "msedge", Headless = true });
            await Wire(context);
            create = async route =>
            {
                if (committed.Accept(route.Request.PostData!)) await route.AbortAsync("failed");
                else await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = Receipt });
            };
            var page = await Open(true);
            await Confirm(page);
            await page.Locator("#checkout-proceed").ClickAsync();
            await RetryReady(page);
            var pending = await Pending(page);
            var cart = await page.EvaluateAsync<string>("localStorage.getItem('" + CartKey + "')");
            await context.DisposeAsync(); context = null!; // Closes the actual persistent browser process.
            context = await playwright.Chromium.LaunchPersistentContextAsync(profile, new() { Channel = "msedge", Headless = true });
            await Wire(context);
            var restored = await Open(); // No manual storageState/localStorage reseeding.
            Assert.Equal(pending, await Pending(restored));
            Assert.Equal(cart, await restored.EvaluateAsync<string>("localStorage.getItem('" + CartKey + "')"));
            await restored.Locator("#checkout-proceed").ClickAsync();
            await AssertRecovered(restored, committed);
            Assert.Equal(2, bodies.Count);
        }
        finally
        {
            if (context is not null) { await context.DisposeAsync(); context = null!; }
            // Delete only this generated test profile after checking its resolved absolute path.
            Assert.StartsWith(profileRoot + Path.DirectorySeparatorChar, profile, StringComparison.OrdinalIgnoreCase);
            if (Directory.Exists(profile)) Directory.Delete(profile, recursive: true);
        }
    }

    [Fact]
    public async Task OtherTabCanRecoverSameCommitAfterSendingTabCloses()
    {
        var committed = new CommittedOrder();
        var firstCommit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        create = async route =>
        {
            if (committed.Accept(route.Request.PostData!)) firstCommit.TrySetResult();
            else await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = Receipt });
        };
        var first = await Open(true);
        var second = await Open();
        await Confirm(first);
        await first.Locator("#checkout-proceed").ClickAsync();
        await firstCommit.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await second.WaitForFunctionAsync("!!localStorage.getItem('fourchang.checkout-intent.v1')");
        await Assertions.Expect(second.Locator("#checkout-pending-details")).ToBeVisibleAsync();
        await second.Locator("#checkout-proceed").ClickAsync();
        Assert.Single(bodies); // Sender still owns the real origin-scoped browser Web Lock.
        await first.CloseAsync();
        await second.Locator("#checkout-proceed").ClickAsync();
        await AssertRecovered(second, committed);
        Assert.Equal(2, bodies.Count);
    }

    [Fact]
    public async Task ServerFlagTurnsOffOnStalePageWithoutLosingPendingRecovery()
    {
        create = route => route.FulfillAsync(new() { Status = 503, ContentType = "application/json", Body = """{"code":"CHECKOUT_DISABLED","message":"Checkout disabled"}""" });
        var page = await Open(true);
        await Confirm(page);
        await page.Locator("#checkout-proceed").ClickAsync(); // Stale enabled page may POST; server must refuse.
        await RetryReady(page);
        var pending = await Pending(page);
        var disabled = await Open(creationEnabled: false);
        await disabled.EvaluateAsync("() => { const b=document.getElementById('checkout-proceed');b.disabled=false;b.click(); }");
        Assert.Single(bodies);
        Assert.Equal(pending, await Pending(disabled));
        create = route => route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = Receipt });
        var enabled = await Open();
        await enabled.Locator("#checkout-proceed").ClickAsync();
        await Assertions.Expect(enabled.Locator("#checkout-receipt")).ToBeVisibleAsync();
        Assert.Equal(bodies[0], bodies[1]);
    }

    [Fact]
    public async Task LogoutAndDifferentCustomerCannotAdoptPendingButOriginalCustomerCanRecover()
    {
        create = route => route.AbortAsync("failed");
        var a = await Open(true);
        await Confirm(a);
        await a.Locator("#checkout-proceed").ClickAsync();
        await RetryReady(a);
        var pending = await Pending(a);
        foreach (var customer in new[] { "", "42" })
        {
            var other = await Open(customerId: customer);
            await Assertions.Expect(other.Locator("#checkout-proceed")).ToBeDisabledAsync();
            await Assertions.Expect(other.Locator("#takeaway-name")).ToHaveValueAsync("");
            await other.EvaluateAsync("() => {const b=document.getElementById('checkout-proceed');b.disabled=false;b.click();}");
            Assert.Single(bodies);
            Assert.Equal(pending, await Pending(other));
        }
        create = route => route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = Receipt });
        var original = await Open();
        await original.Locator("#checkout-proceed").ClickAsync();
        await Assertions.Expect(original.Locator("#checkout-receipt")).ToBeVisibleAsync();
        Assert.Equal(bodies[0], bodies[1]);
    }

    [Fact]
    public async Task NewNonemptyCartFromOtherTabSurvivesOriginalReplayReceipt()
    {
        var committed = new CommittedOrder();
        create = async route =>
        {
            if (committed.Accept(route.Request.PostData!)) await route.AbortAsync("failed");
            else await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = Receipt });
        };
        var first = await Open(true);
        await Confirm(first);
        await first.Locator("#checkout-proceed").ClickAsync();
        await RetryReady(first);
        var pending = await Pending(first);
        var second = await Open();
        var changed = await second.EvaluateAsync<string>("() => {const cart=JSON.parse(localStorage.getItem('fourchang.cart-draft.v1'));cart[0].quantity=3;const raw=JSON.stringify(cart);localStorage.setItem('fourchang.cart-draft.v1',raw);return raw;}");
        await first.WaitForFunctionAsync("JSON.parse(localStorage.getItem('fourchang.cart-draft.v1'))[0].quantity===3");
        Assert.Equal(pending, await Pending(first));
        await first.Locator("#checkout-proceed").ClickAsync();
        await AssertRecovered(first, committed);
        Assert.Equal(changed, await first.EvaluateAsync<string>("localStorage.getItem('" + CartKey + "')"));
        await Assertions.Expect(second.Locator("#checkout-receipt")).ToBeVisibleAsync();
        await Assertions.Expect(second.Locator("#checkout-proceed")).ToBeDisabledAsync();
        Assert.Equal(2, bodies.Count);
    }

    [Fact]
    public async Task ReceiptStorageFailureKeepsPendingAndCartUntilSameOperationCanReplay()
    {
        var committed = new CommittedOrder();
        create = route => route.FulfillAsync(new() { Status = committed.Accept(route.Request.PostData!) ? 201 : 200, ContentType = "application/json", Body = Receipt });
        var page = await Open(true);
        await Confirm(page);
        await page.EvaluateAsync("""
        () => {window.originalSetItem=Storage.prototype.setItem;
          Storage.prototype.setItem=function(key,value){
            if(key==='fourchang.checkout-intent.v1' && JSON.parse(value).receipt) throw new Error('Receipt storage unavailable');
            return window.originalSetItem.call(this,key,value);
          };}
        """);
        await page.Locator("#checkout-proceed").ClickAsync();
        await RetryReady(page);
        await Assertions.Expect(page.Locator("#checkout-receipt")).ToBeHiddenAsync();
        using (var pending = JsonDocument.Parse(await Pending(page))) Assert.False(pending.RootElement.TryGetProperty("receipt", out _));
        Assert.NotNull(await page.EvaluateAsync<string?>("localStorage.getItem('" + CartKey + "')"));
        await page.EvaluateAsync("() => { Storage.prototype.setItem=window.originalSetItem; }");
        await page.Locator("#checkout-proceed").ClickAsync();
        await AssertRecovered(page, committed);
        Assert.Equal(2, bodies.Count);
    }

    [Fact]
    public async Task CorruptPendingRequiresManualReconciliationAndIsNeverRemoved()
    {
        var page = await Open(true);
        const string broken = "{corrupt-pending-operation";
        await page.EvaluateAsync("raw=>{localStorage.setItem('fourchang.checkout-intent.v1',raw);window.dispatchEvent(new Event('pageshow'));}", broken);
        await Assertions.Expect(page.Locator("#checkout-proceed")).ToBeDisabledAsync();
        await page.EvaluateAsync("() => {const b=document.getElementById('checkout-proceed');b.disabled=false;b.click();}");
        Assert.Empty(bodies);
        Assert.Equal(broken, await Pending(page));
    }

    [Fact]
    public async Task MissingWebLocksFailsClosedWithoutSending()
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync("http://localhost:18791/Checkout");
        await page.EvaluateAsync("Object.defineProperty(navigator,'locks',{value:undefined})");
        await Mount(page);
        await Assertions.Expect(page.Locator("#checkout-proceed")).ToBeDisabledAsync();
        await page.EvaluateAsync("() => {const b=document.getElementById('checkout-proceed');b.disabled=false;b.click();}");
        Assert.Empty(bodies);
        Assert.Null(await page.EvaluateAsync<string?>("localStorage.getItem('" + IntentKey + "')"));
    }

    [Theory]
    [InlineData(200, "text/html", "<html>not a receipt</html>")]
    [InlineData(201, "application/json", "{broken")]
    [InlineData(201, "application/octet-stream", "")]
    public async Task InvalidSuccessResponseKeepsPendingAndRecoversOnValidReplay(int status, string media, string body)
    {
        create = route => route.FulfillAsync(new() { Status = status, ContentType = media, Body = body });
        var page = await Open(true);
        await Confirm(page);
        await page.Locator("#checkout-proceed").ClickAsync();
        await RetryReady(page);
        var pending = await Pending(page);
        await Assertions.Expect(page.Locator("#checkout-receipt")).ToBeHiddenAsync();
        Assert.NotNull(await page.EvaluateAsync<string?>("localStorage.getItem('" + CartKey + "')"));
        create = route => route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = Receipt });
        await page.ReloadAsync();
        await Mount(page);
        Assert.Equal(pending, await Pending(page));
        await page.Locator("#checkout-proceed").ClickAsync();
        await Assertions.Expect(page.Locator("#checkout-receipt")).ToBeVisibleAsync();
        Assert.Equal(bodies[0], bodies[1]);
        Assert.Equal(2, bodies.Count);
    }
}
