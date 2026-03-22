#!/bin/bash
# Run this script on your server to apply the database migration
# Copy the migration files to the server first, or clone the repo there

echo "Applying database migration..."

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT/src/KelliPhoto.Web" || exit 1

# Update the database
dotnet ef database update

echo "Migration complete!"
