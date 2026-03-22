# Kelli Photo Gallery

A modern photo gallery website built with ASP.NET Core Blazor Server, PostgreSQL, and Docker. Designed to display and manage large photo collections (50K+ photos) with nested folder structures.

Developer-focused documentation (local setup, architecture) lives in the [docs/](docs/) folder.

## Features

- 📁 **Nested Folder Navigation**: Recursive folder structure display with breadcrumbs
- 🖼️ **Automatic Thumbnail Generation**: On-demand thumbnail generation with caching using ImageSharp
- 📸 **Photo Cataloging**: Background service to scan and index photos from gallery directory
- 🔐 **Admin Authentication**: Login system for managing and uploading photos
- 📱 **Responsive Design**: Mobile-friendly grid layout with Blazor components
- 🔍 **Lightbox Viewer**: Fullscreen photo viewing experience
- ⚡ **Performance Optimized**: Lazy loading, pagination, and thumbnail caching for 50K+ photos

## Technology Stack

- **.NET 10.0** with Blazor Server
- **PostgreSQL 15** for data storage
- **Entity Framework Core** for database access
- **ImageSharp** for image processing and thumbnails
- **Docker** for containerization
- **Portainer** for container management

## Prerequisites

- .NET 10.0 SDK (for local development)
- Docker and Docker Compose
- PostgreSQL (or use Docker Compose)
- Portainer (optional, for GUI management)

## Quick Start

### Development Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/jedon/kelliphoto.git
   cd kelliphoto
   ```

2. **Configure settings:**
   - Copy `src/KelliPhoto.Web/.env.example` to `src/KelliPhoto.Web/.env` and set `ConnectionStrings__DefaultConnection` and `Email__SmtpPassword` (see [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)).
   - Set `GallerySettings` in `appsettings.Development.json` (or override with environment variables) for your gallery paths.

3. **Run database migrations:**
   ```bash
   cd src/KelliPhoto.Web
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

4. **Run the application:**
   ```bash
   dotnet run --project src/KelliPhoto.Web
   ```

### Docker Deployment (Recommended)

#### Using Portainer

1. **Set up GitHub Secrets** (for automated builds):
   - Go to GitHub repo → Settings → Secrets and variables → Actions
   - Add `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN`

2. **Update docker-compose.yml:**
   - Replace `YOUR_DOCKERHUB_USERNAME` with your Docker Hub username

3. **Push to GitHub:**
   ```bash
   git push origin main
   ```
   This triggers GitHub Actions to build and push the Docker image.

4. **Deploy in Portainer:**
   - Go to **Stacks** → **Add Stack**
   - Name: `kelliphoto`
   - Copy contents of `docker/docker-compose.yml`
   - Paste and deploy

5. **Run migrations:**
   - Containers → `kelliphoto-web` → Console
   - Run: `dotnet ef database update`

#### Using Docker Compose CLI

```bash
cd docker
docker-compose up -d
docker-compose exec web dotnet ef database update
```

## Configuration

### Gallery Path

The gallery path should be mounted at `/mnt/gallery` in the container. Update the volume mount in `docker-compose.yml` if needed.

For local development, update `appsettings.Development.json`:
```json
"GallerySettings": {
  "GalleryPath": "\\\\your-network-path\\gallery",
  "ThumbnailPath": "\\\\your-network-path\\gallery\\.thumbnails"
}
```

### Database

Use a PostgreSQL connection string in `.env` as `ConnectionStrings__DefaultConnection` (see `.env.example`). Do not commit passwords to the repository.

For Docker deployment, pass the connection string via environment variables (for example `CONNECTION_STRINGS__DEFAULT_CONNECTION`); see `docker/.env.example`.

### iptables Port Forwarding (Proxmox)

If deploying on Proxmox with external access:
```bash
sudo iptables -t nat -A PREROUTING -p tcp --dport 5432 -j DNAT --to-destination 192.168.10.150:15432
sudo netfilter-persistent save
```

## Project Structure

```
kelli.photo/
├── src/
│   └── KelliPhoto.Web/          # Blazor Server application
│       ├── Components/          # Blazor components (FolderBrowser, PhotoGrid, etc.)
│       ├── Controllers/         # API controllers (ImagesController)
│       ├── Data/                # EF Core models and DbContext
│       ├── Pages/               # Blazor pages (Gallery, PhotoDetail)
│       ├── Services/            # Business logic services
│       └── Shared/              # Shared components and layouts
├── docker/                      # Docker configuration (Dockerfile, compose, nginx configs)
├── docs/                        # Documentation (see docs/README.md)
│   └── docker/                  # Docker / Portainer / deployment guides
├── scripts/                     # Shell and PowerShell automation (migrations, verification)
├── .github/
│   └── workflows/
│       ├── docker-build.yml    # Build and push Docker image
│       └── dotnet-ci.yml       # Restore, build, test on push/PR
└── README.md
```

## Database Schema

- **Folders**: Hierarchical folder structure with parent-child relationships
- **Photos**: Photo metadata (filename, path, dimensions, EXIF data)
- **Thumbnails**: Cached thumbnail paths and sizes
- **Identity**: ASP.NET Core Identity tables for authentication

## Development

### Adding New Features

1. Create database migration:
   ```bash
   dotnet ef migrations add FeatureName --project src/KelliPhoto.Web
   ```

2. Update database:
   ```bash
   dotnet ef database update --project src/KelliPhoto.Web
   ```

### Running Tests

```bash
dotnet test
```

## Deployment

### GitHub Actions

- **`.github/workflows/dotnet-ci.yml`** — restores, builds, and runs `dotnet test` on pushes and pull requests to `main`/`master`. Configure repository secrets `CONNECTION_STRINGS__DEFAULT_CONNECTION` and `EMAIL__SMTP_PASSWORD` so the app configuration is valid during the test run.
- **`docker-build.yml`** — builds the Docker image on push to main/master, pushes to Docker Hub, and tags with `latest`, branch name, and commit SHA.

### Updating the Application

1. Make changes and push to GitHub
2. Wait for GitHub Actions to build and push the image
3. In Portainer: Containers → `kelliphoto-web` → Recreate (pulls latest image)

## Troubleshooting

### Database Connection Issues

- Verify PostgreSQL is running and accessible
- Check connection string in appsettings
- Ensure `pg_hba.conf` allows connections
- Check firewall/iptables rules

### Gallery Not Loading

- Verify gallery path is correct and accessible
- Check file permissions on gallery directory
- Review logs: `docker-compose logs web`

### Thumbnail Generation Failing

- Ensure ImageSharp dependencies are installed
- Check disk space for thumbnail directory
- Review application logs for errors

## Contributing

This is a private project. For issues or questions, please contact the repository owner.

## License

Private project for personal use.

## Acknowledgments

- Built with [ASP.NET Core Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
- Image processing with [ImageSharp](https://sixlabors.com/products/imagesharp/)
- Database with [PostgreSQL](https://www.postgresql.org/)
