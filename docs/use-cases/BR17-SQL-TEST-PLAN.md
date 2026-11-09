# BR-17 — SQL acceptance plan, awaiting approval

Status: **PREPARED / BLOCKED_BR17**. No SQL was opened in this preparation turn.
`tests/FourChang.Br17Integration` supplies an offline catalogue and an approval-gated,
SELECT-only future preflight. It is not a completed transactional test runner.

## Approval and fixture boundary

Approve D1-D6 in `BR-17-EXPIRY-DECISION.md`, including a precise zero-paid-only or partial
scope. Approval of EXPIRED does not authorize migration, fixture writes or production
activation automatically. Record approver, date, versioned up/down hashes, scope and
rollback authority. Acquire separate permission for provisioning and each write exercise.

Use a NEW instance/container/storage, prospective localhost port14340, database
FourChangBr17MigrationFixture. No old fixture/port14330-14339, historical data or volumes.
Prove container ID, image, project, hostname, loopback socket, effective SQL instance,
catalog, run marker and DB GUID before every write group. A dedicated write guard must
check that identity on EF/raw connections, as in the existing fixture harness. This has
not yet been implemented for BR-17 writes. The prepared read-only preflight is insufficient
authorization to write.

Before bootstrap, prove instance has no user DB; before schema load, prove no user tables.
Load the reviewed canonical schema, capture full CHECK/FK/index/trigger/column definitions
and trust state (35 tables,73 FK,11 triggers,113 CHECK baseline). No outbox delivery migration
or EXPIRED application outside the approved fixture. Use random fixture-only credentials,
synthetic data and generated IDs; retain container/evidence without reset/reseed/deletion.

## Migration and rollback

M01: wrong DB, missing session approval, nested transaction, missing/disabled/untrusted or
drifted CHECK all reject before ALTER. Include changed literals containing spaces/brackets,
lowercase, duplicate/missing states and extra boolean clauses. Review-only scripts now
preserve literal characters and compare binary token sets. Their T-SQL changes have **not**
been parsed/executed by SQL Server in this turn.

M02: capture F0 state/rows and full definitions. Apply approved up in a dedicated bounded
session, all writers OFF; capture F1. Only CK_DonHang_7 changes 11->12 values with trusted
WITH CHECK; columns/FK/indexes/triggers and existing rows are identical. Test blocked DDL,
transaction rollback and concurrent schema drift; current proposals check baseline before
BEGIN TRANSACTION, so deployment must be exclusive and this race must be resolved before
operational use. Do not claim online/lock-free migration.

M03: separate rollback authorization. On empty EXPIRED set down restores original trusted
11-state CHECK. With any EXPIRED row, down must throw51203, preserve CHECK/rows/audit/holds.
Do not convert EXPIRED to CANCELLED, delete rows, recreate holds or truncate audit. Retain
expanded schema/compatibility readers and forward-fix if down is unsafe. Test rollback
before expiry DML; the later nonempty-down test intentionally cannot remove its expiry row.

## SQL oracle (before/after every scenario)

Capture snapshots using SELECT, including exact rows as well as counts:

| Table/source | Required assertion |
| --- | --- |
| DonHang | id/owner/channel/status/deadline/rowversion/fees and unchanged historical snapshots |
| ThanhToan | id/reference/requested/verified/status/deadline/rowversion; exactly one payment per fixture intent |
| ChiTietDonHang, MonThemChiTietDonHang, NguyenLieuChiTietDonHang | identical price/quantity/topping/BOM after expiry; no fabricated cancellation rows |
| GiuChoNguyenLieu | released delta=sum(held-consumed-released before); consumed must0; status EXPIRED on changed holds |
| TonKho | held decreases by exact released sum per ingredient; physical/consumed unchanged; never negative |
| NhatKyHeThong | exactly one EXPIRE_ORDER per deterministic operation, original receipt/hash; immutable creation audit remains |
| Audit JSON outbox | one durable order.expired envelope/EventId/order aggregate, same transaction, no in-memory-only event |
| GiaoDichNganHang | verified bank truth unique by provider/account/transaction; late transfer once, no extra order credit |
| HoSoHoanTien / LanChiHoanTien | zero for zero-paid expiry; partial blocked pending D3, no fabricated SYSTEM actor or SUCCEEDED payout |
| HuyMon / HoaDon | unchanged by expiry or view; no fabricated customer cancellation or issued invoice |

