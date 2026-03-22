# Database Setup Notes

## PostgreSQL Connection

The PostgreSQL server is at:
- Internal IP: `192.168.10.150:15432`
- External domain: `postgres.darklingdesign.com:5432` (requires iptables port forwarding)

## iptables Port Forwarding

To enable external access to PostgreSQL, you need a PREROUTING rule:

```bash
sudo iptables -t nat -A PREROUTING -p tcp --dport 5432 -j DNAT --to-destination 192.168.10.150:15432
```

To check if the rule exists:
```bash
sudo iptables -t nat -L PREROUTING -v --line-numbers
```

To make the rule persistent (so it survives reboots):
```bash
# On Debian/Ubuntu
sudo apt-get install iptables-persistent
sudo netfilter-persistent save
```

## Database Setup

### Option 1: Create database from server (Recommended)

1. SSH into your server and connect to PostgreSQL:
   ```bash
   psql -h 192.168.10.150 -p 15432 -U postgres
   # or if PostgreSQL is on the same server:
   psql -h localhost -p 15432 -U postgres
   ```

2. Run the SQL script to create database and user:
   ```bash
   psql -h 192.168.10.150 -p 15432 -U postgres -f create-database.sql
   ```
   Or manually run the SQL commands from `create-database.sql`

3. Copy the migration files to the server (or clone the repo there), then run:
   ```bash
   cd /path/to/kelli.photo/src/KelliPhoto.Web
   dotnet ef database update
   ```

### Option 2: Create database from local (if connection works)

If you can get the connection working, you can run:
```bash
cd src/KelliPhoto.Web
dotnet ef database update
```

## Connection String Options

### Internal Network (if on same network/VPN):
```
Host=192.168.10.150;Port=15432;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!;SSL Mode=Prefer
```

### External Access (requires iptables rule):
```
Host=postgres.darklingdesign.com;Port=5432;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!;SSL Mode=Prefer
```
