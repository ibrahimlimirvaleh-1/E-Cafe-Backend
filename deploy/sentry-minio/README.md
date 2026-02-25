# Sentry + MinIO + ECafe API (Docker Compose)

Bu qovluq lokal/dev mühitdə **Sentry**, **MinIO** və **ECafe API** servislərini birlikdə qaldırmaq üçündür.

> Qeyd: `redis` servisi yalnız **Sentry** üçündür. ECafe API bu compose daxilində Redis istifadə etmir.

## Docker Compose faylını harada yaratmalı?

Bu repo daxilində burada saxlanılır:

- `deploy/sentry-minio/docker-compose.yml`

Komandaları da bu qovluqda işlə:

```bash
cd deploy/sentry-minio
```

## Tez başlat (app + minio + sentry)

```bash
./start-stack.sh
```

Bu script ardıcıllıq məcburiyyəti olmadan bütün servisləri bir komanda ilə qaldırır.

## 1) Hazırlıq

```bash
cp .env.example .env
```

`.env` faylında ən azı bunları dəyişin:

- `SENTRY_SECRET_KEY` (mütləq uzun random dəyər)
- `POSTGRES_PASSWORD`
- `ECAFE_DB_PASSWORD`
- `MINIO_ROOT_PASSWORD`

Secret key yaratmaq üçün:

```bash
openssl rand -hex 32
```


Əgər image pull zamanı `not found` xətası alsanız, `.env` içində tag-ları yoxlayın:

- `MINIO_IMAGE_TAG=latest`
- `SENTRY_IMAGE_TAG=latest`


Port konflikti (məs. `9001 already in use`) olarsa, `.env` içində host portları dəyişin:

- `MINIO_CONSOLE_PORT=9012`
- `MINIO_API_PORT=9005`
- `SENTRY_PORT=9003`
- `ECAFE_API_PORT=8081`

Sonra yenidən:

```bash
docker compose down
docker compose up -d --build
```

## 2) Bütün servisləri qaldır (app daxil)

```bash
docker compose up -d --build
```

Bu komanda aşağıdakı servisləri qaldırır:

- `ecafe-api`
- `ecafe-db`
- `sentry-web`, `sentry-worker`, `sentry-cron`, `sentry-init`
- `postgres` (sentry üçün)
- `redis` (yalnız sentry queue/cache üçün)
- `minio`


Əgər yalnız app + db + minio qaldırmaq istəyirsənsə (Sentry-siz), bu komandadan istifadə et:

```bash
docker compose up -d --build ecafe-db ecafe-api minio
```

## 3) Sentry üçün ilk admin user yarat

`upgrade --noinput` migration-ları edir, amma user yaratmır:

```bash
docker compose exec sentry-web sentry createuser \
  --superuser \
  --email admin@example.com \
  --password StrongAdminPass123
```

## 4) URL-lər

- ECafe API: http://localhost:${ECAFE_API_PORT} (default 8080)
- Sentry: http://localhost:${SENTRY_PORT} (default 9002)
- MinIO API: http://localhost:${MINIO_API_PORT} (default 9000)
- MinIO Console: http://localhost:${MINIO_CONSOLE_PORT} (default 9011)

## 5) ECafe API-ni Sentry-yə bağlamaq (optional)

1. Sentry UI-dan bir project yaradın.
2. Project DSN-ni götürüb `.env` faylında `ECAFE_SENTRY_DSN` dəyərinə yazın.
3. Servisi restart edin:

```bash
docker compose up -d ecafe-api
```

## Faydalı komandalar

Loglar:

```bash
docker compose logs -f ecafe-api sentry-web sentry-worker sentry-cron minio
```

Servisləri söndürmək:

```bash
docker compose down
```

Volume-larla birlikdə silmək:

```bash
docker compose down -v
```