# Direct SQL fix for missing ThumbnailPhotoId column
# Uses Npgsql .NET driver to connect and execute SQL

$connectionString = "Host=postgres.darklingdesign.com;Port=5444;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!;SSL Mode=Prefer"

$sql = @"
-- Add the column if it doesn't exist
DO `$`$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'public'
        AND table_name = 'Folders' 
        AND column_name = 'ThumbnailPhotoId'
    ) THEN
        ALTER TABLE "Folders" ADD COLUMN "ThumbnailPhotoId" integer NULL;
        RAISE NOTICE 'Added ThumbnailPhotoId column';
    ELSE
        RAISE NOTICE 'ThumbnailPhotoId column already exists';
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
        WHERE constraint_schema = 'public'
        AND constraint_name = 'FK_Folders_Photos_ThumbnailPhotoId'
    ) THEN
        ALTER TABLE "Folders"
        ADD CONSTRAINT "FK_Folders_Photos_ThumbnailPhotoId" 
        FOREIGN KEY ("ThumbnailPhotoId") 
        REFERENCES "Photos" ("Id") 
        ON DELETE SET NULL;
        RAISE NOTICE 'Added foreign key constraint';
    ELSE
        RAISE NOTICE 'Foreign key constraint already exists';
    END IF;
END
`$`$;
"@

Write-Host "Connecting to database and applying fix..." -ForegroundColor Yellow

# Try to use dotnet to run a simple C# script
$csharpScript = @"
using System;
using Npgsql;

var connString = "$connectionString";
await using var conn = new NpgsqlConnection(connString);
await conn.OpenAsync();

var sql = @"$sql";

await using var cmd = new NpgsqlCommand(sql, conn);
await cmd.ExecuteNonQueryAsync();

Console.WriteLine("Successfully added ThumbnailPhotoId column!");
"@

# Save the C# script
$csharpScript | Out-File -FilePath "fix-db-temp.cs" -Encoding UTF8

# Try to compile and run it
try {
    Write-Host "Compiling and running fix script..." -ForegroundColor Green
    dotnet script fix-db-temp.cs --package Npgsql 2>&1 | Write-Host
} catch {
    Write-Host "Could not run dotnet script. Please run the SQL manually:" -ForegroundColor Red
    Write-Host ""
    Write-Host $sql
    Write-Host ""
    Write-Host "Or install Npgsql and run the SQL using psql or pgAdmin"
}

# Clean up
if (Test-Path "fix-db-temp.cs") {
    Remove-Item "fix-db-temp.cs"
}
