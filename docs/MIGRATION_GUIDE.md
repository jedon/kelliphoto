# Database Migration Guide

This guide explains how to manage and apply database migrations for the KelliPhoto application.

## Overview

Database migrations are **NOT** applied automatically by the application. All migrations must be run manually from the command line.

## Prerequisites

1. .NET SDK installed
2. Entity Framework Core tools installed:
   ```bash
   dotnet tool install --global dotnet-ef
   ```

## Available Migrations

The following migrations are available in the project:

1. **20260106150058_InitialCreate** - Initial database schema
2. **20260106200000_AddFolderThumbnailPhotoId** - Adds ThumbnailPhotoId to Folders table
3. **20260107141721_AddVisibilityAndMetadataFields** - Adds visibility and metadata fields

## Applying Migrations

### Development Environment

**Using PowerShell:**
```powershell
.\scripts\apply-migrations.ps1 Development
```

**Using Bash:**
```bash
./scripts/apply-migrations.sh Development
```

**Using dotnet CLI directly:**
```bash
cd src/KelliPhoto.Web
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update
```

### Production Environment

**Using PowerShell:**
```powershell
.\scripts\apply-migrations.ps1 Production
```

**Using Bash:**
```bash
./scripts/apply-migrations.sh Production
```

**Using dotnet CLI directly:**
```bash
cd src/KelliPhoto.Web
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet ef database update
```

## Checking Migration Status

To see which migrations have been applied and which are pending:

```bash
cd src/KelliPhoto.Web
dotnet ef migrations list
```

## Creating New Migrations

When you modify the data models (in `src/KelliPhoto.Web/Data/Models/`), you need to create a new migration:

```bash
cd src/KelliPhoto.Web
dotnet ef migrations add YourMigrationName
```

Then review the generated migration file in `src/KelliPhoto.Web/Migrations/` before applying it.

## Generating SQL Script

To generate a SQL script for all migrations (useful for production deployments):

```bash
cd src/KelliPhoto.Web
dotnet ef migrations script --idempotent -o migrations.sql
```

The `--idempotent` flag ensures the script can be run multiple times safely.

## Connection Strings

Migrations use the connection string from `appsettings.json` (or `appsettings.{Environment}.json`).

- **Development**: `appsettings.Development.json`
- **Production**: `appsettings.json`

Make sure the connection string points to the correct database before running migrations.

## Troubleshooting

### "Database does not exist" error

You need to create the database first. Connect to PostgreSQL and run:

```sql
CREATE DATABASE kelli_photo_dev;
-- or for production:
CREATE DATABASE kelli_photo;
```

### "Pending model changes" warning

If you see this warning, it means your models don't match the latest migration. Create a new migration:

```bash
cd src/KelliPhoto.Web
dotnet ef migrations add FixModelChanges
```

### "Migration already applied" error

If a migration is already applied but EF Core thinks it isn't, you can manually update the `__EFMigrationsHistory` table, or use:

```bash
dotnet ef database update MigrationName
```

to apply migrations up to a specific point.
