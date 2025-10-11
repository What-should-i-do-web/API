#!/bin/bash

# WhatShouldIDo Jenkins Setup Script
# This script sets up Jenkins with all necessary configurations

set -e  # Exit on any error

echo "🚀 Setting up Jenkins for WhatShouldIDo CI/CD Pipeline"
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

# Check if running as root
if [ "$EUID" -eq 0 ]; then
    print_error "Don't run this script as root!"
    exit 1
fi

# Check prerequisites
print_status "Checking prerequisites..."

if ! command -v docker &> /dev/null; then
    print_error "Docker is not installed. Please install Docker first."
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    print_error "Docker Compose is not installed. Please install Docker Compose first."
    exit 1
fi

print_success "Prerequisites check passed"

# Create Jenkins directory structure
print_status "Creating Jenkins directory structure..."
mkdir -p jenkins/jenkins-config
mkdir -p jenkins/ssh-keys
mkdir -p jenkins/ssl
mkdir -p jenkins/backups

# Set proper permissions
chmod 700 jenkins/ssh-keys
chmod 755 jenkins/jenkins-config

print_success "Directory structure created"

# Generate SSH keys for deployment if they don't exist
if [ ! -f jenkins/ssh-keys/id_rsa ]; then
    print_status "Generating SSH keys for deployment..."
    ssh-keygen -t rsa -b 4096 -f jenkins/ssh-keys/id_rsa -N ""
    chmod 600 jenkins/ssh-keys/id_rsa
    chmod 644 jenkins/ssh-keys/id_rsa.pub
    print_success "SSH keys generated"
    print_warning "Remember to add the public key (jenkins/ssh-keys/id_rsa.pub) to your deployment servers"
else
    print_success "SSH keys already exist"
fi

# Create Jenkins configuration files
print_status "Creating Jenkins configuration..."

# Create basic Jenkins configuration
cat > jenkins/jenkins-config/basic-security.groovy << 'EOF'
import jenkins.model.*
import hudson.security.*
import hudson.security.csrf.DefaultCrumbIssuer
import jenkins.security.s2m.AdminWhitelistRule

def instance = Jenkins.getInstance()

// Enable security
def hudsonRealm = new HudsonPrivateSecurityRealm(false)
def strategy = new FullControlOnceLoggedInAuthorizationStrategy()
strategy.setAllowAnonymousRead(false)

instance.setSecurityRealm(hudsonRealm)
instance.setAuthorizationStrategy(strategy)

// Enable CSRF protection
instance.setCrumbIssuer(new DefaultCrumbIssuer(true))

// Configure agent security
instance.getInjector().getInstance(AdminWhitelistRule.class).setMasterKillSwitch(false)

instance.save()
EOF

# Create initial admin user
cat > jenkins/jenkins-config/create-admin-user.groovy << EOF
import jenkins.model.*
import hudson.security.*

def instance = Jenkins.getInstance()
def hudsonRealm = new HudsonPrivateSecurityRealm(false)

// Create admin user
def users = hudsonRealm.getAllUsers()
if (!users.find{ it.getId() == "admin" }) {
    hudsonRealm.createAccount("admin", "admin123")  // Change this password!
    print "Created admin user"
} else {
    print "Admin user already exists"
}

instance.setSecurityRealm(hudsonRealm)
instance.save()
EOF

# Create system configuration
cat > jenkins/jenkins-config/system-config.groovy << 'EOF'
import jenkins.model.*
import hudson.model.*
import org.jenkinsci.plugins.workflow.libs.GlobalLibraries
import org.jenkinsci.plugins.workflow.libs.LibraryConfiguration
import org.jenkinsci.plugins.workflow.libs.SCMSourceRetriever

def instance = Jenkins.getInstance()

// Set Jenkins URL (update this with your actual domain)
def jenkinsLocationConfiguration = JenkinsLocationConfiguration.get()
jenkinsLocationConfiguration.setUrl("http://jenkins.yourdomain.com:8080/")
jenkinsLocationConfiguration.setAdminAddress("admin@yourdomain.com")
jenkinsLocationConfiguration.save()

