using FourChang.Api.Features.Customer.Orders;
using FourChang.Api.Features.Customer.Payments;
using FourChang.Domain.Entities.Inventory;
using FourChang.Domain.Entities.Payments;
using FourChang.Presentation.Shared.Contracts.Customer;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class OrderLifecycleOfflineTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(OrderReadTests.Now);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static readonly Guid Operation = Guid.Parse("237e348b-26b4-456c-98e6-f3b640c89c2e");
    private static CancelOrderIntent Cancel() => new(6, 7, Operation, "AQ==", "Khách đổi ý");
    private static OrderLifecycleSnapshot Snapshot(string status = "PENDING_CONFIRMATION", decimal verified = 60000)
    {
        var detail = OrderReadTests.Snapshot(status);
        detail = detail with { Payments = [detail.Payments[0] with { Verified = verified,
            Status = verified == 60000 ? "VERIFIED" : verified > 0 ? "PARTIAL" : "PENDING" }] };
        return new(6, "WEB", detail, [new GiuChoNguyenLieu { MaChiTietDonHang = 1, MaNguyenLieu = 11,
            SoLuongGiuCho = 20, SoLuongDaGiaiPhong = 2, TrangThai = "PARTIAL" }],
            [new TonKho { MaNguyenLieu = 11, TonThucTe = 1000, TongGiuCho = 30 }], true);
    }

    // Test-only transactional ledger, never registered in runtime. Lock represents a serial transaction;
    // it does NOT prove SQL locks, rowversion, persistence, crash recovery or durable operation uniqueness.
    private sealed class MemoryTransaction(OrderLifecycleSnapshot initial, Clock clock) : IOrderLifecycleTransaction
    {
        private readonly object gate = new();
        private readonly Dictionary<(long, Guid), (string Hash, OrderLifecyclePlan Receipt)> operations = [];
        public OrderLifecycleSnapshot State = initial;
        public int AuditCount, OutboxCount, RefundCount, Commits;
        public bool LoseResponse;
        public Task<OrderLifecyclePlan> CancelAsync(CancelOrderIntent command, CancellationToken ct)
        {
            lock (gate)
            {
                if (command.CustomerId != State.CustomerId || command.OrderId != State.Detail.Order.Id)
                    return Task.FromResult(Rejected("NOT_FOUND"));
                var hash = OrderLifecyclePlanner.Fingerprint(command);
                if (operations.TryGetValue((command.CustomerId, command.Operation), out var saved))
                    return Task.FromResult(saved.Hash == hash ? saved.Receipt : Rejected("IDEMPOTENCY_CONFLICT"));
                var plan = new OrderLifecyclePlanner(clock).Cancel(State, command);
                if (!plan.Applicable) return Task.FromResult(plan);
                Apply(plan);
                operations.Add((command.CustomerId, command.Operation), (hash, plan));
                if (LoseResponse) { LoseResponse = false; throw new TimeoutException("Mock response lost after memory commit"); }
                return Task.FromResult(plan);
            }
        }
        public Task<OrderLifecyclePlan> ExpireAsync(ExpireOrderIntent command, CancellationToken ct)
        {
            lock (gate) { return Task.FromResult(new OrderLifecyclePlanner(clock).Expire(State, command)); }
            // No applying an expiry plan: SQL mapping is undecided by BR-17.
        }
        public bool AcceptAsStaff(string version)
        {
            lock (gate)
            {
                if (State.Detail.Version != version || State.Detail.Order.Status != "PENDING_CONFIRMATION") return false;
                State = State with { Detail = State.Detail with { Version = "Ag==", Order = State.Detail.Order with { Status = "ACCEPTED" } } };
                return true;
            }
        }
        private void Apply(OrderLifecyclePlan plan)
        {
            Assert.False(plan.RequiresSqlStatusMapping);
            foreach (var release in plan.Releases)
            {
                State.Holds.Single(x => x.MaChiTietDonHang == release.LineId && x.MaNguyenLieu == release.IngredientId).SoLuongDaGiaiPhong += release.Quantity;
                State.Stocks.Single(x => x.MaNguyenLieu == release.IngredientId).TongGiuCho -= release.Quantity;
            }
            State = State with { Detail = State.Detail with { Version = "Ag==", Order = State.Detail.Order with { Status = plan.TargetOrderStatus! },
                ShippingFee = plan.TargetShippingFee!.Value, Lines = State.Detail.Lines.Select(x => x with { Cancelled = x.Quantity }).ToArray(),
                Refunds = plan.Refunds.Select((x, i) => new CustomerRefundSnapshot(i + 1, "MOCK-RF", x.Status, x.Amount, null, 0)).ToArray() } };
            AuditCount++; OutboxCount++; RefundCount += plan.Refunds.Count; Commits++;
        }
        private OrderLifecyclePlan Rejected(string code) => new(code, State.Detail.Order.Id, State.Detail.Version, null, [], [], false);
    }

    [Fact]
    public void Cancellation_plan_uses_snapshot_remaining_holds_and_requested_refund_without_mutating_entities()
    {
        var snapshot = Snapshot();
        var plan = new OrderLifecyclePlanner(new Clock()).Cancel(snapshot, Cancel());
        Assert.True(plan.Applicable); Assert.Equal("CANCELLED", plan.TargetOrderStatus);
        Assert.Equal(18, Assert.Single(plan.Releases).Quantity);
        Assert.Equal(60000, Assert.Single(plan.Refunds).Amount); Assert.Equal("REQUESTED", plan.Refunds[0].Status);
        Assert.Equal(0, plan.TargetShippingFee); Assert.True(plan.RequiresTransactionalAuditAndOutbox);
        Assert.Equal(2, snapshot.Holds[0].SoLuongDaGiaiPhong); Assert.Equal(30, snapshot.Stocks[0].TongGiuCho);
        Assert.Equal("PENDING_CONFIRMATION", snapshot.Detail.Order.Status);
    }

    [Theory]
    [InlineData("PENDING_PAYMENT")] [InlineData("ACCEPTED")] [InlineData("PREPARING")]
    [InlineData("READY")] [InlineData("COMPLETED")] [InlineData("CANCELLED")]
    public void Only_pending_confirmation_can_be_cancelled(string status) =>
        Assert.Equal("STATE_CONFLICT", new OrderLifecyclePlanner(new Clock()).Cancel(Snapshot(status), Cancel()).Code);

    [Theory]
    [InlineData(9, "AQ==", "Lý do", "NOT_FOUND")]
    [InlineData(6, "Ag==", "Lý do", "VERSION_CONFLICT")]
    [InlineData(6, "AQ==", " ", "INVALID_COMMAND")]
    [InlineData(6, "AQ==", "ab", "INVALID_COMMAND")]
    [InlineData(6, "AQ==", "Lý\ndo", "INVALID_COMMAND")]
    public void Ownership_reason_and_version_are_mandatory(long owner, string version, string reason, string code) =>
        Assert.Equal(code, new OrderLifecyclePlanner(new Clock()).Cancel(Snapshot(), Cancel() with
            { CustomerId = owner, ExpectedVersion = version, Reason = reason }).Code);

    [Fact]
    public void Missing_operation_and_overlong_reason_are_rejected()
    {
        var planner = new OrderLifecyclePlanner(new Clock());
        Assert.Equal("INVALID_COMMAND", planner.Cancel(Snapshot(), Cancel() with { Operation = Guid.Empty }).Code);
        Assert.Equal("INVALID_COMMAND", planner.Cancel(Snapshot(), Cancel() with { Reason = new string('x', 401) }).Code);
    }

    [Theory]
    [InlineData("consumed")] [InlineData("negative")] [InlineData("stock-short")] [InlineData("foreign-hold")]
    [InlineData("duplicate-hold")] [InlineData("missing-holds")] [InlineData("prepared")]
    public void Unsafe_inventory_or_processed_lines_refuse_entire_plan(string problem)
    {
        var snapshot = Snapshot();
        switch (problem)
        {
            case "consumed": snapshot.Holds[0].SoLuongDaTieuHao = 1; break;
            case "negative": snapshot.Holds[0].SoLuongDaGiaiPhong = -1; break;
            case "stock-short": snapshot.Stocks[0].TongGiuCho = 17; break;
            case "foreign-hold": snapshot.Holds[0].MaChiTietDonHang = 999; break;
            case "duplicate-hold": snapshot = snapshot with { Holds = [snapshot.Holds[0], snapshot.Holds[0]] }; break;
            case "missing-holds": snapshot = snapshot with { Holds = [] }; break;
            case "prepared": snapshot = snapshot with { Detail = snapshot.Detail with { Lines = [snapshot.Detail.Lines[0] with { Started = 1 }] } }; break;
        }
        var plan = new OrderLifecyclePlanner(new Clock()).Cancel(snapshot, Cancel());
        Assert.False(plan.Applicable); Assert.Empty(plan.Releases); Assert.Empty(plan.Refunds);
    }

    [Fact]
    public async Task Mock_cancel_replay_and_lost_response_release_and_request_refund_exactly_once()
    {
        var store = new MemoryTransaction(Snapshot(), new Clock()) { LoseResponse = true };
        await Assert.ThrowsAsync<TimeoutException>(() => store.CancelAsync(Cancel(), default));
        var replay = await store.CancelAsync(Cancel(), default);
        Assert.True(replay.Applicable); Assert.Equal(1, store.Commits); Assert.Equal(1, store.AuditCount); Assert.Equal(1, store.OutboxCount);
        Assert.Equal(1, store.RefundCount); Assert.Equal(20, store.State.Holds[0].SoLuongDaGiaiPhong);
        Assert.Equal(12, store.State.Stocks[0].TongGiuCho); Assert.Equal(1000, store.State.Stocks[0].TonThucTe);
        Assert.Equal(0, store.State.Detail.Refunded); Assert.Equal(60000, store.State.Detail.RefundObligation);
        Assert.Equal("REQUESTED", store.State.Detail.Refunds[0].Status);
        Assert.Equal("IDEMPOTENCY_CONFLICT", (await store.CancelAsync(Cancel() with { Reason = "Nội dung khác" }, default)).Code);
        Assert.Equal("NOT_FOUND", (await store.CancelAsync(Cancel() with { CustomerId = 9 }, default)).Code);
        Assert.Equal(1, store.Commits);
    }

    [Fact]
    public async Task Concurrent_mock_cancel_requests_share_one_operation_receipt()
    {
        var store = new MemoryTransaction(Snapshot(), new Clock());
        var results = await Task.WhenAll(Task.Run(() => store.CancelAsync(Cancel(), default)),
            Task.Run(() => store.CancelAsync(Cancel(), default)));
        Assert.All(results, result => Assert.True(result.Applicable)); Assert.Same(results[0], results[1]);
        Assert.Equal(1, store.Commits); Assert.Equal(1, store.RefundCount); Assert.Equal(12, store.State.Stocks[0].TongGiuCho);
    }

    [Fact]
    public void Already_released_holds_have_no_second_release_and_existing_refunds_require_reconciliation()
    {
        var state = Snapshot(); state.Holds[0].SoLuongDaGiaiPhong = 20; state.Holds[0].TrangThai = "RELEASED";
        Assert.Empty(new OrderLifecyclePlanner(new Clock()).Cancel(state, Cancel()).Releases);
        state = state with { Detail = state.Detail with { Refunds = [new(1, "REFUND", "REQUESTED", 60000, null, 0)] } };
        Assert.Equal("RECONCILIATION_REQUIRED", new OrderLifecyclePlanner(new Clock()).Cancel(state, Cancel()).Code);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Mock_cancel_vs_staff_acceptance_has_one_winner_under_version_guard(bool staffFirst)
    {
        var store = new MemoryTransaction(Snapshot(), new Clock());
        if (staffFirst)
        {
            Assert.True(store.AcceptAsStaff("AQ=="));
            Assert.Equal("VERSION_CONFLICT", (await store.CancelAsync(Cancel(), default)).Code);
            Assert.Equal(0, store.Commits); Assert.Equal(30, store.State.Stocks[0].TongGiuCho);
        }
        else
        {
            Assert.True((await store.CancelAsync(Cancel(), default)).Applicable);
            Assert.False(store.AcceptAsStaff("AQ==")); Assert.Equal(1, store.Commits);
        }
    }

    [Fact]
    public void Expiry_boundary_needs_bank_reconciliation_and_unresolved_sql_mapping_stays_blocked()
    {
        var clock = new Clock(); var planner = new OrderLifecyclePlanner(clock);
        var snapshot = Snapshot("PENDING_PAYMENT", 10000); var command = new ExpireOrderIntent(7, Operation, "AQ==");
        Assert.Equal("NOT_DUE", planner.Expire(snapshot, command).Code);
        clock.Now = new(snapshot.Detail.ExpiresUtc!.Value);
        Assert.Equal("RECONCILIATION_REQUIRED", planner.Expire(snapshot with { BankReconciledThroughDeadline = false }, command).Code);
        var plan = planner.Expire(snapshot, command);
        Assert.Equal("EXPIRY_MAPPING_REQUIRED", plan.Code); Assert.False(plan.Applicable); Assert.True(plan.RequiresSqlStatusMapping);
        Assert.Null(plan.TargetOrderStatus); Assert.Equal(18, Assert.Single(plan.Releases).Quantity);
        Assert.Equal(10000, Assert.Single(plan.Refunds).Amount); Assert.Equal("PENDING_PAYMENT", snapshot.Detail.Order.Status);
    }

    [Fact]
    public void Fully_paid_and_confirmed_orders_are_never_expired()
    {
        var clock = new Clock { Now = new(OrderReadTests.Now.AddHours(1)) }; var planner = new OrderLifecyclePlanner(clock);
        Assert.Equal("RECONCILIATION_REQUIRED", planner.Expire(Snapshot("PENDING_PAYMENT"), new(7, Operation, "AQ==")).Code);
        Assert.Equal("STATE_CONFLICT", planner.Expire(Snapshot(), new(7, Operation, "AQ==")).Code);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void Payment_expiry_interleavings_revalidate_version_and_do_not_persist_unmapped_status(bool paymentFirst)
    {
        // A payment assessment started before the deadline; expiry assessed at the deadline against the
        // same version. Re-read the snapshot at the transaction boundary; no SQL mapping is invented.
        var clock = new Clock(); var snapshot = Snapshot("PENDING_PAYMENT", 0);
        var payment = new ThanhToan { MaThanhToan = 3, PhuongThuc = "BANK_TRANSFER", TrangThai = "PENDING",
            SoTienYeuCau = 60000, HetHanLuc = snapshot.Detail.ExpiresUtc, MaThamChieuChuyenKhoan = "REF" };
        var evidence = new VerifiedBankTransfer(new GiaoDichNganHang { TenNhaCungCap = "MOCK", MaTaiKhoanNganHangQuan = "MOCK",
            MaGiaoDichBenNgoai = "TX", ChieuGiaoDich = "IN", SoTien = 60000, NoiDungChuyenKhoan = "REF", ThoiDiemNganHang = clock.Now.UtcDateTime }, "VND");
        Assert.Equal(TransferAssessment.Exact, PaymentVerificationPolicy.Assess(evidence, payment, "PENDING_PAYMENT", "MOCK", "MOCK", clock.Now.UtcDateTime, []));
        clock.Now = new(snapshot.Detail.ExpiresUtc!.Value);
        Assert.True(new OrderLifecyclePlanner(clock).Expire(snapshot, new(7, Operation, "AQ==")).RequiresSqlStatusMapping);
        if (paymentFirst)
        {
            var fresh = snapshot with { Detail = snapshot.Detail with { Version = "Ag==",
                Order = snapshot.Detail.Order with { Status = "PENDING_CONFIRMATION" },
                Payments = [snapshot.Detail.Payments[0] with { Verified = 60000, Status = "VERIFIED" }] } };
            Assert.Equal("VERSION_CONFLICT", new OrderLifecyclePlanner(clock).Expire(fresh, new(7, Operation, "AQ==")).Code);
            Assert.Equal("STATE_CONFLICT", new OrderLifecyclePlanner(clock).Expire(fresh, new(7, Operation, "Ag==")).Code);
        }
        else
        {
            Assert.False(new OrderLifecyclePlanner(clock).Expire(snapshot, new(7, Operation, "AQ==")).Applicable);
            Assert.Equal(TransferAssessment.Reconciliation, PaymentVerificationPolicy.Assess(evidence, payment, "PENDING_PAYMENT", "MOCK", "MOCK", clock.Now.UtcDateTime, []));
        }
        Assert.Equal("PENDING", payment.TrangThai); Assert.Equal(0, payment.SoTienDaXacMinh);
    }

    private sealed class Candidates : IOrderExpiryCandidates
    {
        public int Calls;
        public Task<IReadOnlyList<ExpireOrderIntent>> ReadReconciledAsync(DateTime nowUtc, CancellationToken ct)
        { Calls++; return Task.FromResult<IReadOnlyList<ExpireOrderIntent>>([new(7, Operation, "AQ==")]); }
    }
    [Fact]
    public async Task Worker_default_off_reads_nothing_and_enabled_mock_still_cannot_apply_unmapped_expiry()
    {
        var clock = new Clock { Now = new(OrderReadTests.Now.AddHours(1)) }; var candidates = new Candidates();
        var store = new MemoryTransaction(Snapshot("PENDING_PAYMENT", 0), clock);
        Assert.Empty(await new OrderExpiryWorker(new(), clock, candidates, store).RunOnceAsync(default));
        Assert.Equal(0, candidates.Calls); Assert.Equal(0, store.Commits);
        Assert.True(Assert.Single(await new OrderExpiryWorker(new(true), clock, candidates, store).RunOnceAsync(default)).RequiresSqlStatusMapping);
        Assert.Equal(0, store.Commits); Assert.Equal(30, store.State.Stocks[0].TongGiuCho);
    }
}
