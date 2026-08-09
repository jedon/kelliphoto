#!/usr/bin/env bash
set -euo pipefail
PASS=$(docker exec kelliphoto-web printenv ConnectionStrings__DefaultConnection | sed -n 's/.*Password=\([^;]*\).*/\1/p')
docker exec -e PGPASSWORD="$PASS" kelliphoto-postgres psql -U kelli_photo_app -d kelli_photo -c \
  "SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename IN ('Tags','FolderTags','PhotoTags','PhotoExifs') ORDER BY 1;"
docker exec -e PGPASSWORD="$PASS" kelliphoto-postgres psql -U kelli_photo_app -d kelli_photo -c \
  "SELECT column_name FROM information_schema.columns WHERE table_name = 'Tags' ORDER BY 1;"
