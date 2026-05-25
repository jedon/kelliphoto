# Kelli Photo - Docker Setup

## Quick Start

### 1. Update iptables rule on Proxmox host

Since the PostgreSQL container will be on port 15432, update the iptables rule:

```bash
# Remove old rule (if exists)
sudo iptables -t nat -D PREROUTING -p tcp --dport 5432 -j DNAT --to-destination 142.4.216.160:15432

# Add new rule for kelliphoto-postgres
sudo iptables -t nat -A PREROUTING -p tcp --dport 5432 -j DNAT --to-destination 142.4.216.160:15432

# Make it persistent
sudo apt-get install -y iptables-persistent
sudo netfilter-persistent save
```

### 2. Build and start services

```bash
cd docker
docker-compose up -d
```

### 3. Run database migrations

```bash
# Wait for PostgreSQL to be ready, then:
docker-compose exec web dotnet ef database update
```

### 4. Access the application

- Web: http://your-server:8080
- PostgreSQL (external): postgres.darklingdesign.com:5432
- PostgreSQL (internal): 142.4.216.160:15432

## Services

- **postgres**: PostgreSQL 15 on port 15432 (host) → 5432 (container)
- **web**: ASP.NET Core Blazor Server on ports 8080 (HTTP) and 8443 (HTTPS)

## Database

- Database: `kelli_photo` (example; match your deployment)
- Credentials: set via Compose or `.env` — do not commit real passwords to the repository

## Volumes

- `kelliphoto_postgres_data`: PostgreSQL data persistence
- `/mnt/gallery`: Gallery photos (read-only mount)

## Network

Services communicate via the `kelliphoto-network` bridge network.
