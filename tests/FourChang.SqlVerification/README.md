# UC-WEB-07/08 direct SQL fixture V2

Latest result: **PASS_SQL_FIXTURE**,26scenario records, not bank/HTTP integration.
Read `artifacts/uc-web-07-08/SQL-INTEGRATION-RESULT.md` and raw `sql-integration-v2-run.json`.
The V1 report/diff/evidence and stopped V1 container at14332 are preserved. No reset/reseed/delete.

## Isolation and execution

- V2 only: `fourchang-uc0708-fixture-v2`,127.0.0.1:14333,`FourChangUc0708FixtureV2`.
- `FixtureSqlGuard.cs` pins target; verifies Docker project/name/ID/RunId/hostname/port/freshness,
  actual SQL machine/catalog/TCP/session/database GUID and marker. Full schema fingerprint is checked
  before the suite; each EF connection/command checks schema counts/trust and unmigrated order CHECK
  before SQL. Synchronous EF paths reject. Raw provisioning batches are separately guarded.
- Ports14330/14331/14332 and main/historical/V1 catalogs reject before connection. No.env/default
  connection fallback. Runner does not start API/Web, register fake providers or hosted workers.
- `Invoke-Fixture.ps1` has explicit Create/Identity/Provision/Run/Stop modes, exact owned-container
  checks and process-only credentials. Random credentials are Windows-DPAPI encrypted in ignored
  `artifacts/uc-web-07-08/private/uc0708-fixture-v2.clixml`, never code/argv/report/log. Do not publish
  Docker Config.Env or CLIXML. Production flags remain OFF; fake events only live in this assembly.
- Runner is outside solution/test discovery; Safety=NoDatabase does not select SQL scenarios.

V2 was provisioned once and suite executed once. It is now stopped with data retained. Create refuses
name/credential reuse; Provision refuses a populated instance. Run refuses previously used financial/
audit history. Do not invoke these again on this fixture as an automatic retry. Identity is SELECT-only
and refuses a stopped container; no automatic restart. Stop targets only the saved owned ID.

## Seed and schema

Canonical `docs/database/35-bang/schema.sql`,35tables/73FKs/11triggers, no EXPIRED migration. F0 zero
business rows. One seed transaction produced2locked synthetic Customers,1Admin,10TAKEAWAY orders,
10PENDING bank-transfer requests,10immutable BOMs/RESERVED holds and1stock row (1000g,100g held).
Contacts use distinct reserved example.invalid addresses. All foreign IDs come from entities after
SaveChangesAsync. No caller assigns Admin/Customer ID1 or assumes contiguous IDs.

Canonical SQL itself still has CK_TaiKhoanQuanLy_1 requiring MaQuanLy=1. This rule was not removed
or bypassed. V1 rollback consumed that identity and made reuse unsafe. Fresh V2 naturally generates
its allowed Admin ID; code reads the actual ID. No DBCC CHECKIDENT, IDENTITY_INSERT, schema rewrite
or V1 mutation exists. Any seed failure stops the run.

F1 validates ownership, price/payment equality, versions, no prior financial/audit history, line/BOM/
hold amounts and global stock total before a business writer runs. F0/F1 and schema metadata are
persisted in `sql-fixture-v2-provision.json`.

## Actual SQL assertions executed

- Required-contact regression: insert valid synthetic Customer and get actual ID, then invalid
  missing-contact row must hit CK_TaiKhoanKhachHang_3/547; rollback removes the earlier valid insert.
- Exact/partial/excess payment, wrong receiver, untrusted/null source, wrong reference, delayed
  processing and late bank timestamp. Verify payment/order/ledger actual amounts/states after writes.
- Same bank transaction replay receipt and complete snapshot unchanged; changed duplicate409.
- Two simultaneous trusted events through a barrier and separate contexts ->200/200 same receipt,
  only one bank credit. This is a fixture event boundary, not actual bank signature verification.
- Cancellation owner/state/reason/malformed and stale version checks; success cancels whole order,
  releases only unconsumed holds, leaves physical stock unchanged, creates one REQUESTED refund.
- Original GUID/payload/version replay leaves snapshot identical; changed payload409.
- Payment/cancellation fault inserts a valid SQL audit marker in the active service transaction,
  then invalidates required audit JSON. CK_NhatKyHeThong_4 must throw547; fresh snapshot equality
  proves marker and transaction effects rolled back. Fault exists only in this runner assembly.
- Staff-versus-cancel observes a real sys.dm_exec_requests lock wait on an uncommitted staff order
  update. Staff commits, stale cancellation409; direct EF staff update, not a staff HTTP workflow.

Suite used16payment-service calls,10cancellation-service calls,1staff fixture update; no new order
Create request in the suite. Setup created10fixture orders. No auto retry/cleanup/compensation.
Final stock:1000g physical/90g held;9RESERVED and1RELEASED hold.10bank rows,11audits,1REQUESTED
refund10000VND,0payouts; BOM unchanged. Raw per-step before/after snapshots and grouped snapshots
are saved. The statuses200/400/401/404/409 are direct IResult values, not network HTTP measurements.

EXPIRED/expiry worker/callback race remain BLOCKED_BR17. No migration or worker runs. Bank protocol
crypto, reconciliation/payout execution, real PDF archive, HTTP authentication/CSRF and advanced
UC06 timeout/multi-tab remain separate integration work. Do not unlock production from fixture PASS.
