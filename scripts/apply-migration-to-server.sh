#!/bin/bash
# Apply database migrations to the production server
# This script should be run ON the production server

set -e  # Exit on error

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "=================================================="
echo "KelliPhoto Database Migration Script"
echo "=================================================="
echo ""

# Database connection details (from docker-compose.yml)
DB_HOST="192.168.10.150"
DB_PORT="15432"
DB_NAME="kelli_photo"
DB_USER="kelli_photo_app"
DB_PASSWORD="!kelliphoto13!"

echo "Target database:"
echo "  Host: $DB_HOST:$DB_PORT"
echo "  Database: $DB_NAME"
echo "  User: $DB_USER"
echo ""

# Check if psql is installed
if ! command -v psql &> /dev/null; then
    echo "ERROR: psql is not installed!"
    echo "Install it with: sudo apt-get install postgresql-client"
    exit 1
fi

# Check if migration file exists (expected at repository root)
MIGRATION_FILE="$REPO_ROOT/complete-migration.sql"
if [ ! -f "$MIGRATION_FILE" ]; then
    echo "ERROR: complete-migration.sql not found at: $MIGRATION_FILE"
    echo "Run this script from anywhere; it resolves the repo root from its location."
    exit 1
fi

# Test database connection
echo "Testing database connection..."
PGPASSWORD="$DB_PASSWORD" psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" -c "SELECT version();" > /dev/null 2>&1
if [ $? -eq 0 ]; then
    echo "✓ Database connection successful"
else
    echo "✗ Failed to connect to database"
    echo ""
    echo "Troubleshooting steps:"
    echo "1. Check that PostgreSQL container is running: docker ps | grep postgres"
    echo "2. Check iptables rules allow connection to port $DB_PORT"
    echo "3. Verify PostgreSQL is configured to accept external connections"
    exit 1
fi

echo ""
echo "Applying migrations..."
echo ""

# Apply the migration
PGPASSWORD="$DB_PASSWORD" psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" -f "$MIGRATION_FILE"

if [ $? -eq 0 ]; then
    echo ""
    echo "=================================================="
    echo "✓ Migrations applied successfully!"
    echo "=================================================="
    echo ""
    echo "Next steps:"
    echo "1. Restart the web container: docker restart kelliphoto-web"
    echo "2. Check logs: docker logs -f kelliphoto-web"
    echo "3. Visit https://kelli.photo"
    echo ""
else
    echo ""
    echo "✗ Migration failed!"
    echo "Check the error messages above for details."
    exit 1
fi
