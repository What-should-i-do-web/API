#!/bin/bash

# WhatShouldIDo Development Pipeline Testing Script
# This script comprehensively tests the development CI/CD pipeline

set -e

# Load common functions
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/deploy-common.sh"

print_header "🧪 WhatShouldIDo Development Pipeline Testing"
echo "=============================================="

# Configuration
ENVIRONMENT="development"
COMPOSE_FILE="docker-compose.dev.yml"
ENV_FILE=".env.development"
API_BASE_URL="http://localhost:5001"
HEALTH_URL="${API_BASE_URL}/api/health"
JENKINS_URL="http://localhost:8080"
TEST_RESULTS_DIR="test-results-$(date +%Y%m%d_%H%M%S)"

# Test tracking
TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0
TEST_RESULTS=()

# Function to run a test
run_test() {
    local test_name="$1"
    local test_command="$2"
    local expected_result="${3:-0}"  # Default: expect success (0)
    
    TOTAL_TESTS=$((TOTAL_TESTS + 1))
    print_status "Running test: $test_name"
    
    if eval "$test_command" >/dev/null 2>&1; then
        local actual_result=0
    else
        local actual_result=1
    fi
    
    if [ "$actual_result" -eq "$expected_result" ]; then
        print_success "✅ PASS: $test_name"
        PASSED_TESTS=$((PASSED_TESTS + 1))
        TEST_RESULTS+=("PASS: $test_name")
        return 0
    else
        print_error "❌ FAIL: $test_name"
        FAILED_TESTS=$((FAILED_TESTS + 1))
        TEST_RESULTS+=("FAIL: $test_name")
        return 1
    fi
}

# Function to run API test
run_api_test() {
    local test_name="$1"
    local endpoint="$2"
    local method="${3:-GET}"
    local expected_status="${4:-200}"
    local data="$5"
    
    TOTAL_TESTS=$((TOTAL_TESTS + 1))
    print_status "API Test: $test_name"
    
    local url="${API_BASE_URL}${endpoint}"
    local curl_cmd="curl -s -w '%{http_code}' -o /dev/null"
    
    case "$method" in
        "POST")
            if [ ! -z "$data" ]; then
                curl_cmd="$curl_cmd -X POST -H 'Content-Type: application/json' -d '$data'"
            fi
            ;;
        "GET"|*)
            # Default is GET
            ;;
    esac
    
    local actual_status=$(eval "$curl_cmd '$url'")
    
    if [ "$actual_status" = "$expected_status" ]; then
        print_success "✅ API PASS: $test_name (HTTP $actual_status)"
        PASSED_TESTS=$((PASSED_TESTS + 1))
        TEST_RESULTS+=("API PASS: $test_name")
        return 0
    else
        print_error "❌ API FAIL: $test_name (Expected HTTP $expected_status, got $actual_status)"
        FAILED_TESTS=$((FAILED_TESTS + 1))
        TEST_RESULTS+=("API FAIL: $test_name")
        return 1
    fi
}

# Create test results directory
mkdir -p "$TEST_RESULTS_DIR"

print_status "Starting comprehensive development pipeline testing..."
echo "Results will be saved to: $TEST_RESULTS_DIR"
echo ""

# Phase 1: Prerequisites Testing
print_header "Phase 1: Prerequisites Testing"

run_test "Docker is installed and running" "docker --version && docker ps"
run_test "Docker Compose is available" "docker-compose --version"
run_test "Git repository is available" "git status"
run_test "Environment file exists" "test -f $ENV_FILE"
run_test "Docker Compose file exists" "test -f $COMPOSE_FILE"
run_test "Deployment scripts exist" "test -f scripts/deploy-dev.sh && test -x scripts/deploy-dev.sh"

# Phase 2: Environment Configuration Testing
print_header "Phase 2: Environment Configuration Testing"

if [ -f "$ENV_FILE" ]; then
    source "$ENV_FILE"
    run_test "Database connection string is set" "test ! -z '$DB_CONNECTION_STRING'"
    run_test "Redis connection string is set" "test ! -z '$REDIS_CONNECTION_STRING'"
    run_test "JWT secret is set" "test ! -z '$JWT_SECRET_KEY'"
    run_test "JWT secret is long enough" "test ${#JWT_SECRET_KEY} -ge 32"
else
    print_error "Environment file not found, skipping configuration tests"
    FAILED_TESTS=$((FAILED_TESTS + 4))
