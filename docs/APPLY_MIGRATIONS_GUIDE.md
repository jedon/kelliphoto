# How to Apply Database Migrations to Production

## Problem

The website shows this error:
```
42P01: relation "Folders" does not exist
```

This means the database tables haven't been created on the production server.

## Quick Fix (Easiest Method)

**If you have SSH access to your server:**

1. **SSH to your server** and navigate to where you want the files:
   ```bash
   ssh your-server
   mkdir -p ~/kelli-photo-migration
   cd ~/kelli-photo-migration
   ```

2. **Copy the SQL file** (from your local machine in another terminal):
   ```bash
   # From your local machine
   scp complete-migration.sql your-server:~/kelli-photo-migration/
   ```

3. **Run the migration** (back on the server):
   ```bash
   # Apply the migration
   PGPASSWORD='!kelliphoto13!' psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo -f complete-migration.sql
   
   # Restart the web container
   docker restart kelliphoto-web
   
   # Watch the logs
   docker logs -f kelliphoto-web
   ```

4. **Test**: Visit https://kelli.photo - it should work now!

---

## Detailed Solution with Helper Script

If you prefer a more automated approach:

### Step 1: Get Files on Server

**Option A - Using Git (Recommended):**
```bash
# On the server
cd /path/to/kelli.photo
git pull
```

**Option B - Copy files manually:**
```bash
# From your local machine
scp complete-migration.sql scripts/apply-migration-to-server.sh your-server:/path/to/kelli.photo/
```

### Step 2: Run the Migration Script

SSH into your **production server** and run:

```bash
cd /path/to/kelli.photo
chmod +x scripts/apply-migration-to-server.sh
./scripts/apply-migration-to-server.sh
```

The script will:
- Test the database connection
- Apply both migrations (InitialCreate and AddFolderThumbnailPhotoId)
- Show you the results
- Skip migrations that are already applied (safe to run multiple times)

### Step 3: Restart the Web Container

After migrations are applied:

```bash
docker restart kelliphoto-web
```

### Step 4: Verify

1. Check the logs:
   ```bash
   docker logs -f kelliphoto-web
   ```
   
   You should see:
   - No more "relation does not exist" errors
   - "Starting catalog scan..." message
   - "Scanned X folders" and "Total photos: Y" messages

2. Visit https://kelli.photo - the site should now work!

## Alternative: Manual Application

If you prefer to run the SQL directly:

```bash
# On the server
psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo -f complete-migration.sql
# Enter password when prompted: !kelliphoto13!
```

Then restart:
```bash
docker restart kelliphoto-web
```

## Troubleshooting

### "psql: command not found"

Install PostgreSQL client:
```bash
sudo apt-get update
sudo apt-get install postgresql-client
```

### "Connection refused" or "timeout"

1. Check PostgreSQL is running:
   ```bash
   docker ps | grep postgres
   ```

2. Check iptables rules:
   ```bash
   sudo iptables -L INPUT -n | grep 15432
   ```
   
   Should show:
   ```
   ACCEPT     tcp  --  192.168.10.0/24     0.0.0.0/0            tcp dpt:15432
   ```

3. Re-run the iptables setup if needed:
   ```bash
   cd /path/to/kelli.photo/docker
   sudo bash scripts/docker/iptables-setup.sh
   ```

### "Authentication failed"

Verify the password in `docker/docker-compose.yml` matches what you're using.

### "Migration already applied"

That's OK! The script is idempotent - it safely skips already-applied migrations.

## Future Migrations

Whenever you add new migrations to the codebase:

1. **Generate the SQL** (on your dev machine):
   ```bash
   cd src/KelliPhoto.Web
   dotnet ef migrations script -o ../../complete-migration.sql
   ```

2. **Copy to server and run**:
   ```bash
   scp complete-migration.sql your-server:/path/to/kelli.photo/
   ssh your-server
   cd /path/to/kelli.photo
   ./scripts/apply-migration-to-server.sh
   ```

3. **Restart**:
   ```bash
   docker restart kelliphoto-web
   ```

## Why This is Needed

The Docker runtime image (`mcr.microsoft.com/dotnet/aspnet:10.0`) doesn't include the .NET SDK or EF Core tools, so we can't run `dotnet ef database update` inside the container. Instead, we:

1. Generate SQL scripts from the migrations
2. Apply them directly to PostgreSQL using `psql`

This is a common and reliable approach for production deployments.
