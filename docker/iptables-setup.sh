#!/bin/bash
# iptables setup for Kelli Photo Gallery
# Run this on your Proxmox/Debian host
# NOTE: PostgreSQL is NOT exposed externally to avoid conflicts with existing PostgreSQL on port 5432
# PostgreSQL is only accessible internally on port 15432

echo "Setting up iptables rules for Kelli Photo Gallery..."

echo "ℹ PostgreSQL port forwarding SKIPPED"
echo "   - Kelli Photo PostgreSQL runs on internal port 15432 only"
echo "   - Web container connects via Docker network (no external access needed)"
echo "   - To access from host: psql -h 192.168.10.150 -p 15432 -U kelli_photo_app -d kelli_photo"
echo ""

# Web application port forwarding: External 80 -> Internal 8080 (optional, if you want HTTP on port 80)
# Uncomment if you want to expose the web app on port 80 instead of 8080
# sudo iptables -t nat -A PREROUTING -p tcp --dport 80 -j DNAT --to-destination 192.168.10.150:8080

# Web application port forwarding: External 443 -> Internal 8443 (optional, if you want HTTPS on port 443)
# Uncomment if you want to expose the web app on port 443 instead of 8443
# sudo iptables -t nat -A PREROUTING -p tcp --dport 443 -j DNAT --to-destination 192.168.10.150:8443

# Make rules persistent
if command -v netfilter-persistent &> /dev/null; then
    echo "Saving iptables rules..."
    sudo netfilter-persistent save
    echo "✓ Rules saved"
else
    echo "⚠ netfilter-persistent not installed. Installing..."
    sudo apt-get update
    sudo apt-get install -y iptables-persistent
    sudo netfilter-persistent save
    echo "✓ Rules saved"
fi

echo ""
echo "Current PREROUTING rules for ports 80, 443:"
sudo iptables -t nat -L PREROUTING -v --line-numbers | grep -E "dpt:http|dpt:https"

echo ""
echo "✓ iptables setup complete!"
