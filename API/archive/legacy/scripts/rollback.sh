#!/bin/bash

# WhatShouldIDo Rollback Script
# This script handles rollbacks for both development and production environments

set -e

# Load common functions
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/deploy-common.sh"

print_header "🔄 WhatShouldIDo API Rollback System"
echo "===================================="

# Configuration
ENVIRONMENT=""
BACKUP_DIR=""
TARGET_VERSION=""
COMPOSE_FILE=""
ROLLBACK_REASON=""
EMERGENCY_MODE=false
SKIP_CONFIRMATIONS=false

# Function to show usage
show_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  -e, --environment ENV     Target environment (development|production)"
    echo "  -b, --backup DIR         Backup directory to restore from"
    echo "  -v, --version VERSION    Specific version to rollback to"
    echo "  -r, --reason REASON      Reason for rollback (required for production)"
    echo "  --emergency              Emergency rollback mode (bypasses some checks)"
    echo "  --skip-confirmations     Skip confirmation prompts (use with caution)"
    echo "  --list-backups           List available backups"
    echo "  --help                   Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 --environment production --backup backups/20231201_120000_production"
    echo "  $0 --environment development --version 2.1.0-abc123"
    echo "  $0 --list-backups"
    echo ""
}

# Function to list available backups
list_backups() {
    local env="$1"
    
    print_status "Available backups for $env environment:"
    echo ""
    
    if [ -d "backups" ]; then
        local backup_count=0
        
        for backup in $(ls -1t backups/ | head -20); do  # Show latest 20 backups
            if [[ "$backup" == *"$env"* ]] || [ -z "$env" ]; then
                backup_count=$((backup_count + 1))
                local backup_path="backups/$backup"
                
                echo "📁 $backup"
                
                if [ -f "$backup_path/manifest.txt" ]; then
                    echo "   $(grep "Backup created:" "$backup_path/manifest.txt" | head -1)"
                    echo "   $(grep "Git commit:" "$backup_path/manifest.txt" | head -1)"
                    echo "   $(grep "Git branch:" "$backup_path/manifest.txt" | head -1)"
                else
                    echo "   Created: $(stat -c %y "$backup_path" 2>/dev/null || date)"
                fi
                
                echo ""
            fi
        done
        
        if [ $backup_count -eq 0 ]; then
            print_warning "No backups found for environment: $env"
        else
            echo "Found $backup_count backup(s)"
        fi
    else
        print_error "Backup directory not found"
        exit 1
    fi
}

# Function to validate rollback request
validate_rollback_request() {
    local env="$1"
    local backup_dir="$2"
    local reason="$3"
    
    print_status "Validating rollback request..."
    
    # Check environment
    if [ "$env" != "development" ] && [ "$env" != "production" ]; then
        print_error "Invalid environment: $env. Must be 'development' or 'production'"
        return 1
    fi
    
    # Production specific validations
    if [ "$env" = "production" ]; then
        if [ -z "$reason" ] && [ "$EMERGENCY_MODE" = false ]; then
            print_error "Rollback reason is required for production environment"
            return 1
        fi
        
        if [ "$SKIP_CONFIRMATIONS" = false ]; then
            echo ""
            print_warning "⚠️  PRODUCTION ROLLBACK REQUESTED ⚠️"
            echo "Environment: $env"
            echo "Backup: $backup_dir"
            echo "Reason: $reason"
            echo "Emergency: $EMERGENCY_MODE"
            echo ""
            
            read -p "Are you absolutely sure you want to proceed? (type 'ROLLBACK' to confirm): " confirmation
            if [ "$confirmation" != "ROLLBACK" ]; then
                print_error "Rollback cancelled by user"
                exit 1
            fi
        fi
    fi
    
    # Check backup directory
    if [ ! -z "$backup_dir" ] && [ ! -d "$backup_dir" ]; then
        print_error "Backup directory not found: $backup_dir"
        return 1
    fi
    
    print_success "Rollback request validated"
    return 0
}

# Function to create pre-rollback backup
create_pre_rollback_backup() {
    local env="$1"
    local compose_file="$2"
    
    print_status "Creating pre-rollback backup..."
    
    local pre_rollback_backup="backups/pre-rollback-$(date +%Y%m%d_%H%M%S)_${env}"
    mkdir -p "$pre_rollback_backup"
    
    # Backup current state
    if [ -f ".env.$env" ]; then
        cp ".env.$env" "$pre_rollback_backup/"
    fi
    
    if [ -f "$compose_file" ]; then
        cp "$compose_file" "$pre_rollback_backup/"
    fi
    
    # Save current deployment state
    docker-compose -f "$compose_file" ps > "$pre_rollback_backup/containers-state.txt" 2>/dev/null || true
    docker-compose -f "$compose_file" config > "$pre_rollback_backup/compose-config.yml" 2>/dev/null || true
    
    # Create rollback manifest
    cat > "$pre_rollback_backup/rollback-manifest.txt" << EOF
Pre-rollback backup created: $(date)
Environment: $env
Original backup being restored: $BACKUP_DIR
Rollback reason: $ROLLBACK_REASON
Emergency mode: $EMERGENCY_MODE
Git commit before rollback: $(git rev-parse HEAD 2>/dev/null || echo "unknown")
Git branch before rollback: $(git branch --show-current 2>/dev/null || echo "unknown")
Rollback initiated by: $(whoami)
Rollback script version: 1.0
EOF
    
    print_success "Pre-rollback backup created: $pre_rollback_backup"
    echo "$pre_rollback_backup"
}

