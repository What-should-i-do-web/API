#!/bin/bash

# WhatShouldIDo Backup Management System
# Comprehensive backup, restore, and maintenance operations

set -e

# Load common functions
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/deploy-common.sh"

print_header "💾 WhatShouldIDo Backup Management System"
echo "=========================================="

# Configuration
BACKUP_ROOT_DIR="backups"
MAX_BACKUPS_PER_ENV=10
BACKUP_RETENTION_DAYS=30
ENVIRONMENT=""
BACKUP_TYPE="full"  # full, config-only, database-only
COMPRESSION=true
ENCRYPT_BACKUPS=false
BACKUP_PASSWORD=""
REMOTE_BACKUP_URL=""
VERIFICATION_ENABLED=true

# Function to show usage
show_usage() {
    echo "Usage: $0 COMMAND [OPTIONS]"
    echo ""
    echo "Commands:"
    echo "  create      Create a new backup"
    echo "  restore     Restore from backup"
    echo "  list        List available backups"
    echo "  cleanup     Clean up old backups"
    echo "  verify      Verify backup integrity"
    echo "  schedule    Set up automated backups"
    echo "  sync        Sync backups to remote storage"
    echo ""
    echo "Options:"
    echo "  -e, --environment ENV     Target environment (development|production)"
    echo "  -t, --type TYPE          Backup type (full|config-only|database-only)"
    echo "  -b, --backup DIR         Specific backup directory"
    echo "  -c, --compress           Enable compression (default: true)"
    echo "  -p, --password PASS      Encryption password"
    echo "  -r, --remote URL         Remote backup location"
    echo "  --retention DAYS         Retention period in days (default: 30)"
    echo "  --max-backups COUNT      Maximum backups per environment (default: 10)"
    echo "  --no-verify              Skip backup verification"
    echo "  --help                   Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 create --environment production --type full"
    echo "  $0 list --environment production"
    echo "  $0 cleanup --retention 14"
    echo "  $0 verify --backup backups/20231201_120000_production"
    echo ""
}

