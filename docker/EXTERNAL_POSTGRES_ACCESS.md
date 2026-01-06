# Making PostgreSQL Accessible from Dev Machine

## Current Setup

- **PostgreSQL container**: `kelliphoto-postgres`
- **Host port**: `15432` (mapped from container port `5432`)
- **Host IP**: `192.168.10.150` (Debian VM)
- **External access**: Currently NOT configured

## Steps to Enable External Access

### Option 1: Use Port 15432 Directly (Recommended - No iptables needed)

Use port `15432` directly - no iptables forwarding required since it's already exposed on the host.

#### 1. Configure PostgreSQL to allow external connections

The PostgreSQL container needs to allow connections from outside. By default, Docker containers should allow this, but we need to ensure `pg_hba.conf` is configured.

```bash
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

#### 2. Test connection from your dev machine

```bash
# From your Windows dev machine (using internal IP)
psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo
# Password: !kelliphoto13!
```

Or if you have a domain/DNS pointing to the server:
```bash
psql -h your-server-domain.com -p 15432 -U kelli_photo_app -d kelli_photo
```

Update your connection string in `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=192.168.10.150;Port=15432;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!;SSL Mode=Prefer"
  }
}
```

### Option 2: Use iptables Forwarding (If Direct Access Not Available)

If you need to use a different external port (e.g., 5434) with iptables forwarding:

1. **Add iptables rule on Proxmox host:**
   ```bash
   sudo iptables -t nat -A PREROUTING -p tcp --dport 5434 -j DNAT --to-destination 192.168.10.150:15432
   sudo netfilter-persistent save
   ```
2. **Configure pg_hba.conf** (same as Option 1, step 1)
3. **Test connection:**
   ```bash
   psql -h postgres.darklingdesign.com -p 5434 -U kelli_photo_app -d kelli_photo
   ```

## Verify Setup

### 1. Check PostgreSQL is listening

```bash
# On Debian host
sudo netstat -tlnp | grep 15432
# Should show: 0.0.0.0:15432
```

### 3. Test from dev machine

```bash
# Windows PowerShell or CMD
telnet 192.168.10.150 15432
# Or use psql if you have it installed
psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo
```

## Running Migrations from Dev Machine

Once connected, you can run migrations directly:

```bash
# From your dev machine (in the project directory)
cd src/KelliPhoto.Web

# Set connection string (or use appsettings.Development.json)
$env:ConnectionStrings__DefaultConnection="Host=192.168.10.150;Port=15432;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!;SSL Mode=Prefer"

# Run migrations
dotnet ef database update
```

## Troubleshooting

### Connection refused
- Check PostgreSQL is listening: `sudo netstat -tlnp | grep 15432` (should show `0.0.0.0:15432`)
- Check firewall on Debian host: `sudo ufw status` (may need to allow port 15432)
- Check if you can reach the server: `ping 192.168.10.150`
- If using iptables forwarding, check rule: `sudo iptables -t nat -L PREROUTING | grep 15432`

### Authentication failed
- Verify `pg_hba.conf` has the correct entry
- Check password is correct: `!kelliphoto13!`
- Restart PostgreSQL container after changing `pg_hba.conf`

### SSL connection error
- Add `SSL Mode=Prefer` to connection string
- Or use `SSL Mode=Disable` for testing (not recommended for production)