# Function to perform rollback
perform_rollback() {
    local env="$1"
    local backup_dir="$2"
    local compose_file="$3"
    
    print_status "Starting rollback process..."
    
    local start_time=$(date +%s)
    
    # Step 1: Stop current services
    print_status "Step 1: Stopping current services..."
    docker-compose -f "$compose_file" down --remove-orphans
    
    # Step 2: Restore configuration files
    print_status "Step 2: Restoring configuration files..."
    if [ -f "$backup_dir/.env.$env" ]; then
        cp "$backup_dir/.env.$env" ".env.$env"
        print_success "Environment configuration restored"
    fi
    
    if [ -f "$backup_dir/docker-compose.*.yml" ]; then
        cp "$backup_dir"/docker-compose.*.yml ./
        print_success "Docker compose files restored"
    fi
    
    # Step 3: Restore Docker images
    print_status "Step 3: Restoring Docker images..."
    if [ -f "$backup_dir/current_images.tar" ]; then
        print_status "Loading Docker images from backup..."
        docker load < "$backup_dir/current_images.tar"
        print_success "Docker images restored"
    else
        print_warning "No Docker images found in backup, using current images"
    fi
    
    # Step 4: Restore database (if backup exists)
    print_status "Step 4: Checking for database backup..."
    if ls "$backup_dir"/*.bak 1> /dev/null 2>&1; then
        print_status "Database backup found, attempting to restore..."
        
        # Start database service first
        docker-compose -f "$compose_file" up -d db-${env} 2>/dev/null || docker-compose -f "$compose_file" up -d db
        
        # Wait for database to be ready
        sleep 30
        
        # Restore database
        local db_backup=$(ls "$backup_dir"/*.bak | head -1)
        print_status "Restoring database from: $(basename "$db_backup")"
        
        # This is a simplified restore - adjust based on your database setup
        if [ "$env" = "production" ]; then
            docker-compose -f "$compose_file" exec -T db /opt/mssql-tools/bin/sqlcmd \
                -S localhost -U sa -P "${PROD_DB_PASSWORD:-${SQL_SA_PASSWORD}}" \
                -Q "RESTORE DATABASE [WhatShouldIDo] FROM DISK = '/var/backups/$(basename "$db_backup")' WITH REPLACE" || {
                print_warning "Database restore failed, continuing with rollback"
            }
        else
            docker-compose -f "$compose_file" exec -T db-dev /opt/mssql-tools/bin/sqlcmd \
                -S localhost -U sa -P "${SQL_SA_PASSWORD}" \
                -Q "RESTORE DATABASE [WhatShouldIDo] FROM DISK = '/var/backups/$(basename "$db_backup")' WITH REPLACE" || {
                print_warning "Database restore failed, continuing with rollback"
            }
        fi
        
        print_success "Database restore attempted"
    else
        print_warning "No database backup found in backup directory"
    fi
    
    # Step 5: Start services
    print_status "Step 5: Starting services with restored configuration..."
    docker-compose -f "$compose_file" up -d
    
    # Step 6: Wait for services to be ready
    print_status "Step 6: Waiting for services to be ready..."
    local health_url=""
    if [ "$env" = "production" ]; then
        health_url="http://localhost:5000/api/health"
    else
        health_url="http://localhost:5001/api/health"
    fi
    
    if ! wait_for_service_health "API" "$health_url" 300; then
        print_error "Services failed to start after rollback"
        return 1
    fi
    
    # Step 7: Run basic smoke tests
    print_status "Step 7: Running post-rollback verification..."
    local api_base_url=""
    if [ "$env" = "production" ]; then
        api_base_url="http://localhost:5000"
    else
        api_base_url="http://localhost:5001"
    fi
    
    if ! run_smoke_tests "$api_base_url" "$env"; then
        print_warning "Some post-rollback tests failed"
    fi
    
    local end_time=$(date +%s)
    local rollback_duration=$((end_time - start_time))
    
    print_success "Rollback completed in ${rollback_duration}s"
    
    # Log rollback completion
    cat >> "$backup_dir/rollback-log.txt" << EOF
Rollback completed: $(date)
Duration: ${rollback_duration}s
Status: SUCCESS
Post-rollback health check: $(curl -f "$health_url" -m 10 >/dev/null 2>&1 && echo "PASSED" || echo "FAILED")
EOF
    
    return 0
}

# Function to send rollback notifications
send_rollback_notifications() {
    local env="$1"
    local backup_dir="$2"
    local status="$3"  # SUCCESS, FAILED
    local duration="$4"
    
    local message=""
    local color=""
    
    if [ "$status" = "SUCCESS" ]; then
        message="🔄 **ROLLBACK COMPLETED**"
        color="warning"
    else
        message="💥 **ROLLBACK FAILED**"
        color="danger"
    fi
    
    message="$message
**Environment**: ${env^^}
**Backup**: $(basename "$backup_dir")
**Reason**: $ROLLBACK_REASON
**Duration**: ${duration}s
**Initiated by**: $(whoami)
**Timestamp**: $(date)"
    
    # Send to Slack if configured
    if [ ! -z "$SLACK_WEBHOOK_URL" ]; then
        curl -X POST -H 'Content-type: application/json' \
            --data "{\"text\":\"$message\"}" \
            "$SLACK_WEBHOOK_URL" >/dev/null 2>&1 || true
    fi
    
    # Send email if configured
    if [ ! -z "$NOTIFICATION_EMAIL" ] && command -v mail >/dev/null 2>&1; then
        echo -e "$message" | mail -s "WhatShouldIDo Rollback - $env" "$NOTIFICATION_EMAIL" || true
    fi
    
    # Log to system log
    logger "WhatShouldIDo rollback: $env - $status - $(basename "$backup_dir")"
}

# Main rollback function
main_rollback() {
    local start_time=$(date +%s)
    
    # Validate inputs
    validate_rollback_request "$ENVIRONMENT" "$BACKUP_DIR" "$ROLLBACK_REASON"
    
    # Determine compose file
    if [ "$ENVIRONMENT" = "production" ]; then
        COMPOSE_FILE="docker-compose.prod.yml"
    else
        COMPOSE_FILE="docker-compose.dev.yml"
    fi
    
    # Create pre-rollback backup
    local pre_rollback_backup=$(create_pre_rollback_backup "$ENVIRONMENT" "$COMPOSE_FILE")
    
    # Perform rollback
    if perform_rollback "$ENVIRONMENT" "$BACKUP_DIR" "$COMPOSE_FILE"; then
        local end_time=$(date +%s)
        local total_duration=$((end_time - start_time))
        
        print_success "🎉 Rollback completed successfully!"
        
        echo ""
        echo "📋 Rollback Summary:"
        echo "   Environment: $ENVIRONMENT"
        echo "   Backup restored: $BACKUP_DIR"
        echo "   Pre-rollback backup: $pre_rollback_backup"
        echo "   Duration: ${total_duration}s"
        echo "   Reason: $ROLLBACK_REASON"
        echo ""
        
        # Send success notification
        send_rollback_notifications "$ENVIRONMENT" "$BACKUP_DIR" "SUCCESS" "$total_duration"
        
        return 0
    else
        local end_time=$(date +%s)
        local total_duration=$((end_time - start_time))
        
        print_error "Rollback failed!"
        
        # Send failure notification
        send_rollback_notifications "$ENVIRONMENT" "$BACKUP_DIR" "FAILED" "$total_duration"
        
        return 1
    fi
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -b|--backup)
            BACKUP_DIR="$2"
            shift 2
            ;;
        -v|--version)
            TARGET_VERSION="$2"
            shift 2
            ;;
        -r|--reason)
            ROLLBACK_REASON="$2"
            shift 2
            ;;
        --emergency)
            EMERGENCY_MODE=true
            shift
            ;;
        --skip-confirmations)
            SKIP_CONFIRMATIONS=true
            shift
            ;;
        --list-backups)
            list_backups "$2"
            exit 0
            ;;
        --help)
            show_usage
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Validate required parameters
if [ -z "$ENVIRONMENT" ]; then
    print_error "Environment is required. Use --environment to specify."
    show_usage
    exit 1
fi

# If no backup specified but version is provided, try to find backup
if [ -z "$BACKUP_DIR" ] && [ ! -z "$TARGET_VERSION" ]; then
    print_status "Looking for backup with version: $TARGET_VERSION"
    
    # Search for backup containing the version
    BACKUP_DIR=$(find backups -name "*$ENVIRONMENT*" -type d | xargs -I {} grep -l "$TARGET_VERSION" {}/manifest.txt 2>/dev/null | head -1 | xargs dirname)
    
    if [ ! -z "$BACKUP_DIR" ]; then
        print_success "Found backup: $BACKUP_DIR"
    else
        print_error "No backup found for version: $TARGET_VERSION"
        echo ""
        list_backups "$ENVIRONMENT"
        exit 1
    fi
fi

# If still no backup, list available backups and exit
if [ -z "$BACKUP_DIR" ]; then
    print_error "No backup specified. Use --backup to specify a backup directory."
    echo ""
    list_backups "$ENVIRONMENT"
    exit 1
fi

# Run main rollback
main_rollback