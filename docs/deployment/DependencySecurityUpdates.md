# Mapping And Multipart Dependency Security

## Scope

AutoMapper is pinned to 16.3.0 in Application and Shared. This addresses
GHSA-gpwp-5797-mjff and GHSA-6vhm-j9h7-f9gh without changing mapping profiles,
registration roles, password hashing, DTOs or application services.

The API directly pins Microsoft.AspNetCore.WebUtilities to 2.3.14, overriding
the older transitive 2.3.0 dependency. This addresses GHSA-6527-9fp8-r4x6.
The application remains on its existing ASP.NET Core shared framework; this
package update is not a runtime upgrade or a replacement for upload limits.

No database migration, frontend change, secret or Redis configuration change
is required. Rebuild and redeploy the API and migrator images through the
normal deployment process. Do not only restart an existing image.

## Verification

- DependencySecurityRegressionTests checks customer registration normalization,
  password hashing, customer role and ignored file attachment fields.
- Bounded constructor/record mapping tests ensure MaxDepth actually limits
  recursion. They deliberately use small inputs, not a process-crashing payload.
- Multipart regression tests preserve text fields and binary file bytes,
  enforce a small test body limit and verify buffered temporary-file cleanup.
  These exercise the resolved framework implementation; they are not a memory
  exhaustion attack or proof that every upload endpoint is immune to DoS.
- MobileAuthPostgresTests verifies registration, login, refresh, hashed token
  persistence and duplicate email/phone rejection in an isolated local database.
- Run the full test suite for reservation, order, stock, billing, authorization
  and image-upload regressions. Real DB tests require the existing dedicated
  loopback PostgreSQL test connection settings.
- Run `dotnet list src/ECafe.Api/ECafe.Api.csproj package --vulnerable --include-transitive`.
  AutoMapper and WebUtilities must not appear in the vulnerable package list.
  This does not mean that all other dependencies are free of vulnerabilities.

## Unused Service Bus Removal

Microsoft.Azure.ServiceBus 5.2.0 was referenced only in the Application project.
A tracked-file search found no Service Bus clients, handlers, configuration,
deployment settings or dynamic assembly-loading paths using it. Notification
delivery remains on PostgreSQL outboxes, SignalR/Redis, Expo HTTP and SMTP.
No notification or business implementation was changed.

Removing this single reference reduces the API's resolved NuGet graph from
219 to 127 packages with no additions or unrelated version upgrades. Removed
dependencies include Azure AMQP, legacy Azure authentication, Newtonsoft.Json
10.0.3, System.Net.Http 4.3.0 and System.Text.RegularExpressions 4.3.0. The last
two are old standalone packages, not the HTTP/Regex implementations supplied
by the current .NET shared framework.

The dependency regression test checks ECafe.Api.deps.json, not the test runner
manifest: Microsoft.NET.Test.Sdk legitimately uses Newtonsoft.Json 13.0.1.
The Application, Infrastructure, API and migrator resolved package graphs no
longer contain the retired dependency chain. Rebuild deployment images to
remove previously copied binaries; restarting an old image is insufficient.

On 2026-11-17, NuGet audit no longer reports the Newtonsoft.Json, System.Net.Http
or System.Text.RegularExpressions advisories. System.Security.Cryptography.Pkcs
8.0.1 remains a separate known issue brought in by MailKit/MimeKit and PDFsharp;
it is deliberately not modified in this cleanup. No migration or environment
configuration change is required.

## PKCS Security Patch

On 2026-11-18, Application directly pins System.Security.Cryptography.Pkcs to
9.0.23, overriding the 8.0.1 dependency brought in by MailKit/MimeKit and
PDFsharp/MigraDoc. Both parent packages and all other resolved package versions
remain unchanged. This version matches the existing .NET 9 target and pinned
9.0.23 container runtime, and addresses GHSA-846f-f764-76mx
(SignedCms.CheckSignature CPU exhaustion) and GHSA-2fmv-rv2c-59x7
(Windows EnvelopedCms.Decode CPU exhaustion).

The application still sends ordinary SMTP messages and generates unsigned
contract PDFs. No CMS/S-MIME input endpoint, digital signature feature,
certificate store modification or relaxed TLS validation was added. Package
presence was confirmed, but the audit found no current application call to the
affected verification/decode paths; this is dependency hardening, not evidence
of an exploited application endpoint.

The API manifest regression test pins the patched package version. A second
test checks the informational version of the actually loaded PKCS assembly:
pinning an 8.x package alone can leave an older 9.x shared-framework assembly
in use, even after the NuGet warning disappears. No machine-wide SDK or runtime
settings were changed. Small local
CMS tests verify signature round-trips, changed-content rejection and malformed
input rejection, using only ephemeral test keys. MIME/outbox tests and existing
PDF generation/import tests cover mail content and document compatibility.
No real SMTP credentials are used and no external email is sent by these tests;
live SMTP delivery remains a post-deployment smoke check with a test account.
The tests do not run resource-exhaustion payloads.

Rebuild the API and migrator deployment images to include the patched DLL.
No database migration, business logic, SMTP setting, frontend change or runtime
major-version upgrade is required. NuGet audit on 2026-11-18 reports no known
vulnerable packages for the API with the configured sources; this is not a
claim that the whole application or infrastructure has no security risks.

## References

- https://github.com/LuckyPennySoftware/AutoMapper/security/advisories/GHSA-gpwp-5797-mjff
- https://github.com/LuckyPennySoftware/AutoMapper/security/advisories/GHSA-6vhm-j9h7-f9gh
- https://github.com/dotnet/aspnetcore/security/advisories/GHSA-6527-9fp8-r4x6
- https://github.com/dotnet/runtime/security/advisories/GHSA-846f-f764-76mx
- https://github.com/dotnet/runtime/security/advisories/GHSA-2fmv-rv2c-59x7
- https://www.nuget.org/packages/System.Security.Cryptography.Pkcs/9.0.23
