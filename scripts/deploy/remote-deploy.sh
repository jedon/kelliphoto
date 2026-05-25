#!/usr/bin/env bash
# Run on the deployment host (via SSH from GitHub Actions).
# Pulls the latest image and recreates the web container.
set -euo pipefail

COMPOSE_DIR="${COMPOSE_DIR:-$HOME/kelli.photo/docker}"
IMAGE="${DEPLOY_IMAGE:-jedon/kelliphoto-web:latest}"
HEALTH_URL="${DEPLOY_HEALTH_URL:-http://127.0.0.1:3004/}"
CONTAINER_NAME="${DEPLOY_CONTAINER_NAME:-kelliphoto-web}"

echo "==> Deploying ${IMAGE}"

if docker info &>/dev/null 2>&1; then
  DOCKER=(docker)
elif sudo docker info &>/dev/null 2>&1; then
  DOCKER=(sudo docker)
else
  echo "ERROR: Cannot access Docker." >&2
  exit 1
fi

echo "==> Pulling image..."
"${DOCKER[@]}" pull "$IMAGE"

if "${DOCKER[@]}" ps -a --format '{{.Names}}' | grep -qx "$CONTAINER_NAME"; then
  echo "==> Recreating existing container: ${CONTAINER_NAME}"

  NETWORK=$("${DOCKER[@]}" inspect "$CONTAINER_NAME" --format '{{range $k,$v := .NetworkSettings.Networks}}{{$k}}{{end}}' | head -1)
  HOST_PORT=$("${DOCKER[@]}" inspect "$CONTAINER_NAME" --format '{{range $p, $conf := .NetworkSettings.Ports}}{{if $conf}}{{(index $conf 0).HostPort}}{{end}}{{end}}')
  CONTAINER_PORT=$("${DOCKER[@]}" inspect "$CONTAINER_NAME" --format '{{range $p, $conf := .NetworkSettings.Ports}}{{if $conf}}{{$p}}{{end}}{{end}}' | cut -d/ -f1)

  ENV_FILE=$(mktemp)
  BINDS_FILE=$(mktemp)
  trap 'rm -f "$ENV_FILE" "$BINDS_FILE"' EXIT
  "${DOCKER[@]}" inspect "$CONTAINER_NAME" --format '{{range .Config.Env}}{{println .}}{{end}}' >"$ENV_FILE"
  "${DOCKER[@]}" inspect "$CONTAINER_NAME" --format '{{range .HostConfig.Binds}}{{println .}}{{end}}' >"$BINDS_FILE"

  "${DOCKER[@]}" stop "$CONTAINER_NAME"
  "${DOCKER[@]}" rm "$CONTAINER_NAME"

  RUN_ARGS=(run -d --name "$CONTAINER_NAME" --restart unless-stopped)
  while IFS= read -r line; do
    [[ -n "$line" ]] && RUN_ARGS+=( -e "$line" )
  done <"$ENV_FILE"
  while IFS= read -r bind; do
    [[ -n "$bind" ]] && RUN_ARGS+=( -v "$bind" )
  done <"$BINDS_FILE"
  RUN_ARGS+=( -p "${HOST_PORT}:${CONTAINER_PORT}" )
  if [[ -n "$NETWORK" ]]; then
    RUN_ARGS+=( --network "$NETWORK" )
  fi
  RUN_ARGS+=( "$IMAGE" )
  "${DOCKER[@]}" "${RUN_ARGS[@]}"
elif [[ -d "$COMPOSE_DIR" ]]; then
  echo "==> Compose directory: ${COMPOSE_DIR}"
  cd "$COMPOSE_DIR"
  if "${DOCKER[@]}" compose version &>/dev/null; then
    COMPOSE=("${DOCKER[@]}" compose)
  else
    COMPOSE=(docker-compose)
  fi
  "${COMPOSE[@]}" pull web 2>/dev/null || true
  "${COMPOSE[@]}" up -d --pull always --force-recreate web
else
  echo "ERROR: No container '${CONTAINER_NAME}' and no compose dir ${COMPOSE_DIR}." >&2
  exit 1
fi

echo "==> Waiting for health check (${HEALTH_URL})..."
for i in $(seq 1 60); do
  if curl -sf --max-time 5 "$HEALTH_URL" >/dev/null; then
    echo "==> Deployment healthy."
    exit 0
  fi
  sleep 2
done

echo "ERROR: Health check failed after 120s." >&2
"${DOCKER[@]}" ps -a --filter "name=${CONTAINER_NAME}"
"${DOCKER[@]}" logs "$CONTAINER_NAME" --tail 80 2>/dev/null || true
exit 1