# Function to create comprehensive backup
create_backup() {
    local env="$1"
    local backup_type="$2"
    local timestamp=$(date +%Y%m%d_%H%M%S)
    local backup_name="${timestamp}_${env}_${backup_type}"
    local backup_dir="$BACKUP_ROOT_DIR/$backup_name"
    
    print_status "Creating $backup_type backup for $env environment..."
    
    # Create backup directory structure
    mkdir -p "$backup_dir"/{config,database,docker,logs,metadata}
    
    # Determine compose file
    local compose_file=""
    if [ "$env" = "production" ]; then
        compose_file="docker-compose.prod.yml"
    else
        compose_file="docker-compose.dev.yml"
    fi
    
    # Create backup manifest
    cat > "$backup_dir/metadata/manifest.json" << EOF
{
    "backup_id": "$backup_name",
    "timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
    "environment": "$env",
    "type": "$backup_type",
    "version": "$(git describe --tags --always 2>/dev/null || echo 'unknown')",
    "git_commit": "$(git rev-parse HEAD 2>/dev/null || echo 'unknown')",
    "git_branch": "$(git branch --show-current 2>/dev/null || echo 'unknown')",
    "created_by": "$(whoami)",
    "hostname": "$(hostname)",
    "docker_version": "$(docker --version 2>/dev/null || echo 'unknown')",
    "backup_script_version": "1.0",
    "compression_enabled": $COMPRESSION,
    "encryption_enabled": $ENCRYPT_BACKUPS
}
EOF
    
    # Backup configuration files
    if [ "$backup_type" = "full" ] || [ "$backup_type" = "config-only" ]; then
        print_status "Backing up configuration files..."
        
        # Environment files
        if [ -f ".env.$env" ]; then
            cp ".env.$env" "$backup_dir/config/"
        fi
        
        if [ -f ".env.example" ]; then
            cp ".env.example" "$backup_dir/config/"
        fi
        
        # Docker compose files
        if [ -f "$compose_file" ]; then
            cp "$compose_file" "$backup_dir/config/"
        fi
        
        # Additional config files
        for config_file in docker-compose.*.yml Dockerfile* nginx.conf *.json *.yml; do
            if [ -f "$config_file" ]; then
                cp "$config_file" "$backup_dir/config/" 2>/dev/null || true
            fi
        done
        
        # Backup scripts and documentation
        cp -r scripts "$backup_dir/" 2>/dev/null || true
        cp *.md "$backup_dir/" 2>/dev/null || true
        
        print_success "Configuration files backed up"
    fi
    
    # Backup database
    if [ "$backup_type" = "full" ] || [ "$backup_type" = "database-only" ]; then
        print_status "Backing up database..."
        
        # Check if database is running
        local db_service="db"
        if [ "$env" = "development" ]; then
            db_service="db-dev"
        fi
        
        if docker-compose -f "$compose_file" ps "$db_service" 2>/dev/null | grep -q "Up"; then
            local db_password=""
            if [ "$env" = "production" ]; then
                db_password="${PROD_DB_PASSWORD:-${SQL_SA_PASSWORD}}"
            else
                db_password="${SQL_SA_PASSWORD}"
            fi
            
            # Create database backup
            local db_backup_file="database_${timestamp}.bak"
            docker-compose -f "$compose_file" exec -T "$db_service" /opt/mssql-tools/bin/sqlcmd \
                -S localhost -U sa -P "$db_password" \
                -Q "BACKUP DATABASE [WhatShouldIDo] TO DISK = '/var/backups/$db_backup_file' WITH FORMAT, INIT, COMPRESSION" \
                && print_success "Database backup created: $db_backup_file" \
                || print_warning "Database backup failed"
            
            # Copy database backup to backup directory
            if docker-compose -f "$compose_file" exec -T "$db_service" test -f "/var/backups/$db_backup_file"; then
                docker cp "$(docker-compose -f "$compose_file" ps -q "$db_service"):/var/backups/$db_backup_file" "$backup_dir/database/"
                
                # Create database schema dump for reference
                docker-compose -f "$compose_file" exec -T "$db_service" /opt/mssql-tools/bin/sqlcmd \
                    -S localhost -U sa -P "$db_password" \
                    -Q "SELECT SCHEMA_NAME(schema_id) AS SchemaName, name AS TableName FROM sys.tables ORDER BY SchemaName, TableName" \
                    > "$backup_dir/database/schema_info.txt" 2>/dev/null || true
            fi
        else
            print_warning "Database service not running, skipping database backup"
        fi
    fi
    
    # Backup Docker images and containers
    if [ "$backup_type" = "full" ]; then
        print_status "Backing up Docker images..."
        
        # Get list of images used by the application
        local app_images=$(docker-compose -f "$compose_file" config --services | \
            xargs -I {} docker-compose -f "$compose_file" images -q {} | sort -u)
        
        if [ ! -z "$app_images" ]; then
            # Save Docker images
            echo "$app_images" | xargs docker save > "$backup_dir/docker/images.tar"
            
            # Save image information
            docker-compose -f "$compose_file" config > "$backup_dir/docker/compose-resolved.yml"
            docker-compose -f "$compose_file" images > "$backup_dir/docker/images-info.txt"
            docker-compose -f "$compose_file" ps > "$backup_dir/docker/containers-info.txt"
            
            print_success "Docker images backed up"
        else
            print_warning "No Docker images found to backup"
        fi
    fi
    
    # Backup logs
    print_status "Backing up logs..."
    if [ -d "logs" ]; then
        cp -r logs "$backup_dir/" 2>/dev/null || true
    fi
    
    # Backup application data (if any)
    if [ -d "data" ]; then
        print_status "Backing up application data..."
        cp -r data "$backup_dir/" 2>/dev/null || true
    fi
    
    # Create checksums for verification
    if [ "$VERIFICATION_ENABLED" = true ]; then
        print_status "Creating checksums for verification..."
        find "$backup_dir" -type f -exec sha256sum {} \; > "$backup_dir/metadata/checksums.txt"
    fi
    
    # Compress backup if enabled
    if [ "$COMPRESSION" = true ]; then
        print_status "Compressing backup..."
        local compressed_file="$backup_dir.tar.gz"
        
        tar -czf "$compressed_file" -C "$BACKUP_ROOT_DIR" "$backup_name"
        
        if [ $? -eq 0 ]; then
            rm -rf "$backup_dir"
            backup_dir="$compressed_file"
            print_success "Backup compressed: $(basename "$compressed_file")"
        else
            print_warning "Compression failed, keeping uncompressed backup"
        fi
    fi
    
    # Encrypt backup if enabled
    if [ "$ENCRYPT_BACKUPS" = true ] && [ ! -z "$BACKUP_PASSWORD" ]; then
        print_status "Encrypting backup..."
        
        if command -v gpg >/dev/null 2>&1; then
            gpg --cipher-algo AES256 --compress-algo 1 --s2k-mode 3 \
                --s2k-digest-algo SHA512 --s2k-count 65536 --symmetric \
                --passphrase "$BACKUP_PASSWORD" --batch --yes \
                --output "${backup_dir}.gpg" "$backup_dir"
            
            if [ $? -eq 0 ]; then
                rm -f "$backup_dir"
                backup_dir="${backup_dir}.gpg"
                print_success "Backup encrypted"
            else
                print_warning "Encryption failed, keeping unencrypted backup"
            fi
        else
            print_warning "GPG not available, skipping encryption"
        fi
    fi
    
    # Calculate final backup size
    local backup_size=$(du -sh "$backup_dir" | cut -f1)
    
    # Update manifest with final information
    local final_manifest="$BACKUP_ROOT_DIR/${backup_name}_final_manifest.json"
    cat > "$final_manifest" << EOF
{
    "backup_id": "$backup_name",
    "final_path": "$backup_dir",
    "size": "$backup_size",
    "completion_time": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
    "status": "completed",
    "files_count": $(find "$backup_dir" -type f 2>/dev/null | wc -l || echo 0)
}
EOF
    
    print_success "🎉 Backup completed!"
    echo ""
    echo "📁 Backup Details:"
    echo "   Name: $backup_name"
    echo "   Path: $backup_dir"
    echo "   Size: $backup_size"
    echo "   Type: $backup_type"
    echo "   Environment: $env"
    echo "   Compressed: $COMPRESSION"
    echo "   Encrypted: $ENCRYPT_BACKUPS"
    echo ""
    
    return 0
}

