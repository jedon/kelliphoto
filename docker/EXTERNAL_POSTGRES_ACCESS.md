# Making PostgreSQL Accessible from Dev Machine

## Current Setup

- **PostgreSQL container**: `kelliphoto-postgres`
- **Host port**: `15432` (mapped from container port `5432`)
- **Host IP**: `192.168.10.150` (Debian VM)
- **External access**: Currently NOT configured

## Steps to Enable External Access

### Option 1: Use a Different External Port (Recommended)

Use port `5433` externally to avoid conflicts with your existing PostgreSQL on port `5432`.

#### 1. Add iptables rule on Proxmox host

```bash
# Forward external port 5433 to internal 15432
sudo iptables -t nat -A PREROUTING -p tcp --dport 5433 -j DNAT --to-destination 192.168.10.150:15432

# Make it persistent
sudo apt-get install -y iptables-persistent
sudo netfilter-persistent save
```

#### 2. Configure PostgreSQL to allow external connections

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

#### 3. Test connection from your dev machine

```bash
# From your Windows dev machine
psql -h postgres.darklingdesign.com -p 5433 -U kelli_photo_app -d kelli_photo
# Password: !kelliphoto13!
```

Or update your connection string in `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres.darklingdesign.com;Port=5433;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!;SSL Mode=Prefer"
  }
}
```

### Option 2: Use Port 15432 Directly (If Firewall Allows)

If your firewall/router allows direct access to port 15432:

1. **No iptables rule needed** (direct access)
2. **Configure pg_hba.conf** (same as Option 1, step 2)
3. **Test connection:**
   ```bash
   psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo
   ```

## Verify Setup

### 1. Check iptables rule (if using Option 1)

```bash
# On Proxmox host
sudo iptables -t nat -L PREROUTING -v --line-numbers | grep 5433
```

Should show:
```
DNAT tcp -- any any anywhere anywhere tcp dpt:5433 to:192.168.10.150:15432
```

### 2. Check PostgreSQL is listening

```bash
# On Debian host
sudo netstat -tlnp | grep 15432
# Should show: 0.0.0.0:15432
```

### 3. Test from dev machine

```bash
# Windows PowerShell or CMD
telnet postgres.darklingdesign.com 5433
# Or use psql if you have it installed
```

## Running Migrations from Dev Machine

Once connected, you can run migrations directly:

```bash
# From your dev machine (in the project directory)
cd src/KelliPhoto.Web

# Set connection string (or use appsettings.Development.json)
$env:ConnectionStrings__DefaultConnection="Host=postgres.darklingdesign.com;Port=5433;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!;SSL Mode=Prefer"

# Run migrations
dotnet ef database update
```

## Troubleshooting

### Connection refused
- Check iptables rule is active: `sudo iptables -t nat -L PREROUTING | grep 5433`
- Check PostgreSQL is listening: `sudo netstat -tlnp | grep 15432`
- Check firewall on Debian host: `sudo ufw status`

### Authentication failed
- Verify `pg_hba.conf` has the correct entry
- Check password is correct: `!kelliphoto13!`
- Restart PostgreSQL container after changing `pg_hba.conf`

### SSL connection error
- Add `SSL Mode=Prefer` to connection string
- Or use `SSL Mode=Disable` for testing (not recommended for production)
