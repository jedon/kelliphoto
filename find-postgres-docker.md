# Finding PostgreSQL in Docker Setup

## Steps to find PostgreSQL container

### 1. From the Proxmox host (where iptables rules are):

```bash
# List all Docker containers
docker ps -a

# Find PostgreSQL container
docker ps -a | grep postgres

# Check PostgreSQL container details
docker inspect <postgres-container-name> | grep -A 10 Mounts
```

### 2. Check PostgreSQL container configuration:

```bash
# See what port PostgreSQL is mapped to
docker ps | grep postgres
# Look for port mapping like: 0.0.0.0:15432->5432/tcp

# Check PostgreSQL logs
docker logs <postgres-container-name>

# Execute into PostgreSQL container
docker exec -it <postgres-container-name> bash
```

### 3. Inside PostgreSQL container, find pg_hba.conf:

```bash
# Once inside PostgreSQL container:
find /var/lib/postgresql -name pg_hba.conf
# or
find /etc/postgresql -name pg_hba.conf
# or check the data directory
ls -la /var/lib/postgresql/data/
```

### 4. Check PostgreSQL configuration:

```bash
# Inside PostgreSQL container:
psql -U postgres
# Then:
SHOW config_file;
SHOW hba_file;
```

### 5. Network connectivity test from Proxmox host:

```bash
# Test if you can reach PostgreSQL from Proxmox host
telnet 192.168.10.150 15432
# or
nc -zv 192.168.10.150 15432
```

### 6. Check Docker network:

```bash
# See Docker networks
docker network ls

# Inspect the network PostgreSQL is on
docker network inspect <network-name>
```
