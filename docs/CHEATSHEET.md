# 🚀 kelli.photo Database Fix - Cheat Sheet

## THE FIX (Copy This)

```bash
cd ~/kelli.photo
PGPASSWORD='!kelliphoto13!' psql -h 142.4.216.160 -p 15432 -U kelli_photo_app -d kelli_photo -f complete-migration.sql
docker restart kelliphoto-web
```

## Files to Copy to Server

- `complete-migration.sql` (required)
- `quick-fix-database.sh` (optional, convenience)
- `verify-deployment.sh` (optional, for testing)

## SCP Command

```bash
scp complete-migration.sql your-server:~/kelli.photo/
```

## Verify It Worked

```bash
# Check logs
docker logs kelliphoto-web | tail -20

# Check tables
PGPASSWORD='!kelliphoto13!' psql -h 142.4.216.160 -p 15432 -U kelli_photo_app -d kelli_photo -c "\dt"

# Check migrations
PGPASSWORD='!kelliphoto13!' psql -h 142.4.216.160 -p 15432 -U kelli_photo_app -d kelli_photo -c "SELECT * FROM \"__EFMigrationsHistory\";"
```

## Common Commands

```bash
# Restart container
docker restart kelliphoto-web

# View logs
docker logs -f kelliphoto-web

# Check running containers
docker ps

# Stop container
docker stop kelliphoto-web

# Start container
docker start kelliphoto-web

# Full restart
docker-compose down && docker-compose up -d
```

## Database Connection String

```
Host=142.4.216.160;Port=15432;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!
```

## Troubleshooting One-Liners

```bash
# Install psql
sudo apt-get update && sudo apt-get install postgresql-client

# Check iptables
sudo iptables -L INPUT -n | grep 15432

# Re-apply iptables
cd ~/kelli.photo && sudo bash scripts/docker/iptables-setup.sh

# Check PostgreSQL is running
docker ps | grep postgres

# Restart PostgreSQL
docker restart kelliphoto-postgres

# Access database directly
PGPASSWORD='!kelliphoto13!' psql -h 142.4.216.160 -p 15432 -U kelli_photo_app -d kelli_photo
```

## URLs

- Production: https://kelli.photo
- Login: https://kelli.photo/Identity/Account/Login
- Admin: https://kelli.photo/admin

## Admin Account

- Email defaults to `admin@kelliphoto.com` (override with `Admin__Email`)
- Password must be set via `Admin__Password` in `.env` (or env / secrets). There is no committed default password; seeding skips if unset outside Testing.

## Identity / users

- `Identity:AllowRegistration` in `appsettings.json` (default `false`) — when false, Register is hidden and `/Identity/Account/Register` redirects to Login; create users in **Admin → Users**.
- Development (`appsettings.Development.json`) sets `AllowRegistration: true` for local public sign-up testing.
- Roles: `User` (regular accounts), `Admin` (gallery admin + Users tab). Seeded admin: `Admin:Email` / `Admin:Password`.

## File Quick Reference

| Read This First | For Details | For Help |
|----------------|-------------|----------|
| FIX_SUMMARY.md | DEPLOYMENT_FIX_SUMMARY.md | APPLY_MIGRATIONS_GUIDE.md |
| DATABASE_FIX_README.md | FIX_DEPLOYED_SITE.md | verify-deployment.sh |

## The Error You're Fixing

```
Npgsql.PostgresException: 42P01: relation "Folders" does not exist
```

**Cause:** Database tables not created  
**Fix:** Run complete-migration.sql  
**Time:** 30 seconds

---

**Remember:** The SQL file is idempotent (safe to run multiple times)
