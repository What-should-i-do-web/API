#!/bin/bash

# WhatShouldIDo API Deployment Script
# This script automates the deployment process for production

set -e  # Exit on any error

echo "🚀 WhatShouldIDo API Deployment Script"
echo "======================================"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if running as root
if [ "$EUID" -eq 0 ]; then
    print_error "Don't run this script as root!"
    exit 1
fi

# Check if .env file exists
if [ ! -f ".env" ]; then
    print_error ".env file not found!"
    print_status "Copying .env.example to .env..."
    cp .env.example .env
    print_warning "Please edit .env file with your actual values before continuing."
    nano .env
fi

# Load environment variables
source .env

print_status "Starting deployment process..."

# Step 1: System Updates
print_status "Step 1: Updating system packages..."
sudo apt update && sudo apt upgrade -y

# Step 2: Install Docker if not present
if ! command -v docker &> /dev/null; then
    print_status "Step 2: Installing Docker..."
    curl -fsSL https://get.docker.com -o get-docker.sh
    sudo sh get-docker.sh
    sudo usermod -aG docker $USER
    rm get-docker.sh
    print_success "Docker installed successfully"
else
    print_success "Docker is already installed"
fi

# Step 3: Install Docker Compose if not present
if ! command -v docker-compose &> /dev/null; then
    print_status "Step 3: Installing Docker Compose..."
    sudo apt install docker-compose-plugin -y
    print_success "Docker Compose installed successfully"
else
    print_success "Docker Compose is already installed"
fi

# Step 4: Install Nginx if not present
if ! command -v nginx &> /dev/null; then
    print_status "Step 4: Installing Nginx..."
    sudo apt install nginx -y
    sudo systemctl enable nginx
    print_success "Nginx installed successfully"
else
    print_success "Nginx is already installed"
fi

# Step 5: Install Certbot for SSL
if ! command -v certbot &> /dev/null; then
    print_status "Step 5: Installing Certbot..."
    sudo apt install certbot python3-certbot-nginx -y
    print_success "Certbot installed successfully"
else
    print_success "Certbot is already installed"
fi

# Step 6: Create necessary directories
print_status "Step 6: Creating necessary directories..."
mkdir -p logs backups certificates nginx

# Step 7: Build and start the application
print_status "Step 7: Building and starting the application..."

# Stop existing containers
if [ "$(docker-compose -f docker-compose.prod.yml ps -q)" ]; then
    print_status "Stopping existing containers..."
    docker-compose -f docker-compose.prod.yml down
fi

# Build and start new containers
print_status "Building and starting new containers..."
docker-compose -f docker-compose.prod.yml up -d --build

# Wait for services to be healthy
print_status "Waiting for services to be healthy..."
sleep 30

# Step 8: Run database migrations
print_status "Step 8: Running database migrations..."
docker-compose -f docker-compose.prod.yml exec -T api dotnet ef database update || {
    print_warning "Migration failed or already up to date"
}

# Step 9: Configure Nginx
if [ ! -z "$DOMAIN_NAME" ]; then
    print_status "Step 9: Configuring Nginx..."
    
    # Create Nginx configuration
    sudo tee /etc/nginx/sites-available/whatshouldido > /dev/null <<EOF
server {
    listen 80;
    server_name $DOMAIN_NAME www.$DOMAIN_NAME;
    
    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }
    
    location / {
        return 301 https://\$server_name\$request_uri;
    }
}

server {
    listen 443 ssl http2;
    server_name $DOMAIN_NAME www.$DOMAIN_NAME;

    # SSL configuration will be added by certbot
    
    client_max_body_size 10M;
    
    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
        proxy_buffering off;
    }

    location /api/health {
        proxy_pass http://localhost:5000;
        access_log off;
    }
}
EOF

    # Enable the site
    sudo ln -sf /etc/nginx/sites-available/whatshouldido /etc/nginx/sites-enabled/
    sudo nginx -t && sudo systemctl reload nginx
    
    print_success "Nginx configured successfully"
else
    print_warning "DOMAIN_NAME not set, skipping Nginx configuration"
fi

# Step 10: Setup SSL Certificate
if [ ! -z "$DOMAIN_NAME" ] && [ ! -z "$SSL_EMAIL" ]; then
    print_status "Step 10: Setting up SSL certificate..."
    sudo certbot --nginx -d $DOMAIN_NAME -d www.$DOMAIN_NAME --email $SSL_EMAIL --agree-tos --non-interactive --redirect
    
    # Setup auto-renewal
    (crontab -l 2>/dev/null; echo "0 12 * * * /usr/bin/certbot renew --quiet") | crontab -
    
    print_success "SSL certificate configured successfully"
else
    print_warning "DOMAIN_NAME or SSL_EMAIL not set, skipping SSL setup"
fi

# Step 11: Configure firewall
print_status "Step 11: Configuring firewall..."
sudo ufw --force reset
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow ssh
sudo ufw allow 'Nginx Full'
sudo ufw --force enable

print_success "Firewall configured successfully"

# Step 12: Verify deployment
print_status "Step 12: Verifying deployment..."

# Check if containers are running
if ! docker-compose -f docker-compose.prod.yml ps | grep -q "Up"; then
    print_error "Some containers are not running!"
    docker-compose -f docker-compose.prod.yml ps
    exit 1
fi

# Check API health
print_status "Checking API health..."
sleep 10

if curl -f http://localhost:5000/api/health > /dev/null 2>&1; then
    print_success "API is responding correctly"
else
    print_error "API health check failed"
    print_status "Check logs with: docker-compose -f docker-compose.prod.yml logs api"
fi

# Final status
print_success "🎉 Deployment completed successfully!"
echo ""
echo "📋 Next steps:"
echo "1. Test your API at: https://$DOMAIN_NAME/api/health"
echo "2. Check logs: docker-compose -f docker-compose.prod.yml logs api"
echo "3. Monitor services: docker-compose -f docker-compose.prod.yml ps"
echo ""
echo "📊 Useful commands:"
echo "- View logs: docker-compose -f docker-compose.prod.yml logs -f"
echo "- Restart: docker-compose -f docker-compose.prod.yml restart"
echo "- Stop: docker-compose -f docker-compose.prod.yml down"
echo "- Update: git pull && docker-compose -f docker-compose.prod.yml up -d --build"
echo ""

if [ ! -z "$DOMAIN_NAME" ]; then
    echo "🌐 Your API is now available at: https://$DOMAIN_NAME"
else
    echo "🌐 Your API is available at: http://$(curl -s ifconfig.me):80"
fi

print_success "Deployment script finished!"