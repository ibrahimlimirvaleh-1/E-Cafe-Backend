# ECafe API Deployment

This document describes the production deployment shape for ECafe API.

## Artifacts

Build two images from the same `Dockerfile`:

```powershell
docker build --target api -t ghcr.io/your-org/ecafe-api:CHANGE_ME .
docker build --target migrator -t ghcr.io/your-org/ecafe-api-migrator:CHANGE_ME .
```

Use the same tag for API and migrator images so the migration code matches the API code being deployed.

## Configuration

Do not commit production secrets. Inject them with environment variables, Kubernetes Secrets, Docker secrets, or your hosting provider's secret manager.

Required runtime values:

- `ConnectionStrings__ECafeDb`
- `Jwt__Key`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Cors__AllowedOrigins__0`
- `AllowedHosts`
- `Redis__Connection`
- `SignalR__Redis__Connection` when more than one API replica is used
- `MinIO__Endpoint`, `MinIO__AccessKey`, `MinIO__SecretKey`
- `Email__Username`, `Email__Password`
- `Sentry__Dsn`

Reference files:

- `deploy/api/api.env.example`
- `src/ECafe.Api/appsettings.Production.example.json`
- `deploy/k8s/api/secret.example.yaml`

## Health Checks

The API exposes:

- `GET /health/live` - process is running.
- `GET /health/ready` - process is ready to receive traffic and can connect to the database.

Load balancers and orchestrators should route traffic only to pods/containers where `/health/ready` is healthy.

## Docker Compose

Copy the example env file and fill real values outside Git:

```powershell
Copy-Item deploy/api/api.env.example deploy/api/api.env
```

Run migration once:

```powershell
docker compose -f deploy/api/docker-compose.production.yml --profile migrate run --rm migrator
```

Start API:

```powershell
docker compose -f deploy/api/docker-compose.production.yml up -d api
```

Smoke test:

```powershell
curl -fsS http://localhost:8080/health/live
curl -fsS http://localhost:8080/health/ready
```

## Kubernetes

Apply base resources:

```powershell
kubectl apply -f deploy/k8s/api/namespace.yaml
kubectl apply -f deploy/k8s/api/configmap.yaml
kubectl apply -f deploy/k8s/api/secret.yaml
```

Run migration job before rolling the new API version:

```powershell
kubectl delete job ecafe-api-migration -n ecafe --ignore-not-found
kubectl apply -f deploy/k8s/api/migration-job.yaml
kubectl wait --for=condition=complete job/ecafe-api-migration -n ecafe --timeout=120s
```

Deploy API:

```powershell
kubectl apply -f deploy/k8s/api/deployment.yaml
kubectl apply -f deploy/k8s/api/service.yaml
kubectl apply -f deploy/k8s/api/ingress.yaml
kubectl rollout status deployment/ecafe-api -n ecafe
```

Smoke test:

```powershell
kubectl get pods -n ecafe
curl -fsS https://api.ecafe.example/health/live
curl -fsS https://api.ecafe.example/health/ready
```

## Deployment Order

1. Take a database backup.
2. Build and push API and migrator images with the same immutable tag.
3. Apply config and secrets.
4. Run migration job.
5. Deploy API.
6. Run smoke tests.
7. Monitor Sentry/logs, API health, DB load, and SignalR connection errors.

## Notes

Do not run database migrations automatically from every API replica. Use the migration job so schema changes happen once and are visible in deployment logs.
