# .NET 10 LTS migration

## Scope

- All eight projects target `net10.0`, including tests and the translation-seed tool.
- SDK: `10.0.404`; ASP.NET/Core runtime: `10.0.15`.
- EF Core: `10.0.15`; Npgsql EF provider: `10.0.3`.
- JWT bearer, SignalR Redis and distributed Redis cache: `10.0.15`.
- API and migrator images explicitly use Ubuntu 24.04 (`noble`), including the
  PostgreSQL driver's GSSAPI native library. Runtime containers remain non-root.
- CI reads the SDK from `global.json`; its required check name remains
  `Restore and build backend`. Upgrade branches now run push CI too.

No business rules, API routes, JWT signing keys, session versions, Redis key
names/ACLs, payment rules or persisted data-protection keys are changed.
The old redundant ASP.NET packages are replaced by the supported shared
framework; the unused Microsoft OpenAPI generator package is removed.
Swashbuckle and the existing API documentation format are retained.

## Developer prerequisites

Install SDK `10.0.404` or a later **10.0.4xx patch**, not just the .NET runtime.
`global.json` deliberately rejects older servicing patches and preview SDKs.
The isolated validation SDK is not a machine-wide SDK installation.

```powershell
dotnet --version
dotnet restore ECafe.sln -p:NuGetAudit=true -p:NuGetAuditMode=all -warnaserror:NU1900,NU1901,NU1902,NU1903,NU1904,NU1905
dotnet build ECafe.sln -c Release --no-restore
dotnet build tools/GenerateFrozenTranslationSeed/GenerateFrozenTranslationSeed.csproj -c Release
```

Use EF tooling `10.0.15` when creating future migrations. This platform upgrade
does not add a migration or edit the historical snapshots. Do not suppress
`PendingModelChangesWarning` to make a migration succeed.

## Compatibility protections

`DotNet10CompatibilityTests` verifies the target/runtime, aligned platform
dependencies and the absence of pending schema changes. Existing real PostgreSQL
tests continue to exercise row locks, competing requests, stock/cost accounting,
payment/session completion and tenant boundaries. Redis tests include separate
ACL identities, connection admission and actual stop/restart recovery.

Pkcs regression tests now verify the loaded `10.0.15` implementation: the shared
framework can supply it without a separate entry in the API dependency manifest.
EF 10 design-time tools introduce `Newtonsoft.Json/13.0.4`; the regression test
checks that patched version and ensures it does not replace the API's
System.Text.Json formatter. Retired ServiceBus/authentication dependencies remain
forbidden. Package auditing remains mandatory.

## Local validation - 2026-11-19

- Release solution and translation-seed tool builds: zero warnings/errors.
- Full suite with real isolated PostgreSQL, Redis, ACL accounts and Redis
  stop/restart tests: **1,686 passed, zero failed, zero skipped**.
- Ubuntu .NET 10 image/PDF/Pkcs/dependency/model/realtime-limit subset:
  **106 passed, zero failed, zero skipped**.
- NuGet all-dependency audit: zero known vulnerable packages in the solution.
- Production API and migrator Docker targets build and run on runtime `10.0.15`.
- The alternative `deploy/sentry-minio/Dockerfile` also builds successfully.
- API live/ready checks: 200; anonymous notifications/SignalR and invalid JWT:
  401; allowed CORS origin accepted and unknown origin rejected.
- Real HTTP registration/login, cookie-only refresh and rotation pass; replayed
  refresh is rejected (403), logout returns 204 and the previous session's
  access token is rejected (401). HttpOnly/Secure cookie policy is retained.
- Authenticated SignalR WebSocket JSON handshake passes with Redis backplane
  and separate cache/realtime ACL identities enabled.
- A database created by the unchanged .NET 9 code at `ce6202c` is opened by the
  .NET 10 Linux migrator: all 115 migration records, schema and seed data remain
  unchanged (full dump comparison). Fresh .NET 10 migration also produces the
  same schema and 115 records. No migration or snapshot is modified.

One loaded Windows run hit the existing translation matcher's 50 ms regex
timeout. Its 13 localization tests passed in isolation, and the subsequent
complete serial run passed all 1,686 tests without concurrent builds. No timeout,
input-size restriction or localization/business rule was relaxed to pass tests.
This pre-existing load sensitivity remains a separate performance follow-up.

No production traffic, actual SMTP/Expo delivery, hosted CI or live deployment
was exercised. No commit or push was performed. Existing appsettings and tracked
TestResults files were not changed; new results are stored outside the repository.

## Release procedure

1. Require successful CI and review; no validation result substitutes for PR review.
2. Back up PostgreSQL and retain the previous **matching API/migrator image tags**.
3. Retain the same secrets, Redis ACL configuration, private-release mount and
   data-protection key volume. Do not delete volumes or flush Redis as part of
   this upgrade. Keeping the JWT key is necessary to preserve issued tokens.
4. Build both Docker targets from the same reviewed commit. The server host does
   not need a .NET SDK when running the supplied Docker images.
5. Run the new migrator once before rolling the API. No new application schema
   migration is expected from this change.
6. Verify `/health/live`, `/health/ready`, login/refresh and existing sessions.
7. Verify one reservation-to-payment flow, waiter/kitchen notifications, image
   upload and one PDF/email flow. Real SMTP/Expo delivery must be checked in the
   intended environment; local tests do not send messages to customers.
8. Watch application errors, Redis reconnects and latency before wider rollout.

The validation uses owned, loopback-only PostgreSQL/Redis containers. It does
not stop production Redis, read a production database or modify the live server.

## Rollback

If health checks or smoke tests fail, stop the rollout and restore the previous
API/migrator image pair, without changing secrets or deleting key volumes.
No new schema migration is introduced here, so do not run a destructive database
downgrade. Restoring a database backup is a separate incident decision, not an
automatic rollback step. The previous .NET 9 runtime is no longer supported;
use it only as a short emergency rollback while addressing the failure.

## Primary references

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [EF Core 10 breaking changes](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/breaking-changes)
- [Npgsql EF 10 release notes](https://www.npgsql.org/efcore/release-notes/10.0.html)
- [.NET 10 Ubuntu container change](https://learn.microsoft.com/en-us/dotnet/core/compatibility/containers/10.0/default-images-use-ubuntu)
