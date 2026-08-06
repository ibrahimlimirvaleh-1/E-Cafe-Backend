# Local configuration

ECafe API localda iki yolla işlədilə bilər:

## IDE ilə debug

IDE-də `ECafe.Api - Local` profilini seçin. Bu profil:

- `ASPNETCORE_ENVIRONMENT=Local` istifadə edir.
- `appsettings.json` oxuyur.
- `appsettings.Local.json` varsa onu oxuyur.
- `dotnet user-secrets` dəyərlərini oxuyur.

Real secret-ləri Git-ə yazmayın. Tövsiyə olunan yol:

```powershell
cd src\ECafe.Api

dotnet user-secrets set "ConnectionStrings:ECafeDb" "Host=localhost;Port=5433;Database=ECafe;Username=ecafe_user;Password=YOUR_LOCAL_PASSWORD"
dotnet user-secrets set "Jwt:Key" "YOUR_LONG_RANDOM_LOCAL_JWT_KEY"
dotnet user-secrets set "Jwt:Issuer" "ecafe"
dotnet user-secrets set "Jwt:Audience" "ecafe-users"
dotnet user-secrets set "MinIO:Endpoint" "localhost:9000"
dotnet user-secrets set "MinIO:AccessKey" "minioadmin"
dotnet user-secrets set "MinIO:SecretKey" "YOUR_LOCAL_MINIO_PASSWORD"
dotnet user-secrets set "MinIO:BucketName" "ecafefiles"
dotnet user-secrets set "MinIO:UseSSL" "false"
dotnet user-secrets set "Redis:Connection" "localhost:6379"
```

Email lazımdırsa əlavə edin:

```powershell
dotnet user-secrets set "Email:SmtpHost" "smtp.gmail.com"
dotnet user-secrets set "Email:SmtpPort" "587"
dotnet user-secrets set "Email:Username" "YOUR_SMTP_USERNAME"
dotnet user-secrets set "Email:Password" "YOUR_SMTP_APP_PASSWORD"
dotnet user-secrets set "Email:From" "YOUR_FROM_EMAIL"
dotnet user-secrets set "Email:FromName" "E-Cafe Admin"
```

Alternativ olaraq `src/ECafe.Api/appsettings.Local.example.json` faylını `appsettings.Local.json` adı ilə kopyalayıb lokal dəyərləri ora yaza bilərsiniz. `appsettings.Local.json` Git tərəfindən ignore olunur.

## Docker ilə local stack

```bash
cd deploy/sentry-minio
cp .env.example .env.local
```

`.env.local` içində lokal DB, MinIO, JWT və email dəyərlərini doldurun. Sonra:

```bash
./start-stack.sh
```

Manual compose komandası:

```bash
docker compose --env-file .env.local up -d --build
```

`.env.local` Git tərəfindən ignore olunur. Repo-da qalan `.env` və `.env.example` yalnız placeholder dəyərlər saxlamalıdır.
