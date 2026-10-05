# Private staff APK release

The ECafe Android APK is a restaurant-staff product, not a public website download.
Platform SuperAdmin enables it per restaurant through `PUT /api/v1/admin/restaurants/{restaurantId}/mobile-module`
with `showDownloadLink: true`. Active Owner, Manager, Waiter and Kitchen assignments
can then call the authenticated `access`, `release` and `download` endpoints.

Keep the APK outside `wwwroot`, public object buckets and reverse-proxy static paths.
Set `ECAFE_APK_RELEASE_DIR` to a host directory mounted read-only into the API container
at `/var/lib/ecafe/private-releases`; the default is `./private-releases` next to
the compose file. Ensure the container's non-root user can read the file. Every API
replica must have the same bytes. The old public release endpoints do not serve APKs.

For a verified release, set `MobileApp__Release__ApkPath` to the absolute container path
and configure `Version`, positive `VersionCode`, exact `SizeBytes` and SHA-256 hex digest.
Only then set `MobileApp__Release__Ready=true`. The API checks file size and digest
before returning metadata or bytes; absent or mismatched files remain unavailable.
Keep `Ready=false` until a signed production APK has been tested on a real device.
`MobileApp__PushDeliveryReady` remains independent and false until FCM/Expo delivery
credentials and physical-device tests are complete.

Changing this server-side switch blocks future metadata and download requests. An
already downloaded APK cannot be remotely removed, and the shared website APIs are
not mobile-client-specific. The native app must also check `access` on login,
restore, restaurant switch and resume, and fail closed on errors.
