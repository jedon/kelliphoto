# Simple script to apply missing ThumbnailPhotoId column
$ErrorActionPreference = "Stop"

$connectionString = "Host=postgres.darklingdesign.com;Port=5444;Database=kelli_photo_dev;Username=kelli_photo_app;Password=!kelliphoto13!;SSL Mode=Prefer"

Write-Host "Applying missing ThumbnailPhotoId migration..." -ForegroundColor Cyan

$sqlScript = @"
DO `$$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'Folders' 
        AND column_name = 'ThumbnailPhotoId'
    ) THEN
        ALTER TABLE "Folders" ADD COLUMN "ThumbnailPhotoId" integer;
        CREATE INDEX IF NOT EXISTS "IX_Folders_ThumbnailPhotoId" ON "Folders" ("ThumbnailPhotoId");
        ALTER TABLE "Folders" 
        ADD CONSTRAINT "FK_Folders_Photos_ThumbnailPhotoId" 
        FOREIGN KEY ("ThumbnailPhotoId") 
        REFERENCES "Photos" ("Id") 
        ON DELETE SET NULL;
        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20260106200000_AddFolderThumbnailPhotoId', '10.0.0')
        ON CONFLICT DO NOTHING;
        RAISE NOTICE 'ThumbnailPhotoId column added successfully';
    ELSE
        RAISE NOTICE 'ThumbnailPhotoId column already exists';
    END IF;
END `$$;
"@

# Save to temp file
$tempFile = [System.IO.Path]::GetTempFileName() + ".sql"
$sqlScript | Out-File -FilePath $tempFile -Encoding UTF8

Write-Host "SQL script saved to: $tempFile" -ForegroundColor Yellow
Write-Host "Attempting to apply using dotnet ef..." -ForegroundColor Yellow

# Try using dotnet ef database update with a custom script
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location (Join-Path $repoRoot "src\KelliPhoto.Web")

# Actually, let's use Npgsql directly via a C# snippet
$csharpCode = @"
using Npgsql;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var connectionString = @"$connectionString";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        
        var sql = @"$sqlScript";
        
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
        
        Console.WriteLine("Migration applied successfully!");
    }
}
"@

$tempCsFile = [System.IO.Path]::GetTempFileName() + ".cs"
$csharpCode | Out-File -FilePath $tempCsFile -Encoding UTF8

Write-Host "Creating temporary C# program..." -ForegroundColor Yellow

# Create a simple console app to run this
$projectDir = Join-Path $PSScriptRoot "temp-migration-fix"  # under scripts/
if (Test-Path $projectDir) {
    Remove-Item $projectDir -Recurse -Force
}
New-Item -ItemType Directory -Path $projectDir | Out-Null

$csprojContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql" Version="8.0.0" />
  </ItemGroup>
</Project>
"@

$csprojContent | Out-File -FilePath (Join-Path $projectDir "Program.csproj") -Encoding UTF8
$csharpCode | Out-File -FilePath (Join-Path $projectDir "Program.cs") -Encoding UTF8

Push-Location $projectDir
try {
    Write-Host "Restoring packages..." -ForegroundColor Yellow
    dotnet restore 2>&1 | Out-Null
    
    Write-Host "Running migration fix..." -ForegroundColor Yellow
    dotnet run
    
    Write-Host "`nCleaning up..." -ForegroundColor Yellow
}
catch {
    Write-Error "Failed: $_"
}
finally {
    Pop-Location
    Remove-Item $projectDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
    Remove-Item $tempCsFile -Force -ErrorAction SilentlyContinue
}

Write-Host "Done!" -ForegroundColor Green
