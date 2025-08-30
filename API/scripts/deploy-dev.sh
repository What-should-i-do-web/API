#!/bin/bash

# WhatShouldIDo Development Environment Deployment Script
# This script deploys the application to development environment with comprehensive checks

set -e  # Exit on any error

# Load common deployment functions
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/deploy-common.sh"

print_header "🚀 WhatShouldIDo API - Development Deployment"
echo "============================================================"

# Configuration
ENVIRONMENT="development"
COMPOSE_FILE="docker-compose.dev.yml"
ENV_FILE=".env.development"
PROJECT_PATH="src/WhatShouldIDo.API"
HEALTH_CHECK_URL="http://localhost:5001/api/health"
API_BASE_URL="http://localhost:5001"
START_TIME=$(date +%s)

# Required environment variables for development
REQUIRED_VARS=(
    "DB_CONNECTION_STRING"
    "SQL_SA_PASSWORD"
    "REDIS_CONNECTION_STRING"
    "REDIS_PASSWORD"
    "JWT_SECRET_KEY"
)

# Main deployment function
main() {
    # Pre-deployment validation
    print_status "Step 1: Pre-deployment validation..."
    
    # Check if environment file exists
    if [ ! -f "$ENV_FILE" ]; then
        print_error "Environment file $ENV_FILE not found!"
        print_status "Creating from template..."
        cp .env.example "$ENV_FILE"
        print_warning "Please edit $ENV_FILE with your development values before continuing."
        exit 1
    fi
    
    # Validate environment variables
    validate_env_vars "$ENV_FILE" REQUIRED_VARS[@]
    
    # Check system requirements
    check_system_requirements 1 2  # 1GB RAM, 2GB disk minimum for dev
    
    print_success "Pre-deployment validation completed"
    
    # Create backup
    print_status "Step 2: Creating backup..."
    BACKUP_DIR=$(create_backup "$ENVIRONMENT" "$COMPOSE_FILE")
    
    # Install dependencies
    print_status "Step 3: Installing dependencies..."
    install_dependencies "$PROJECT_PATH"
    
    # Build application
    print_status "Step 4: Building application..."
    build_application "$PROJECT_PATH" "Debug"
    
    # Update service configuration
    print_status "Step 5: Updating service configuration..."
    update_service_configuration "$COMPOSE_FILE" "$ENVIRONMENT"
    
    # Deploy services
    print_status "Step 6: Deploying services..."
    deploy_services
    
    # Run database migrations
    print_status "Step 7: Database migrations..."
    run_database_migrations "$COMPOSE_FILE" "api-dev"
    
    # Health checks and smoke tests
    print_status "Step 8: Running health checks and smoke tests..."
    if ! wait_for_service_health "API" "$HEALTH_CHECK_URL" 180; then
        print_error "Health check failed, rolling back..."
        rollback_deployment "$BACKUP_DIR" "$COMPOSE_FILE" "$ENVIRONMENT"
        exit 1
    fi
    
    # Run smoke tests
    run_smoke_tests "$API_BASE_URL" "$ENVIRONMENT"
    
    # Cleanup old resources
    print_status "Step 9: Cleaning up..."
    cleanup_old_resources 3 2  # Keep 3 backups, 2 images
    
    # Display summary
    print_status "Step 10: Deployment summary..."
    display_deployment_summary "$ENVIRONMENT" "$START_TIME" "$API_BASE_URL" "$COMPOSE_FILE"
    
    # Send success notification
    send_deployment_notification "$ENVIRONMENT" "success" "Development deployment completed successfully"
    
    print_success "Development deployment completed successfully! 🎉"
}

# Function to deploy services
deploy_services() {
    # Stop existing containers
    print_status "Stopping existing development containers..."
    if docker-compose -f "$COMPOSE_FILE" ps -q >/dev/null 2>&1; then
        docker-compose -f "$COMPOSE_FILE" down --remove-orphans
        print_success "Existing containers stopped"
    else
        print_status "No existing containers found"
    fi
    
    # Pull latest images
    print_status "Pulling latest Docker images..."
    docker-compose -f "$COMPOSE_FILE" pull
    
    # Build and start services
    print_status "Building and starting development services..."
    docker-compose -f "$COMPOSE_FILE" up -d --build
    
    print_success "Services deployed successfully"
}

# Error handling function
handle_deployment_error() {
    local error_message="$1"
    local line_number="$2"
    
    print_error "Deployment failed at line $line_number: $error_message"
    
    # Send failure notification
    send_deployment_notification "$ENVIRONMENT" "failure" "Deployment failed: $error_message"
    
    # Offer rollback if backup exists
    if [ ! -z "$BACKUP_DIR" ] && [ -d "$BACKUP_DIR" ]; then
        read -p "Do you want to rollback to the previous version? (y/n): " rollback_choice
        if [ "$rollback_choice" = "y" ] || [ "$rollback_choice" = "yes" ]; then
            rollback_deployment "$BACKUP_DIR" "$COMPOSE_FILE" "$ENVIRONMENT"
        fi
    fi
    
    exit 1
}

# Set up error handling
trap 'handle_deployment_error "Unexpected error occurred" $LINENO' ERR

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-backup)
            SKIP_BACKUP=true
            shift
            ;;
        --skip-tests)
            SKIP_TESTS=true
            shift
            ;;
        --force)
            FORCE_DEPLOY=true
            shift
            ;;
        --help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --skip-backup    Skip creating backup before deployment"
            echo "  --skip-tests     Skip smoke tests after deployment"
            echo "  --force          Force deployment even if validation fails"
            echo "  --help           Show this help message"
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Run main deployment
main

echo ""
echo "🛠️ Development Environment Ready!"
echo ""
echo "📋 Quick Reference:"
echo "   API: http://localhost:5001"
echo "   Health: $HEALTH_CHECK_URL" 
echo "   Database Admin: http://localhost:8080 (Adminer)"
echo "   Redis Admin: http://localhost:8081 (Redis Commander)"
echo "   Logs: http://localhost:5341 (Seq)"
echo ""
echo "🔧 Management Commands:"
echo "   View logs: docker-compose -f $COMPOSE_FILE logs -f"
echo "   Restart: docker-compose -f $COMPOSE_FILE restart api-dev"
echo "   Stop: docker-compose -f $COMPOSE_FILE down"
echo ""
print_success "Development deployment completed successfully! 🚀"