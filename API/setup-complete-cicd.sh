#!/bin/bash

# WhatShouldIDo Complete CI/CD Setup Script
# This script sets up the entire CI/CD pipeline in one go

set -e

echo "🚀 WhatShouldIDo Complete CI/CD Setup"
echo "===================================="
echo ""
echo "This script will set up:"
echo "✅ Development and Production environments"
echo "✅ Jenkins CI/CD server"
echo "✅ Automated deployment pipeline"
echo "✅ Git integration with webhooks"
echo "✅ Comprehensive testing"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

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

# Check prerequisites
print_status "Checking prerequisites..."

if ! command -v docker &> /dev/null; then
    print_error "Docker is required but not installed. Please install Docker first."
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    print_error "Docker Compose is required but not installed. Please install Docker Compose first."
    exit 1
fi

if ! command -v git &> /dev/null; then
    print_error "Git is required but not installed. Please install Git first."
    exit 1
fi

print_success "All prerequisites are installed"

# Step 1: Environment Configuration
print_status "Step 1: Setting up environment configuration..."

if [ ! -f ".env.development" ]; then
    cp .env.example .env.development
    print_warning "Created .env.development - please edit with your development API keys"
fi

if [ ! -f ".env.production" ]; then
    cp .env.example .env.production
    print_warning "Created .env.production - please edit with your production API keys"
fi

print_success "Environment files configured"

# Step 2: Make scripts executable
print_status "Step 2: Making scripts executable..."
chmod +x jenkins/setup-jenkins.sh
chmod +x scripts/*.sh
print_success "Scripts are now executable"

# Step 3: Jenkins Setup
print_status "Step 3: Setting up Jenkins..."
if [ "$1" != "--skip-jenkins" ]; then
    ./jenkins/setup-jenkins.sh
    print_success "Jenkins setup completed"
else
    print_warning "Skipping Jenkins setup (--skip-jenkins flag provided)"
fi

# Step 4: Test Development Environment
print_status "Step 4: Testing development environment..."
if [ "$1" != "--skip-dev-test" ]; then
    print_status "This will test the development deployment..."
    read -p "Do you want to test development deployment now? (y/n): " test_dev
    
    if [ "$test_dev" = "y" ] || [ "$test_dev" = "yes" ]; then
        ./scripts/deploy-dev.sh
        print_success "Development environment tested"
    else
        print_warning "Skipping development test - run './scripts/deploy-dev.sh' manually later"
    fi
else
    print_warning "Skipping development test (--skip-dev-test flag provided)"
fi

# Step 5: Instructions for Git Integration
print_status "Step 5: Git Integration Setup..."
echo ""
echo "🔗 To complete the setup, follow these steps:"
echo ""
echo "1. Add the SSH public key to your Git repository:"
echo "   - Copy the key: cat jenkins/ssh-keys/id_rsa.pub"
echo "   - GitHub: Repository → Settings → Deploy keys → Add deploy key"
echo "   - GitLab: Project → Settings → Repository → Deploy Keys"
echo ""
echo "2. Configure webhook in your repository:"
echo "   - GitHub webhook URL: http://your-server:8080/github-webhook/"
echo "   - GitLab webhook URL: http://your-server:8080/project/WhatShouldIDo-API-Pipeline"
echo "   - Content type: application/json"
echo "   - Events: Push events, Pull requests"
echo ""
echo "3. Create pipeline job in Jenkins:"
echo "   - Access Jenkins: http://localhost:8080"
echo "   - Username: admin, Password: admin123 (CHANGE THIS!)"
echo "   - Create new Multibranch Pipeline job"
echo "   - Configure with your repository URL"
echo ""

# Step 6: Final Summary
print_success "🎉 CI/CD Setup Complete!"
echo ""
echo "📁 Files Created:"
echo "   ├── .env.development (edit with your dev API keys)"
echo "   ├── .env.production (edit with your prod API keys)"
echo "   ├── jenkins/ (complete Jenkins setup)"
echo "   ├── scripts/ (deployment and testing scripts)"
echo "   ├── Jenkinsfile (CI/CD pipeline definition)"
echo "   └── COMPLETE-CICD-SETUP.md (comprehensive guide)"
echo ""
echo "🔧 What's Working:"
echo "   ✅ Jenkins server running on http://localhost:8080"
echo "   ✅ Development environment configured"
echo "   ✅ Production environment configured"
echo "   ✅ Automated deployment scripts"
echo "   ✅ Comprehensive testing pipeline"
echo ""
echo "📋 Next Steps:"
echo "   1. Edit .env.development and .env.production with your API keys"
echo "   2. Add SSH key to your Git repository (see above)"
echo "   3. Configure webhook in your Git repository (see above)"
echo "   4. Create Jenkins pipeline job (see above)"
echo "   5. Test the pipeline by pushing code changes"
echo ""
echo "📖 Documentation:"
echo "   - Complete guide: COMPLETE-CICD-SETUP.md"
echo "   - Git integration: jenkins/git-integration-setup.md"
echo "   - Hosting strategy: CI-CD-DEPLOYMENT-GUIDE.md"
echo ""
echo "🚀 Commands to try:"
echo "   ./scripts/deploy-dev.sh      # Deploy to development"
echo "   ./scripts/deploy-prod.sh     # Deploy to production"
echo "   ./scripts/test-pipeline.sh   # Run comprehensive tests"
echo ""
echo "💡 Tips:"
echo "   - Change Jenkins admin password immediately"
echo "   - Test development deployment before production"
echo "   - Monitor Jenkins logs for any issues"
echo "   - Use 'develop' branch for development, 'main' for production"
echo ""

print_success "Setup completed successfully! Happy deploying! 🎉"