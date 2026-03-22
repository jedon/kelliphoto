# PowerShell script to apply the missing ThumbnailPhotoId column migration
# This fixes the error: column f.ThumbnailPhotoId does not exist

$connectionString = "Host=postgres.darklingdesign.com;Port=5444;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!;SSL Mode=Prefer"

# Extract connection details
$dbHost = "postgres.darklingdesign.com"
$dbPort = "5444"
$database = "kelli_photo"
$username = "kelli_photo_app"
$password = "!kelliphoto13!"

Write-Host "Applying migration to add ThumbnailPhotoId column..." -ForegroundColor Yellow

# SQL commands
$sql = @"
-- Add the column if it doesn't exist
DO `$`$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'Folders' 
        AND column_name = 'ThumbnailPhotoId'
    ) THEN
        ALTER TABLE "Folders" ADD COLUMN "ThumbnailPhotoId" integer NULL;
    END IF;
END
`$`$;

-- Create index if it doesn't exist
CREATE INDEX IF NOT EXISTS "IX_Folders_ThumbnailPhotoId" ON "Folders" ("ThumbnailPhotoId");

-- Add foreign key constraint if it doesn't exist
DO `$`$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.table_constraints 
        WHERE constraint_name = 'FK_Folders_Photos_ThumbnailPhotoId'
    ) THEN
        ALTER TABLE "Folders"
        ADD CONSTRAINT "FK_Folders_Photos_ThumbnailPhotoId" 
        FOREIGN KEY ("ThumbnailPhotoId") 
        REFERENCES "Photos" ("Id") 
        ON DELETE SET NULL;
    END IF;
END
`$`$;

-- Update migration history
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260106200000_AddFolderThumbnailPhotoId', '10.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;
"@

# Check if psql is available
$psqlPath = Get-Command psql -ErrorAction SilentlyContinue

if ($psqlPath) {
    Write-Host "Using psql command line tool..." -ForegroundColor Green
    
    # Set PGPASSWORD environment variable
    $env:PGPASSWORD = $password
    
    # Run the SQL
    $sql | & psql -h $dbHost -p $dbPort -U $username -d $database
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Migration applied successfully!" -ForegroundColor Green
    } else {
        Write-Host "Error applying migration. Exit code: $LASTEXITCODE" -ForegroundColor Red
    }
    
    # Clear password from environment
    Remove-Item Env:\PGPASSWORD
} else {
    Write-Host "psql not found. Please install PostgreSQL client tools or run the SQL manually." -ForegroundColor Red
    Write-Host ""
    Write-Host "SQL to run:" -ForegroundColor Yellow
    Write-Host $sql
    Write-Host ""
    Write-Host "Or install PostgreSQL client and run:" -ForegroundColor Yellow
    Write-Host "  `$env:PGPASSWORD='$password'; `$sql | psql -h $dbHost -p $dbPort -U $username -d $database"
}
