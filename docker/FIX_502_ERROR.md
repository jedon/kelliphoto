# Fix 502 Bad Gateway Error

## Problem Identified

1. **Database migrations not run** - Tables don't exist yet
2. **Nginx proxy_pass using wrong IP** - Should use `127.0.0.1:8080` since port is mapped to host

## Solution

### Step 1: Run Database Migrations

The container is running but the database tables don't exist. Run migrations:

**In Portainer:**
1. Go to **Containers** → `kelliphoto-web`
2. Click **Console**
3. Run:
   ```bash
   dotnet ef database update
   ```

**Or from command line:**
```bash
docker exec -it kelliphoto-web dotnet ef database update
```

### Step 2: Fix Nginx Configuration

The nginx config is trying to connect to `192.168.10.150:8080`, but since the port is mapped to the host, use `127.0.0.1:8080`:

```bash
# Update nginx config
sudo nano /etc/nginx/sites-available/kelli.photo

# Change all instances of:
# proxy_pass http://192.168.10.150:8080;
# To:
# proxy_pass http://127.0.0.1:8080;

# Or use the updated config file:
sudo cp docker/nginx/kelli.photo.conf /etc/nginx/sites-available/kelli.photo

# Test and reload
sudo nginx -t
sudo systemctl reload nginx
```

### Step 3: Verify Application is Running

After running migrations, check logs:
```bash
docker logs kelliphoto-web --tail 20
```

You should see:
- "Now listening on: http://[::]:80"
- No database errors

### Step 4: Test Connection

```bash
# Test direct access
curl http://127.0.0.1:8080

# Should return HTML (not connection refused)
```

## After Fixes

1. **Run migrations** → Creates database tables
2. **Fix nginx config** → Points to correct address
3. **Reload nginx** → Applies changes
4. **Access site** → `https://kelli.photo` should work

## Verify Everything Works

```bash
# 1. Check containers
docker ps | grep kelliphoto

# 2. Check application logs (should show "Now listening")
docker logs kelliphoto-web | grep "listening"

# 3. Test direct access
curl http://127.0.0.1:8080

# 4. Check nginx can reach it
curl http://127.0.0.1:8080 -H "Host: kelli.photo"
```
