# ECafe local Docker stack

Bu qovluq lokal development üçün ECafe API, frontend, PostgreSQL, Redis və MinIO servislərini qaldırır.

## Env faylları

- `.env.example` repo-da qalır və nümunə dəyərlər üçündür.
- `.env` lokal işlətmə üçündür və Git-ə düşməməlidir.
- `.env.local` lokal işlətmə üçündür və Git-ə düşməməlidir.

Əgər köhnə flow ilə işləyirsinizsə, `.env` faylını saxlayıb adi compose komandası işlədə bilərsiniz:

```bash
docker compose up -d --build
```

Daha təhlükəsiz flow üçün `.env.local` istifadə edin:

```bash
cp .env.example .env.local
docker compose --env-file .env.local up -d --build
```

`start-stack.sh` avtomatik olaraq əvvəl `.env.local`, sonra `.env` axtarır. Heç biri yoxdursa `.env.example`-dan `.env.local` yaradır.

```bash
./start-stack.sh
```

## Vacib dəyişənlər

Bu dəyərlər lokal faylda doldurulmalıdır:

- `ECAFE_DB_PASSWORD`
- `MINIO_ROOT_PASSWORD`
- `ECAFE_JWT_KEY`
- `ECAFE_EMAIL_USERNAME`
- `ECAFE_EMAIL_PASSWORD`
- `ECAFE_EMAIL_FROM`

## URL-lər

- ECafe API: `http://localhost:8080`
- ECafe Frontend: `http://localhost:5173`
- MinIO API: `http://localhost:9000`
- MinIO Console: `http://localhost:9011`

## Faydalı komandalar

Loglar:

```bash
docker compose logs -f ecafe-api ecafe-db minio
```

Söndürmək:

```bash
docker compose down
```

Volume-larla birlikdə silmək:

```bash
docker compose down -v
```

`down -v` database və MinIO datalarını silir. Demo data lazımdırsa bu komandadan ehtiyatla istifadə edin.
