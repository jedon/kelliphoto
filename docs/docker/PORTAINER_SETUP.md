# Deploying Kelli Photo in Portainer

## Prerequisites

1. **Docker Hub Image**: The image should be built and pushed to Docker Hub
   - Image name: `YOUR_DOCKERHUB_USERNAME/kelliphoto-web:latest`
   - Update `docker-compose.yml` with your Docker Hub username

2. **GitHub Actions**: Automatically builds and pushes on push to main/master
   - Set up secrets in GitHub: `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN`

## Option 1: Deploy as a Stack in Portainer (Recommended)

1. **Update docker-compose.yml:**
   - Replace `YOUR_DOCKERHUB_USERNAME` with your actual Docker Hub username
   - Or use the image tag you want (e.g., `latest`, `main`, or a version tag)

2. **In Portainer UI:**
   - Go to **Stacks** → **Add Stack**
   - Name: `kelliphoto`
   - Build method: **Web editor**

3. **Copy the docker-compose.yml content:**
   - Copy the contents of `docker/docker-compose.yml`
   - Paste into the web editor in Portainer
   - Make sure to update the image name with your Docker Hub username

4. **Deploy the stack:**
   - Click **Deploy the stack**
   - Portainer will pull the image from Docker Hub and start both services

5. **Run database migrations:**
   - Go to **Containers** → find `kelliphoto-web`
   - Click **Console** or use **Exec**
   - Run:
     ```bash
     dotnet ef database update
     ```

## Option 2: Build Image Locally in Portainer

If you prefer to build locally instead of using Docker Hub:

1. **Upload project files:**
   - Upload the entire `kelli.photo` folder to your server
   - Note the path where you uploaded it

2. **Update docker-compose.yml:**
   - Uncomment the `build:` section
   - Comment out the `image:` line
   - Update the `context:` path if needed

3. **Deploy in Portainer** (same as Option 1)

## GitHub Actions Setup

The `.github/workflows/docker-build.yml` will automatically:
- Build the Docker image on push to main/master
- Push to Docker Hub
- Tag with branch name, SHA, and latest

**Required GitHub Secrets:**
- `DOCKERHUB_USERNAME`: Your Docker Hub username
- `DOCKERHUB_TOKEN`: Your Docker Hub access token (create at https://hub.docker.com/settings/security)

## Post-Deployment

### Update iptables rule (on Proxmox host):
```bash
# Verify the rule exists for port 5432 → 15432
sudo iptables -t nat -L PREROUTING -v --line-numbers | grep 5432

# If not, add it:
sudo iptables -t nat -A PREROUTING -p tcp --dport 5432 -j DNAT --to-destination 192.168.10.150:15432

# Make persistent:
sudo apt-get install -y iptables-persistent
sudo netfilter-persistent save
```

### Access the application:
- Web UI: `http://your-server:8080`
- PostgreSQL (external): `postgres.darklingdesign.com:5432`
- PostgreSQL (internal): `192.168.10.150:15432`

## Portainer Tips

- **View logs**: Stacks → kelliphoto → click on service → Logs
- **Restart services**: Stacks → kelliphoto → click service → Restart
- **Update stack**: Stacks → kelliphoto → Editor → Update the stack
- **Pull latest image**: Containers → kelliphoto-web → Recreate (will pull latest)

## Updating the Application

1. **Push code to GitHub** (triggers GitHub Actions)
2. **Wait for build to complete** (check GitHub Actions tab)
3. **In Portainer**: Containers → kelliphoto-web → Recreate
   - This will pull the latest image from Docker Hub

## Troubleshooting

- **Image not found**: Check Docker Hub username in docker-compose.yml
- **Build fails**: Check GitHub Actions logs
- **Database connection fails**: Verify the postgres service is healthy
- **Port conflicts**: Change port mappings in docker-compose.yml if 8080/15432 are in use
