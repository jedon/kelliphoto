#!/bin/bash
# Nginx setup script for Kelli Photo Gallery
# Run this on your server where nginx is installed

echo "Setting up Nginx reverse proxy for Kelli Photo Gallery..."

NGINX_SITES_AVAILABLE="/etc/nginx/sites-available"
NGINX_SITES_ENABLED="/etc/nginx/sites-enabled"
CONFIG_FILE="kelliphoto.darklingdesign.com"

# Check if nginx is installed
if ! command -v nginx &> /dev/null; then
    echo "⚠ Nginx is not installed. Installing..."
    sudo apt-get update
    sudo apt-get install -y nginx
fi

# Copy configuration file
echo "Copying nginx configuration..."
sudo cp nginx/${CONFIG_FILE} ${NGINX_SITES_AVAILABLE}/${CONFIG_FILE}

# Create symlink if it doesn't exist
if [ ! -L "${NGINX_SITES_ENABLED}/${CONFIG_FILE}" ]; then
    echo "Creating symlink..."
    sudo ln -s ${NGINX_SITES_AVAILABLE}/${CONFIG_FILE} ${NGINX_SITES_ENABLED}/${CONFIG_FILE}
    echo "✓ Symlink created"
else
    echo "✓ Symlink already exists"
fi

# Test nginx configuration
echo "Testing nginx configuration..."
sudo nginx -t

if [ $? -eq 0 ]; then
    echo "✓ Nginx configuration is valid"
    echo ""
    echo "Next steps:"
    echo "1. Update SSL certificate paths in ${NGINX_SITES_AVAILABLE}/${CONFIG_FILE}"
    echo "2. If using Let's Encrypt, run: sudo certbot --nginx -d kelliphoto.darklingdesign.com"
    echo "3. Reload nginx: sudo systemctl reload nginx"
else
    echo "✗ Nginx configuration has errors. Please fix them before reloading."
    exit 1
fi
