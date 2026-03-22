# 🔍 Determine Your Database Setup

## Quick Tests to Run

### Test 1: Are They the Same Server?

```powershell
# On your Windows machine
nslookup postgres.darklingdesign.com
ping 192.168.10.150
```

**Compare the IP addresses:**

- **If they're the SAME** → You have ONE PostgreSQL server (likely the Docker one)
  - ⚠️ Local and production share the same database
  - → Need to rename and separate databases
  - → Follow **CRITICAL_DATABASE_SEPARATION.md**

- **If they're DIFFERENT** → You have TWO separate PostgreSQL servers
  - ✅ Already have server separation
  - → Just need to apply migrations to both
  - → Follow **Scenario B** below

---

## Scenario A: Same Server (ONE PostgreSQL)

### Current Problem:
```
Both local dev and production point to:
  → The same PostgreSQL server
  → The same database (kelli_photo)
  → Different ports but same underlying data
```

### Fix:

1. **Rename the database for local dev:**

```bash
# On postgres.darklingdesign.com
psql -h postgres.darklingdesign.com -p 5444 -U kelli_photo_app -d postgres
ALTER DATABASE kelli_photo RENAME TO kelli_photo_dev;
\q
```

2. **I've already updated** `appsettings.Development.json` to use `kelli_photo_dev`

3. **Apply migrations to local dev:**

```bash
cd G:\Programming\kelli.photo\src\KelliPhoto.Web
dotnet ef database update
```

4. **Create new production database:**

```bash
# On the server
psql -h postgres.darklingdesign.com -p 5444 -U kelli_photo_app -d postgres
CREATE DATABASE kelli_photo;
\q
```

5. **Apply migrations to production:**

```bash
# On production server
PGPASSWORD='!kelliphoto13!' psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo -f complete-migration.sql
docker restart kelliphoto-web
```

---

## Scenario B: Different Servers (TWO PostgreSQL)

### Current Setup:
```
Local Dev:       postgres.darklingdesign.com:5444
Production:      192.168.10.150:15432 (Docker)
                 (Different servers, different PostgreSQL instances)
```

### Fix (Simpler):

1. **Apply migrations to local dev:**

```bash
cd G:\Programming\kelli.photo\src\KelliPhoto.Web
dotnet ef database update
```

2. **Apply migrations to production:**

```bash
# On production server
cd ~/kelli.photo
PGPASSWORD='!kelliphoto13!' psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo -f complete-migration.sql
docker restart kelliphoto-web
```

3. **Optionally rename local database** for clarity:

Change `appsettings.Development.json`:
```json
"DefaultConnection": "Host=postgres.darklingdesign.com;Port=5444;Database=kelli_photo_dev;..."
```

Then rename the database and run migrations:
```bash
psql -h postgres.darklingdesign.com -p 5444 -U kelli_photo_app -d postgres
ALTER DATABASE kelli_photo RENAME TO kelli_photo_dev;
\q

cd G:\Programming\kelli.photo\src\KelliPhoto.Web
dotnet ef database update
```

---

## How to Verify Separation

### Test 1: Check Connection Strings

```bash
# Local (when running locally)
cd G:\Programming\kelli.photo\src\KelliPhoto.Web
dotnet run
# Check console output for connection string
```

```bash
# Production
docker logs kelliphoto-web | grep ConnectionStrings
# or
docker exec kelliphoto-web env | grep ConnectionStrings
```

### Test 2: Create Test Data

```bash
# Create test folder in LOCAL database
# On Windows with psql installed:
$env:PGPASSWORD='!kelliphoto13!'
psql -h postgres.darklingdesign.com -p 5444 -U kelli_photo_app -d kelli_photo_dev -c "INSERT INTO \"Folders\" (\"Name\", \"Path\", \"CreatedAt\") VALUES ('LOCAL_TEST', '/local_test', NOW());"

# Check it does NOT appear in production
# On production server:
PGPASSWORD='!kelliphoto13!' psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo -c "SELECT * FROM \"Folders\" WHERE \"Name\" = 'LOCAL_TEST';"
# Should return: (0 rows)
```

### Test 3: Count Records

```bash
# Local
psql -h postgres.darklingdesign.com -p 5444 -U kelli_photo_app -d kelli_photo_dev -c "SELECT COUNT(*) FROM \"Folders\";"

# Production
PGPASSWORD='!kelliphoto13!' psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo -c "SELECT COUNT(*) FROM \"Folders\";"

# Should be different counts!
```

---

## Summary Based on Test Results

Run this and tell me the results:

```powershell
# Windows PowerShell
Write-Host "=== Server Comparison ===" -ForegroundColor Cyan
Write-Host "`nLocal PostgreSQL:"
nslookup postgres.darklingdesign.com

Write-Host "`nProduction Server:"
Test-Connection 192.168.10.150 -Count 1

Write-Host "`nAre they the same? Check the IPs above." -ForegroundColor Yellow
```

Then:

- **If IPs match** → Follow **Scenario A** (Same Server)
- **If IPs differ** → Follow **Scenario B** (Different Servers)

---

## Either Way, Here's What to Do RIGHT NOW:

1. **✅ I've updated** `appsettings.Development.json` to use `kelli_photo_dev`
   
2. **🔄 Apply migrations to production** (safe to do now):

```bash
# SSH to production server
ssh your-server
cd ~/kelli.photo

# Apply migrations
PGPASSWORD='!kelliphoto13!' psql \
  -h 192.168.10.150 \
  -p 15432 \
  -U kelli_photo_app \
  -d kelli_photo \
  -f complete-migration.sql

# Restart
docker restart kelliphoto-web

# Check logs
docker logs -f kelliphoto-web
```

3. **🔄 Fix local dev database:**

```bash
# On your Windows machine
cd G:\Programming\kelli.photo\src\KelliPhoto.Web

# Apply migrations to local dev
dotnet ef database update
```

4. **✅ Verify** both are working independently

---

## What I've Done:

✅ Updated `appsettings.Development.json` to use `kelli_photo_dev`  
✅ Created separation guide  
✅ Migration SQL is ready for production  

## What You Need to Do:

1. Determine if servers are same or different (run tests above)
2. Apply migration to production (command above)
3. Apply migration to local dev (command above)
4. Test separation (verify commands above)

---

**Bottom Line:** 

The production fix is straightforward - just run the SQL migration. The local dev needs to be pointed to its own database, which I've already configured in the code. You just need to:

1. Run migrations on production
2. Run migrations on local dev
3. Verify they're separate

Done! 🚀
