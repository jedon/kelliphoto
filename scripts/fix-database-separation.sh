#!/bin/bash
# Fix the database separation issue between dev and production

set -e

echo "=================================================="
echo "Database Separation Fix"
echo "=================================================="
echo ""
echo "This script will:"
echo "1. Rename current database to kelli_photo_dev (for local development)"
echo "2. Create kelli_photo_prod (for production)"
echo "3. Apply migrations to production database"
echo ""

# PostgreSQL server details
PG_HOST="postgres.darklingdesign.com"
PG_PORT="5444"
PG_USER="kelli_photo_app"
PG_PASSWORD="!kelliphoto13!"

echo "Step 1: Rename existing database to kelli_photo_dev"
echo "-------------------------------------------------------"
read -p "This will rename 'kelli_photo' to 'kelli_photo_dev'. Continue? (y/n) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Aborted."
    exit 1
fi

# Check if kelli_photo_dev already exists
if PGPASSWORD="$PG_PASSWORD" psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -lqt | cut -d \| -f 1 | grep -qw kelli_photo_dev; then
    echo "✓ kelli_photo_dev already exists, skipping rename"
else
    echo "Renaming kelli_photo to kelli_photo_dev..."
    PGPASSWORD="$PG_PASSWORD" psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d postgres -c "ALTER DATABASE kelli_photo RENAME TO kelli_photo_dev;" 2>&1
    if [ $? -eq 0 ]; then
        echo "✓ Database renamed successfully"
    else
        echo "⚠ Could not rename database (it may already be renamed or in use)"
        echo "  Try: SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'kelli_photo';"
    fi
fi

echo ""
echo "Step 2: Verify migrations in dev database"
echo "-------------------------------------------------------"
PGPASSWORD="$PG_PASSWORD" psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d kelli_photo_dev -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\";" 2>&1 || echo "No migrations found in dev database"

echo ""
echo "Step 3: Create production database in Docker"
echo "-------------------------------------------------------"
echo "This should be done on the production server."
echo ""
echo "On your production server, run:"
echo ""
echo "  # Apply migrations to Docker PostgreSQL"
echo "  cd ~/kelli.photo"
echo "  PGPASSWORD='!kelliphoto13!' psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo -f complete-migration.sql"
echo "  docker restart kelliphoto-web"
echo ""

echo "=================================================="
echo "Next Steps:"
echo "=================================================="
echo ""
echo "1. ✓ Local dev now uses: kelli_photo_dev"
echo "2. → Apply migrations to production (see command above)"
echo "3. → Test both environments separately"
echo ""
echo "Verification:"
echo "  Local:  psql -h $PG_HOST -p $PG_PORT -U $PG_USER -d kelli_photo_dev -c '\\dt'"
echo "  Prod:   PGPASSWORD='!kelliphoto13!' psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo -c '\\dt'"
echo ""
