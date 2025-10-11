#!/bin/bash

# WhatShouldIDo Production Environment Deployment Script
# This script deploys the application to production environment with zero-downtime

set -e  # Exit on any error

echo "🚀 Deploying WhatShouldIDo API to Production Environment"
echo "======================================================"

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

# Configuration
ENVIRONMENT="production"
COMPOSE_FILE="docker-compose.prod.yml"
ENV_FILE=".env.production"
HEALTH_CHECK_URL="http://localhost:5000/api/health"
MAX_WAIT_TIME=300
BACKUP_DIR="./backups/$(date +%Y%m%d_%H%M%S)"

# Check if running in production (safety check)
read -p "⚠️  This will deploy to PRODUCTION. Are you sure? (yes/no): " confirm
if [ "$confirm" != "yes" ]; then
    print_error "Production deployment cancelled"
    exit 1
fi

# Check if environment file exists
if [ ! -f "$ENV_FILE" ]; then
    print_error "Production environment file $ENV_FILE not found!"
    print_status "Creating from template..."
    cp .env.example "$ENV_FILE"
    print_warning "Please edit $ENV_FILE with your production values before continuing."
    exit 1
fi

# Load environment variables
print_status "Loading production environment variables..."
source "$ENV_FILE"

# Validate required environment variables
required_vars=(
    "PROD_DB_PASSWORD"
    "PROD_REDIS_PASSWORD"
    "PROD_GOOGLE_PLACES_API_KEY"
    "PROD_JWT_SECRET_KEY"
    "PROD_DOMAIN_NAME"
    "PROD_SSL_EMAIL"
)

for var in "${required_vars[@]}"; do
    if [ -z "${!var}" ]; then
        print_error "Required production environment variable $var is not set in $ENV_FILE"
        exit 1
    fi
done

print_success "Production environment validation passed"

# Pre-deployment safety checks
print_status "Running pre-deployment safety checks..."

# Check disk space
available_space=$(df / | awk 'NR==2{printf "%.0f", $4/1024/1024}')
if [ "$available_space" -lt 2 ]; then
    print_error "Insufficient disk space. At least 2GB required, found ${available_space}GB"
    exit 1
fi

# Check memory
available_memory=$(free -m | awk 'NR==2{printf "%.0f", $7}')
if [ "$available_memory" -lt 512 ]; then
    print_error "Insufficient available memory. At least 512MB required, found ${available_memory}MB"
    exit 1
fi

print_success "System resource checks passed"

# Create backup directory
print_status "Creating backup directory..."
mkdir -p "$BACKUP_DIR"

# Backup current deployment (if exists)
if docker-compose -f "$COMPOSE_FILE" ps -q >/dev/null 2>&1; then
    print_status "Creating backup of current production deployment..."
    
    # Backup database
    print_status "Backing up production database..."
    if docker-compose -f "$COMPOSE_FILE" exec -T db /opt/mssql-tools/bin/sqlcmd \
        -S localhost -U sa -P "$PROD_DB_PASSWORD" \
        -Q "BACKUP DATABASE [WhatShouldIDo] TO DISK = '/var/backups/prod_backup_$(date +%Y%m%d_%H%M%S).bak'" 2>/dev/null; then
        print_success "Database backup completed"
    else
        print_warning "Database backup failed, but continuing with deployment"
    fi
    
    # Export current Docker images
    print_status "Backing up current Docker images..."
    docker save $(docker-compose -f "$COMPOSE_FILE" config --services | xargs -I {} echo "whatshouldido-api:production-latest") \
        > "$BACKUP_DIR/current_images.tar" 2>/dev/null || print_warning "Image backup failed"
    
    print_success "Backup completed to $BACKUP_DIR"
else
    print_status "No existing production deployment found"
fi

# Pre-deployment tasks
print_status "Running pre-deployment tasks..."

# Create necessary directories
mkdir -p logs backups certificates nginx/ssl

# Set proper permissions
chmod 755 logs backups
chmod 700 certificates nginx/ssl

# Pull latest images
print_status "Pulling latest production Docker images..."
docker-compose -f "$COMPOSE_FILE" pull

# Zero-downtime deployment using blue-green strategy
print_status "Starting blue-green deployment..."

# Step 1: Start new instances alongside existing ones
print_status "Starting new application instances..."
docker-compose -f "$COMPOSE_FILE" up -d --scale api=2 --no-recreate

# Step 2: Wait for new instances to be healthy
print_status "Waiting for new instances to become healthy..."
timeout=0
healthy_instances=0

while [ $timeout -lt $MAX_WAIT_TIME ] && [ $healthy_instances -lt 2 ]; do
    healthy_instances=$(docker-compose -f "$COMPOSE_FILE" ps api | grep -c "Up (healthy)" || echo "0")
    echo -n "."
    sleep 10
    timeout=$((timeout + 10))
done
echo ""

if [ $healthy_instances -lt 2 ]; then
    print_error "New instances failed to become healthy within $MAX_WAIT_TIME seconds"
    
    # Rollback
    print_status "Rolling back to previous version..."
    docker-compose -f "$COMPOSE_FILE" up -d --scale api=1
    
    # Show logs for debugging
    print_status "Recent logs from failed deployment:"
    docker-compose -f "$COMPOSE_FILE" logs api --tail=50
    
    exit 1
fi

print_success "New instances are healthy"

# Step 3: Health check on new instances
print_status "Performing comprehensive health checks..."

