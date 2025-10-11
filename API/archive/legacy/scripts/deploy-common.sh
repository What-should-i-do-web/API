#!/bin/bash

# WhatShouldIDo Common Deployment Functions
# Shared functions used by both development and production deployment scripts

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

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

print_header() {
    echo -e "${CYAN}[HEADER]${NC} $1"
}

# Function to validate environment variables
validate_env_vars() {
    local env_file="$1"
    local required_vars=("${!2}")
    
    print_status "Validating environment variables in $env_file..."
    
    if [ ! -f "$env_file" ]; then
        print_error "Environment file $env_file not found!"
        return 1
    fi
    
    # Source the environment file
    set -a  # Export all variables
    source "$env_file"
    set +a  # Stop exporting
    
    local missing_vars=()
    for var in "${required_vars[@]}"; do
        if [ -z "${!var}" ]; then
            missing_vars+=("$var")
        fi
    done
    
    if [ ${#missing_vars[@]} -gt 0 ]; then
        print_error "Missing required environment variables:"
        printf '  - %s\n' "${missing_vars[@]}"
        print_status "Please edit $env_file and set these variables"
        return 1
    fi
    
    print_success "All required environment variables are set"
    return 0
}

# Function to check system requirements
check_system_requirements() {
    local min_memory_gb="$1"
    local min_disk_gb="$2"
    
    print_status "Checking system requirements..."
    
    # Check available memory
    if command -v free >/dev/null 2>&1; then
        available_memory_gb=$(free -g | awk 'NR==2{printf "%.1f", $7}')
        if (( $(echo "$available_memory_gb < $min_memory_gb" | bc -l 2>/dev/null || echo "0") )); then
            print_warning "Low memory: ${available_memory_gb}GB available, ${min_memory_gb}GB recommended"
        else
            print_success "Memory check passed: ${available_memory_gb}GB available"
        fi
    fi
    
    # Check available disk space
    available_disk_gb=$(df / | awk 'NR==2{printf "%.1f", $4/1024/1024}')
    if (( $(echo "$available_disk_gb < $min_disk_gb" | bc -l 2>/dev/null || echo "0") )); then
        print_error "Insufficient disk space: ${available_disk_gb}GB available, ${min_disk_gb}GB required"
        return 1
    else
        print_success "Disk space check passed: ${available_disk_gb}GB available"
    fi
    
    # Check Docker
    if ! command -v docker >/dev/null 2>&1; then
        print_error "Docker is not installed"
        return 1
    fi
    
    if ! docker ps >/dev/null 2>&1; then
        print_error "Docker is not running or not accessible"
        return 1
    fi
    
    print_success "Docker is available and running"
    
    # Check Docker Compose
    if ! command -v docker-compose >/dev/null 2>&1; then
        print_error "Docker Compose is not installed"
        return 1
    fi
    
    print_success "System requirements check passed"
    return 0
}

# Function to create backup
create_backup() {
    local environment="$1"
    local compose_file="$2"
    local backup_dir="./backups/$(date +%Y%m%d_%H%M%S)_${environment}"
    
    print_status "Creating backup for $environment environment..."
    
    mkdir -p "$backup_dir"
    
    # Backup configuration files
    if [ -f ".env.$environment" ]; then
        cp ".env.$environment" "$backup_dir/"
    fi
    
    if [ -f "$compose_file" ]; then
        cp "$compose_file" "$backup_dir/"
    fi
    
    # Backup database (if containers are running)
    if docker-compose -f "$compose_file" ps db-${environment} >/dev/null 2>&1; then
        print_status "Backing up database..."
        docker-compose -f "$compose_file" exec -T "db-${environment}" \
            /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "${SQL_SA_PASSWORD}" \
            -Q "BACKUP DATABASE [WhatShouldIDo] TO DISK = '/var/backups/${environment}_backup_$(date +%Y%m%d_%H%M%S).bak'" \
            2>/dev/null || print_warning "Database backup failed"
    fi
    
    # Export current Docker images
    if docker-compose -f "$compose_file" ps api-${environment} >/dev/null 2>&1; then
        print_status "Backing up Docker images..."
        docker save $(docker-compose -f "$compose_file" config --services | head -1) \
            > "$backup_dir/current_images.tar" 2>/dev/null || print_warning "Image backup failed"
    fi
    
    # Create backup manifest
    cat > "$backup_dir/manifest.txt" << EOF
Backup created: $(date)
Environment: $environment
Compose file: $compose_file
Git commit: $(git rev-parse HEAD 2>/dev/null || echo "unknown")
Git branch: $(git branch --show-current 2>/dev/null || echo "unknown")
Docker images: $(docker-compose -f "$compose_file" config --services | tr '\n' ' ')
EOF
    
    print_success "Backup created at: $backup_dir"
    echo "$backup_dir"
}

# Function to install dependencies
install_dependencies() {
    local project_path="$1"
    
    print_status "Installing dependencies..."
    
    # Restore NuGet packages
    if [ -f "$project_path/WhatShouldIDo.API.csproj" ]; then
        print_status "Restoring NuGet packages..."
        dotnet restore "$project_path/WhatShouldIDo.API.csproj" --verbosity minimal
        
        if [ $? -eq 0 ]; then
            print_success "Dependencies restored successfully"
        else
            print_error "Failed to restore dependencies"
            return 1
        fi
    else
        print_warning "Project file not found at $project_path, skipping NuGet restore"
    fi
    
    return 0
}

# Function to build application
build_application() {
    local project_path="$1"
    local build_config="${2:-Release}"
    
    print_status "Building application with configuration: $build_config..."
    
    if [ -f "$project_path/WhatShouldIDo.API.csproj" ]; then
        dotnet build "$project_path/WhatShouldIDo.API.csproj" \
            --configuration "$build_config" \
            --no-restore \
            --verbosity minimal
        
        if [ $? -eq 0 ]; then
            print_success "Application built successfully"
            return 0
        else
            print_error "Application build failed"
            return 1
        fi
    else
        print_warning "Project file not found, skipping application build"
        return 0
    fi
}

# Function to wait for service health
wait_for_service_health() {
    local service_name="$1"
    local health_url="$2"
    local max_wait_time="${3:-300}"
    local check_interval="${4:-10}"
    
    print_status "Waiting for $service_name to become healthy..."
    
    local elapsed=0
    while [ $elapsed -lt $max_wait_time ]; do
        if curl -f "$health_url" -m 5 >/dev/null 2>&1; then
            print_success "$service_name is healthy"
            return 0
        fi
        
        echo -n "."
        sleep $check_interval
        elapsed=$((elapsed + check_interval))
    done
    
    echo ""
    print_error "$service_name failed to become healthy within ${max_wait_time}s"
    return 1
}

# Function to run database migrations
run_database_migrations() {
    local compose_file="$1"
    local api_service="$2"
    
    print_status "Running database migrations..."
    
    # Wait a bit for the database to be ready
    sleep 10
    
    if docker-compose -f "$compose_file" exec -T "$api_service" dotnet ef database update; then
        print_success "Database migrations completed successfully"
        return 0
    else
        print_warning "Database migrations failed or are not needed"
        return 1
    fi
}

# Function to perform smoke tests
run_smoke_tests() {
    local base_url="$1"
    local environment="$2"
    
    print_status "Running smoke tests for $environment environment..."
    
    # Test endpoints
    local endpoints=(
        "/api/health"
        "/api/discover/random"
    )
    
    local failed_tests=0
    
    for endpoint in "${endpoints[@]}"; do
        local url="${base_url}${endpoint}"
        print_status "Testing endpoint: $endpoint"
        
        if curl -f "$url" -m 10 >/dev/null 2>&1; then
            print_success "✓ $endpoint responded correctly"
        else
            print_error "✗ $endpoint failed to respond"
            failed_tests=$((failed_tests + 1))
        fi
    done
    
    # Test with sample data (if API supports it)
    print_status "Testing prompt endpoint with sample data..."
    local response=$(curl -s -X POST "${base_url}/api/discover/prompt" \
        -H "Content-Type: application/json" \
        -d '{"prompt":"test","latitude":41.0082,"longitude":28.9784}' \
        -w "%{http_code}" -o /tmp/api_response.json 2>/dev/null || echo "000")
    
    if [ "$response" = "200" ] || [ "$response" = "201" ]; then
        print_success "✓ Prompt endpoint test passed"
    else
        print_warning "⚠ Prompt endpoint test failed with HTTP $response"
        failed_tests=$((failed_tests + 1))
    fi
    
    if [ $failed_tests -eq 0 ]; then
        print_success "🎉 All smoke tests passed!"
        return 0
    else
        print_warning "$failed_tests smoke test(s) failed"
        return 1
    fi
}

# Function to update service configuration
update_service_configuration() {
    local compose_file="$1"
    local environment="$2"
    
    print_status "Updating service configuration for $environment..."
    
    # Create necessary directories
    mkdir -p logs certificates nginx/ssl backups
    
    # Set proper permissions
    chmod 755 logs backups
    chmod 700 certificates nginx/ssl
    
    # Generate or update configuration files as needed
    if [ "$environment" = "production" ]; then
        # Production-specific configurations
        print_status "Applying production configurations..."
        
        # Update log rotation
        if command -v logrotate >/dev/null 2>&1; then
            cat > /etc/logrotate.d/whatshouldido << EOF
/var/log/whatshouldido/*.log {
    daily
    rotate 30
    compress
    delaycompress
    missingok
    notifempty
    create 644 root root
    postrotate
        docker-compose -f $compose_file restart api
    endscript
}
EOF
            print_success "Log rotation configured"
        fi
    else
        # Development-specific configurations
        print_status "Applying development configurations..."
    fi
    
    print_success "Service configuration updated"
}

# Function to cleanup old resources
cleanup_old_resources() {
    local keep_backups="${1:-5}"
    local keep_images="${2:-3}"
    
    print_status "Cleaning up old resources..."
    
    # Clean up old backups
    if [ -d "backups" ]; then
        local backup_count=$(ls -1 backups | wc -l)
        if [ $backup_count -gt $keep_backups ]; then
            print_status "Removing old backups (keeping latest $keep_backups)..."
            ls -1t backups | tail -n +$((keep_backups + 1)) | xargs -I {} rm -rf "backups/{}"
            print_success "Old backups cleaned up"
        fi
    fi
    
    # Clean up Docker resources
    print_status "Cleaning up Docker resources..."
    docker system prune -f >/dev/null 2>&1
    
    # Remove old unused images (keep recent ones)
    if [ "$keep_images" -gt 0 ]; then
        docker images --format "table {{.Repository}}\t{{.Tag}}\t{{.CreatedAt}}" | \
        grep whatshouldido | \
        tail -n +$((keep_images + 2)) | \
        awk '{print $1":"$2}' | \
        xargs -r docker rmi >/dev/null 2>&1 || true
    fi
    
    print_success "Resource cleanup completed"
}

# Function to send deployment notification
send_deployment_notification() {
    local environment="$1"
    local status="$2"  # success, failure, warning
    local details="$3"
    
    local message="🚀 WhatShouldIDo API Deployment - $environment"
    
    case "$status" in
        "success")
            message="✅ $message - SUCCESS"
            ;;
        "failure")
            message="❌ $message - FAILED"
            ;;
        "warning")
            message="⚠️ $message - WARNING"
            ;;
    esac
    
    if [ ! -z "$details" ]; then
        message="$message\n\nDetails: $details"
    fi
    
    message="$message\n\nTimestamp: $(date)\nBranch: $(git branch --show-current 2>/dev/null || echo 'unknown')\nCommit: $(git rev-parse --short HEAD 2>/dev/null || echo 'unknown')"
    
    # Send to Slack if webhook is configured
    if [ ! -z "$SLACK_WEBHOOK_URL" ]; then
        curl -X POST -H 'Content-type: application/json' \
            --data "{\"text\":\"$message\"}" \
            "$SLACK_WEBHOOK_URL" >/dev/null 2>&1 || true
    fi
    
    # Send email if configured
    if [ ! -z "$NOTIFICATION_EMAIL" ] && command -v mail >/dev/null 2>&1; then
        echo -e "$message" | mail -s "WhatShouldIDo Deployment - $environment" "$NOTIFICATION_EMAIL" || true
    fi
    
    # Log to system log
    logger "WhatShouldIDo deployment: $environment - $status"
}

# Function to rollback deployment
rollback_deployment() {
    local backup_dir="$1"
    local compose_file="$2"
    local environment="$3"
    
    print_warning "Rolling back deployment using backup: $backup_dir"
    
    if [ ! -d "$backup_dir" ]; then
        print_error "Backup directory not found: $backup_dir"
        return 1
    fi
    
    # Stop current services
    docker-compose -f "$compose_file" down
    
    # Restore configuration files
    if [ -f "$backup_dir/.env.$environment" ]; then
        cp "$backup_dir/.env.$environment" ".env.$environment"
        print_success "Environment configuration restored"
    fi
    
    # Restore Docker images
    if [ -f "$backup_dir/current_images.tar" ]; then
        print_status "Restoring Docker images..."
        docker load < "$backup_dir/current_images.tar"
        print_success "Docker images restored"
    fi
    
    # Start services with restored configuration
    docker-compose -f "$compose_file" up -d
    
    print_success "Rollback completed"
    
    # Send rollback notification
    send_deployment_notification "$environment" "warning" "Deployment rolled back to backup: $backup_dir"
}

# Function to display deployment summary
display_deployment_summary() {
    local environment="$1"
    local start_time="$2"
    local api_url="$3"
    local compose_file="$4"
    
    local end_time=$(date +%s)
    local deployment_time=$((end_time - start_time))
    
    print_success "🎉 $environment deployment completed!"
    echo ""
    echo "📊 Deployment Summary:"
    echo "   Environment: $environment"
    echo "   Duration: ${deployment_time}s"
    echo "   Timestamp: $(date)"
    echo "   Git Branch: $(git branch --show-current 2>/dev/null || echo 'unknown')"
    echo "   Git Commit: $(git rev-parse --short HEAD 2>/dev/null || echo 'unknown')"
    echo ""
    echo "🌐 Service URLs:"
    echo "   API: $api_url"
    echo "   Health: ${api_url}/api/health"
    echo ""
    echo "🐳 Container Status:"
    docker-compose -f "$compose_file" ps
    echo ""
    echo "📈 System Resources:"
    if command -v free >/dev/null 2>&1; then
        echo "   Memory: $(free -h | awk 'NR==2{printf "%.1f%% used", $3/$2*100}')"
    fi
    echo "   Disk: $(df -h / | awk 'NR==2{print $5 " used"}')"
    if command -v uptime >/dev/null 2>&1; then
        echo "   Load: $(uptime | awk -F'load average:' '{print $2}')"
    fi
    echo ""
}

# Export functions for use in other scripts
export -f print_status print_success print_warning print_error print_header
export -f validate_env_vars check_system_requirements create_backup
export -f install_dependencies build_application wait_for_service_health
export -f run_database_migrations run_smoke_tests update_service_configuration
export -f cleanup_old_resources send_deployment_notification rollback_deployment
export -f display_deployment_summary