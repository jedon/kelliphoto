# Troubleshooting PostgreSQL External Access

## Steps to make PostgreSQL accessible

### 1. Check PostgreSQL is listening on the right interface

On the server (where PostgreSQL is running):
```bash
# Check what PostgreSQL is listening on
sudo netstat -tlnp | grep 15432
# or
sudo ss -tlnp | grep 15432

# Should show something like:
# 0.0.0.0:15432 or 192.168.10.150:15432
```

### 2. Check PostgreSQL configuration

Edit PostgreSQL config:
```bash
# Find the config file
sudo find /etc -name postgresql.conf | grep -v sample

# Edit it (usually in /etc/postgresql/*/main/postgresql.conf)
sudo nano /etc/postgresql/*/main/postgresql.conf
```

Make sure these settings are correct:
```conf
listen_addresses = '*'  # or '0.0.0.0' to listen on all interfaces
port = 15432
```

### 3. Check pg_hba.conf (Host-Based Authentication)

This is the most common issue! Edit:
```bash
sudo nano /etc/postgresql/*/main/pg_hba.conf
```

Add a line to allow connections from your network:
```
# Allow connections from anywhere (adjust for security)
host    all             all             0.0.0.0/0               md5

# Or more secure - allow only specific IPs:
host    all             all             YOUR_IP_ADDRESS/32      md5
```

### 4. Restart PostgreSQL after changes

```bash
sudo systemctl restart postgresql
# or if in Docker:
docker restart <postgres-container-name>
```

### 5. Check firewall on the server

```bash
# Check if firewall is blocking
sudo ufw status
# or
sudo iptables -L -n -v | grep 15432

# If firewall is active, allow the port:
sudo ufw allow 15432/tcp
```

### 6. Verify iptables rule is correct

The rule should be:
```bash
sudo iptables -t nat -L PREROUTING -v --line-numbers | grep 5432
```

Should show:
```
DNAT tcp -- any any anywhere anywhere tcp dpt:postgresql to:192.168.10.150:15432
```

### 7. Test connection from server itself

```bash
# From the server, test internal connection:
psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo
```

### 8. Test external connection

From your local machine:
```bash
# PowerShell
Test-NetConnection -ComputerName postgres.darklingdesign.com -Port 5432

# Or telnet
telnet postgres.darklingdesign.com 5432
```

## Common Issues

1. **pg_hba.conf not allowing connections** - Most common!
2. **PostgreSQL not listening on external interface** - Check listen_addresses
3. **Firewall blocking** - Check ufw/iptables
4. **iptables rule not working** - Check PREROUTING chain
5. **PostgreSQL container not exposing port** - Check Docker port mapping
