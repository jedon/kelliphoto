# PostgreSQL Access Configuration

## Current Setup

Kelli Photo Gallery PostgreSQL is configured to **avoid conflicts** with your existing PostgreSQL on port 5432.

## Access Methods

### 1. From Web Container (Automatic)
The web container connects via Docker network using the service name:
- **Connection**: `postgres:5432` (internal Docker network)
- **No configuration needed** - this works automatically

### 2. From Host/Server
Access from the Debian host where Docker is running:
```bash
psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo
# Password: !kelliphoto13!
```

### 3. From Another Container
If you need to access from another container on the same Docker network:
```bash
# Connect to the kelliphoto-network
docker network connect kelliphoto-network <your-container-name>

# Then from inside that container:
psql -h kelliphoto-postgres -p 5432 -U kelli_photo_app -d kelli_photo
```

## Port Configuration

- **Container internal**: `5432` (PostgreSQL default)
- **Host mapping**: `15432` (to avoid conflicts)
- **External access**: **NOT configured** (to avoid conflict with existing PostgreSQL)

## Why This Setup?

- **No iptables conflict**: Doesn't interfere with your existing PostgreSQL on port 5432
- **Internal access**: Web container connects via Docker network (no external port needed)
- **Host access**: Available on port 15432 for direct access from the server
- **Security**: Not exposed externally (only accessible from host/containers)

## If You Need External Access

If you later need external access and can use a different external port:

1. **Choose a different external port** (e.g., 5433):
   ```bash
   sudo iptables -t nat -A PREROUTING -p tcp --dport 5433 -j DNAT --to-destination 192.168.10.150:15432
   ```

2. **Update connection string** to use the new external port:
   ```
   Host=your-server;Port=5433;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!
   ```

## Connection String Reference

### From Web Container (docker-compose.yml)
```
Host=postgres;Port=5432;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!
```

### From Host
```
Host=192.168.10.150;Port=15432;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!
```

### From External (if you add port forwarding)
```
Host=your-server;Port=5433;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!
```
