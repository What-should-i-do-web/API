#!/bin/bash

# WhatShouldIDo Git Webhooks Configuration Script
# This script helps set up webhooks for GitHub, GitLab, and other Git providers

set -e

echo "🔗 WhatShouldIDo Git Webhooks Configuration"
echo "==========================================="

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

# Configuration
JENKINS_URL="${JENKINS_URL:-http://localhost:8080}"
WEBHOOK_SECRET=""
GIT_PROVIDER=""

# Function to generate webhook secret
generate_webhook_secret() {
    WEBHOOK_SECRET=$(openssl rand -hex 32)
    echo "$WEBHOOK_SECRET" > .webhook-secret
    chmod 600 .webhook-secret
    print_success "Generated webhook secret and saved to .webhook-secret"
}

# Function to display webhook configuration for different providers
configure_github_webhook() {
    local repo_url="$1"
    local jenkins_url="$2"
    
    print_status "GitHub Webhook Configuration:"
    echo ""
    echo "1. Go to your GitHub repository: $repo_url"
    echo "2. Navigate to Settings → Webhooks → Add webhook"
    echo "3. Configure the webhook:"
    echo "   📍 Payload URL: ${jenkins_url}/github-webhook/"
    echo "   📦 Content type: application/json"
    echo "   🔐 Secret: $WEBHOOK_SECRET"
    echo "   🎯 Which events would you like to trigger this webhook?"
    echo "      ☑️ Pushes"
    echo "      ☑️ Pull requests"
    echo "      ☑️ Branch or tag creation"
    echo "      ☑️ Branch or tag deletion"
    echo ""
    echo "4. Click 'Add webhook'"
    echo ""
}

configure_gitlab_webhook() {
    local repo_url="$1"
    local jenkins_url="$2"
    
    print_status "GitLab Webhook Configuration:"
    echo ""
    echo "1. Go to your GitLab project: $repo_url"
    echo "2. Navigate to Settings → Webhooks"
    echo "3. Configure the webhook:"
    echo "   📍 URL: ${jenkins_url}/project/WhatShouldIDo-API-Pipeline"
    echo "   🔐 Secret Token: $WEBHOOK_SECRET"
    echo "   🎯 Trigger events:"
    echo "      ☑️ Push events"
    echo "      ☑️ Merge request events"
    echo "      ☑️ Tag push events"
    echo ""
    echo "4. Click 'Add webhook'"
    echo ""
}

configure_bitbucket_webhook() {
    local repo_url="$1"
    local jenkins_url="$2"
    
    print_status "Bitbucket Webhook Configuration:"
    echo ""
    echo "1. Go to your Bitbucket repository: $repo_url"
    echo "2. Navigate to Repository settings → Webhooks"
    echo "3. Configure the webhook:"
    echo "   📍 Title: Jenkins CI/CD"
    echo "   📍 URL: ${jenkins_url}/bitbucket-hook/"
    echo "   ✅ Status: Active"
    echo "   🎯 Triggers:"
    echo "      ☑️ Repository push"
    echo "      ☑️ Pull request created"
    echo "      ☑️ Pull request updated"
    echo ""
    echo "4. Save the webhook"
    echo ""
}

# Function to test webhook connectivity
test_webhook() {
    local webhook_url="$1"
    local git_provider="$2"
    
    print_status "Testing webhook connectivity..."
    
    case "$git_provider" in
        "github")
            # Test GitHub webhook
            curl -X POST "$webhook_url" \
                -H "Content-Type: application/json" \
                -H "X-GitHub-Event: ping" \
                -H "X-Hub-Signature-256: sha256=$(echo -n '{"zen":"Keep it simple."}' | openssl dgst -sha256 -hmac "$WEBHOOK_SECRET" | cut -d' ' -f2)" \
                -d '{"zen":"Keep it simple.","hook":{"type":"Repository","id":1}}'
            ;;
        "gitlab")
            # Test GitLab webhook
            curl -X POST "$webhook_url" \
                -H "Content-Type: application/json" \
                -H "X-Gitlab-Token: $WEBHOOK_SECRET" \
                -d '{"object_kind":"ping"}'
            ;;
        "bitbucket")
            # Test Bitbucket webhook
            curl -X POST "$webhook_url" \
                -H "Content-Type: application/json" \
                -d '{"repository":{"name":"test"},"push":{"changes":[]}}'
            ;;
    esac
    
    if [ $? -eq 0 ]; then
        print_success "Webhook test successful"
    else
        print_warning "Webhook test failed - please check configuration"
    fi
}

