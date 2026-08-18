#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

if ! command -v docker >/dev/null 2>&1; then
  echo "Error: docker tapılmadı. Docker Desktop və ya Docker Engine quraşdırın." >&2
  exit 1
fi

ENV_FILE=".env.local"

if [ ! -f "$ENV_FILE" ] && [ -f .env ]; then
  ENV_FILE=".env"
fi

if [ ! -f "$ENV_FILE" ]; then
  cp .env.example .env.local
  ENV_FILE=".env.local"
  echo ".env.local yaradıldı (.env.example-dan)." >&2
  echo "Zəhmət olmasa lokal DB, MinIO, JWT və email dəyərlərini .env.local faylında dəyişin." >&2
fi

docker compose --env-file "$ENV_FILE" up -d --build

set -a
# shellcheck disable=SC1091
. "./$ENV_FILE"
set +a

ECAFE_API_PORT="${ECAFE_API_PORT:-8081}"
MINIO_API_PORT="${MINIO_API_PORT:-9000}"
MINIO_CONSOLE_PORT="${MINIO_CONSOLE_PORT:-9011}"
ECAFE_FRONTEND_PORT="${ECAFE_FRONTEND_PORT:-5173}"

echo "Servislər qaldırıldı:"
echo "- ECafe API:      http://localhost:${ECAFE_API_PORT}"
echo "- ECafe Frontend: http://localhost:${ECAFE_FRONTEND_PORT}"
echo "- MinIO API:      http://localhost:${MINIO_API_PORT}"
echo "- MinIO UI:       http://localhost:${MINIO_CONSOLE_PORT}"
