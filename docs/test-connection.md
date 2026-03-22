# Testing PostgreSQL Connection from Windows

## Quick Tests

### 1. Test if port is reachable (PowerShell)
```powershell
Test-NetConnection -ComputerName postgres.darklingdesign.com -Port 5432
```

### 2. Test with telnet (if available)
```cmd
telnet postgres.darklingdesign.com 5432
```

### 3. Test with psql (if PostgreSQL client is installed)
```cmd
psql -h postgres.darklingdesign.com -p 5432 -U postgres -d postgres
# Password: !ifpnet13!
```

### 4. Test with .NET (using your connection string)
```powershell
# This will test if the connection string works
cd src/KelliPhoto.Web
dotnet ef database update
```

## Alternative: Test from server itself

If you can SSH into your server, test from there:
```bash
# From the server
psql -h 192.168.10.150 -p 15432 -U postgres -d postgres
# or
psql -h localhost -p 15432 -U postgres -d postgres
```

## If connection fails

The issue might be:
1. **Firewall on your local network** blocking outbound port 5432
2. **Firewall on the server** not allowing connections from your IP
3. **PostgreSQL not listening** on the external interface
4. **iptables rule** not matching your source IP

Check PostgreSQL is listening:
```bash
# On the server
sudo netstat -tlnp | grep 15432
# or
sudo ss -tlnp | grep 15432
```

Check PostgreSQL config:
```bash
# On the server
sudo grep listen_addresses /etc/postgresql/*/main/postgresql.conf
# Should be: listen_addresses = '*' or listen_addresses = '0.0.0.0'
```
