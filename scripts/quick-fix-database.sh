#!/bin/bash
# One-line fix for the database issue
# Run this on your production server

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SQL_FILE="$REPO_ROOT/complete-migration.sql"

echo "Applying database migrations to kelli.photo..."
PGPASSWORD='!kelliphoto13!' psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo -f "$SQL_FILE" && \
docker restart kelliphoto-web && \
echo "" && \
echo "✓ Done! Watching logs (press Ctrl+C to exit)..." && \
docker logs -f kelliphoto-web