# API health check
for i in {1..5}; do
    if curl -f "$HEALTH_CHECK_URL" -m 10 >/dev/null 2>&1; then
        print_success "API health check $i/5 passed"
    else
        print_error "API health check $i/5 failed"
        
        # Rollback on health check failure
        print_status "Rolling back due to health check failure..."
        docker-compose -f "$COMPOSE_FILE" up -d --scale api=1
        exit 1
    fi
    
    sleep 2
done

# Database connectivity check
print_status "Verifying database connectivity..."
if docker-compose -f "$COMPOSE_FILE" exec -T db /opt/mssql-tools/bin/sqlcmd \
    -S localhost -U sa -P "$PROD_DB_PASSWORD" -Q "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES" >/dev/null 2>&1; then
    print_success "Database connectivity verified"
else
    print_error "Database connectivity failed"
    docker-compose -f "$COMPOSE_FILE" up -d --scale api=1
    exit 1
fi

# Step 4: Switch to new instances only
print_status "Switching to new instances..."
docker-compose -f "$COMPOSE_FILE" up -d --scale api=1

# Wait a moment for the switch
sleep 10

# Step 5: Final health check
print_status "Final health verification..."
if curl -f "$HEALTH_CHECK_URL" -m 10 >/dev/null 2>&1; then
    print_success "Final health check passed"
else
    print_error "Final health check failed - this is critical!"
    
    # Emergency rollback
    print_status "Performing emergency rollback..."
    if [ -f "$BACKUP_DIR/current_images.tar" ]; then
        docker load < "$BACKUP_DIR/current_images.tar"
        docker-compose -f "$COMPOSE_FILE" up -d
    fi
    
    exit 1
fi

# Run database migrations
print_status "Running database migrations..."
if docker-compose -f "$COMPOSE_FILE" exec -T api dotnet ef database update; then
    print_success "Database migrations completed"
else
    print_warning "Database migrations failed - manual intervention may be needed"
fi

# Clean up old Docker resources
print_status "Cleaning up old Docker resources..."
docker system prune -f >/dev/null 2>&1

# Update SSL certificates if needed
if [ ! -z "$PROD_DOMAIN_NAME" ] && [ ! -z "$PROD_SSL_EMAIL" ]; then
    print_status "Updating SSL certificates..."
    if command -v certbot >/dev/null 2>&1; then
        certbot renew --quiet || print_warning "SSL certificate renewal failed"
    else
        print_warning "Certbot not found, skipping SSL renewal"
    fi
fi

# Restart Nginx to pick up any changes
if systemctl is-active --quiet nginx; then
    print_status "Restarting Nginx..."
    systemctl reload nginx || print_warning "Nginx reload failed"
fi

# Post-deployment verification
print_status "Running post-deployment verification..."

# Test critical endpoints
endpoints=(
    "/api/health"
    "/api/discover/random"
)

for endpoint in "${endpoints[@]}"; do
    url="http://localhost:5000$endpoint"
    if curl -f "$url" -m 10 >/dev/null 2>&1; then
        print_success "Endpoint $endpoint is responding"
    else
        print_warning "Endpoint $endpoint failed to respond"
    fi
done

# Check resource usage
print_status "Checking resource usage..."
cpu_usage=$(top -bn1 | grep "Cpu(s)" | awk '{print $2}' | awk -F'%' '{print $1}')
memory_usage=$(free | grep Mem | awk '{printf "%.1f", $3/$2 * 100.0}')

echo "   CPU Usage: ${cpu_usage}%"
echo "   Memory Usage: ${memory_usage}%"

# Display deployment summary
print_success "🎉 Production deployment completed successfully!"
echo ""
echo "📋 Deployment Summary:"
echo "   Environment: $ENVIRONMENT"
echo "   Timestamp: $(date)"
echo "   Backup Location: $BACKUP_DIR"
echo "   Domain: $PROD_DOMAIN_NAME"
echo ""
echo "🌐 Production URLs:"
if [ ! -z "$PROD_DOMAIN_NAME" ]; then
    echo "   API: https://$PROD_DOMAIN_NAME"
    echo "   Health Check: https://$PROD_DOMAIN_NAME/api/health"
else
    echo "   API: http://localhost:5000"
    echo "   Health Check: $HEALTH_CHECK_URL"
fi
echo ""
echo "🐳 Container Status:"
docker-compose -f "$COMPOSE_FILE" ps
echo ""
echo "📊 System Status:"
echo "   CPU: ${cpu_usage}%"
echo "   Memory: ${memory_usage}%"
echo "   Disk: $(df -h / | awk 'NR==2{print $5}') used"
echo ""
echo "🛠️ Management Commands:"
echo "   View logs: docker-compose -f $COMPOSE_FILE logs -f"
echo "   Restart API: docker-compose -f $COMPOSE_FILE restart api"
echo "   Scale API: docker-compose -f $COMPOSE_FILE up -d --scale api=N"
echo "   Rollback: Use backup in $BACKUP_DIR"
echo ""

# Send deployment notification (if configured)
if command -v curl >/dev/null 2>&1 && [ ! -z "$SLACK_WEBHOOK_URL" ]; then
    curl -X POST -H 'Content-type: application/json' \
        --data '{"text":"🚀 WhatShouldIDo API production deployment completed successfully!"}' \
        "$SLACK_WEBHOOK_URL" >/dev/null 2>&1 || print_warning "Slack notification failed"
fi

print_success "Production deployment completed! 🎉"
print_status "Monitor the application closely for the next few minutes"