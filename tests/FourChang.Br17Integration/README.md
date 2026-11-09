# BR-17 integration preparation (NOT APPROVED)

This standalone executable prepares the next isolated SQL exercise. It is not part of
solution test discovery and has **no SQL write, migration, seed or worker execution path**.
It does not reuse or change CheckoutSql / SqlVerification historical fixture runners.

Build with `dotnet build tests/FourChang.Br17Integration/FourChang.Br17Integration.csproj`.
Run the built DLL with `--plan` to print the pending SQL scenario catalogue without opening
SQL or calling Docker. Invalid commands/missing approval fail closed.

After separate BR-17 and fixture authorization, `--preflight --approval <local JSON>`
supports SELECT-only checks against a newly provisioned fixture. **Do not run that mode
in the current unapproved phase.** The prospective pin is:

- container/project `fourchang-br17-migration-fixture`, hostname `fc-br17-migration-fixture`;
- loopback `127.0.0.1:14340`, database `FourChangBr17MigrationFixture`;
- label `fourchang.fixture.runid` and DB extended property `FourChang.Br17.FixtureRunId`;
- exact container ID, DB service-broker GUID and reviewed up/down SHA256 in approval evidence.

Port availability has not been checked or reserved. The fixture does not exist as a result
of this package. Before provisioning, inspect existing containers/ports and choose a new
reviewed pin if 14340 is occupied; do not reset/reuse anything. Never repoint at ports
14330-14339 or old fixture volumes. The runner does not provision or bootstrap a schema.

Approval JSON is a local record of an external human decision, **not authorization by
itself**. Keep it under ignored artifacts. Required case-sensitive fields match the
`Approval` record: Status=`APPROVED_FIXTURE_READ_PREFLIGHT`, Approver, DecisionReference,
ApprovedUtc (within 24h), Decisions D1-D6=`APPROVED`, Stage=`Baseline11` or `Migrated12`,
ContainerId, RunId, DatabaseIdentity, UpSha256, DownSha256. Do not fabricate these values.
For D3, the decision can explicitly approve a zero-paid-only first fixture while leaving
partial-paid expiry blocked. That scope must be present in the external decision record;
the string value in this file is not a substitute for reviewing it.

Provide a dedicated SELECT/VIEW DEFINITION connection through process-only
`BR17_FIXTURE_READ_CONNECTION`, never `.env`/source/command arguments/logs. The runner
rejects effective DB/schema INSERT/UPDATE/DELETE/ALTER permissions. Object-level grants
must also be audited when provisioning; privilege checks do not replace the fact that
this program executes SELECT only. Docker inspect output and connection secrets are not
printed. Test flags are never set by this executable.

The preflight checks container/label/binding and effective SQL hostname/catalog/TCP port,
DB identity/marker, canonical counts/trust and exact order CHECK shape. It is only
`PASS_READ_PREFLIGHT_ONLY`, not proof of full definition/trigger equality or isolation of
mounted storage. The approved provisioner must compare the complete schema fingerprint,
image/volume provenance, login grants and F0 baseline before any future write. Copy the
existing reviewed fingerprint query; do not weaken it to counts alone.

## SQL scenarios after approval

The `--plan` output lists M01-M03, E01-E04, R01-R06, A01 and C01. These are **prepared,
not executed SQL tests**. A future guarded write harness must provision a fresh fixture,
apply only the approved EXPIRED proposal, use actual worker/payment/cancel/Create services,
fresh contexts and fake authenticated provider evidence, and capture all SELECT assertions
in `docs/use-cases/BR17-SQL-TEST-PLAN.md`. No bank/dispatcher/external destination is allowed.

Write orchestration and provisioning are intentionally not implemented yet: D1-D6,
partial actor/obligation, consumer compatibility and cross-writer lock ordering remain
approval/integration gates. Do not interpret a successful build or `--plan` as expiry SQL
PASS. Do not enable `ExpiryEnabled`, `ExpiryMappingApproved`, `ExpiryConsumersValidated`
or `SqlIntegrationValidated` in shared runtime. Rehearsal requires process-local settings
only, stops on any drift/deadlock/unknown commit and never automatically retries.
