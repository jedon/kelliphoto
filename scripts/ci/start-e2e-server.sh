#!/usr/bin/env bash
# Starts KelliPhoto.Web for Playwright (in-memory DB, isolated temp gallery).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
E2E_ROOT="${E2E_ROOT:-${TMPDIR:-/tmp}/kelliphoto-e2e}"
GALLERY="${E2E_ROOT}/gallery"
THUMBS="${E2E_ROOT}/thumbnails"
WEB="${E2E_ROOT}/web"
ASSETS="${GALLERY}/.web"

mkdir -p "$GALLERY" "$THUMBS" "$WEB" "$ASSETS"

export ASPNETCORE_ENVIRONMENT=Testing
export KELLIPHOTO_INTEGRATION_TEST=1
export ASPNETCORE_URLS=http://127.0.0.1:5050
export GallerySettings__GalleryPath="$GALLERY"
export GallerySettings__ThumbnailPath="$THUMBS"
export GallerySettings__WebImagePath="$WEB"
export GallerySettings__WebAssetsPath="$ASSETS"
export WatermarkSettings__Enabled=false

cd "$REPO_ROOT"
exec dotnet run --project src/KelliPhoto.Web/KelliPhoto.Web.csproj --no-launch-profile
