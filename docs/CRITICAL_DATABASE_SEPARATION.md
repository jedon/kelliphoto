# 🚨 CRITICAL: Database Configuration Issue

## THE PROBLEM

**Your local development and production deployment are using THE SAME DATABASE!**

### Current Setup (WRONG):

- **Local Dev**: `postgres.darklingdesign.com:5444` → `kelli_photo` database
- **Production**: `postgres.darklingdesign.com:5444` (via 142.4.216.160:15432) → `kelli_photo` database

**This is EXTREMELY DANGEROUS because:**

1. ❌ Local development changes affect production
2. ❌ Production data appears in your dev environment
3. ❌ If you drop tables locally, production breaks
4. ❌ Can't test destructive operations safely
5. ❌ No isolation between environments
6. ❌ Risk of data corruption or loss

## THE SOLUTION

You need **separate databases** for each environment:

### Recommended Setup:

```
Local Dev     → postgres.darklingdesign.com:5444 → kelli_photo_dev
Production    → 142.4.216.160:15432 (Docker)    → kelli_photo_prod
```

Or even better:

```
Local Dev     → localhost:5432 (Local PostgreSQL) → kelli_photo_dev
Production    → Docker PostgreSQL container       → kelli_photo_prod
```

---

## IMMEDIATE FIX

### Step 1: Create Separate Databases on Your Server

SSH to your PostgreSQL server and create a production database:

```bash
# Connect to PostgreSQL server
ssh postgres.darklingdesign.com  # or however you access it

# Create production database
sudo -u postgres psql

# In PostgreSQL:
CREATE DATABASE kelli_photo_prod;
GRANT ALL PRIVILEGES ON DATABASE kelli_photo_prod TO kelli_photo_app;
\q
```

### Step 2: Rename Your Current Database (For Safety)

```bash
# Rename the existing database to _dev
sudo -u postgres psql
ALTER DATABASE kelli_photo RENAME TO kelli_photo_dev;
\q
```

### Step 3: Update Configuration Files

#### Local Development (appsettings.Development.json):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres.darklingdesign.com;Port=5444;Database=kelli_photo_dev;Username=kelli_photo_app;Password=!kelliphoto13!;SSL Mode=Prefer;Timeout=30;Command Timeout=30;Pooling=true;MinPoolSize=0;MaxPoolSize=100"
  },
  "GallerySettings": {
    "GalleryPath": "\\\\darklingnas\\Kelli\\kelli.photo",
    "ThumbnailPath": "\\\\darklingnas\\Kelli\\kelli.photo\\.thumbnails"
  }
}
```

#### Production - Option A: Use Docker's Built-in PostgreSQL (RECOMMENDED)

**Update docker-compose.yml** to use a separate database:

```yaml
services:
  postgres:
    image: postgres:15
    container_name: kelliphoto-postgres
    environment:
      POSTGRES_DB: kelli_photo_prod
      POSTGRES_USER: kelli_photo_prod_user
      POSTGRES_PASSWORD: !kelliphoto_prod_2025!
    volumes:
      - kelliphoto_postgres_data:/var/lib/postgresql/data
    ports:
      - "15432:5432"
    networks:
      - kelliphoto-network

  web:
    image: jedon/kelliphoto-web:latest
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=kelli_photo_prod;Username=kelli_photo_prod_user;Password=!kelliphoto_prod_2025!
```

#### Production - Option B: Use External PostgreSQL But Different Database

Keep using `postgres.darklingdesign.com` but use a different database name:

```yaml
web:
  environment:
    - ConnectionStrings__DefaultConnection=Host=142.4.216.160;Port=15432;Database=kelli_photo_prod;Username=kelli_photo_app;Password=!kelliphoto13!
```

---

## RECOMMENDED: Option A (Separate Docker PostgreSQL)

This is the cleanest separation:

1. **Local Dev** → Your existing `postgres.darklingdesign.com:5444` → `kelli_photo_dev`
2. **Production** → Docker container PostgreSQL → `kelli_photo_prod`

### Why This Is Better:

✅ Complete isolation - no way to accidentally affect production from dev  
✅ Production is self-contained in Docker  
✅ Easy backups and restore  
✅ Can test with different PostgreSQL versions  
✅ Matches Docker best practices  

### Implementation:

Your `docker-compose.yml` **already has this setup**! You just need to:

1. Make sure production connects to the Docker PostgreSQL, not external
2. Apply migrations to the Docker PostgreSQL
3. Update your local to use `_dev` database

---

## UPDATED FIX INSTRUCTIONS

Since your production should use the **Docker PostgreSQL container** (which is already configured in docker-compose.yml), here's what to do:

### 1. Fix Local Development Configuration

**Edit: `src/KelliPhoto.Web/appsettings.Development.json`**

Change database name from `kelli_photo` to `kelli_photo_dev`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres.darklingdesign.com;Port=5444;Database=kelli_photo_dev;Username=kelli_photo_app;Password=!kelliphoto13!;SSL Mode=Prefer;Timeout=30;Command Timeout=30;Pooling=true;MinPoolSize=0;MaxPoolSize=100"
  }
}
```