# Function to create Jenkins webhook credentials
create_jenkins_credentials() {
    local secret="$1"
    
    print_status "Creating Jenkins webhook credentials..."
    
    # Create credential XML
    cat > webhook-credential.xml << EOF
<com.cloudbees.plugins.credentials.impl.UsernamePasswordCredentialsImpl>
  <scope>GLOBAL</scope>
  <id>github-webhook-secret</id>
  <description>GitHub Webhook Secret</description>
  <username>webhook</username>
  <password>$secret</password>
</com.cloudbees.plugins.credentials.impl.UsernamePasswordCredentialsImpl>
EOF

    # Try to add credential to Jenkins (requires Jenkins CLI)
    if command -v jenkins-cli.jar &> /dev/null; then
        java -jar jenkins-cli.jar -s "$JENKINS_URL" create-credentials-by-xml system::system::jenkins _ < webhook-credential.xml
        print_success "Jenkins webhook credentials created"
    else
        print_warning "Jenkins CLI not found. Please add the webhook secret manually:"
        echo "  1. Go to Jenkins → Manage Jenkins → Manage Credentials"
        echo "  2. Click 'Jenkins' → 'Global credentials' → 'Add Credentials'"
        echo "  3. Kind: Secret text"
        echo "  4. Secret: $secret"
        echo "  5. ID: github-webhook-secret"
    fi
    
    rm -f webhook-credential.xml
}

# Main configuration flow
main() {
    print_status "Starting webhook configuration..."
    
    # Get repository information
    if git remote get-url origin &>/dev/null; then
        REPO_URL=$(git remote get-url origin)
        print_status "Detected repository: $REPO_URL"
        
        # Determine Git provider
        if [[ "$REPO_URL" == *"github.com"* ]]; then
            GIT_PROVIDER="github"
        elif [[ "$REPO_URL" == *"gitlab.com"* ]] || [[ "$REPO_URL" == *"gitlab"* ]]; then
            GIT_PROVIDER="gitlab"
        elif [[ "$REPO_URL" == *"bitbucket.org"* ]]; then
            GIT_PROVIDER="bitbucket"
        else
            print_warning "Unknown Git provider, defaulting to GitHub configuration"
            GIT_PROVIDER="github"
        fi
        
        print_status "Detected Git provider: $GIT_PROVIDER"
    else
        print_error "No Git repository found. Please run this script from your repository root."
        exit 1
    fi
    
    # Get Jenkins URL
    read -p "Enter your Jenkins URL [$JENKINS_URL]: " input_jenkins_url
    if [ ! -z "$input_jenkins_url" ]; then
        JENKINS_URL="$input_jenkins_url"
    fi
    
    # Generate webhook secret
    if [ ! -f ".webhook-secret" ]; then
        generate_webhook_secret
    else
        WEBHOOK_SECRET=$(cat .webhook-secret)
        print_status "Using existing webhook secret"
    fi
    
    # Configure webhook based on provider
    case "$GIT_PROVIDER" in
        "github")
            configure_github_webhook "$REPO_URL" "$JENKINS_URL"
            WEBHOOK_URL="${JENKINS_URL}/github-webhook/"
            ;;
        "gitlab")
            configure_gitlab_webhook "$REPO_URL" "$JENKINS_URL"
            WEBHOOK_URL="${JENKINS_URL}/project/WhatShouldIDo-API-Pipeline"
            ;;
        "bitbucket")
            configure_bitbucket_webhook "$REPO_URL" "$JENKINS_URL"
            WEBHOOK_URL="${JENKINS_URL}/bitbucket-hook/"
            ;;
    esac
    
    # Create Jenkins credentials
    create_jenkins_credentials "$WEBHOOK_SECRET"
    
    # Offer to test webhook
    read -p "Do you want to test the webhook connection? (y/n): " test_webhook_choice
    if [ "$test_webhook_choice" = "y" ] || [ "$test_webhook_choice" = "yes" ]; then
        test_webhook "$WEBHOOK_URL" "$GIT_PROVIDER"
    fi
    
    # Generate configuration summary
    print_success "🎉 Webhook configuration completed!"
    echo ""
    echo "📋 Configuration Summary:"
    echo "   Git Provider: $GIT_PROVIDER"
    echo "   Repository: $REPO_URL"
    echo "   Jenkins URL: $JENKINS_URL"
    echo "   Webhook URL: $WEBHOOK_URL"
    echo "   Secret saved to: .webhook-secret"
    echo ""
    echo "🔧 Next Steps:"
    echo "   1. Follow the configuration steps shown above"
    echo "   2. Create a multibranch pipeline in Jenkins"
    echo "   3. Test the webhook by making a commit"
    echo "   4. Monitor Jenkins for automatic builds"
    echo ""
    echo "🧪 Test Commands:"
    echo "   # Make a test commit to trigger the webhook"
    echo "   echo '# Test webhook' >> README.md"
    echo "   git add README.md"
    echo "   git commit -m 'Test webhook trigger'"
    echo "   git push origin main  # Should trigger production pipeline"
    echo "   git push origin dev   # Should trigger development pipeline"
    echo ""
}

