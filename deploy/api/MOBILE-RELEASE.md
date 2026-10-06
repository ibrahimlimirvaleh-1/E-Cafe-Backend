# Private Android release

The Android APK supports customer and restaurant-staff sign-in. An active
Customer account can request private release metadata and download the APK
through `/api/v1/mobile/customer/release` and
`/api/v1/mobile/customer/download`; no restaurant grant is required. Staff
downloads remain gated by their restaurant assignment, active restaurant and
contract, and the admin-controlled `ShowMobileDownloadLink` flag. Global/public
APK publication remains disabled.
Only the platform admin changes each restaurant's `MobilePushEnabled` and
`ShowMobileDownloadLink` settings in the admin panel. Deployment must not
change those tenant settings.

## Gates

- Keep the APK outside `wwwroot`, public buckets and reverse-proxy static paths.
- Mount the same private APK read-only into every API replica. The non-root API
  user must be able to traverse the directory and read the file.
- Set `MobileApp__Release__Ready=true` only with an independently verified,
  signed APK, absolute container `ApkPath`, `Version`, positive `VersionCode`,
  exact `SizeBytes` and SHA-256. The API checks size and hash before returning
  customer or staff release metadata or bytes. A missing or mismatched APK stays
  unavailable. Each download rechecks current account access; a stale JWT role
  does not override the active role in the database.
- `MobileApp__PushDeliveryReady` is a separate technical test switch. It must
  be true before phone token registration; it does not prove phone delivery.
  Set it only after the matching APK, Expo project and FCM V1 assignment are
  verified and the pending push queues are checked. Read-only EAS project and
  owner-account checks on 2026-10-05 found Expo push security disabled, so an
  unset `MobileApp__ExpoAccessToken` is not a current blocker. Recheck before
  rollout; if security is enabled later, supply the token through a secret
  channel.
- Enabling technical readiness does not grant restaurant access. Push delivery
  still requires admin-enabled restaurant flags, an active contract, an
  eligible recipient and a newly created notification. There is no old inbox
  replay or SMS fallback.

## Test-server handoff for build4

Do not run this procedure while the build receipt is queued or failed. First
verify receipt status `FINISHED_VERIFIED_INTERNAL_TEST`, build ID
`8c3fc014-c7e2-439e-a385-f35bdae625fb`, package
`com.ecafeadmins.ecafemobile`, version `0.1.0`, version code `4`, certificate
SHA-256 `969b540437d08c7193ee7063e5a804d220c74f26b107aa060840dc0709d15f94`,
and all bundle checks. Independently hash the local APK and Desktop copy
against the receipt, then verify the copied server file. Physical-device and
provider booleans remain false until tested.

On the test server, confirm the current API image, health, existing backup,
Expo project ID `db71d0d5-bde7-47f0-8e84-95475e07588c`, and zero unprocessed
`MobilePushRequested` outbox events and zero pending/ticket-pending push
deliveries. Stop and investigate nonzero queues before enabling readiness.
Do not run migrations for this configuration-only release.

Upload the APK to a private staging path, then install it under
`/opt/ecafe/private-releases` with directory mode `0700` and file mode `0600`.
The deployment user owns both; grant API UID 1654 directory `rx` and file `r`
using ACLs. Check ancestor traversal and verify the hash as UID 1654. Do not
use a public URL or world-readable file.

Create a restricted Compose override outside the repository, alongside the
existing test deployment backup. Replace placeholders using the verified
receipt, not the Expo dashboard URL:

```yaml
services:
  api:
    volumes:
      - type: bind
        source: /opt/ecafe/private-releases
        target: /var/lib/ecafe/private-releases
        read_only: true
    environment:
      MobileApp__Release__Ready: "true"
      MobileApp__Release__ApkPath: /var/lib/ecafe/private-releases/ECafe-0.1.0-build4-VERIFIED_SHA_PREFIX.apk
      MobileApp__Release__Version: "0.1.0"
      MobileApp__Release__VersionCode: "4"
      MobileApp__Release__SizeBytes: "VERIFIED_SIZE"
      MobileApp__Release__Sha256: "VERIFIED_SHA256"
      MobileApp__PushDeliveryReady: "true"
      MobileApp__ExpoProjectId: "db71d0d5-bde7-47f0-8e84-95475e07588c"
```

Before `up`, inspect `docker compose -f docker-compose.yml -f OVERRIDE config`
to confirm the existing data-protection mount is preserved, the new APK bind
is read-only, and all release fields match the receipt. Then recreate only
`api` with `docker compose -f docker-compose.yml -f OVERRIDE up -d --no-build api`.
Check `/health/ready`, container file size/hash, and the authenticated admin
publication response (`releaseReady=true`, `publicDownloadEnabled=false`).
Do not toggle restaurant flags in SQL or through the admin API; the human admin
will use the panel. APK metadata readiness and successful token registration
do not prove that a notification arrived on a physical phone.

For rollback, run `docker compose -f docker-compose.yml up -d --no-build
--force-recreate api` from `/opt/ecafe` and recheck `/health/ready`. Omitting
the override restores the baked `Release:Ready=false` and
`PushDeliveryReady=false`; keep the backup and private artifact for diagnosis.

Revoking server-side release access blocks future metadata/download requests,
but cannot remove an APK already installed. The native app must also check
access on login, restore, restaurant switch and resume, and fail closed.

## Build5 replacement plan (not deployed by this code change)

After separate deployment approval, verify the signed local build5 receipt and
APK before uploading: package `com.ecafeadmins.ecafemobile`, version `0.1.0`,
version code `5`, size `72196663`, SHA-256
`4d79477bb71d96fcbbebeec65472c1e79506998ca28940dc4704a31d8490b683`,
and signing certificate SHA-256
`969b540437d08c7193ee7063e5a804d220c74f26b107aa060840dc0709d15f94`.
Stage it as a separate private artifact, retaining build4 for rollback. Replace
only the APK path, version code, size and hash in a restricted Compose override;
preserve existing data-protection mounts, Expo/push settings and tenant flags.
Validate the merged Compose configuration, API health, container-side hash and
authenticated customer/staff metadata and download gates. A failed validation
must restore the previous build4 override. A code push does not make build5
available on the server or establish physical push delivery.
