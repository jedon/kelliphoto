#!/bin/bash
# Setup script to enable external PostgreSQL access for Kelli Photo Gallery
# This allows connecting from your dev machine via port 15432 (direct access)

echo "Setting up external PostgreSQL access for Kelli Photo Gallery..."
echo ""

echo "ℹ Using direct access on port 15432 (no iptables forwarding needed)"
echo "   PostgreSQL is already exposed on host port 15432"
echo ""

echo "Next steps:"
echo ""
echo "1. Configure PostgreSQL pg_hba.conf to allow external connections:"
echo "   docker exec -it kelliphoto-postgres bash"
echo "   echo 'host    all             all             0.0.0.0/0               md5' >> /var/lib/postgresql/data/pg_hba.conf"
echo "   exit"
echo "   docker restart kelliphoto-postgres"
echo ""
echo "2. Check firewall allows port 15432 (if firewall is active):"
echo "   sudo ufw allow 15432/tcp"
echo "   # or check: sudo ufw status"
echo ""
echo "3. Test connection from dev machine:"
echo "   psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo"
echo "   Password: !kelliphoto13!"
echo ""
echo "4. Update appsettings.Development.json connection string:"
echo "   Host=192.168.10.150;Port=15432;Database=kelli_photo;Username=kelli_photo_app;Password=!kelliphoto13!;SSL Mode=Prefer"
echo ""
echo "✓ Setup instructions complete!"
