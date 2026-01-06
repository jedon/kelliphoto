# Replacing PhotoPrism with Kelli Photo Gallery

## Migration Steps

### 1. Backup Current Configuration

```bash
# Backup the existing PhotoPrism config
sudo cp /etc/nginx/sites-available/kelli.photo /etc/nginx/sites-available/kelli.photo.photoprism.backup
```

### 2. Stop PhotoPrism (Optional - if you want to keep it running temporarily)

```bash
# If PhotoPrism is running in Docker
docker stop <photoprism-container-name>
# Or if using docker-compose
cd /path/to/photoprism
docker-compose down
```

### 3. Deploy Kelli Photo Gallery

Follow the Portainer deployment steps in `DEPLOY_PORTAINER.md` to deploy the new stack.

### 4. Update Nginx Configuration

```bash
# Copy the new configuration
sudo cp docker/nginx/kelli.photo.conf /etc/nginx/sites-available/kelli.photo

# Test the configuration
sudo nginx -t

# If test passes, reload nginx
sudo systemctl reload nginx
```

### 5. Verify the New Application

1. **Check containers are running:**
   ```bash
   docker ps | grep kelliphoto
   ```

2. **Access the application:**
   - Open: `https://kelli.photo`
   - Should see the new Kelli Photo Gallery interface

3. **Check logs if issues:**
   ```bash
   # Application logs
   docker logs kelliphoto-web
   
   # Nginx logs
   sudo tail -f /var/log/nginx/kelliphoto_error.log
   ```

### 6. Run Database Migrations

1. **Portainer** → **Containers** → `kelliphoto-web`
2. Click **Console**
3. Run: `dotnet ef database update`

### 7. Create Admin User

1. Navigate to: `https://kelli.photo/Identity/Account/Register`
2. Create your admin account

## Rollback (if needed)

If you need to rollback to PhotoPrism:

```bash
# Restore the backup
sudo cp /etc/nginx/sites-available/kelli.photo.photoprism.backup /etc/nginx/sites-available/kelli.photo

# Update the proxy_pass port back to 2342
sudo nano /etc/nginx/sites-available/kelli.photo

# Test and reload
sudo nginx -t
sudo systemctl reload nginx

# Restart PhotoPrism
docker start <photoprism-container-name>
```

## Key Changes

- **Port**: Changed from `127.0.0.1:2342` (PhotoPrism) to `192.168.10.150:8080` (Kelli Photo Gallery)
- **Domain**: Same domain (`kelli.photo`) - no DNS changes needed
- **SSL**: Uses existing Let's Encrypt certificates - no changes needed
- **WebSocket**: Configured for Blazor Server SignalR (same as PhotoPrism had)

## Differences from PhotoPrism

- **Technology**: ASP.NET Core Blazor Server instead of PhotoPrism
- **Database**: PostgreSQL instead of SQLite
- **Architecture**: Custom-built gallery vs PhotoPrism's all-in-one solution
- **Features**: Focused on gallery display vs PhotoPrism's full photo management suite
