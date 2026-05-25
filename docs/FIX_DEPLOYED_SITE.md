# Fix for "relation 'Folders' does not exist" Error

## What's Wrong

Your deployed website at https://kelli.photo is failing with:
```
Npgsql.PostgresException: 42P01: relation "Folders" does not exist
```

**Root Cause:** The database tables were never created on your production PostgreSQL database. The application is trying to query tables that don't exist.

## The Solution

You need to apply the database migrations to create the tables. I've created everything you need.

## Files Created

1. **`complete-migration.sql`** - SQL script that creates all database tables
   - Creates AspNet Identity tables (for user authentication)
   - Creates Folders, Photos, and Thumbnails tables
   - Creates all necessary indexes and constraints
   - Safe to run multiple times (checks if migrations are already applied)

2. **`apply-migration-to-server.sh`** - Bash script to apply the migration
   - Tests database connection
   - Applies the SQL script
   - Shows helpful error messages

3. **`apply-migration-to-server.ps1`** - PowerShell version (if needed)

4. **`APPLY_MIGRATIONS_GUIDE.md`** - Detailed instructions with troubleshooting

## Quick Start - Run This on Your Server

```bash
# 1. SSH to your production server
ssh your-server

# 2. Create a temporary directory
mkdir -p ~/kelli-migration && cd ~/kelli-migration

# 3. Copy the SQL file here (from another terminal on your local machine):
#    scp complete-migration.sql your-server:~/kelli-migration/

# 4. Apply the migration
PGPASSWORD='!kelliphoto13!' psql \
  -h 142.4.216.160 \
  -p 15432 \
  -U kelli_photo_app \
  -d kelli_photo \
  -f complete-migration.sql

# 5. Restart the web container
docker restart kelliphoto-web

# 6. Check the logs
docker logs -f kelliphoto-web
```

You should see:
- ✓ "Migration completed successfully!"
- ✓ No more "relation does not exist" errors in the logs
- ✓ "Starting catalog scan..." message
- ✓ "Scanned X folders" and "Total photos: Y"

## What the Migration Creates

### Tables Created:
- `AspNetUsers`, `AspNetRoles`, etc. - Identity/Authentication
- `Folders` - Directory structure of your photo gallery
- `Photos` - Individual photo metadata
- `Thumbnails` - Generated thumbnail information

### Features:
- Unique constraints to prevent duplicates
- Foreign key relationships for data integrity
- Indexes for fast queries
- Properly handles timezone-aware timestamps

## If Something Goes Wrong

### "psql: command not found"
```bash
sudo apt-get update && sudo apt-get install postgresql-client
```

### "Connection refused"
1. Check PostgreSQL is running: `docker ps | grep postgres`
2. Check iptables: `sudo iptables -L INPUT -n | grep 15432`
3. Re-apply iptables rules: `sudo bash ~/kelli.photo/scripts/docker/iptables-setup.sh`

### "Authentication failed"
Double-check the password is exactly: `!kelliphoto13!`

### Still having issues?
See `APPLY_MIGRATIONS_GUIDE.md` for detailed troubleshooting.

## After the Fix

Once migrations are applied and the container is restarted:

1. **Visit https://kelli.photo** - should load without errors
2. **Login page should work** - go to /Identity/Account/Login
3. **Gallery should scan** - check logs for "Scanned X folders"

## Why This Happened

The Docker image doesn't include the .NET SDK or Entity Framework tools, so we can't run `dotnet ef database update` inside the container. This is normal for production deployments.

The standard solution is to:
1. Generate SQL migration scripts during development
2. Apply them to production using native database tools (psql)

## For Future Updates

When you add new migrations:

```bash
# On your dev machine
cd src/KelliPhoto.Web
dotnet ef migrations script -o ../../complete-migration.sql

# Copy to server and run the same commands as above
```

---

**TL;DR:** Copy `complete-migration.sql` to your server, run it with psql, restart the container, and you're done! 🚀
