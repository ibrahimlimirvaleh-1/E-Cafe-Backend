#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

if ! command -v docker >/dev/null 2>&1; then
  echo "Error: docker tapılmadı. Docker Desktop və ya Docker Engine quraşdırın." >&2
  exit 1
fi

if [ ! -f .env ]; then
  cp .env.example .env
  echo ".env yaradıldı (.env.example-dan). Zəhmət olmasa şifrələri dəyişin." >&2
fi

# Ardıcıllıq olmadan bütün stack-i qaldır
docker compose up -d --build

ECAFE_API_PORT="${ECAFE_API_PORT:-8080}"
SENTRY_PORT="${SENTRY_PORT:-9002}"
MINIO_API_PORT="${MINIO_API_PORT:-9000}"
MINIO_CONSOLE_PORT="${MINIO_CONSOLE_PORT:-9011}"

echo "Bütün servislər qaldırıldı:"
echo "- ECafe API:   http://localhost:${ECAFE_API_PORT}"
echo "- Sentry:      http://localhost:${SENTRY_PORT}"
echo "- MinIO API:   http://localhost:${MINIO_API_PORT}"
echo "- MinIO UI:    http://localhost:${MINIO_CONSOLE_PORT}"
