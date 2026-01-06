# Making PostgreSQL Accessible from Dev Machine

## Current Setup

- **Architecture**: Proxmox host → Debian VM (192.168.10.150) → Docker containers
- **PostgreSQL container**: `kelliphoto-postgres`
- **Debian VM port**: `15432` (mapped from container port `5432`)
- **External access**: Requires iptables forwarding from Proxmox host

## Steps to Enable External Access

### Step 1: Add iptables rule on Proxmox host

Forward external port to Debian VM. Using port `5434` externally to avoid conflicts with existing PostgreSQL on port `5432`.

```bash
# On Proxmox host (where iptables rules are configured)
# Forward external port 5434 to Debian VM port 15432
sudo iptables -t nat -A PREROUTING -p tcp --dport 5434 -j DNAT --to-destination 192.168.10.150:15432

# Make it persistent
sudo apt-get install -y iptables-persistent
sudo netfilter-persistent save
```

**Note**: If you prefer a different external port, replace `5434` with your chosen port.

### Step 2: Configure PostgreSQL to allow external connections

The PostgreSQL container needs to allow connections from outside. We need to ensure `pg_hba.conf` is configured.

```bash
# On Debian VM (Docker host)
# Connect to the PostgreSQL container
docker exec -it kelliphoto-postgres bash

# Check current pg_hba.conf
cat /var/lib/postgresql/data/pg_hba.conf

# Edit pg_hba.conf to allow external connections
# Add this line (or modify existing):
echo "host    all             all             0.0.0.0/0               md5" >> /var/lib/postgresql/data/pg_hba.conf

# Or more secure - allow only specific IPs:
# echo "host    all             all             YOUR_DEV_IP/32      md5" >> /var/lib/postgresql/data/pg_hba.conf

# Restart PostgreSQL
exit
docker restart kelliphoto-postgres
```

### Step 3: Test connection from your dev machine

```bash
# From your Windows dev machine (using external domain/port)
psql -h postgres.darklingdesign.com -p 5434 -U kelli_photo_app -d kelli_photo
# Password: !kelliphoto13!
```

Update your connection string in `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres.darklingdesign.com;Port=5434;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!;SSL Mode=Prefer"
  }
}
```

## Verify Setup

### 1. Check iptables rule on Proxmox host

```bash
# On Proxmox host
sudo iptables -t nat -L PREROUTING -v --line-numbers | grep 5434
```

Should show:
```
DNAT tcp -- any any anywhere anywhere tcp dpt:5434 to:192.168.10.150:15432
```

### 2. Check PostgreSQL is listening on Debian VM

```bash
# On Debian VM (Docker host)
sudo netstat -tlnp | grep 15432
# Should show: 0.0.0.0:15432
```

### 3. Test from dev machine

```bash
# Windows PowerShell or CMD
telnet postgres.darklingdesign.com 5434
# Or use psql if you have it installed
psql -h postgres.darklingdesign.com -p 5434 -U kelli_photo_app -d kelli_photo
```

## Running Migrations from Dev Machine

Once connected, you can run migrations directly:

```bash
# From your dev machine (in the project directory)
cd src/KelliPhoto.Web

# Set connection string (or use appsettings.Development.json)
$env:ConnectionStrings__DefaultConnection="Host=postgres.darklingdesign.com;Port=5434;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!;SSL Mode=Prefer"

# Run migrations
dotnet ef database update
```

## Troubleshooting

### Connection refused
- Check iptables rule on Proxmox host: `sudo iptables -t nat -L PREROUTING | grep 5434`
- Check PostgreSQL is listening on Debian VM: `sudo netstat -tlnp | grep 15432` (should show `0.0.0.0:15432`)
- Check firewall on Debian VM: `sudo ufw status` (may need to allow port 15432)
- Check if you can reach the server: `ping postgres.darklingdesign.com` or `ping 142.4.216.160`

### Authentication failed
- Verify `pg_hba.conf` has the correct entry
- Check password is correct: `!kelliphoto13!`
- Restart PostgreSQL container after changing `pg_hba.conf`

### SSL connection error
- Add `SSL Mode=Prefer` to connection string
- Or use `SSL Mode=Disable` for testing (not recommended for production)
