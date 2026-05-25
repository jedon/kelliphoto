#!/usr/bin/env bash
# Run on the deployment host (via SSH from GitHub Actions).
# Pulls the latest image and recreates the web container.
set -euo pipefail

COMPOSE_DIR="${COMPOSE_DIR:-$HOME/kelli.photo/docker}"
IMAGE="${DEPLOY_IMAGE:-jedon/kelliphoto-web:latest}"
HEALTH_URL="${DEPLOY_HEALTH_URL:-http://127.0.0.1:8888/}"

echo "==> Deploying ${IMAGE}"
echo "==> Compose directory: ${COMPOSE_DIR}"

if [[ ! -d "$COMPOSE_DIR" ]]; then
  echo "ERROR: Compose directory not found: $COMPOSE_DIR" >&2
  echo "Clone the repo on the server or set COMPOSE_DIR." >&2
  exit 1
fi

cd "$COMPOSE_DIR"

if docker info &>/dev/null 2>&1; then
  DOCKER=(docker)
elif sudo docker info &>/dev/null 2>&1; then
  DOCKER=(sudo docker)
else
  echo "ERROR: Cannot access Docker (add user to docker group or configure sudo)." >&2
  exit 1
fi

if "${DOCKER[@]}" compose version &>/dev/null; then
  COMPOSE=("${DOCKER[@]}" compose)
elif command -v docker-compose &>/dev/null; then
  COMPOSE=(docker-compose)
else
  echo "ERROR: docker compose is not installed." >&2
  exit 1
fi

echo "==> Pulling image..."
"${DOCKER[@]}" pull "$IMAGE"

echo "==> Recreating web service..."
"${COMPOSE[@]}" pull web 2>/dev/null || true
"${COMPOSE[@]}" up -d --pull always --force-recreate web

echo "==> Waiting for health check (${HEALTH_URL})..."
for i in $(seq 1 60); do
  if curl -sf --max-time 5 "$HEALTH_URL" >/dev/null; then
    echo "==> Deployment healthy."
    exit 0
  fi
  sleep 2
done

echo "ERROR: Health check failed after 120s." >&2
"${COMPOSE[@]}" ps
"${DOCKER[@]}" logs kelliphoto-web --tail 80 2>/dev/null || true
exit 1
