# ECafe API Rollback Plan

Use this plan when a production deployment causes errors after release.

## Before Deploy

Always keep:

- The previous API image tag.
- The previous migrator image tag.
- A database backup taken immediately before migration.
- A short smoke test checklist.

## Safe Rollback Cases

If the deployment changed only application code or backward-compatible database columns, roll back the API image:

```powershell
kubectl rollout undo deployment/ecafe-api -n ecafe
kubectl rollout status deployment/ecafe-api -n ecafe
```

For Docker Compose:

```powershell
docker compose -f deploy/api/docker-compose.production.yml up -d api
```

Make sure `ECAFE_API_IMAGE` points to the previous image tag.

## Database Rollback

Prefer backward-compatible migrations:

- Add nullable columns first.
- Deploy code that can work with both old and new values.
- Backfill data separately if needed.
- Remove old columns only in a later release.

If a migration is not backward compatible and production breaks:

1. Stop new writes if data corruption risk exists.
2. Restore the pre-deploy database backup.
3. Roll back the API image to the previous tag.
4. Run smoke tests.

Do not blindly run `dotnet ef database update PreviousMigration` in production unless the migration was explicitly designed and tested as reversible.

## Smoke Test After Rollback

Check:

- `GET /health/live`
- `GET /health/ready`
- Login and refresh token flow.
- Admin dashboard loads.
- One protected endpoint returns data.
- File view/download works for a private file.
- SignalR connection opens without negotiate errors.

## Monitoring

After rollback, watch:

- Sentry error rate.
- 401/403/500 response spikes.
- Database CPU and connection count.
- Redis connectivity when SignalR backplane is enabled.
- Outbox worker retry/failure counts.
