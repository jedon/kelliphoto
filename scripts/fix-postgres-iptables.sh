#!/bin/bash
# Fix iptables rule for PostgreSQL container

echo "Current iptables PREROUTING rules:"
sudo iptables -t nat -L PREROUTING -v --line-numbers | grep 5432

echo ""
echo "PostgreSQL container info:"
docker inspect smarttrax-postgres | grep -A 10 "IPAddress\|Networks"

echo ""
echo "To fix, run one of these:"
echo ""
echo "Option 1: Update rule to forward to port 5432 (if container IP is 192.168.10.150):"
echo "  sudo iptables -t nat -D PREROUTING 17"
echo "  sudo iptables -t nat -A PREROUTING -p tcp --dport 5432 -j DNAT --to-destination 192.168.10.150:5432"
echo ""
echo "Option 2: If container has different IP, update the destination IP:"
echo "  sudo iptables -t nat -D PREROUTING 17"
echo "  sudo iptables -t nat -A PREROUTING -p tcp --dport 5432 -j DNAT --to-destination <CONTAINER_IP>:5432"
