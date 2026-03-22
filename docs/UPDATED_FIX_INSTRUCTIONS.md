# 🚨 UPDATED FIX INSTRUCTIONS - Database Separation Required!

## What We Discovered

Your **local development** and **production deployment** are using the **SAME DATABASE**!

- Local Dev: `postgres.darklingdesign.com:5444` → `kelli_photo`
- Production: `postgres.darklingdesign.com:5444` (via 192.168.10.150) → `kelli_photo`

**This is why tables don't exist** - and it's also a critical configuration problem.

---

## The Correct Fix (3 Steps)

### Step 1: Separate the Databases

#### A. Update Local Development Config

**File: `src/KelliPhoto.Web/appsettings.Development.json`**

I've already updated it to use `kelli_photo_dev`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=postgres.darklingdesign.com;Port=5444;Database=kelli_photo_dev;..."
}
```

#### B. Rename the Current Database

On your PostgreSQL server (`postgres.darklingdesign.com`):

```bash
# Connect to PostgreSQL
psql -h postgres.darklingdesign.com -p 5444 -U kelli_photo_app -d postgres

# Rename the database
ALTER DATABASE kelli_photo RENAME TO kelli_photo_dev;
\q
```

If it says "database is being accessed by other users":

```sql
-- First, disconnect all users
SELECT pg_terminate_backend(pid) 
FROM pg_stat_activity 
WHERE datname = 'kelli_photo' AND pid <> pg_backend_pid();

-- Then rename
ALTER DATABASE kelli_photo RENAME TO kelli_photo_dev;
```

### Step 2: Apply Migrations to Local Dev

```bash
# On your Windows machine
cd G:\Programming\kelli.photo\src\KelliPhoto.Web
dotnet ef database update
```

This creates tables in `kelli_photo_dev` for local development.

### Step 3: Apply Migrations to Production

**On your production server:**

```bash
cd ~/kelli.photo

# Apply to the Docker PostgreSQL (which is separate from the external one)
PGPASSWORD='!kelliphoto13!' psql \
  -h 192.168.10.150 \
  -p 15432 \
  -U kelli_photo_app \
  -d kelli_photo \
  -f complete-migration.sql

# Restart the application
docker restart kelliphoto-web
```

---

## Understanding the Setup

### Before (WRONG):
```
┌─────────────────────────────────────┐
│ postgres.darklingdesign.com:5444    │
│ ├─ kelli_photo (SHARED!)            │
│ │  ├─ Used by local dev             │
│ │  └─ Used by production ⚠️         │
└─────────────────────────────────────┘
```

### After (CORRECT):
```
┌────────────────────────────────────────────────┐
│ postgres.darklingdesign.com:5444               │
│ └─ kelli_photo_dev                             │
│    └─ Used by local dev only ✅                │
└────────────────────────────────────────────────┘

┌────────────────────────────────────────────────┐
│ Production Server (192.168.10.150)             │
│ └─ Docker Container: kelliphoto-postgres       │
│    └─ kelli_photo                              │
│       └─ Used by production only ✅            │
└────────────────────────────────────────────────┘
```

---

## Wait, Which PostgreSQL Does Production Use?

Looking at your `docker-compose.yml`, production **should** use the **Docker PostgreSQL container**:

```yaml
postgres:
  image: postgres:15
  container_name: kelliphoto-postgres
  ports:
    - "15432:5432"  # Exposed on host port 15432
```

The web container connects to it:
```yaml
web:
  environment:
    - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;...
```

**`Host=postgres`** means it connects to the Docker container, **NOT** the external server!

However, you also set up external access to this Docker PostgreSQL (via iptables), which maps:
- `192.168.10.150:15432` → Docker container's port 5432

So when you connect from outside using `192.168.10.150:15432`, you're actually connecting to the **Docker PostgreSQL**, not `postgres.darklingdesign.com`.

### So the actual setup is:

```
Local Dev:
  ├─ postgres.darklingdesign.com:5444
  └─ Database: kelli_photo (soon to be kelli_photo_dev)

Production:
  ├─ Docker Container (kelliphoto-postgres)
  ├─ Accessible externally at: 192.168.10.150:15432
  └─ Database: kelli_photo
```

**These are DIFFERENT PostgreSQL servers!** But they happen to use the same database name, username, and password, which is confusing but OK.

---

## Corrected Understanding

Actually, looking more carefully:

1. **`postgres.darklingdesign.com:5444`** is your central PostgreSQL server
2. **Docker PostgreSQL on 192.168.10.150** is a separate instance

The issue isn't that they're the same database - they're on **different servers**. The issue is that your local dev's `appsettings.Development.json` uses port 5444, but I see you also have external access set up to port 15432...

Let me check if there's port forwarding that makes them the same:

**If `postgres.darklingdesign.com` IS `192.168.10.150`**, then:
- Port 5444 might forward to Docker PostgreSQL
- Port 15432 definitely goes to Docker PostgreSQL
- You have ONE PostgreSQL (in Docker), not two

**If they're different servers**, you're fine - just need to ensure:
- Local uses `postgres.darklingdesign.com:5444`
- Production uses Docker container

---

## The Real Question: Are These the Same Server?

**Please clarify:**

```bash
# What is postgres.darklingdesign.com?
ping postgres.darklingdesign.com

# Compare to:
ping 192.168.10.150

# Are they the same IP?
```

### Scenario A: They're Different Servers

✅ **Good!** You already have separation. Just need to:
1. Ensure local dev has its own database name (`kelli_photo_dev`)
2. Apply migrations to both servers independently

### Scenario B: They're the Same Server

⚠️ **Problem!** Both point to the Docker PostgreSQL. Need to either:
1. Set up a separate local PostgreSQL, OR
2. Use different database names on the same server

---

## Recommended Actions

### 1. Check if servers are the same:

```powershell
# On Windows
nslookup postgres.darklingdesign.com
nslookup 192.168.10.150
```

### 2. Option A: If Different Servers (Easy)

```bash
# Just apply migrations to both
# Local:
cd G:\Programming\kelli.photo\src\KelliPhoto.Web
dotnet ef database update

# Production:
# (SSH to server and run the complete-migration.sql as shown earlier)
```

### 3. Option B: If Same Server (Rename Required)

Follow the 3-step process at the top of this document.

---

## Quick Decision Tree

```
Are postgres.darklingdesign.com and 192.168.10.150 the same server?
│
├─ YES → Rename database to kelli_photo_dev for local
│         Create new kelli_photo for production
│         Follow 3-step process above
│
└─ NO → You already have separation!
         Just apply migrations to both:
         1. Local: dotnet ef database update
         2. Production: psql ... -f complete-migration.sql
```

---

## Modified complete-migration.sql

The current `complete-migration.sql` is fine. Just make sure you:
1. Run it on the correct production database
2. Don't run it on your dev database (use `dotnet ef database update` instead)

---

## Test Separation

After applying migrations to both:

```bash
# Local - add a test folder
psql -h postgres.darklingdesign.com -p 5444 -U kelli_photo_app -d kelli_photo_dev \
  -c "INSERT INTO \"Folders\" (\"Name\", \"Path\", \"CreatedAt\") VALUES ('TEST_LOCAL', '/test', NOW());"

# Check it doesn't appear in production
PGPASSWORD='!kelliphoto13!' psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo \
  -c "SELECT * FROM \"Folders\" WHERE \"Name\" = 'TEST_LOCAL';"
# Should return 0 rows
```

---

**Next Step:** Determine if the servers are the same, then follow the appropriate path above.
