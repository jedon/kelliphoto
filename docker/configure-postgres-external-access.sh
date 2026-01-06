#!/bin/bash
# Script to configure PostgreSQL for external access
# Run this on the Debian VM (Docker host)

echo "Configuring PostgreSQL for external access..."
echo ""

# Check if container exists
if ! docker ps -a | grep -q kelliphoto-postgres; then
    echo "❌ Error: kelliphoto-postgres container not found"
    echo "   Make sure the stack is deployed in Portainer"
    exit 1
fi

# Check if container is running
if ! docker ps | grep -q kelliphoto-postgres; then
    echo "⚠ Container is not running. Starting it..."
    docker start kelliphoto-postgres
    sleep 2
fi

echo "Adding external access rule to pg_hba.conf..."
echo ""

# Add the rule to pg_hba.conf
docker exec kelliphoto-postgres bash -c 'echo "host    all             all             0.0.0.0/0               md5" >> /var/lib/postgresql/data/pg_hba.conf'

if [ $? -eq 0 ]; then
    echo "✓ Rule added to pg_hba.conf"
else
    echo "❌ Failed to add rule. Trying alternative method..."
    
    # Alternative: Copy file, modify, copy back
    echo "Attempting alternative method..."
    docker cp kelliphoto-postgres:/var/lib/postgresql/data/pg_hba.conf /tmp/pg_hba.conf
    echo "host    all             all             0.0.0.0/0               md5" >> /tmp/pg_hba.conf
    docker cp /tmp/pg_hba.conf kelliphoto-postgres:/var/lib/postgresql/data/pg_hba.conf
    rm /tmp/pg_hba.conf
    echo "✓ Rule added using alternative method"
fi

echo ""
echo "Restarting PostgreSQL container..."
docker restart kelliphoto-postgres

echo ""
echo "Waiting for PostgreSQL to be ready..."
sleep 3

# Verify the rule was added
echo ""
echo "Verifying pg_hba.conf configuration:"
docker exec kelliphoto-postgres cat /var/lib/postgresql/data/pg_hba.conf | grep "0.0.0.0/0"

if [ $? -eq 0 ]; then
    echo ""
    echo "✓ PostgreSQL configured for external access!"
    echo ""
    echo "Next steps:"
    echo "1. Make sure iptables rule is set on Proxmox host (port 5434 -> 192.168.10.150:15432)"
    echo "2. Test connection: psql -h postgres.darklingdesign.com -p 5434 -U kelli_photo_app -d kelli_photo"
else
    echo ""
    echo "⚠ Warning: Could not verify the rule was added. Please check manually:"
    echo "   docker exec -it kelliphoto-postgres cat /var/lib/postgresql/data/pg_hba.conf"
fi
