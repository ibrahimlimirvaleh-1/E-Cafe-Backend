# ECafe API configuration

Do not store real secrets in `appsettings.json` or `appsettings.Development.json`.

For local development, store secrets with .NET user secrets:

```powershell
cd src/ECafe.Api

dotnet user-secrets set "ConnectionStrings:ECafeDb" "Host=localhost;Port=5433;Database=ECafe;Username=ecafe_user;Password=YOUR_LOCAL_PASSWORD"
dotnet user-secrets set "Jwt:Key" "YOUR_LONG_RANDOM_LOCAL_JWT_KEY"
dotnet user-secrets set "MinIO:AccessKey" "YOUR_MINIO_ACCESS_KEY"
dotnet user-secrets set "MinIO:SecretKey" "YOUR_MINIO_SECRET_KEY"
dotnet user-secrets set "Email:Username" "YOUR_SMTP_USERNAME"
dotnet user-secrets set "Email:Password" "YOUR_SMTP_APP_PASSWORD"
dotnet user-secrets set "Email:From" "YOUR_FROM_EMAIL"
dotnet user-secrets set "Sentry:Dsn" "YOUR_SENTRY_DSN"
```

For production, set environment variables instead. ASP.NET Core maps double underscores to nested configuration keys:

```powershell
$env:ConnectionStrings__ECafeDb="Host=...;Port=...;Database=...;Username=...;Password=..."
$env:Jwt__Key="..."
$env:Jwt__AccessTokenLifetimeMinutes="10"
$env:MinIO__AccessKey="..."
$env:MinIO__SecretKey="..."
$env:Email__Password="..."
$env:Sentry__Dsn="..."
$env:Cors__AllowedOrigins__0="https://admin.ecafe.example"
$env:Cors__AllowedOrigins__1="https://ecafe.example"
$env:AllowedHosts="api.ecafe.example"
$env:ForwardedHeaders__KnownProxies__0="10.0.0.10"
$env:SignalR__UseRedisBackplane="true"
$env:SignalR__Redis__Connection="prod-redis:6379,password=...,ssl=True,abortConnect=False"
```

Use `appsettings.Production.example.json` as a checklist only. Do not copy real
secrets into source-controlled JSON files.

Frontend production image can use the stricter nginx configuration without
affecting local Docker usage:

```powershell
docker build --build-arg NGINX_CONF=nginx.prod.conf -t ecafe-frontend:prod .
```

Before production deploy, replace example domains such as `ecafe.example` with
the real API, admin, and public site domains.

Rotate any credential that was previously committed to `appsettings*.json`.
