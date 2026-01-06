#!/bin/bash
# Setup script to enable external PostgreSQL access for Kelli Photo Gallery
# This allows connecting from your dev machine via port 5433

echo "Setting up external PostgreSQL access for Kelli Photo Gallery..."
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then 
    echo "Please run as root (use sudo)"
    exit 1
fi

# Add iptables rule for external PostgreSQL access
echo "Adding iptables rule: External port 5433 -> Internal 192.168.10.150:15432"
iptables -t nat -C PREROUTING -p tcp --dport 5433 -j DNAT --to-destination 192.168.10.150:15432 2>/dev/null
if [ $? -eq 0 ]; then
    echo "⚠ Rule already exists, skipping..."
else
    iptables -t nat -A PREROUTING -p tcp --dport 5433 -j DNAT --to-destination 192.168.10.150:15432
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
echo "Current PREROUTING rule for port 5433:"
iptables -t nat -L PREROUTING -v --line-numbers | grep 5433

echo ""
echo "✓ External PostgreSQL access configured!"
echo ""
echo "Next steps:"
echo "1. Configure PostgreSQL pg_hba.conf to allow external connections:"
echo "   docker exec -it kelliphoto-postgres bash"
echo "   echo 'host    all             all             0.0.0.0/0               md5' >> /var/lib/postgresql/data/pg_hba.conf"
echo "   exit"
echo "   docker restart kelliphoto-postgres"
echo ""
echo "2. Test connection from dev machine:"
echo "   psql -h postgres.darklingdesign.com -p 5433 -U kelli_photo_app -d kelli_photo"
echo "   Password: !kelliphoto13!"
echo ""
echo "3. Update appsettings.Development.json connection string:"
echo "   Host=postgres.darklingdesign.com;Port=5433;Database=kelli_photo;..."