# Advanced webhook configuration for enterprise setups
configure_enterprise_webhooks() {
    print_status "Enterprise Webhook Configuration"
    echo ""
    echo "For enterprise setups with firewalls and security restrictions:"
    echo ""
    echo "1. Webhook Proxy Setup:"
    echo "   - Use nginx or HAProxy to proxy webhooks"
    echo "   - Configure SSL termination"
    echo "   - Set up IP whitelisting"
    echo ""
    echo "2. Security Considerations:"
    echo "   - Use HTTPS for webhook URLs"
    echo "   - Validate webhook signatures"
    echo "   - Rate limit webhook endpoints"
    echo "   - Monitor webhook failures"
    echo ""
    echo "3. High Availability:"
    echo "   - Set up multiple Jenkins masters"
    echo "   - Configure webhook load balancing"
    echo "   - Implement webhook retry mechanisms"
    echo ""
}

# Add webhook validation and monitoring
setup_webhook_monitoring() {
    print_status "Setting up webhook monitoring..."
    
    # Create webhook monitoring script
    cat > webhook-monitor.sh << 'EOF'
#!/bin/bash

# Webhook monitoring script
JENKINS_URL="${JENKINS_URL:-http://localhost:8080}"
LOG_FILE="/var/log/webhook-monitor.log"

# Function to check webhook health
check_webhook_health() {
    response=$(curl -s -o /dev/null -w "%{http_code}" "$JENKINS_URL/github-webhook/")
    
    if [ "$response" = "200" ] || [ "$response" = "405" ]; then
        echo "$(date): Webhook endpoint healthy (HTTP $response)" >> "$LOG_FILE"
        return 0
    else
        echo "$(date): Webhook endpoint unhealthy (HTTP $response)" >> "$LOG_FILE"
        return 1
    fi
}

# Monitor webhook endpoint every 5 minutes
while true; do
    if ! check_webhook_health; then
        # Send alert (customize as needed)
        echo "ALERT: Webhook endpoint is down" | mail -s "Jenkins Webhook Alert" admin@example.com
    fi
    sleep 300
done
EOF
    
    chmod +x webhook-monitor.sh
    print_success "Webhook monitoring script created: webhook-monitor.sh"
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --jenkins-url)
            JENKINS_URL="$2"
            shift 2
            ;;
        --enterprise)
            configure_enterprise_webhooks
            exit 0
            ;;
        --monitor)
            setup_webhook_monitoring
            exit 0
            ;;
        --help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --jenkins-url URL    Set Jenkins URL"
            echo "  --enterprise         Show enterprise configuration"
            echo "  --monitor           Set up webhook monitoring"
            echo "  --help              Show this help message"
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Run main configuration
main