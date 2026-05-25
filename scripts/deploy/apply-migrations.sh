#!/usr/bin/env bash
# Apply EF Core migrations using the same Docker image as production (run on deploy host).
# Invoked by remote-deploy.sh after pull, before container recreate.
set -euo pipefail

COMPOSE_DIR="${COMPOSE_DIR:-$HOME/kelli.photo/docker}"
IMAGE="${DEPLOY_IMAGE:-jedon/kelliphoto-web:latest}"
CONTAINER_NAME="${DEPLOY_CONTAINER_NAME:-kelliphoto-web}"

if docker info &>/dev/null 2>&1; then
  DOCKER=(docker)
elif sudo docker info &>/dev/null 2>&1; then
  DOCKER=(sudo docker)
else
  echo "ERROR: Cannot access Docker." >&2
  exit 1
fi

resolve_connection_string() {
  local compose_dir="${COMPOSE_DIR/#\~/$HOME}"
  local env_file="$compose_dir/.env"

  if [[ -f "$env_file" ]]; then
    local line
    line="$(grep -E '^ConnectionStrings__DefaultConnection=' "$env_file" | tail -1 || true)"
    if [[ -n "$line" ]]; then
      echo "${line#ConnectionStrings__DefaultConnection=}"
      return 0
    fi
  fi

  if "${DOCKER[@]}" ps --format '{{.Names}}' | grep -qx "$CONTAINER_NAME"; then
    "${DOCKER[@]}" inspect "$CONTAINER_NAME" --format '{{range .Config.Env}}{{println .}}{{end}}' \
      | grep -E '^ConnectionStrings__DefaultConnection=' \
      | tail -1 \
      | sed 's/^ConnectionStrings__DefaultConnection=//'
    return 0
  fi

  return 1
}

resolve_docker_network() {
  if "${DOCKER[@]}" ps --format '{{.Names}}' | grep -qx "$CONTAINER_NAME"; then
    "${DOCKER[@]}" inspect "$CONTAINER_NAME" --format '{{range $k,$v := .NetworkSettings.Networks}}{{$k}}{{end}}' | head -1
  fi
}

echo "==> Applying database migrations (${IMAGE})"

CONN="$(resolve_connection_string || true)"
if [[ -z "${CONN:-}" ]]; then
  echo "ERROR: ConnectionStrings__DefaultConnection not found in ${COMPOSE_DIR}/.env or container ${CONTAINER_NAME}." >&2
  exit 1
fi

NETWORK="$(resolve_docker_network || true)"
RUN_ARGS=(run --rm -e "ConnectionStrings__DefaultConnection=${CONN}" -e ASPNETCORE_ENVIRONMENT=Production)
if [[ -n "${NETWORK:-}" ]]; then
  RUN_ARGS+=(--network "$NETWORK")
  echo "    Using Docker network: ${NETWORK}"
fi
RUN_ARGS+=("$IMAGE" --migrate)

"${DOCKER[@]}" "${RUN_ARGS[@]}"

echo "==> Migrations complete."
