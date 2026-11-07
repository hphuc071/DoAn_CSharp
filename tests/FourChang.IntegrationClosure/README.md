# SQL outbox / HTTP in-process closure harness

Latest V2 result: **PASS_SQL_OUTBOX + PASS_HTTP_CUSTOMER**,88 assertions on a NEW fixture,
localhost14335 / `FourChangOutboxFixtureV2`, container `fourchang-uc0708-outbox-fixture-v2`.
The old14334 fixture/evidence are retained unchanged. Output: `artifacts/final-review/integration-closure-v2`;
summary: `artifacts/final-review/INTEGRATION-CLOSURE-V2.md`. The wrapper is one-shot and now refuses
the existing V2 name/credentials/write marker. No automatic rerun/reset is permitted.

Customer API and Razor login used actual CustomerAccessService and SessionAuthentication, real
HttpOnly session cookie and antiforgery. The server HMAC secret/customer test password are random
process-only environment values, restored by wrapper; not logged or committed. FixtureSessionClock
is linked into the exact timestamp regression. SQL SELECT after SaveChanges proves all prepared
sessions meet the CHECK, including the intentionally expired historical one.

Outbox UP uses the reviewed DDL with ONLY the fixed database guard relocated to the authorized V2
catalog; both source and applied hashes are recorded. No EXPIRED. SQL delivery proves at-least-once
with a durable synthetic inbox effect, not exactly-once transport or a production notification sink.
Remaining sections describe original design/history; the V2 report is authoritative for results.

Standalone explicit runner, **not** Safety=NoDatabase discovery and not an API/Web runtime service.
TestServer 8.0.11 uses real `SessionAuthentication`, SQL session rows and actual Customer routes;
Razor uses its real antiforgery pipeline. The composition is explicit: API Program/.env/bootstrap
are not executed. No fake authentication handler or claim-injection middleware is installed.

Fixed isolated target: `fourchang-uc0708-outbox-fixture-v2`, project of the same name,
hostname `fc-uc0708-outbox-fixture-v2`, localhost14335, database `FourChangOutboxFixtureV2`.
Never accepts main/historical/V1/V2 targets or arbitrary caller-supplied connections.
SQL2022 image must exist locally. Schema35 source is reused; ONLY the reviewed outbox proposal
is eligible, never EXPIRED. Docker run identity/ports/run marker and effective SQL machine/DB/
transport/DB GUID/schema are checked. EF commands have the write guard; raw provisioning also
checks identity before execution. No external financial/network delivery.

Fixture guard/seed are derived from the successful V2 recipe with separate hard pins/artifacts;
the V2 files, credentials and data are untouched. Compile-time isolation intentionally keeps
fixture targets distinct. Extra brew choice is required by actual Create pricing validation.

## Original14334 attempt history / do not rerun

The single authorized attempt provisioned schema35, seeded F0/F1, applied the outbox migration,
then **failed session preparation before HTTP/outbox scenarios**. The wrapper stopped the owned
container, preserving data/evidence. Existing name, credentials and write-once marker refuse reuse.
See `artifacts/final-review/INTEGRATION-CLOSURE.md`; this is NOT a SQL outbox/positive HTTP PASS.

The production context overwrites Added.ThoiDiemTao. The original expired-session seed supplied
a historical creation time which was overwritten, making expiry <= creation, inconsistent with
CK_PhienDangNhap_2. A no-connection diagnostic confirms this production behavior. The original
exception lacked inner SQL diagnostic evidence; do not claim a captured SQL error number.
The corrected fixture-only HistoricalSessionClock interceptor runs AFTER the production stamp;
it has been built but **not rerun on SQL**. Future execution needs separate authorization/new
fixture identity; never delete markers/reset the failed fixture to bypass the stop condition.

## Safe diagnostic modes

- `--probe-session-clock`: executes the production timestamp override, captures values in a
  SaveChanges interceptor and aborts before SQL. Not a SQL test.
- `--http-readonly-preflight`: TestServer anonymous authorization + Razor CSRF + Checkout OFF,
  SQL interceptor and outbound handler prohibit SQL/backend access. Does not prove valid sessions,
  ownership, enabled writes, cancel replay or actual production hosting configuration.

`--run-once` and Invoke-Closure.ps1 are WRITE-CAPABLE and must not be used without explicit scope.
Credentials are generated randomly and DPAPI encrypted only in ignored artifacts/private; never
publish CLIXML/tokens/browser storage/connection strings. Environment variables are restored and
only the container created by the wrapper is stopped. No auto retry/provisioning/reset/cleanup.

## Prepared scenarios, NOT RUN in failed SQL attempt

HTTP: anonymous/invalid/revoked/expired/admin sessions; Customer create flag OFF despite Web ON;
key/accepted-total errors; one create201, replay200/conflict/foreign owner; own/foreign detail;
real Razor CSRF and locked Web cancel; API cancel owner/reason/version/replay; no PAID/payout route.

SQL outbox: commit events; rollback prior marker/audit/business/event; two claimers and same-stream
ordering; crash before delivery and lost ACK; stale lease fencing; durable SQL fixture inbox dedupe;
poison5 attempts/dead stream blocking while unrelated aggregate progresses. Lease/backoff deadlines
are forced ONLY on fixture delivery state to simulate elapsed time, not a wall-clock120s test.
The SQL inbox writes fixture audit effects, never money/emails/webhooks. It is a test consumer,
not a selected production notification provider/inbox. Exception messages are not logged; future
runner captures only SQL numbers and constraint identifiers. No sensitive-data logging.
