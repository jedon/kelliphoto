#!/bin/bash
# Verify that the kelli.photo deployment is working correctly

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=========================================="
echo "KelliPhoto Deployment Verification"
echo "=========================================="
echo ""

# Configuration
DB_HOST="142.4.216.160"
DB_PORT="15432"
DB_NAME="kelli_photo"
DB_USER="kelli_photo_app"
DB_PASSWORD="!kelliphoto13!"
WEB_URL="https://kelli.photo"

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

# Check 1: Docker containers running
echo "1. Checking Docker containers..."
if docker ps | grep -q "kelliphoto-postgres"; then
    print_success "PostgreSQL container is running"
else
    print_error "PostgreSQL container is NOT running"
    echo "   Fix: cd ~/kelli.photo/docker && docker-compose up -d postgres"
fi

if docker ps | grep -q "kelliphoto-web"; then
    print_success "Web container is running"
else
    print_error "Web container is NOT running"
    echo "   Fix: cd ~/kelli.photo/docker && docker-compose up -d web"
fi
echo ""

# Check 2: Database connection
echo "2. Checking database connection..."
if PGPASSWORD="$DB_PASSWORD" psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" -c "SELECT 1;" > /dev/null 2>&1; then
    print_success "Database connection successful"
else
    print_error "Cannot connect to database"
    echo "   Check: iptables rules, PostgreSQL config, container status"
fi
echo ""

# Check 3: Database tables exist
echo "3. Checking database schema..."
TABLES=$(PGPASSWORD="$DB_PASSWORD" psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('Folders', 'Photos', 'Thumbnails', 'AspNetUsers');" 2>/dev/null)

if [ "$TABLES" = "4" ] || [ "$TABLES" = " 4" ]; then
    print_success "All required tables exist"
else
    print_error "Missing database tables (found $TABLES of 4)"
    echo "   Fix: Run $SCRIPT_DIR/quick-fix-database.sh or apply complete-migration.sql at repo root"
fi
echo ""

# Check 4: Migrations applied
echo "4. Checking migration history..."
MIGRATIONS=$(PGPASSWORD="$DB_PASSWORD" psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" -t -c "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";" 2>/dev/null || echo "0")

if [ "$MIGRATIONS" = "2" ] || [ "$MIGRATIONS" = " 2" ]; then
    print_success "All migrations applied (2/2)"
    PGPASSWORD="$DB_PASSWORD" psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\";" 2>/dev/null | grep "20260106"
elif [ "$MIGRATIONS" = "0" ] || [ "$MIGRATIONS" = " 0" ]; then
    print_error "No migrations applied"
    echo "   Fix: Run $SCRIPT_DIR/quick-fix-database.sh"
else
    print_warning "Some migrations applied ($MIGRATIONS of 2)"
    echo "   You may need to apply remaining migrations"
fi
echo ""

# Check 5: Web application logs
echo "5. Checking web application logs (last 10 lines)..."
echo "----------------------------------------"
docker logs --tail 10 kelliphoto-web 2>/dev/null || print_error "Cannot read logs"
echo "----------------------------------------"

# Look for specific error patterns
if docker logs --tail 50 kelliphoto-web 2>/dev/null | grep -q "relation.*does not exist"; then
    print_error "Found 'relation does not exist' errors in logs"
    echo "   Fix: Apply database migrations"
elif docker logs --tail 50 kelliphoto-web 2>/dev/null | grep -q "Database migrations applied successfully"; then
    print_success "Migrations applied successfully (from logs)"
else
    print_warning "No clear migration status in recent logs"
fi
echo ""

# Check 6: Gallery scanning
echo "6. Checking gallery scan status..."
if docker logs --tail 100 kelliphoto-web 2>/dev/null | grep -q "Catalog scan completed"; then
    FOLDERS=$(docker logs --tail 100 kelliphoto-web 2>/dev/null | grep "Scanned" | tail -1 | grep -oP '\d+(?= folders)')
    PHOTOS=$(docker logs --tail 100 kelliphoto-web 2>/dev/null | grep "Total photos" | tail -1 | grep -oP '\d+$')
    print_success "Gallery scanned: $FOLDERS folders, $PHOTOS photos"
else
    print_warning "Gallery scan not completed or not in recent logs"
fi
echo ""

# Check 7: Website accessibility
echo "7. Checking website accessibility..."
if command -v curl &> /dev/null; then
    HTTP_CODE=$(curl -k -s -o /dev/null -w "%{http_code}" "$WEB_URL" 2>/dev/null || echo "000")
    if [ "$HTTP_CODE" = "200" ]; then
        print_success "Website is accessible (HTTP $HTTP_CODE)"
    else
        print_error "Website returned HTTP $HTTP_CODE"
    fi
else
    print_warning "curl not installed, skipping web check"
fi
echo ""

# Check 8: Nginx configuration
echo "8. Checking Nginx..."
if docker ps | grep -q "nginx"; then
    print_success "Nginx is running"
    if docker exec nginx nginx -t > /dev/null 2>&1; then
        print_success "Nginx configuration is valid"
    else
        print_error "Nginx configuration has errors"
    fi
else
    print_warning "Nginx container not found (may not be Dockerized)"
fi
echo ""

# Summary
echo "=========================================="
echo "Verification Complete"
echo "=========================================="
echo ""
echo "Common fixes:"
echo "  - Apply migrations: $SCRIPT_DIR/quick-fix-database.sh"
echo "  - Restart containers: docker restart kelliphoto-web"
echo "  - View logs: docker logs -f kelliphoto-web"
echo "  - Check iptables: sudo iptables -L INPUT -n | grep 15432"
echo ""