### 2. Rename Existing Database

```bash
# On postgres.darklingdesign.com
sudo -u postgres psql
ALTER DATABASE kelli_photo RENAME TO kelli_photo_dev;
\q
```

### 3. Apply Migrations to LOCAL Dev Database

```bash
# On your local machine
cd G:\Programming\kelli.photo\src\KelliPhoto.Web
dotnet ef database update
```

### 4. Apply Migrations to PRODUCTION Docker Database

**Update the migration SQL for the production database name:**

Edit `complete-migration.sql` if needed, or just run as-is since the Docker PostgreSQL uses `kelli_photo` as configured in docker-compose.yml.

**On production server:**

```bash
# Connect to the Docker PostgreSQL (not external)
cd ~/kelli.photo
docker exec -i kelliphoto-postgres psql -U kelli_photo_app -d kelli_photo < complete-migration.sql

# Or use the external access:
PGPASSWORD='!kelliphoto13!' psql \
  -h 142.4.216.160 \
  -p 15432 \
  -U kelli_photo_app \
  -d kelli_photo \
  -f complete-migration.sql

# Restart web container
docker restart kelliphoto-web
```

---

## VERIFICATION

### Check Local Uses Dev Database:

```bash
# On your local machine
cd src/KelliPhoto.Web
dotnet run
# Check logs - should connect to kelli_photo_dev
```

### Check Production Uses Docker PostgreSQL:

```bash
# On production server
docker logs kelliphoto-web | grep "Host=postgres"
# Should show: Host=postgres;Port=5432;Database=kelli_photo
```

### Verify They're Different:

```bash
# Add a test record to local
# Check it doesn't appear in production

# Or check row counts are different:
# Local:
psql -h postgres.darklingdesign.com -p 5444 -U kelli_photo_app -d kelli_photo_dev -c "SELECT COUNT(*) FROM \"Folders\";"

# Production:
PGPASSWORD='!kelliphoto13!' psql -h 142.4.216.160 -p 15432 -U kelli_photo_app -d kelli_photo -c "SELECT COUNT(*) FROM \"Folders\";"
```

---

## WHY THIS HAPPENED

Looking at your docker-compose.yml, it's **correctly configured** to use a Docker PostgreSQL container:

```yaml
postgres:
  image: postgres:15
  environment:
    POSTGRES_DB: kelli_photo
```

But somewhere the connection got mixed up, possibly because:

1. The external PostgreSQL access was set up (port 15432 on 142.4.216.160)
2. Both local and production pointed to the same underlying server
3. The Docker network wasn't properly isolated

---

## ACTION ITEMS

- [ ] **URGENT**: Rename current shared database to `kelli_photo_dev`
- [ ] Update `appsettings.Development.json` to use `kelli_photo_dev`
- [ ] Verify production docker-compose.yml uses internal `postgres` container
- [ ] Apply migrations to local dev database
- [ ] Apply migrations to production Docker database
- [ ] Test both environments are isolated
- [ ] Document this in deployment guide
- [ ] Consider adding environment indicators to UI (dev vs prod banner)

---

## LONG-TERM: Environment Strategy

### Future Setup:

```
┌─────────────────────────────────────────────────┐
│ Local Dev                                       │
│ └─ postgres.darklingdesign.com:5444            │
│    └─ kelli_photo_dev                          │
│    └─ Small dataset for testing                │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│ Production (kelli.photo)                        │
│ └─ Docker Container: kelliphoto-postgres       │
│    └─ kelli_photo_prod                         │
│    └─ Real user data                           │
│ └─ Automated backups                           │
│ └─ No external access (security)               │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│ Optional: Staging Environment                   │
│ └─ staging.kelli.photo                         │
│ └─ kelli_photo_staging                         │
│ └─ Copy of production data for testing         │
└─────────────────────────────────────────────────┘
```

---

**CRITICAL**: Do NOT apply the migration to production until you've separated the databases. Otherwise, you might create tables in a database that's still shared with dev!
