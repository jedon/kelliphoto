# Deploy Kelli Photo in Portainer - Quick Guide

## Step-by-Step Deployment

### 1. Access Portainer

Open your Portainer interface (usually at `http://your-server:9000` or `http://your-server:9443`)

### 2. Create a New Stack

1. In Portainer, go to **Stacks** (left sidebar)
2. Click **Add stack** button
3. Name: `kelliphoto`

### 3. Copy docker-compose.yml

1. Open `docker/docker-compose.yml` from your local project
2. Copy the entire contents
3. Paste into Portainer's **Web editor** tab

### 4. Verify Configuration

Make sure the docker-compose.yml has:
- Image: `jedon/kelliphoto-web:latest` ✅
- PostgreSQL on port 15432 ✅
- Web app on ports 8888 (HTTP) and 8444 (HTTPS) ✅
- Gallery path: `/mnt/gallery` ✅

### 5. Deploy the Stack

1. Click **Deploy the stack** button
2. Wait for Portainer to:
   - Pull the `jedon/kelliphoto-web:latest` image from Docker Hub
   - Pull the `postgres:15` image
   - Create the network
   - Start both containers

### 6. Run Database Migrations

Once the stack is running:

1. Go to **Containers** (left sidebar)
2. Find `kelliphoto-web` container
3. Click on it to open details
4. Click **Console** tab (or use **Exec**)
5. Run:
   ```bash
   dotnet ef database update
   ```

### 7. Verify Everything is Running

1. **Check containers:**
   - Go to **Containers**
   - Both `kelliphoto-postgres` and `kelliphoto-web` should show as "Running"

2. **Check logs:**
   - Click on `kelliphoto-web` → **Logs** tab
   - Should see ASP.NET Core startup messages
   - Look for: "Now listening on: http://[::]:80"

3. **Access the application:**
   - Open browser: `http://your-server:8080`
   - You should see the gallery interface

### 8. Create Admin User (First Time)

1. Navigate to: `http://your-server:8080/Identity/Account/Register`
2. Create your admin account
3. Or use the console to create via command line (if you add that feature)

## Troubleshooting

### Container won't start
- Check **Logs** tab for errors
- Verify the image exists: `jedon/kelliphoto-web:latest` on Docker Hub
- Check port conflicts (8080, 15432)

### Database connection fails
- Verify `kelliphoto-postgres` container is running
- Check logs: `kelliphoto-postgres` → **Logs**
- Ensure health check passed

### Gallery not loading
- Verify `/mnt/gallery` is accessible on the host
- Check volume mount in docker-compose.yml
- Review application logs for file access errors

### Can't access from outside
- Check firewall rules
- Verify iptables port forwarding (if using Proxmox)
- Ensure ports 8080/8443 are accessible

## Updating the Application

When you push new code and GitHub Actions builds a new image:

1. Go to **Stacks** → `kelliphoto`
2. Click **Editor** tab
3. Click **Update the stack** (this will pull the latest image)
4. Or manually recreate the `kelliphoto-web` container:
   - **Containers** → `kelliphoto-web` → **Recreate** (check "Pull latest image")

## Useful Portainer Features

- **Logs**: View real-time container logs
- **Stats**: Monitor CPU, memory, network usage
- **Console**: Execute commands inside containers
- **Editor**: Update stack configuration
- **Environment**: View/Edit environment variables
