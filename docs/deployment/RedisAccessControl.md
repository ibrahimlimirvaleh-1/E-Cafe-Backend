# Redis Access Control

## Accounts And Boundaries

The API already supports named Redis users in its connection strings. No business
service, database migration, authentication token format or notification recipient
policy changes are needed for this deployment hardening.

| Account | Purpose | Allowed scope |
| --- | --- | --- |
| `ecafe_cache` | Distributed permission/session/restaurant-access cache | Existing cache prefixes; hash reads/writes, expiry and deletion |
| `ecafe_realtime` | SignalR backplane and shared connection leases | `ecafe:signalr*` channels and `ecafe:realtime:connections:*` keys |
| `ecafe_viewer` | RedisInsight inspection through an SSH tunnel | Explicit read/metadata commands; no write, delete, expiry update, script or publication |
| `ecafe_admin` | Controlled maintenance | Full administration; never configured in the API or RedisInsight |
| `default` | Legacy unauthenticated access | Disabled and reset |

`deploy/redis/users.acl.template` is the source of the command allowlists. Redis
checks both the command and its key/channel permissions. Cache and realtime
accounts cannot modify each other's keys. The backplane needs Lua plus the
commands used inside its lease scripts, so removing every scripting command would
break the shared connection-limit protection. Do not replace these allowlists
with `+@all` to fix a missing-command error.

The distributed-cache library uses `HMSET` and may map key deletion to `UNLINK`;
both are allowed only within cache prefixes. StackExchange.Redis can make optional
discovery requests such as `CONFIG GET`, `CLUSTER NODES` and a tie-breaker `GET`.
Those requests remain denied for application accounts; they are not permission to
add configuration or arbitrary-key access. Test the actual installed client before
altering ACLs. If a cache namespace changes (for example a new permission-cache
version), update the ACL and its tests in the same deployment.

RedisInsight may still display edit/delete controls. The server rejects the
operation regardless of the UI. Profiler, Pub/Sub publishing, Redis configuration,
module installation and writable search-index creation are intentionally not
available to the viewer. Read access still exposes cache contents to a trusted
operator; do not distribute this credential to customers.

## Private Files And Persistence

Generate fresh random credentials on the target host, not in source control:

```sh
python3 deploy/redis/render_acl.py --output-directory /private/new-redis-bundle
```

The parent directory must already exist. The utility refuses to overwrite an
existing bundle, generates a distinct 256-bit random password for each account,
and prints no password. The directory is `0700`; `credentials.json` and `users.acl`
are `0600`. Passwords in the ACL file are SHA-256 hashes. The credential file is
still plaintext and must be protected as a secret. Do not use a generated bundle
as a blind password-rotation script for a running system.

Copy only `users.acl` into a separate ACL directory for the Redis container. The
directory must be traversable and the file readable by the Redis process, but
neither may be world-readable. In the existing Hetzner installation the host
deployment group and Redis container primary group are both GID 1000: directory
`0750`, ACL file `0640`, owned by the deployment user/group. Verify the actual
UID/GID in another environment; never fix a permission error with `0777` or `0666`.
Mount that ACL directory read-only. Never mount `credentials.json` into Redis.

The persistent Compose configuration must start Redis with `--aclfile` and
`--protected-mode yes`, preserve its original `/data` volume and publish no Redis
port. See `deploy/redis/docker-compose.acl.example.yml` for the service fragment.
Runtime `ACL SETUSER` alone is not persistent. A restart must load the same tested
ACL file; `ACL SAVE` cannot write to this deliberately read-only mount.

Use different secret values for the two existing API environment settings:

```dotenv
Redis__Connection=redis:6379,user=ecafe_cache,password=FROM_SECRET_STORE,abortConnect=false
SignalR__UseRedisBackplane=true
SignalR__Redis__Connection=redis:6379,user=ecafe_realtime,password=FROM_SECRET_STORE,abortConnect=false
```

Do not commit the real environment file, print resolved `docker compose config`,
or dump Docker environment values to a terminal/log. Restrict the environment
file and any private backups to `0600`. Compose `environment` values override
`env_file` values: ensure no old connection literal silently takes precedence.

These examples use the existing single-host private Docker network, where Redis
itself does not provide TLS. The production example settings retain `ssl=True`
for a TLS-enabled external Redis deployment. Do not enable that flag against a
non-TLS listener. Use TLS for Redis traffic crossing untrusted hosts/networks.

## Staged Cutover

1. Back up the server Compose/environment files privately. Record the API image,
   data-protection keyring mount and Redis data-volume identity.
2. Create and test the named users on the current Redis without disabling the
   account still used by the running API. Prepare the final persistent ACL file.
3. Update the API secret connection strings and recreate only the API with its
   existing image (`--no-deps --no-build`). Confirm both health endpoints and the
   new cache/backplane connections. Do not rotate JWT keys or refresh tokens.
4. Recreate only Redis with the final ACL mount and original data volume. The
   `default` account is now disabled persistently. Existing Redis sockets close;
   brief backend reconnects are expected, not a promise of zero downtime.
5. Confirm anonymous/wrong-password authentication is denied; verify viewer reads
   and refused writes, cache read/write/expiry/invalidation, lease Lua and isolated
   Pub/Sub. Use unique diagnostic keys/channels, not real order/user data.
6. Verify healthy API endpoints, authenticated application client counts, no
   continuing session-cache/authentication errors and unchanged data/keyring
   mounts. Old RedisInsight connections using `default` must be updated.

Keep an emergency rollback private. Restoring old passwordless configuration
reopens the original risk; it is not a normal recovery operation. Never run
`docker compose down -v`, flush Redis or delete the original data volume as part
of this change. No real user token should be copied or forged for a smoke test.

