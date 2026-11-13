# Realtime Connection Security

## Scope

SignalR Redis is patched to 9.0.23 and MessagePack is pinned to 2.5.307 without
changing its major version. Browser clients continue using the existing JSON
protocol. MessagePack is used internally by the Redis backplane; it is not enabled
as a client hub protocol. API and migrator runtime images use ASP.NET Core 9.0.23.
Rebuild the Docker images to apply the runtime patch; deploying DLLs into an old
container does not update its runtime.

Order states, recipients, durable outbox processing, notification inbox entries,
and mobile push delivery are unchanged. No database migration is required.

## Limits

The default is five concurrent logical hub connections per account, across its
tabs and devices. A reconnect is a new connection. Existing connections are not
evicted to admit a sixth one. Long-polling HTTP requests share the same logical
connection and do not each consume a slot. Admission happens in the hub lifetime
filter, after authentication and the SignalR handshake. Excess connections are
closed by SignalR; this is not an HTTP 429 admission endpoint.

Configuration uses:

- `SignalR__ConnectionLimits__MaxConnectionsPerUser`: 5, allowed range 1-50.
- `SignalR__ConnectionLimits__LeaseSeconds`: 90, allowed range 30-300.
- `SignalR__ConnectionLimits__RenewalSeconds`: 20, at least 5 and at most one third
  of the lease duration.

These are concurrency limits, not connection-attempt rate limits or a complete
denial-of-service solution. Reverse-proxy request/connection limits remain useful
defense in depth.

## Redis And Recovery

With `SignalR__UseRedisBackplane=true`, reservations are atomic Lua operations in
the same Redis database as the backplane. All replicas share a per-account sorted
set under `ecafe:realtime:connections:{userId}`. Redis server time controls expiry.
The Redis ACL must permit EVAL, TIME, ZREMRANGEBYSCORE, ZSCORE, ZCARD, ZADD, PEXPIRE,
ZREM and DEL for that namespace, in addition to existing backplane permissions.
Keep this Redis instance private, authenticated and separated between environments.

Each reservation has a server-generated identifier. Disconnect releases only that
identifier; a delayed old disconnect cannot release a new connection's slot.
Heartbeat renewal keeps live reservations counted. A crashed process leaves
reservations that expire automatically. During Redis outages, new admission fails
closed instead of silently falling back to per-process limits. Existing leases
tolerate short renewal failures; sustained failure closes their connection before
the locally confirmed lease window ends. A timed-out command may have reserved a
slot; the TTL bounds this stale reservation until Redis becomes available again.

When the backplane is disabled, the same policy is enforced by a singleton in
memory. That mode is for a single API instance only. Multi-replica deployments must
enable the Redis backplane and use identical limit settings on every replica.

## Verification

Existing realtime tests cover recipient isolation, authentication and reconnect.
Additional tests cover parallel admission, idempotent cleanup, orphan expiry,
heartbeat failure/recovery, JSON-only protocol, long-polling and two real API
instances sending notifications through the patched Redis/MessagePack backplane.

Set `ECAFE_TEST_REDIS` to an isolated test Redis server to run the real Redis tests.
Never point it at production: the tests create temporary lease keys and exercise
expiry. Results should be written outside pre-existing TestResults directories.

After deployment, test one normal tab, multiple tabs below the configured limit,
one excess tab, closing a tab and reconnecting, and restaurant recipient isolation.
An excess tab may show only disconnected/reconnecting state in the current client.
Changing that presentation requires a separate frontend change.
