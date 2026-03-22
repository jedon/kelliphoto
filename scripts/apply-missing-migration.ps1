# PowerShell script to apply the missing ThumbnailPhotoId migration
param(
    [string]$ConnectionString = "Host=postgres.darklingdesign.com;Port=5444;Database=kelli_photo_dev;Username=kelli_photo_app;Password=!kelliphoto13!;SSL Mode=Prefer"
)

$ErrorActionPreference = "Stop"

Write-Host "Applying missing ThumbnailPhotoId migration..." -ForegroundColor Cyan

$sql = @"
-- Apply missing AddFolderThumbnailPhotoId migration manually
DO `$`$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'Folders' 
        AND column_name = 'ThumbnailPhotoId'
    ) THEN
        -- Add the column
        ALTER TABLE "Folders" ADD COLUMN "ThumbnailPhotoId" integer;
        
        -- Create index
        CREATE INDEX IF NOT EXISTS "IX_Folders_ThumbnailPhotoId" ON "Folders" ("ThumbnailPhotoId");
        
        -- Add foreign key
        ALTER TABLE "Folders" 
        ADD CONSTRAINT "FK_Folders_Photos_ThumbnailPhotoId" 
        FOREIGN KEY ("ThumbnailPhotoId") 
        REFERENCES "Photos" ("Id") 
        ON DELETE SET NULL;
        
        -- Record the migration as applied
        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20260106200000_AddFolderThumbnailPhotoId', '10.0.0')
        ON CONFLICT DO NOTHING;
        
        RAISE NOTICE 'ThumbnailPhotoId column added successfully';
    ELSE
        RAISE NOTICE 'ThumbnailPhotoId column already exists';
    END IF;
END `$`$;
"@

# Check if psql is available
$psqlPath = Get-Command psql -ErrorAction SilentlyContinue

if ($psqlPath) {
    Write-Host "Using psql to apply migration..." -ForegroundColor Yellow
    
    # Extract connection details from connection string
    $connBuilder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $connBuilder.ConnectionString = $ConnectionString
    
    $host = $connBuilder['Host']
    $port = $connBuilder['Port']
    $database = $connBuilder['Database']
    $username = $connBuilder['Username']
    $password = $connBuilder['Password']
    
    $env:PGPASSWORD = $password
    $sql | psql -h $host -p $port -U $username -d $database
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Migration applied successfully!" -ForegroundColor Green
    } else {
        Write-Error "Failed to apply migration"
        exit 1
    }
} else {
    Write-Host "psql not found. Using .NET to apply migration..." -ForegroundColor Yellow
    
    # Use .NET to apply the SQL
    $repoRoot = Split-Path $PSScriptRoot -Parent
    $projectPath = Join-Path $repoRoot "src\KelliPhoto.Web"
    Push-Location $projectPath
    
    try {
        # Create a temporary C# script to apply the migration
        $scriptContent = @"
using Npgsql;
using System;

var connectionString = "$ConnectionString";
await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

var sql = @"$sql";

await using var command = new NpgsqlCommand(sql, connection);
await command.ExecuteNonQueryAsync();

Console.WriteLine("Migration applied successfully!");
"@
        
        # Write to temp file and execute with dotnet script if available
        # Otherwise, use dotnet run with a simple console app
        Write-Host "Please run the SQL manually or install psql (PostgreSQL client tools)" -ForegroundColor Yellow
        Write-Host "SQL to execute:" -ForegroundColor Cyan
        Write-Host $sql
    }
    finally {
        Pop-Location
    }
}
