#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

if [ -z "${CODESPACE_NAME:-}" ]; then
  echo "[post-create] non in Codespaces, skip env override"
  exit 0
fi

FRONTEND_URL="https://${CODESPACE_NAME}-8080.app.github.dev"
API_URL="https://${CODESPACE_NAME}-5000.app.github.dev"

if [ ! -f .env ]; then
  cp .env.example .env
fi

# JWT key random se è ancora il placeholder
if grep -q "^JWT__Key=ChangeMe" .env; then
  KEY=$(openssl rand -hex 32)
  sed -i "s|^JWT__Key=.*|JWT__Key=${KEY}|" .env
fi

# sovrascrive le URL pubbliche del codespace
grep -v -E "^(Frontend__Url|VITE_API_URL)=" .env > .env.tmp || true
mv .env.tmp .env
{
  echo "Frontend__Url=${FRONTEND_URL}"
  echo "VITE_API_URL=${API_URL}"
} >> .env

echo "[post-create] .env configurato per Codespaces"
echo "  Frontend: ${FRONTEND_URL}"
echo "  API:      ${API_URL}"
echo ""
echo "Avvio stack: docker compose up --build -d"