fi

# Phase 3: Build and Deployment Testing
print_header "Phase 3: Build and Deployment Testing"

print_status "Running development deployment..."
if ./scripts/deploy-dev.sh --skip-tests; then
    print_success "Development deployment succeeded"
    PASSED_TESTS=$((PASSED_TESTS + 1))
    TEST_RESULTS+=("PASS: Development deployment")
    
    # Wait for services to stabilize
    sleep 15
    
    # Test if containers are running
    run_test "API container is running" "docker-compose -f $COMPOSE_FILE ps api-dev | grep -q 'Up'"
    run_test "Database container is running" "docker-compose -f $COMPOSE_FILE ps db-dev | grep -q 'Up'"
    run_test "Redis container is running" "docker-compose -f $COMPOSE_FILE ps redis-dev | grep -q 'Up'"
    run_test "Adminer container is running" "docker-compose -f $COMPOSE_FILE ps adminer-dev | grep -q 'Up'"
    
else
    print_error "Development deployment failed"
    FAILED_TESTS=$((FAILED_TESTS + 1))
    TEST_RESULTS+=("FAIL: Development deployment")
    
    # Show logs for debugging
    print_status "Deployment logs:"
    docker-compose -f "$COMPOSE_FILE" logs --tail=50 > "$TEST_RESULTS_DIR/deployment-logs.txt"
    echo "Logs saved to: $TEST_RESULTS_DIR/deployment-logs.txt"
fi

# Phase 4: API Functionality Testing
print_header "Phase 4: API Functionality Testing"

# Wait for API to be fully ready
print_status "Waiting for API to be ready..."
sleep 30

# Basic API tests
run_api_test "Health endpoint" "/api/health" "GET" "200"
run_api_test "Random discover endpoint" "/api/discover/random" "GET" "200"

# Advanced API tests
run_api_test "Prompt search endpoint" "/api/discover/prompt" "POST" "200" '{"prompt":"restaurants","latitude":41.0082,"longitude":28.9784}'

# Test invalid requests
run_api_test "Invalid endpoint returns 404" "/api/nonexistent" "GET" "404"

# Phase 5: Database and Redis Testing
print_header "Phase 5: Database and Redis Testing"

run_test "Database is accessible" "docker-compose -f $COMPOSE_FILE exec -T db-dev /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P '$SQL_SA_PASSWORD' -Q 'SELECT 1'"
run_test "Redis is accessible" "docker-compose -f $COMPOSE_FILE exec -T redis-dev redis-cli ping | grep -q PONG"

# Phase 6: Service Integration Testing
print_header "Phase 6: Service Integration Testing"

run_test "Adminer web interface is accessible" "curl -f http://localhost:8080"
run_test "Redis Commander is accessible" "curl -f http://localhost:8081"
run_test "Seq logging interface is accessible" "curl -f http://localhost:5341"

# Phase 7: Performance and Load Testing
print_header "Phase 7: Performance Testing"

print_status "Running basic performance tests..."

# Test API response times
response_time=$(curl -w "%{time_total}" -s -o /dev/null "$HEALTH_URL")
if (( $(echo "$response_time < 2.0" | bc -l 2>/dev/null || echo "0") )); then
    print_success "✅ PASS: API response time under 2s ($response_time s)"
    PASSED_TESTS=$((PASSED_TESTS + 1))
    TEST_RESULTS+=("PASS: API response time")
else
    print_error "❌ FAIL: API response time too slow ($response_time s)"
    FAILED_TESTS=$((FAILED_TESTS + 1))
    TEST_RESULTS+=("FAIL: API response time")
fi

TOTAL_TESTS=$((TOTAL_TESTS + 1))

# Test multiple concurrent requests
print_status "Testing concurrent requests..."
if command -v ab >/dev/null 2>&1; then
    ab -n 50 -c 5 "$HEALTH_URL" > "$TEST_RESULTS_DIR/load-test.txt" 2>&1
    if [ $? -eq 0 ]; then
        print_success "✅ PASS: Concurrent requests test"
        PASSED_TESTS=$((PASSED_TESTS + 1))
        TEST_RESULTS+=("PASS: Concurrent requests")
    else
        print_error "❌ FAIL: Concurrent requests test"
        FAILED_TESTS=$((FAILED_TESTS + 1))
        TEST_RESULTS+=("FAIL: Concurrent requests")
    fi
    TOTAL_TESTS=$((TOTAL_TESTS + 1))
