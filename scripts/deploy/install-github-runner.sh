#!/usr/bin/env bash
# Install and register a GitHub Actions self-hosted runner for jedon/kelliphoto.
# Usage (on Debian VM):
#   export RUNNER_TOKEN='...'   # from: gh api repos/jedon/kelliphoto/actions/runners/registration-token -X POST
#   bash install-github-runner.sh
set -euo pipefail

REPO_URL="${REPO_URL:-https://github.com/jedon/kelliphoto}"
RUNNER_NAME="${RUNNER_NAME:-kelliphoto-$(hostname -s)}"
RUNNER_LABELS="${RUNNER_LABELS:-self-hosted,Linux,X64,kelliphoto}"
RUNNER_USER="${RUNNER_USER:-$(whoami)}"
INSTALL_DIR="${INSTALL_DIR:-$HOME/actions-runner}"
RUNNER_VERSION="${RUNNER_VERSION:-2.334.0}"

if [[ -z "${RUNNER_TOKEN:-}" ]]; then
  echo "ERROR: Set RUNNER_TOKEN (registration token from GitHub, expires in ~1 hour)." >&2
  echo "  gh api repos/jedon/kelliphoto/actions/runners/registration-token --method POST" >&2
  exit 1
fi

if ! command -v curl &>/dev/null; then
  echo "Installing curl..."
  sudo apt-get update -qq
  sudo apt-get install -y curl ca-certificates
fi

# Dependencies for runner service (libicu, etc.)
if ! dpkg -s libicu72 &>/dev/null 2>&1 && ! dpkg -s libicu71 &>/dev/null 2>&1; then
  sudo apt-get install -y libicu72 2>/dev/null || sudo apt-get install -y libicu71 2>/dev/null || true
fi

mkdir -p "$INSTALL_DIR"
cd "$INSTALL_DIR"

TARBALL="actions-runner-linux-x64-${RUNNER_VERSION}.tar.gz"
if [[ ! -f "$INSTALL_DIR/config.sh" ]]; then
  echo "==> Downloading runner ${RUNNER_VERSION}..."
  curl -fsSL -o "$TARBALL" \
    "https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/${TARBALL}"
  tar xzf "$TARBALL"
  rm -f "$TARBALL"
fi

if [[ -f "$INSTALL_DIR/.runner" ]]; then
  echo "==> Runner already configured in $INSTALL_DIR"
else
  echo "==> Configuring runner ${RUNNER_NAME}..."
  ./config.sh \
    --url "$REPO_URL" \
    --token "$RUNNER_TOKEN" \
    --name "$RUNNER_NAME" \
    --labels "$RUNNER_LABELS" \
    --unattended \
    --replace
fi

echo "==> Installing systemd service (user ${RUNNER_USER})..."
sudo ./svc.sh install "$RUNNER_USER"
sudo ./svc.sh start
sudo ./svc.sh status || true

echo ""
echo "==> Done. In GitHub: Settings -> Actions -> Runners — look for '${RUNNER_NAME}' (Idle)."
echo "    Set repository variable DEPLOY_RUNNER=self-hosted and ENABLE_DEPLOY=true"
