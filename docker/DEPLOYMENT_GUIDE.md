# Complete Deployment Guide - Kelli Photo Gallery

## Prerequisites

- Proxmox host with Debian VM
- Docker and Portainer installed
- Nginx installed (for reverse proxy)
- Domain name pointing to your server (optional, for SSL)

## Step 1: iptables Port Forwarding

### Option A: Run the setup script

```bash
cd docker
chmod +x iptables-setup.sh
sudo ./iptables-setup.sh
```

### Option B: Manual setup

```bash
# PostgreSQL port forwarding
sudo iptables -t nat -A PREROUTING -p tcp --dport 5432 -j DNAT --to-destination 192.168.10.150:15432

# Make persistent
sudo apt-get install -y iptables-persistent
sudo netfilter-persistent save
```

### Verify rules

```bash
sudo iptables -t nat -L PREROUTING -v --line-numbers | grep -E "5432|8080|8443"
```

## Step 2: Deploy in Portainer

1. **Portainer** → **Stacks** → **Add stack**
2. Name: `kelliphoto`
3. Copy contents of `docker/docker-compose.yml`
4. Paste and **Deploy the stack**
5. Wait for containers to start

## Step 3: Run Database Migrations

1. **Portainer** → **Containers** → `kelliphoto-web`
2. Click **Console**
3. Run: `dotnet ef database update`

## Step 4: Nginx Reverse Proxy Setup

### Option A: HTTP Only (for testing)

```bash
# Copy HTTP-only config
sudo cp docker/nginx/kelliphoto-http-only.conf /etc/nginx/sites-available/kelliphoto.darklingdesign.com

# Create symlink
sudo ln -s /etc/nginx/sites-available/kelliphoto.darklingdesign.com /etc/nginx/sites-enabled/

# Test and reload
sudo nginx -t
sudo systemctl reload nginx
```

### Option B: HTTPS with SSL (production)

1. **Install SSL certificate** (Let's Encrypt):
   ```bash
   sudo apt-get install certbot python3-certbot-nginx
   sudo certbot --nginx -d kelliphoto.darklingdesign.com
   ```

2. **Copy HTTPS config**:
   ```bash
   sudo cp docker/nginx/kelliphoto.conf /etc/nginx/sites-available/kelliphoto.darklingdesign.com
   ```

3. **Update SSL paths** in the config file:
   ```bash
   sudo nano /etc/nginx/sites-available/kelliphoto.darklingdesign.com
   # Update ssl_certificate and ssl_certificate_key paths
   ```

4. **Create symlink and reload**:
   ```bash
   sudo ln -s /etc/nginx/sites-available/kelliphoto.darklingdesign.com /etc/nginx/sites-enabled/
   sudo nginx -t
   sudo systemctl reload nginx
   ```

### Option C: Use the setup script

```bash
cd docker/nginx
chmod +x nginx-setup.sh
sudo ./nginx-setup.sh
```

## Step 5: Verify Deployment

### Check containers
- **Portainer** → **Containers**: Both `kelliphoto-web` and `kelliphoto-postgres` should be running

### Check nginx
```bash
sudo systemctl status nginx
sudo nginx -t
```

### Access the application
- **Via Nginx**: `http://kelliphoto.darklingdesign.com` or `https://kelliphoto.darklingdesign.com`
- **Direct**: `http://your-server:8080`

### Check logs
```bash
# Application logs
docker logs kelliphoto-web

# Nginx logs
sudo tail -f /var/log/nginx/kelliphoto-access.log
sudo tail -f /var/log/nginx/kelliphoto-error.log
```

## Step 6: Create Admin User

1. Navigate to: `http://kelliphoto.darklingdesign.com/Identity/Account/Register`
2. Create your admin account
3. Log in and start managing photos

## Troubleshooting

### iptables not working
- Verify rule exists: `sudo iptables -t nat -L PREROUTING -n | grep 5432`
- Check firewall: `sudo ufw status`
- Test connection: `telnet your-server-ip 5432`

### Nginx 502 Bad Gateway
- Check if web container is running: `docker ps | grep kelliphoto-web`
- Verify port 8080 is accessible: `curl http://192.168.10.150:8080`
- Check nginx error logs: `sudo tail -f /var/log/nginx/kelliphoto-error.log`

### Database connection fails
- Verify PostgreSQL container is running
- Check logs: `docker logs kelliphoto-postgres`
- Test connection: `docker exec -it kelliphoto-postgres psql -U kelli_photo_app -d kelli_photo`

### Gallery not loading
- Verify `/mnt/gallery` is mounted correctly
- Check file permissions
- Review application logs: `docker logs kelliphoto-web`

## Updating the Application

1. **Push code to GitHub** → Triggers GitHub Actions build
2. **Wait for build** → Check https://github.com/jedon/kelliphoto/actions
3. **In Portainer**: **Containers** → `kelliphoto-web` → **Recreate** (check "Pull latest image")
4. **Or update stack**: **Stacks** → `kelliphoto` → **Editor** → **Update the stack**

## Configuration Files

- **iptables**: `docker/iptables-setup.sh`
- **Nginx HTTPS**: `docker/nginx/kelliphoto.conf`
- **Nginx HTTP**: `docker/nginx/kelliphoto-http-only.conf`
- **Docker Compose**: `docker/docker-compose.yml`