else
    print_warning "Apache Bench (ab) not available, skipping load test"
fi

# Phase 8: Security Testing
print_header "Phase 8: Security Testing"

run_test "No secrets in environment variables are exposed via API" "! curl -s $API_BASE_URL/api/health | grep -i 'password\\|secret\\|key'"

# Test CORS headers (if configured)
cors_headers=$(curl -s -I "$HEALTH_URL" | grep -i "access-control" | wc -l)
if [ $cors_headers -gt 0 ]; then
    print_success "✅ PASS: CORS headers present"
    PASSED_TESTS=$((PASSED_TESTS + 1))
    TEST_RESULTS+=("PASS: CORS headers")
else
    print_warning "⚠ CORS headers not found (may be intentional)"
fi
TOTAL_TESTS=$((TOTAL_TESTS + 1))

# Phase 9: Jenkins Integration Testing
print_header "Phase 9: Jenkins Integration Testing"

if curl -f "$JENKINS_URL" -m 10 >/dev/null 2>&1; then
    print_success "✅ PASS: Jenkins is accessible"
    PASSED_TESTS=$((PASSED_TESTS + 1))
    TEST_RESULTS+=("PASS: Jenkins accessibility")
    
    # Test Jenkins API
    if curl -f "$JENKINS_URL/api/json" -m 10 >/dev/null 2>&1; then
        print_success "✅ PASS: Jenkins API is responding"
        PASSED_TESTS=$((PASSED_TESTS + 1))
        TEST_RESULTS+=("PASS: Jenkins API")
    else
        print_error "❌ FAIL: Jenkins API not responding"
        FAILED_TESTS=$((FAILED_TESTS + 1))
        TEST_RESULTS+=("FAIL: Jenkins API")
    fi
    
    TOTAL_TESTS=$((TOTAL_TESTS + 2))
else
    print_warning "⚠ Jenkins not accessible at $JENKINS_URL (may not be running)"
    TOTAL_TESTS=$((TOTAL_TESTS + 2))
fi

# Phase 10: Cleanup and Rollback Testing
print_header "Phase 10: Cleanup and Rollback Testing"

# Test backup creation
if [ -d "backups" ]; then
    backup_count=$(ls backups | wc -l)
    if [ $backup_count -gt 0 ]; then
        print_success "✅ PASS: Backups are being created"
        PASSED_TESTS=$((PASSED_TESTS + 1))
        TEST_RESULTS+=("PASS: Backup creation")
    else
        print_error "❌ FAIL: No backups found"
        FAILED_TESTS=$((FAILED_TESTS + 1))
        TEST_RESULTS+=("FAIL: Backup creation")
    fi
else
    print_error "❌ FAIL: Backup directory not found"
    FAILED_TESTS=$((FAILED_TESTS + 1))
    TEST_RESULTS+=("FAIL: Backup directory")
fi
TOTAL_TESTS=$((TOTAL_TESTS + 1))

# Generate comprehensive test report
print_header "Generating Test Report"

