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

## References

- https://github.com/LuckyPennySoftware/AutoMapper/security/advisories/GHSA-gpwp-5797-mjff
- https://github.com/LuckyPennySoftware/AutoMapper/security/advisories/GHSA-6vhm-j9h7-f9gh
- https://github.com/dotnet/aspnetcore/security/advisories/GHSA-6527-9fp8-r4x6
