# Apply database migrations to the production server
# This script should be run ON the production server (if it has PowerShell)
# Or use the bash version (apply-migration-to-server.sh) which is preferred on Linux

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "KelliPhoto Database Migration Script" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# Database connection details (from docker-compose.yml)
$DB_HOST = "142.4.216.160"
$DB_PORT = "15432"
$DB_NAME = "kelli_photo"
$DB_USER = "kelli_photo_app"
$DB_PASSWORD = "!kelliphoto13!"

Write-Host "Target database:"
Write-Host "  Host: ${DB_HOST}:${DB_PORT}"
Write-Host "  Database: $DB_NAME"
Write-Host "  User: $DB_USER"
Write-Host ""

# Check if psql is available
$psqlPath = Get-Command psql -ErrorAction SilentlyContinue
if (-not $psqlPath) {
    Write-Host "ERROR: psql is not installed!" -ForegroundColor Red
    Write-Host "Options:"
    Write-Host "1. Install PostgreSQL client from: https://www.postgresql.org/download/"
    Write-Host "2. Use WSL and run the bash version: bash apply-migration-to-server.sh"
    Write-Host "3. Copy complete-migration.sql to the Linux server and run it there"
    exit 1
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$migrationFile = Join-Path $repoRoot "complete-migration.sql"
if (-not (Test-Path $migrationFile)) {
    Write-Host "ERROR: complete-migration.sql not found at: $migrationFile" -ForegroundColor Red
    exit 1
}

# Test database connection
Write-Host "Testing database connection..."
$env:PGPASSWORD = $DB_PASSWORD
$testResult = & psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -c "SELECT version();" 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Database connection successful" -ForegroundColor Green
} else {
    Write-Host "✗ Failed to connect to database" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting steps:"
    Write-Host "1. Check that PostgreSQL container is running on the server"
    Write-Host "2. Check server firewall allows connection to port $DB_PORT"
    Write-Host "3. Verify you can reach the server from this machine"
    Write-Host ""
    Write-Host "Error details:" -ForegroundColor Yellow
    Write-Host $testResult
    exit 1
}

Write-Host ""
Write-Host "Applying migrations..." -ForegroundColor Cyan
Write-Host ""

# Apply the migration
$env:PGPASSWORD = $DB_PASSWORD
& psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -f $migrationFile

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "==================================================" -ForegroundColor Green
    Write-Host "✓ Migrations applied successfully!" -ForegroundColor Green
    Write-Host "==================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:"
    Write-Host "1. Restart the web container on server: docker restart kelliphoto-web"
    Write-Host "2. Check logs: docker logs -f kelliphoto-web"
    Write-Host "3. Visit https://kelli.photo"
    Write-Host ""
} else {
    Write-Host ""
    Write-Host "✗ Migration failed!" -ForegroundColor Red
    Write-Host "Check the error messages above for details."
    exit 1
}
