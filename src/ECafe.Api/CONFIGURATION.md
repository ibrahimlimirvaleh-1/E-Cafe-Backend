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
$env:MinIO__AccessKey="..."
$env:MinIO__SecretKey="..."
$env:Email__Password="..."
$env:Sentry__Dsn="..."
```

Rotate any credential that was previously committed to `appsettings*.json`.
