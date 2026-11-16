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

## References

- https://github.com/LuckyPennySoftware/AutoMapper/security/advisories/GHSA-gpwp-5797-mjff
- https://github.com/LuckyPennySoftware/AutoMapper/security/advisories/GHSA-6vhm-j9h7-f9gh
- https://github.com/dotnet/aspnetcore/security/advisories/GHSA-6527-9fp8-r4x6
