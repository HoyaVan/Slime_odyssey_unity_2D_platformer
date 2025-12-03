#!/bin/bash

# Digital Ocean Deployment Script for Unity WebGL Game
# Run this script on your Digital Ocean droplet

set -e

echo "🚀 Starting deployment..."

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Update system
echo -e "${YELLOW}Updating system packages...${NC}"
apt update && apt upgrade -y

# Install Nginx
if ! command -v nginx &> /dev/null; then
    echo -e "${YELLOW}Installing Nginx...${NC}"
    apt install nginx -y
    systemctl start nginx
    systemctl enable nginx
else
    echo -e "${GREEN}Nginx is already installed${NC}"
fi

# Install Node.js (if deploying full stack)
read -p "Do you want to install Node.js for backend server? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    if ! command -v node &> /dev/null; then
        echo -e "${YELLOW}Installing Node.js...${NC}"
        curl -fsSL https://deb.nodesource.com/setup_20.x | bash -
        apt install -y nodejs
    else
        echo -e "${GREEN}Node.js is already installed${NC}"
    fi
    
    # Install PM2
    if ! command -v pm2 &> /dev/null; then
        echo -e "${YELLOW}Installing PM2...${NC}"
        npm install -g pm2
    fi
fi

# Set up firewall
echo -e "${YELLOW}Configuring firewall...${NC}"
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable

# Create web directory
mkdir -p /var/www/html
chown -R www-data:www-data /var/www/html
chmod -R 755 /var/www/html

echo -e "${GREEN}✅ Basic setup complete!${NC}"
echo ""
echo "Next steps:"
echo "1. Upload your MyFirstGameForHosting folder to /var/www/html/"
echo "2. Copy nginx-unity.conf to /etc/nginx/sites-available/unity-game"
echo "3. Enable the site: ln -s /etc/nginx/sites-available/unity-game /etc/nginx/sites-enabled/"
echo "4. Test: nginx -t"
echo "5. Reload: systemctl reload nginx"
echo ""
echo "For SSL certificate:"
echo "apt install certbot python3-certbot-nginx -y"
echo "certbot --nginx -d yourdomain.com"

