#!/bin/bash
# Bash script to apply database migrations
# Usage: ./scripts/apply-migrations.sh [environment]   (from repository root)
# Example: ./scripts/apply-migrations.sh Development

ENVIRONMENT=${1:-Development}

echo "Applying database migrations for $ENVIRONMENT environment..."

# Repository root (this script lives in scripts/)
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT_PATH="$REPO_ROOT/src/KelliPhoto.Web"
if [ ! -d "$PROJECT_PATH" ]; then
    echo "Error: Project path not found: $PROJECT_PATH"
    exit 1
fi

cd "$PROJECT_PATH" || exit 1

# Set the environment
export ASPNETCORE_ENVIRONMENT=$ENVIRONMENT

# List pending migrations
echo ""
echo "Checking pending migrations..."
dotnet ef migrations list

# Apply migrations
echo ""
echo "Applying migrations..."
dotnet ef database update

if [ $? -eq 0 ]; then
    echo ""
    echo "✓ Migrations applied successfully!"
else
    echo ""
    echo "✗ Failed to apply migrations"
    exit 1
fi