cat > "$TEST_RESULTS_DIR/test-report.html" << EOF
<!DOCTYPE html>
<html>
<head>
    <title>WhatShouldIDo Development Pipeline Test Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }
        .container { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        .header { background: #2c3e50; color: white; padding: 15px; border-radius: 5px; margin-bottom: 20px; }
        .summary { display: flex; justify-content: space-around; margin: 20px 0; }
        .metric { text-align: center; padding: 15px; border-radius: 5px; }
        .metric h3 { margin: 0; font-size: 2em; }
        .metric p { margin: 5px 0 0 0; }
        .pass { background: #d5f4e6; color: #27ae60; }
        .fail { background: #fdf2f2; color: #e74c3c; }
        .total { background: #ebf3fd; color: #3498db; }
        .test-results { margin-top: 20px; }
        .test-item { padding: 10px; margin: 5px 0; border-radius: 3px; }
        .test-pass { background: #d5f4e6; border-left: 4px solid #27ae60; }
        .test-fail { background: #fdf2f2; border-left: 4px solid #e74c3c; }
        .phase { margin: 20px 0; padding: 15px; background: #f8f9fa; border-radius: 5px; }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>🧪 WhatShouldIDo Development Pipeline Test Report</h1>
            <p>Generated: $(date)</p>
            <p>Environment: Development</p>
        </div>
        
        <div class="summary">
            <div class="metric total">
                <h3>$TOTAL_TESTS</h3>
                <p>Total Tests</p>
            </div>
            <div class="metric pass">
                <h3>$PASSED_TESTS</h3>
                <p>Passed</p>
            </div>
            <div class="metric fail">
                <h3>$FAILED_TESTS</h3>
                <p>Failed</p>
            </div>
        </div>
        
        <div class="phase">
            <h2>📊 Test Results Summary</h2>
            <p><strong>Success Rate:</strong> $(( PASSED_TESTS * 100 / TOTAL_TESTS ))%</p>
            <p><strong>API Base URL:</strong> $API_BASE_URL</p>
            <p><strong>Docker Compose File:</strong> $COMPOSE_FILE</p>
            <p><strong>Environment File:</strong> $ENV_FILE</p>
        </div>
        
        <div class="test-results">
            <h2>🔍 Detailed Test Results</h2>
EOF

for result in "${TEST_RESULTS[@]}"; do
    if [[ "$result" == PASS:* ]]; then
        echo "            <div class=\"test-item test-pass\">✅ ${result#PASS: }</div>" >> "$TEST_RESULTS_DIR/test-report.html"
    else
        echo "            <div class=\"test-item test-fail\">❌ ${result#FAIL: }</div>" >> "$TEST_RESULTS_DIR/test-report.html"
    fi
done

cat >> "$TEST_RESULTS_DIR/test-report.html" << EOF
        </div>
        
        <div class="phase">
            <h2>📝 Recommendations</h2>
EOF

if [ $FAILED_TESTS -gt 0 ]; then
    echo "            <p><strong>⚠️ Issues Found:</strong> Please review failed tests and fix issues before proceeding to production.</p>" >> "$TEST_RESULTS_DIR/test-report.html"
    echo "            <ul>" >> "$TEST_RESULTS_DIR/test-report.html"
    for result in "${TEST_RESULTS[@]}"; do
        if [[ "$result" == FAIL:* ]]; then
            echo "                <li>${result#FAIL: }</li>" >> "$TEST_RESULTS_DIR/test-report.html"
        fi
    done
    echo "            </ul>" >> "$TEST_RESULTS_DIR/test-report.html"
else
    echo "            <p><strong>✅ All tests passed!</strong> The development pipeline is ready for production deployment.</p>" >> "$TEST_RESULTS_DIR/test-report.html"
fi

cat >> "$TEST_RESULTS_DIR/test-report.html" << EOF
        </div>
    </div>
</body>
</html>
EOF

# Save detailed results
cat > "$TEST_RESULTS_DIR/test-summary.txt" << EOF
WhatShouldIDo Development Pipeline Test Report
============================================

Generated: $(date)
Environment: Development
API URL: $API_BASE_URL

Summary:
- Total Tests: $TOTAL_TESTS
- Passed: $PASSED_TESTS
- Failed: $FAILED_TESTS  
- Success Rate: $(( PASSED_TESTS * 100 / TOTAL_TESTS ))%

Test Results:
EOF

for result in "${TEST_RESULTS[@]}"; do
    echo "- $result" >> "$TEST_RESULTS_DIR/test-summary.txt"
done

# Final summary
print_header "🧪 Test Results Summary"

echo "📊 Total Tests: $TOTAL_TESTS"
echo "✅ Passed: $PASSED_TESTS"
echo "❌ Failed: $FAILED_TESTS"
echo "📈 Success Rate: $(( PASSED_TESTS * 100 / TOTAL_TESTS ))%"
echo ""
echo "📁 Test Results saved to: $TEST_RESULTS_DIR/"
echo "   - test-report.html (web report)"
echo "   - test-summary.txt (text summary)"
if [ -f "$TEST_RESULTS_DIR/deployment-logs.txt" ]; then
    echo "   - deployment-logs.txt (deployment logs)"
fi
echo ""

if [ $FAILED_TESTS -eq 0 ]; then
    print_success "🎉 All tests passed! Development pipeline is ready for production."
    send_deployment_notification "development" "success" "All $TOTAL_TESTS pipeline tests passed successfully"
    exit 0
else
    print_error "❌ $FAILED_TESTS test(s) failed. Please review and fix issues before proceeding."
    print_status "Review the detailed report: $TEST_RESULTS_DIR/test-report.html"
    send_deployment_notification "development" "warning" "$FAILED_TESTS of $TOTAL_TESTS pipeline tests failed"
    exit 1
fi