For failure/rejection/replay compare complete relevant rows, not only counts/HTTP. Exact
identity sequences need not be gapless after rollback; never reseed identities. Audit
timestamp+configured15m is the Create deadline; expiry processing time is distinct.

## Transaction scenarios

- E01/E02: expired deadline and zero verified money, WEB/PENDING_PAYMENT, one BANK_TRANSFER
  request, valid reference/version and bank watermark through deadline with no unresolved
  transfers. First run writes EXPIRED/release/audit once; second run/new worker context
  writes nothing. Preserve item/BOM/price/deadline, creation receipt and physical stock.
- E03: null/mismatched/stale proof, future watermark, unresolved transfer, changed payment
  version/reference/deadline, missing hold or stock mismatch must not write. Provider lookup
  occurs outside SQL transaction; worker rechecks rowversions/state under locks.
- E04: no deadline/future deadline/POS/partial/verified/confirmed/accepted/preparing/terminal
  orders do not get timer-expired. Partial must report policy/reconciliation pending, not
  pretend the full BR-17 path is implemented. Test a blocked oldest candidate: current
  batch Take10 + break can starve later orders; no autonomous scheduler until resolved.
- A01: inject failures only in harness at mandatory order/payment/stock/audit save boundaries;
  SELECT must show complete rollback and no dispatchable event. Cancellation and lock wait
  errors must unwind; restart uses deterministic operation, never creates another intent.

## Controlled competition (separate SQL sessions/DbContexts)

Use a fake clock and deterministic barriers around actual SQL work. Record SPIDs, SQL
transaction IDs/isolation4, app-lock resources, waits, timeout/SQL1205 and system_health
graphs. Do not hold external provider calls inside transactions. No automatic retries.

R01: start before deadline; valid full payment commits PENDING_CONFIRMATION first. Expiry
then observes changed order/payment version/state and leaves holds intact. Include payment
waiting across deadline: re-evaluate clock at decision boundary per D2.

R02: expiry commits first with a reconciled zero-paid watermark. A later authenticated
transfer is recorded once for reconciliation, never restores order/holds, never marks
payment/order paid. Exercise callback replay and conflicting external transaction payload.

R03: bank timestamp before deadline but processing after it; expected behavior must follow
approved D2. Current code reconciles because both bank time and decision time must be
strictly before deadline. Exactly-at-deadline also reconciles. Do not change this to pass
tests without a decision.

R04: cancellation is valid only for owner/PENDING_CONFIRMATION with reason/GUID/version.
Run expiry vs cancel on pending (cancel rejects), and payment/cancel/expiry sequence; cancel
winner creates refund REQUESTED only when owed. Employee accept race remains a separate
version/state boundary; no implicit permission for timer cancellation of accepted orders.

R05/R06: Create with accepted-total and its gate vs expiry/payment/cancel sharing ingredient
and audit index; also different orders, duplicate keys and two workers. **Known design risk**:
expiry takes operation->orderstate->audit, while payment/cancel read audit before orderstate.
SERIALIZABLE audit range locks can create cycles with order locks or stock updates. The
Customer Create gate does not serialize these other writers. Rowversion/order application
locks alone do not prove absence of SQL range conversion deadlocks. Resolve/order this
protocol based on captured evidence, then rerun only impacted cases in another new fixture.
Do not remove SERIALIZABLE, take a session lock or add unbounded retry to hide failure.

Finally verify C01 API/Web labels, filters, ownership/no-store, financial Remaining semantics,
POS shift close/recovery and Admin reports; EXPIRED is not CANCELLED/COMPLETED revenue.
Existing POS terminal predicates do not yet include EXPIRED. Keep ExpiryConsumersValidated
false until those compatibility changes and SQL/HTTP checks pass.