# Function to list backups with detailed information
list_backups() {
    local env_filter="$1"
    local format="${2:-table}"  # table, json, csv
    
    print_status "Listing backups..."
    
    if [ ! -d "$BACKUP_ROOT_DIR" ]; then
        print_warning "No backup directory found"
        return 1
    fi
    
    local backups=()
    
    # Collect backup information
    for backup in $(ls -1t "$BACKUP_ROOT_DIR"/ 2>/dev/null || true); do
        local backup_path="$BACKUP_ROOT_DIR/$backup"
        
        # Skip if not a backup (no timestamp pattern)
        if [[ ! "$backup" =~ ^[0-9]{8}_[0-9]{6}_ ]]; then
            continue
        fi
        
        # Filter by environment if specified
        if [ ! -z "$env_filter" ] && [[ "$backup" != *"$env_filter"* ]]; then
            continue
        fi
        
        local size=""
        local type=""
        local env=""
        local date=""
        local status="unknown"
        
        # Extract information from backup name
        if [[ "$backup" =~ ^([0-9]{8}_[0-9]{6})_([^_]+)_(.+)$ ]]; then
            date="${BASH_REMATCH[1]}"
            env="${BASH_REMATCH[2]}"
            type="${BASH_REMATCH[3]}"
        fi
        
        # Get size
        if [ -f "$backup_path" ]; then
            size=$(du -sh "$backup_path" | cut -f1)
            status="compressed"
        elif [ -d "$backup_path" ]; then
            size=$(du -sh "$backup_path" | cut -f1)
            status="directory"
        fi
        
        # Check for manifest
        local manifest_file=""
        if [ -d "$backup_path" ] && [ -f "$backup_path/metadata/manifest.json" ]; then
            manifest_file="$backup_path/metadata/manifest.json"
        elif [ -f "$BACKUP_ROOT_DIR/${backup%.*}_final_manifest.json" ]; then
            manifest_file="$BACKUP_ROOT_DIR/${backup%.*}_final_manifest.json"
        fi
        
        backups+=("$backup|$date|$env|$type|$size|$status|$manifest_file")
    done
    
    # Display results
    if [ ${#backups[@]} -eq 0 ]; then
        print_warning "No backups found"
        return 0
    fi
    
    case "$format" in
        "json")
            echo "["
            local first=true
            for backup_info in "${backups[@]}"; do
                IFS='|' read -r name date env type size status manifest <<< "$backup_info"
                
                if [ "$first" = true ]; then
                    first=false
                else
                    echo ","
                fi
                
                echo "  {"
                echo "    \"name\": \"$name\","
                echo "    \"date\": \"$date\","
                echo "    \"environment\": \"$env\","
                echo "    \"type\": \"$type\","
                echo "    \"size\": \"$size\","
                echo "    \"status\": \"$status\""
                echo -n "  }"
            done
            echo ""
            echo "]"
            ;;
        "csv")
            echo "Name,Date,Environment,Type,Size,Status"
            for backup_info in "${backups[@]}"; do
                IFS='|' read -r name date env type size status manifest <<< "$backup_info"
                echo "$name,$date,$env,$type,$size,$status"
            done
            ;;
        "table"|*)
            printf "%-30s %-12s %-12s %-15s %-8s %-10s\n" "NAME" "DATE" "ENVIRONMENT" "TYPE" "SIZE" "STATUS"
            printf "%-30s %-12s %-12s %-15s %-8s %-10s\n" "----" "----" "-----------" "----" "----" "------"
            
            for backup_info in "${backups[@]}"; do
                IFS='|' read -r name date env type size status manifest <<< "$backup_info"
                
                # Format date for display
                local display_date=$(echo "$date" | sed 's/_/ /')
                
                printf "%-30s %-12s %-12s %-15s %-8s %-10s\n" \
                    "${name:0:28}${name:28:+..}" \
                    "$display_date" \
                    "$env" \
                    "$type" \
                    "$size" \
                    "$status"
            done
            
            echo ""
            echo "Total backups: ${#backups[@]}"
            ;;
    esac
}