// Configure global tools
def dotnetInstallation = new hudson.plugins.dotnet.DotNetSDK("dotnet-9", "/usr/share/dotnet", [])
instance.getDescriptorByType(hudson.plugins.dotnet.DotNetSDK.DescriptorImpl.class).setInstallations(dotnetInstallation)

// Configure Docker
def dockerInstallation = new com.cloudbees.jenkins.plugins.docker.DockerTool("docker", "/usr/bin/docker", [])
instance.getDescriptorByType(com.cloudbees.jenkins.plugins.docker.DockerTool.DescriptorImpl.class).setInstallations(dockerInstallation)

instance.save()
EOF

print_success "Jenkins configuration files created"

# Create nginx configuration for reverse proxy
cat > jenkins/nginx.conf << 'EOF'
events {
    worker_connections 1024;
}

http {
    upstream jenkins {
        server jenkins:8080;
    }
    
    server {
        listen 80;
        server_name jenkins.yourdomain.com;
        return 301 https://$server_name$request_uri;
    }
    
    server {
        listen 443 ssl http2;
        server_name jenkins.yourdomain.com;
        
        ssl_certificate /etc/nginx/ssl/jenkins.crt;
        ssl_certificate_key /etc/nginx/ssl/jenkins.key;
        
        client_max_body_size 50M;
        
        location / {
            proxy_pass http://jenkins;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_read_timeout 300;
        }
    }
}
EOF

# Create docker-compose override for development
cat > jenkins/docker-compose.override.yml << 'EOF'
version: '3.8'

services:
  jenkins:
    environment:
      - JENKINS_OPTS=--httpPort=8080 --prefix=/jenkins
    volumes:
      - ./jenkins/backups:/var/jenkins_home/backups
EOF

# Build and start Jenkins
print_status "Building and starting Jenkins..."
docker-compose -f jenkins/docker-compose.jenkins.yml up -d --build

# Wait for Jenkins to start
print_status "Waiting for Jenkins to start (this may take a few minutes)..."
timeout=300
count=0
while ! curl -sSf http://localhost:8080 > /dev/null 2>&1; do
    if [ $count -ge $timeout ]; then
        print_error "Jenkins failed to start within $timeout seconds"
        exit 1
    fi
    sleep 5
    count=$((count + 5))
    echo -n "."
done
echo ""

print_success "Jenkins is running!"

# Get initial admin password
if [ -f jenkins_home/_data/secrets/initialAdminPassword ]; then
    INITIAL_PASSWORD=$(docker exec whatshouldido-jenkins cat /var/jenkins_home/secrets/initialAdminPassword 2>/dev/null || echo "Not available")
    print_success "Initial admin password: $INITIAL_PASSWORD"
else
    print_warning "Initial admin password not found. Jenkins may already be configured."
fi

# Display connection information
print_success "Jenkins Setup Complete!"
echo ""
echo "📋 Connection Information:"
echo "   URL: http://localhost:8080"
echo "   Username: admin"
echo "   Password: admin123 (please change this!)"
echo ""
echo "📁 Important Files:"
echo "   SSH Public Key: jenkins/ssh-keys/id_rsa.pub"
echo "   Configuration: jenkins/jenkins-config/"
echo "   Logs: docker-compose -f jenkins/docker-compose.jenkins.yml logs -f"
echo ""
echo "🔧 Next Steps:"
echo "1. Access Jenkins at http://localhost:8080"
echo "2. Complete the setup wizard if prompted"
echo "3. Change the default admin password"
echo "4. Add your SSH public key to deployment servers"
echo "5. Configure webhooks in your Git repository"
echo "6. Create your first pipeline job"
echo ""
echo "🛠️ Useful Commands:"
echo "   Start:   docker-compose -f jenkins/docker-compose.jenkins.yml up -d"
echo "   Stop:    docker-compose -f jenkins/docker-compose.jenkins.yml down"
echo "   Logs:    docker-compose -f jenkins/docker-compose.jenkins.yml logs -f"
echo "   Restart: docker-compose -f jenkins/docker-compose.jenkins.yml restart"

print_success "Jenkins setup script completed successfully!"