## RedisInsight

Use the desktop app through a local-only OpenSSH tunnel:

```powershell
& "$env:WINDIR\System32\OpenSSH\ssh.exe" `
  -i "$env:USERPROFILE\.ssh\ecafe_github_actions" `
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes `
  -o ExitOnForwardFailure=yes -N -T `
  -L 127.0.0.1:16379:REDIS_CONTAINER_IP:6379 `
  deploy@SERVER_IP
```

Read the current container IP from Docker metadata after recreation; do not assume
it remains unchanged. Redis is not bound to a host loopback port in this setup, so
forwarding to the server's `127.0.0.1:6379` would not reach it. Bind the local end
only to `127.0.0.1`, never `0.0.0.0`, and do not publish `6379` in Compose.

In RedisInsight use host `127.0.0.1`, port `16379`, username `ecafe_viewer`, its
separate password and TLS off for this particular non-TLS Redis listener. The SSH
tunnel encrypts the desktop-to-server leg. Close the tunnel with Ctrl+C when done.
Keep the viewer password in a local user-only file or password manager; never use
the application/admin credentials in the desktop GUI.

## Verification

### Permission Cache Outages

`PermissionCacheService` treats Redis as an optional read cache, not as the source
of role permissions. A cache read failure causes the existing filtered
`RolePermissions` database query to run. That request does not attempt another
Redis write after the failed read, avoiding a second cache timeout. If the read
worked but a subsequent write fails, the verified database result is still used.
Invalid/null JSON cache payloads are rebuilt from the database. Valid empty lists
remain empty; no default or administrator permission is invented.

Only cache calls are inside the recovery boundary. Database query errors,
cancellation, programming/configuration errors and memory exhaustion are not
converted into permission success. Warnings include the role ID and exception
type, not raw exception messages, connection strings or cached payloads. The
existing `role_permissions_v3_*` keys, 30-minute lifetime, soft-delete query filters,
restaurant-role resolution and session middleware are unchanged. No schema,
Redis ACL, package or frontend change is needed.

Explicit cache invalidation and the role-permission save interceptor retain their
existing strict error behavior. This patch does not make permission mutations
outage-tolerant or silently report a failed invalidation as success. In particular,
a post-save invalidation error is not a promise that the database write rolled
back: do not blindly retry a role mutation or assume a stale retained cache has
been invalidated. Durable cross-instance revocation/revision handling is separate
from this read fallback. Direct database permission edits remain outside the
cache-invalidation contract.

Recovery needs no application restart: a later successful cache miss writes the
current database result back to Redis. An outage can still add the client's read
timeout and database load. SignalR backplane availability and fail-closed connection
limits are separate; this fallback does not claim Redis-independent realtime
delivery or full-site availability.

For a real stop/start test, set `ECAFE_TEST_PERMISSION_REDIS_LIFECYCLE=1` and, if
needed, `ECAFE_TEST_DOCKER` to the local Docker executable. The test itself creates
a uniquely named, labelled Redis container using the checked-in ACL template and
a fixed-for-its-lifetime loopback-only port. It accepts no existing Redis endpoint
or production credentials. It verifies permission fallback, active sessions,
deactivated/revoked/version-mismatched session denial, cache rebuilding and normal
permission revocation after recovery, then removes only its own container/files.
The lifecycle test is skipped unless explicitly enabled; `redis:7-alpine` must
already be available locally (the test does not pull images).

```powershell
$env:ECAFE_TEST_PERMISSION_REDIS_LIFECYCLE="1"
$env:ECAFE_TEST_DOCKER="C:/Program Files/Docker/Docker/resources/bin/docker.exe"
dotnet test tests/ECafe.Tests/ECafe.Tests.csproj --filter "FullyQualifiedName~PermissionCache" --results-directory "$env:TEMP/ecafe-permission-tests"
```

### ACL And Realtime Tests

Run the renderer tests with Python's standard library:

```sh
python3 -m unittest discover -s deploy/redis -p 'test_*.py'
```

Start a disposable local Redis 7.4 server with a rendered ACL and bind its port only
to loopback. Supply its four connection strings through `ECAFE_TEST_REDIS_ACL_CACHE`,
`ECAFE_TEST_REDIS_ACL_REALTIME`, `ECAFE_TEST_REDIS_ACL_VIEWER` and
`ECAFE_TEST_REDIS_ACL_ADMIN`. The ACL tests reject non-loopback endpoints. Set
`ECAFE_TEST_REDIS` to the same restricted realtime connection for the existing
two-API, concurrency, disconnect, expiry and long-polling tests. Never set any of
these test variables to production. Test outputs belong outside existing
`tests/ECafe.Tests/TestResults/` artifacts.

Coverage includes rejected anonymous/default/wrong-permission access, read-only
inspection, cross-namespace denial, real distributed session cache hits without
database fallback, revocation invalidation, sliding expiry, socket reauthentication
and SignalR delivery through the authenticated backplane. Destructive permissions
are checked with admin `ACL DRYRUN`, not by executing `FLUSHALL`.

Redis Insight's official implementation falls back to `COMMAND INFO`/`HELLO` when
module discovery is denied, and to `INFO keyspace` when `CONFIG GET databases` is
denied. Do not grant configuration writes for database browsing.

References: [Redis ACL](https://redis.io/docs/latest/operate/oss_and_stack/management/security/acl/),
[RedisInsight discovery implementation](https://github.com/RedisInsight/RedisInsight/blob/main/redisinsight/api/src/modules/database/providers/database-info.provider.ts).