# Function to clean up old backups
cleanup_backups() {
    local retention_days="$1"
    local max_backups="$2"
    local env_filter="$3"
    local dry_run="${4:-false}"
    
    print_status "Cleaning up old backups..."
    echo "Retention: ${retention_days} days, Max per environment: ${max_backups}"
    
    if [ "$dry_run" = true ]; then
        print_warning "DRY RUN MODE - No files will be deleted"
    fi
    
    local deleted_count=0
    local freed_space=0
    
    # Clean up by age
    if [ $retention_days -gt 0 ]; then
        print_status "Removing backups older than ${retention_days} days..."
        
        while IFS= read -r -d '' backup; do
            local backup_name=$(basename "$backup")
            
            # Skip if environment filter doesn't match
            if [ ! -z "$env_filter" ] && [[ "$backup_name" != *"$env_filter"* ]]; then
                continue
            fi
            
            local backup_size=$(du -sb "$backup" | cut -f1)
            
            if [ "$dry_run" = true ]; then
                print_status "Would delete: $backup_name ($(du -sh "$backup" | cut -f1))"
            else
                print_status "Deleting old backup: $backup_name"
                rm -rf "$backup"
            fi
            
            deleted_count=$((deleted_count + 1))
            freed_space=$((freed_space + backup_size))
            
        done < <(find "$BACKUP_ROOT_DIR" -maxdepth 1 -type d -mtime +$retention_days -print0 2>/dev/null || true)
        
        # Also handle compressed/encrypted backups
        while IFS= read -r -d '' backup; do
            local backup_name=$(basename "$backup")
            
            # Skip if environment filter doesn't match
            if [ ! -z "$env_filter" ] && [[ "$backup_name" != *"$env_filter"* ]]; then
                continue
            fi
            
            local backup_size=$(du -sb "$backup" | cut -f1)
            
            if [ "$dry_run" = true ]; then
                print_status "Would delete: $backup_name ($(du -sh "$backup" | cut -f1))"
            else
                print_status "Deleting old backup: $backup_name"
                rm -f "$backup"
            fi
            
            deleted_count=$((deleted_count + 1))
            freed_space=$((freed_space + backup_size))
            
        done < <(find "$BACKUP_ROOT_DIR" -maxdepth 1 -type f \( -name "*.tar.gz" -o -name "*.gpg" \) -mtime +$retention_days -print0 2>/dev/null || true)
    fi
    
    # Clean up by count per environment
    if [ $max_backups -gt 0 ]; then
        print_status "Ensuring max ${max_backups} backups per environment..."
        
        # Get unique environments
        local environments=()
        if [ ! -z "$env_filter" ]; then
            environments=("$env_filter")
        else
            # Extract environments from backup names
            for backup in "$BACKUP_ROOT_DIR"/*; do
                if [[ $(basename "$backup") =~ ^[0-9]{8}_[0-9]{6}_([^_]+)_ ]]; then
                    local env="${BASH_REMATCH[1]}"
                    if [[ ! " ${environments[@]} " =~ " ${env} " ]]; then
                        environments+=("$env")
                    fi
                fi
            done
        fi
        
        # Process each environment
        for env in "${environments[@]}"; do
            print_status "Checking $env environment backups..."
            
            local env_backups=()
            
            # Collect backups for this environment
            for backup in "$BACKUP_ROOT_DIR"/*; do
                if [[ $(basename "$backup") == *"$env"* ]]; then
                    env_backups+=("$backup")
                fi
            done
            
            # Sort by modification time (newest first)
            IFS=$'\n' sorted_backups=($(printf '%s\n' "${env_backups[@]}" | xargs ls -1t 2>/dev/null || true))
            
            # Remove excess backups
            local count=0
            for backup in "${sorted_backups[@]}"; do
                count=$((count + 1))
                
                if [ $count -gt $max_backups ]; then
                    local backup_name=$(basename "$backup")
                    local backup_size=$(du -sb "$backup" | cut -f1)
                    
                    if [ "$dry_run" = true ]; then
                        print_status "Would delete excess backup: $backup_name ($(du -sh "$backup" | cut -f1))"
                    else
                        print_status "Deleting excess backup: $backup_name"
                        rm -rf "$backup"
                    fi
                    
                    deleted_count=$((deleted_count + 1))
                    freed_space=$((freed_space + backup_size))
                fi
            done
        done
    fi
    
    # Convert freed space to human readable
    local freed_space_human=""
    if command -v numfmt >/dev/null 2>&1; then
        freed_space_human=$(numfmt --to=iec-i --suffix=B $freed_space)
    else
        freed_space_human="${freed_space} bytes"
    fi
    
    if [ "$dry_run" = true ]; then
        print_success "DRY RUN: Would delete $deleted_count backup(s) and free $freed_space_human"
    else
        print_success "Cleanup completed: Deleted $deleted_count backup(s) and freed $freed_space_human"
    fi
    
    return 0
}

# Function to verify backup integrity
verify_backup() {
    local backup_path="$1"
    
    print_status "Verifying backup integrity: $(basename "$backup_path")"
    
    # Check if backup exists
    if [ ! -e "$backup_path" ]; then
        print_error "Backup not found: $backup_path"
        return 1
    fi
    
    local backup_dir=""
    local is_compressed=false
    local is_encrypted=false
    local temp_dir=""
    
    # Determine backup format
    if [[ "$backup_path" == *.gpg ]]; then
        is_encrypted=true
        print_status "Backup is encrypted"
        
        if [ -z "$BACKUP_PASSWORD" ]; then
            print_error "Password required to verify encrypted backup"
            return 1
        fi
        
        # Create temporary directory for decryption
        temp_dir=$(mktemp -d)
        local decrypted_file="$temp_dir/$(basename "${backup_path%.gpg}")"
        
        if ! gpg --batch --yes --passphrase "$BACKUP_PASSWORD" --decrypt "$backup_path" > "$decrypted_file"; then
            print_error "Failed to decrypt backup"
            rm -rf "$temp_dir"
            return 1
        fi
        
        backup_path="$decrypted_file"
    fi
    
    if [[ "$backup_path" == *.tar.gz ]] || [[ "$backup_path" == *.tgz ]]; then
        is_compressed=true
        print_status "Backup is compressed"
        
        # Create temporary directory for extraction
        if [ -z "$temp_dir" ]; then
            temp_dir=$(mktemp -d)
        fi
        
        if ! tar -tzf "$backup_path" >/dev/null 2>&1; then
            print_error "Compressed backup appears to be corrupted"
            rm -rf "$temp_dir"
            return 1
        fi
        
        # Extract for verification
        tar -xzf "$backup_path" -C "$temp_dir"
        backup_dir="$temp_dir/$(tar -tzf "$backup_path" | head -1 | cut -d'/' -f1)"
    else
        backup_dir="$backup_path"
    fi
    
    # Verify directory structure
    local required_dirs=("metadata")
    local verification_passed=true
    
    for dir in "${required_dirs[@]}"; do
        if [ ! -d "$backup_dir/$dir" ]; then
            print_warning "Missing directory: $dir"
            verification_passed=false
        fi
    done
    
    # Verify manifest file
    local manifest_file="$backup_dir/metadata/manifest.json"
    if [ -f "$manifest_file" ]; then
        print_success "Manifest file found"
        
        # Validate JSON
        if command -v jq >/dev/null 2>&1; then
            if jq empty "$manifest_file" 2>/dev/null; then
                print_success "Manifest JSON is valid"
                
                # Extract basic info
                local backup_id=$(jq -r '.backup_id' "$manifest_file")
                local backup_env=$(jq -r '.environment' "$manifest_file")
                local backup_type=$(jq -r '.type' "$manifest_file")
                
                echo "  Backup ID: $backup_id"
                echo "  Environment: $backup_env"
                echo "  Type: $backup_type"
            else
                print_error "Manifest JSON is invalid"
                verification_passed=false
            fi
        else
            print_warning "jq not available, skipping JSON validation"
        fi
    else
        print_warning "Manifest file missing"
        verification_passed=false
    fi
    
    # Verify checksums if available
    local checksums_file="$backup_dir/metadata/checksums.txt"
    if [ -f "$checksums_file" ]; then
        print_status "Verifying file checksums..."
        
        if (cd "$backup_dir" && sha256sum -c metadata/checksums.txt >/dev/null 2>&1); then
            print_success "All checksums verified"
        else
            print_error "Checksum verification failed"
            verification_passed=false
        fi
    else
        print_warning "No checksums found, skipping integrity check"
    fi
    
    # Verify specific backup content based on type
    if [ -f "$manifest_file" ] && command -v jq >/dev/null 2>&1; then
        local backup_type=$(jq -r '.type' "$manifest_file")
        
        case "$backup_type" in
            "full"|"config-only")
                if [ -d "$backup_dir/config" ]; then
                    local config_files=$(find "$backup_dir/config" -type f | wc -l)
                    print_success "Configuration backup contains $config_files files"
                else
                    print_warning "Configuration directory missing"
                fi
                ;;
        esac
        
        case "$backup_type" in
            "full"|"database-only")
                if [ -d "$backup_dir/database" ]; then
                    local db_files=$(find "$backup_dir/database" -name "*.bak" | wc -l)
                    if [ $db_files -gt 0 ]; then
                        print_success "Database backup contains $db_files database file(s)"
                    else
                        print_warning "No database backup files found"
                    fi
                else
                    print_warning "Database directory missing"
                fi
                ;;
        esac
    fi
    
    # Cleanup temporary directory
    if [ ! -z "$temp_dir" ]; then
        rm -rf "$temp_dir"
    fi
    
    if [ "$verification_passed" = true ]; then
        print_success "✅ Backup verification passed"
        return 0
    else
        print_error "❌ Backup verification failed"
        return 1
    fi
}

# Function to schedule automated backups
schedule_backups() {
    local env="$1"
    local frequency="$2"  # daily, weekly, monthly
    local backup_type="$3"
    
    print_status "Setting up automated backup schedule..."
    
    local cron_schedule=""
    local cron_job=""
    
    # Determine cron schedule
    case "$frequency" in
        "daily")
            cron_schedule="0 2 * * *"  # 2 AM daily
            ;;
        "weekly")
            cron_schedule="0 2 * * 0"  # 2 AM every Sunday
            ;;
        "monthly")
            cron_schedule="0 2 1 * *"  # 2 AM on 1st of each month
            ;;
        *)
            print_error "Invalid frequency: $frequency. Use daily, weekly, or monthly."
            return 1
            ;;
    esac
    
    # Create cron job
    local script_path="$(realpath "$0")"
    cron_job="$cron_schedule cd $(pwd) && $script_path create --environment $env --type $backup_type >> logs/backup-$(date +\%Y\%m).log 2>&1"
    
    # Add to crontab
    (crontab -l 2>/dev/null; echo "$cron_job") | crontab -
    
    print_success "Automated backup scheduled: $frequency $backup_type backups for $env"
    echo "Cron job: $cron_job"
    
    return 0
}

# Main command processing
COMMAND="$1"
shift

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -t|--type)
            BACKUP_TYPE="$2"
            shift 2
            ;;
        -b|--backup)
            BACKUP_DIR="$2"
            shift 2
            ;;
        -c|--compress)
            COMPRESSION=true
            shift
            ;;
        -p|--password)
            BACKUP_PASSWORD="$2"
            ENCRYPT_BACKUPS=true
            shift 2
            ;;
        --retention)
            BACKUP_RETENTION_DAYS="$2"
            shift 2
            ;;
        --max-backups)
            MAX_BACKUPS_PER_ENV="$2"
            shift 2
            ;;
        --no-verify)
            VERIFICATION_ENABLED=false
            shift
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

# Execute command
case "$COMMAND" in
    "create")
        if [ -z "$ENVIRONMENT" ]; then
            print_error "Environment is required for backup creation"
            exit 1
        fi
        create_backup "$ENVIRONMENT" "$BACKUP_TYPE"
        ;;
    "list")
        list_backups "$ENVIRONMENT" "table"
        ;;
    "cleanup")
        cleanup_backups "$BACKUP_RETENTION_DAYS" "$MAX_BACKUPS_PER_ENV" "$ENVIRONMENT" false
        ;;
    "verify")
        if [ -z "$BACKUP_DIR" ]; then
            print_error "Backup directory is required for verification"
            exit 1
        fi
        verify_backup "$BACKUP_DIR"
        ;;
    "schedule")
        if [ -z "$ENVIRONMENT" ]; then
            print_error "Environment is required for scheduling"
            exit 1
        fi
        schedule_backups "$ENVIRONMENT" "daily" "$BACKUP_TYPE"
        ;;
    *)
        print_error "Unknown command: $COMMAND"
        show_usage
        exit 1
        ;;
esac