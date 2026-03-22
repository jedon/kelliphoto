#!/bin/bash
# Setup script to enable external PostgreSQL access for Kelli Photo Gallery
# This sets up iptables forwarding from Proxmox host to Debian VM
# Run this on the Proxmox host (where iptables rules are configured)

echo "Setting up external PostgreSQL access for Kelli Photo Gallery..."
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then 
    echo "Please run as root (use sudo)"
    exit 1
fi

# External port (change if needed)
EXTERNAL_PORT=5444
DEBIAN_VM_IP=192.168.10.150
DEBIAN_VM_PORT=15432

echo "Architecture: Proxmox host → Debian VM ($DEBIAN_VM_IP) → Docker container"
echo "Forwarding: External port $EXTERNAL_PORT → $DEBIAN_VM_IP:$DEBIAN_VM_PORT"
echo ""

# Add iptables rule for external PostgreSQL access
echo "Adding iptables rule: External port $EXTERNAL_PORT -> $DEBIAN_VM_IP:$DEBIAN_VM_PORT"
iptables -t nat -C PREROUTING -p tcp --dport $EXTERNAL_PORT -j DNAT --to-destination $DEBIAN_VM_IP:$DEBIAN_VM_PORT 2>/dev/null
if [ $? -eq 0 ]; then
    echo "⚠ Rule already exists, skipping..."
else
    iptables -t nat -A PREROUTING -p tcp --dport $EXTERNAL_PORT -j DNAT --to-destination $DEBIAN_VM_IP:$DEBIAN_VM_PORT
    echo "✓ iptables rule added"
fi

# Make rules persistent
if command -v netfilter-persistent &> /dev/null; then
    echo "Saving iptables rules..."
    netfilter-persistent save
    echo "✓ Rules saved"
else
    echo "⚠ netfilter-persistent not installed. Installing..."
    apt-get update
    apt-get install -y iptables-persistent
    netfilter-persistent save
    echo "✓ Rules saved"
fi

echo ""
echo "Current PREROUTING rule for port $EXTERNAL_PORT:"
iptables -t nat -L PREROUTING -v --line-numbers | grep $EXTERNAL_PORT

echo ""
echo "✓ iptables forwarding configured on Proxmox host!"
echo ""
echo "Next steps (on Debian VM - Docker host):"
echo ""
echo "1. Configure PostgreSQL pg_hba.conf to allow external connections:"
echo "   docker exec -it kelliphoto-postgres bash"
echo "   echo 'host    all             all             0.0.0.0/0               md5' >> /var/lib/postgresql/data/pg_hba.conf"
echo "   exit"
echo "   docker restart kelliphoto-postgres"
echo ""
echo "2. Test connection from dev machine:"
echo "   psql -h postgres.darklingdesign.com -p $EXTERNAL_PORT -U kelli_photo_app -d kelli_photo"
echo "   Password: !kelliphoto13!"
echo ""
echo "3. Update appsettings.Development.json connection string:"
echo "   Host=postgres.darklingdesign.com;Port=$EXTERNAL_PORT;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!;SSL Mode=Prefer"
echo ""
echo "✓ Setup instructions complete!"
