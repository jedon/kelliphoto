# Database Fix for kelli.photo - Quick Start

## The Problem
Website shows: `42P01: relation "Folders" does not exist`

## The Solution (Copy & Paste This)

SSH to your production server and run:

```bash
# Navigate to project directory (adjust path as needed)
cd ~/kelli.photo

# If you have the files via Git:
git pull

# Apply the migration
PGPASSWORD='!kelliphoto13!' psql \
  -h 142.4.216.160 \
  -p 15432 \
  -U kelli_photo_app \
  -d kelli_photo \
  -f complete-migration.sql

# Restart the application
docker restart kelliphoto-web

# Watch the logs to verify
docker logs -f kelliphoto-web
```

Press `Ctrl+C` when you see: `Catalog scan completed. Total photos: X`

## What Just Happened?

1. ✅ Created all database tables (Folders, Photos, Thumbnails, AspNet* tables)
2. ✅ Created indexes for fast queries
3. ✅ Set up foreign key relationships
4. ✅ Recorded migrations in `__EFMigrationsHistory`

## Verify It Worked

Visit: https://kelli.photo

You should see:
- ✅ No errors
- ✅ Gallery page loads
- ✅ Login page works

Or run the verification script:
```bash
chmod +x scripts/verify-deployment.sh
./scripts/verify-deployment.sh
```

## If You Don't Have the Files

Copy just the SQL file to your server:

```bash
# From your local machine (G:\Programming\kelli.photo)
scp complete-migration.sql your-server:~/

# Then on the server
PGPASSWORD='!kelliphoto13!' psql -h 142.4.216.160 -p 15432 -U kelli_photo_app -d kelli_photo -f ~/complete-migration.sql
docker restart kelliphoto-web
```

## Troubleshooting

### "psql: command not found"
```bash
sudo apt-get update && sudo apt-get install postgresql-client
```

### "Connection refused"
```bash
# Check PostgreSQL is running
docker ps | grep postgres

# Check iptables
sudo iptables -L INPUT -n | grep 15432

# Should see: ACCEPT tcp -- 192.168.10.0/24 0.0.0.0/0 tcp dpt:15432
```

### "Still getting errors"
```bash
# Check what tables exist
PGPASSWORD='!kelliphoto13!' psql -h 142.4.216.160 -p 15432 -U kelli_photo_app -d kelli_photo -c "\dt"

# Check migrations
PGPASSWORD='!kelliphoto13!' psql -h 142.4.216.160 -p 15432 -U kelli_photo_app -d kelli_photo -c "SELECT * FROM \"__EFMigrationsHistory\";"

# View detailed logs
docker logs kelliphoto-web | tail -50
```

## Files in This Fix

| File | Purpose |
|------|---------|
| `complete-migration.sql` | **Main file** - Creates all database tables |
| `quick-fix-database.sh` | One-command fix (apply + restart) |
| `apply-migration-to-server.sh` | Interactive script with error checking |
| `verify-deployment.sh` | Checks everything is working |
| `APPLY_MIGRATIONS_GUIDE.md` | Detailed step-by-step guide |
| `DEPLOYMENT_FIX_SUMMARY.md` | Technical analysis and options |
| `FIX_DEPLOYED_SITE.md` | Quick reference guide |

## For Future Deployments

When you add new database changes:

```bash
# On your dev machine
cd src/KelliPhoto.Web
dotnet ef migrations add YourMigrationName
dotnet ef migrations script -o ../../complete-migration.sql

# Push to Git
git add .
git commit -m "Add new migration"
git push

# On production server
cd ~/kelli.photo
git pull
PGPASSWORD='!kelliphoto13!' psql -h 142.4.216.160 -p 15432 -U kelli_photo_app -d kelli_photo -f complete-migration.sql
docker restart kelliphoto-web
```

## Need Help?

1. Read: `APPLY_MIGRATIONS_GUIDE.md` for detailed instructions
2. Read: `DEPLOYMENT_FIX_SUMMARY.md` for technical details
3. Run: `./scripts/verify-deployment.sh` to diagnose issues

---

**TL;DR:** Run the SQL file with psql, restart Docker container, done! 🎉
