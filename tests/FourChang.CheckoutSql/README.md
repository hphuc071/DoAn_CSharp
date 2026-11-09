# UC06 accepted-total / SERIALIZABLE SQL integration

Standalone, explicit `--run-once`; never part of `Safety=NoDatabase` or automatic test discovery.
Current **one-shot** target: `127.0.0.1:14339 / FourChangUc06ConsentFixtureV4`, container/project
`fourchang-uc06-consent-fixture-v4`, hostname `fc-uc06-consent-fixture-v4`.
The wrapper refuses existing container, credential evidence or write-once marker. Never reset/reseed
or repoint this runner at main/history/earlier fixtures (14330–14338).

The user must authorize fixture SQL writes before running. Build the project first, then invoke
`Invoke-Fixture.ps1`. It uses an already-local SQL image, fresh credentials (DPAPI ignored storage),
loopback-only binding, canonical source SHA256, effective SQL identity/DB GUID/run marker and
schema guards before EF commands/raw writes. Only the newly owned container is stopped; its data
and evidence are retained. No `.env`, application bootstrap, migration or EXPIRED is used.

FixtureSqlGuard/FixtureProvisioner derive from the reviewed IntegrationClosure fixture, with a
new fixed target/credentials/marker and a zero-order base seed. The schema fingerprint source is
linked from that project. Added synthetic topping/recipe and historical-format receipt use real
SQL constraints and actual generated IDs. This is not a production seeder or payment provider.

API and Razor use TestServer and actual Customer login, SessionAuthentication, authorization,
HttpOnly cookie, CSRF and ownerProof. Production route mappers/services are called without fake
authentication. Only this harness sets Customer/Web Create flags; no worker/dispatcher/payment
processing is enabled. Existing Admin/POS GET policy checks are limited regression evidence,
not a complete POS shift/order workflow certification.

SaveControl is fixture-only: it observes SQL isolation/transaction IDs, pauses before a first
order SaveChanges to create deterministic overlap, and can throw at mandatory audit SaveChanges
to prove rollback. SQL price updates and app-lock holders are fixture-only and guarded. No
financial API, bank, routing provider or production fault hook is called.

Coverage: exact Create/replay, missing/mismatched consent, changed total/payload/owner, historical
DTO hash/deadline replay; same/different GUID overlap; menu price writer blocked by SERIALIZABLE;
changed cart/topping/quantity totals and BOM; audit rollback; 10s operation/common gate timeout;
SQL B0/B1/B2 snapshots, journal outbox, pending payment, holds and physical stock. No automatic
business retry, GUID replacement or retry-on-failure SQL strategy is enabled.

Artifacts under `artifacts/uc-web-06/sql-consent*` preserve successive runs, source/DLL hashes,
safe container proof, snapshots and deadlock graphs. V1 found independent Create failure; V2
targeted diagnostic had a harness flag setup error; V3 captured actual SQL1205 + audit-index
range conversion deadlock. V4 verifies the narrow Customer Create transaction coordination fix.
Do not interpret failed or incomplete earlier runs as PASS. Review `ACCEPTED-TOTAL-SQL-RESULT.md`
for final measured results and limits. `UC06_INDEPENDENT_ONLY=true` is a harness diagnostic mode,
not a production setting; the full final run must leave it unset.

`--inspect-failure` is a SELECT-only diagnostic entry for the currently pinned fixture; it never
replays or provisions. Earlier evidence has its own source/binary identity. Do not use the newest
binary with an earlier wrapper/credential file. Any additional fixture requires a separately
reviewed new target/pin and authorization; this runner deliberately cannot reuse its target.
