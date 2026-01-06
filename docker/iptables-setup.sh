#!/bin/bash
# iptables setup for Kelli Photo Gallery
# Run this on your Proxmox/Debian host

echo "Setting up iptables rules for Kelli Photo Gallery..."

# PostgreSQL port forwarding: External 5432 -> Internal 15432
# Check if rule already exists
EXISTING_RULE=$(sudo iptables -t nat -L PREROUTING -n --line-numbers | grep -E "5432.*192.168.10.150.*15432" | head -1 | awk '{print $1}')

if [ -z "$EXISTING_RULE" ]; then
    echo "Adding iptables rule for PostgreSQL (5432 -> 192.168.10.150:15432)..."
    sudo iptables -t nat -A PREROUTING -p tcp --dport 5432 -j DNAT --to-destination 192.168.10.150:15432
    echo "✓ Rule added"
else
    echo "✓ Rule already exists at line $EXISTING_RULE"
fi

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
echo "Current PREROUTING rules for ports 5432, 80, 443:"
sudo iptables -t nat -L PREROUTING -v --line-numbers | grep -E "5432|dpt:http|dpt:https"

echo ""
echo "✓ iptables setup complete!"
