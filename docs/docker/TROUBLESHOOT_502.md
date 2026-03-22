# Troubleshooting 502 Bad Gateway

## Quick Checks

### 1. Verify containers are running

```bash
# Check if containers are running
docker ps | grep kelliphoto

# Should show both:
# - kelliphoto-web
# - kelliphoto-postgres
```

### 2. Check web container logs

```bash
# View recent logs
docker logs kelliphoto-web --tail 50

# Follow logs in real-time
docker logs kelliphoto-web -f
```

Look for:
- Application startup errors
- Database connection errors
- Port binding issues
- "Now listening on: http://[::]:80" (should see this)

### 3. Test direct connection to container

```bash
# From the host, test if port 8080 is accessible
curl http://192.168.10.150:8080

# Or test from inside the container
docker exec kelliphoto-web curl http://localhost:80
```

### 4. Check nginx error logs

```bash
# View nginx error log
sudo tail -f /var/log/nginx/kelliphoto_error.log

# Common errors:
# - "connect() failed (111: Connection refused)"
# - "upstream prematurely closed connection"
```

### 5. Verify nginx configuration

```bash
# Test nginx config
sudo nginx -t

# Check the proxy_pass URL matches your setup
sudo grep proxy_pass /etc/nginx/sites-available/kelli.photo
# Should show: proxy_pass http://192.168.10.150:8080;
```

### 6. Check container network

```bash
# Verify container IP
docker inspect kelliphoto-web | grep IPAddress

# Test connectivity from nginx host
telnet 192.168.10.150 8080
# Or
nc -zv 192.168.10.150 8080
```

## Common Fixes

### Fix 1: Container not running
```bash
# Restart the container
docker restart kelliphoto-web

# Or recreate the stack in Portainer
```

### Fix 2: Wrong IP address in nginx
If the container has a different IP, update nginx config:
```bash
# Find container IP
docker inspect kelliphoto-web | grep IPAddress

# Update nginx config
sudo nano /etc/nginx/sites-available/kelli.photo
# Change proxy_pass to the correct IP
sudo nginx -t
sudo systemctl reload nginx
```

### Fix 3: Container crashed on startup
Check logs for errors:
```bash
docker logs kelliphoto-web
```

Common issues:
- Database connection failed
- Missing environment variables
- Port already in use

### Fix 4: Database not ready
The web container depends on PostgreSQL. Check:
```bash
# Check PostgreSQL is healthy
docker ps | grep kelliphoto-postgres

# Check PostgreSQL logs
docker logs kelliphoto-postgres

# Test database connection from web container
docker exec kelliphoto-web dotnet ef database update
```

### Fix 5: Network connectivity
If using Docker bridge network, nginx might not reach the container:
```bash
# Check if nginx can reach the container
curl http://192.168.10.150:8080

# If that works but nginx doesn't, check firewall
sudo ufw status
```

## Step-by-Step Debugging

1. **Check container status:**
   ```bash
   docker ps -a | grep kelliphoto
   ```

2. **If container is stopped, check why:**
   ```bash
   docker logs kelliphoto-web
   ```

3. **If container is running, test direct access:**
   ```bash
   curl -v http://192.168.10.150:8080
   ```

4. **Check nginx can reach it:**
   ```bash
   # From nginx host
   curl http://192.168.10.150:8080
   ```

5. **Review nginx error log:**
   ```bash
   sudo tail -20 /var/log/nginx/kelliphoto_error.log
   ```
