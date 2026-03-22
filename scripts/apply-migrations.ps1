# PowerShell script to apply database migrations
# Usage: .\scripts\apply-migrations.ps1 [environment]   (from repository root)
# Example: .\scripts\apply-migrations.ps1 Development

param(
    [string]$Environment = "Development"
)

$ErrorActionPreference = "Stop"

Write-Host "Applying database migrations for $Environment environment..." -ForegroundColor Cyan

# Repository root (this script lives in scripts/)
$repoRoot = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $repoRoot "src\KelliPhoto.Web"
if (-not (Test-Path $projectPath)) {
    Write-Error "Project path not found: $projectPath"
    exit 1
}

Push-Location $projectPath

try {
    # Set the environment
    $env:ASPNETCORE_ENVIRONMENT = $Environment
    
    # List pending migrations
    Write-Host ""
    Write-Host "Checking pending migrations..." -ForegroundColor Yellow
    dotnet ef migrations list
    
    # Apply migrations
    Write-Host ""
    Write-Host "Applying migrations..." -ForegroundColor Yellow
    dotnet ef database update
    
    Write-Host ""
    Write-Host "Migrations applied successfully!" -ForegroundColor Green
}
catch {
    $errorMessage = $_.Exception.Message
    Write-Error "Failed to apply migrations: $errorMessage"
    exit 1
}
finally {
    Pop-Location
}
