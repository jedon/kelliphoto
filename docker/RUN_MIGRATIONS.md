# Running Database Migrations

## Current Issue

The runtime Docker image doesn't have the .NET SDK, so `dotnet ef` isn't available.

## Solution Options

### Option 1: Rebuild Image with EF Tools (Recommended)

I've updated the Dockerfile to include EF tools. After the next GitHub Actions build:

1. **Wait for build to complete** (check GitHub Actions)
2. **Update stack in Portainer** to pull the new image
3. **Run migrations:**
   ```bash
   docker exec -it kelliphoto-web dotnet ef database update
   ```

### Option 2: Run Migrations from Host (If you have .NET SDK)

If your server has .NET SDK installed:

```bash
# Set connection string
export ConnectionStrings__DefaultConnection="Host=192.168.10.150;Port=15432;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!"

# Run migrations
cd /path/to/kelli.photo/src/KelliPhoto.Web
dotnet ef database update
```

### Option 3: Use SQL Script (Temporary Workaround)

Extract the migration SQL and run it directly:

```bash
# From your local machine (with .NET SDK)
cd src/KelliPhoto.Web
dotnet ef migrations script -o migration.sql

# Copy migration.sql to server and run:
psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo -f migration.sql
```

### Option 4: Create Migration Container (Advanced)

Create a temporary container with SDK just for migrations:

```bash
docker run --rm -it \
  -v /path/to/kelli.photo:/src \
  -e ConnectionStrings__DefaultConnection="Host=192.168.10.150;Port=15432;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!" \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -c "cd /src/src/KelliPhoto.Web && dotnet ef database update"
```

## Quick Fix for Now

**Easiest immediate solution:**

1. **Extract migration SQL from your local machine:**
   ```bash
   cd src/KelliPhoto.Web
   dotnet ef migrations script -o migration.sql
   ```

2. **Copy to server and run:**
   ```bash
   # On server
   psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo -f migration.sql
   ```

3. **Restart the web container:**
   ```bash
   docker restart kelliphoto-web
   ```

After this, the application should start properly!
