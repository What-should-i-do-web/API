#!/bin/bash

# WhatShouldIDo Automated Testing Pipeline Script
# This script runs comprehensive tests for CI/CD pipeline

set -e  # Exit on any error

echo "🧪 Running WhatShouldIDo API Test Pipeline"
echo "========================================="

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
PROJECT_PATH="src/WhatShouldIDo.API"
TEST_PATH="src/WhatShouldIDo.Tests"
TEST_RESULTS_DIR="TestResults"
COVERAGE_DIR="Coverage"
BUILD_CONFIG="Release"

# Initialize test tracking
total_tests=0
failed_tests=0
skipped_tests=0

# Clean up previous test results
print_status "Cleaning up previous test results..."
rm -rf "$TEST_RESULTS_DIR" "$COVERAGE_DIR"
mkdir -p "$TEST_RESULTS_DIR" "$COVERAGE_DIR"

# Restore dependencies
print_status "Restoring NuGet packages..."
dotnet restore "$PROJECT_PATH/WhatShouldIDo.API.csproj" --verbosity minimal
if [ -d "$TEST_PATH" ]; then
    dotnet restore "$TEST_PATH/WhatShouldIDo.API.IntegrationTests.csproj" --verbosity minimal
fi

# Build the application
print_status "Building application..."
dotnet build "$PROJECT_PATH/WhatShouldIDo.API.csproj" \
    --configuration $BUILD_CONFIG \
    --no-restore \
    --verbosity minimal

if [ $? -eq 0 ]; then
    print_success "Build completed successfully"
else
    print_error "Build failed"
    exit 1
fi

# Static Code Analysis
print_status "Running static code analysis..."

# Security scan for secrets
print_status "Scanning for potential secrets in source code..."
secret_patterns=(
    "API_KEY"
    "SECRET"
    "PASSWORD"
    "TOKEN"
    "PRIVATE_KEY"
)

secret_found=false
for pattern in "${secret_patterns[@]}"; do
    if grep -r "$pattern" src/ --include="*.cs" --include="*.json" --exclude-dir=bin --exclude-dir=obj | grep -v "YOUR_.*_HERE"; then
        print_warning "Potential secret found: $pattern"
        secret_found=true
    fi
done

if [ "$secret_found" = false ]; then
    print_success "No secrets found in source code"
fi

# Check for TODO and FIXME comments
print_status "Checking for TODO/FIXME comments..."
todo_count=$(grep -r "TODO\|FIXME" src/ --include="*.cs" | wc -l)
if [ "$todo_count" -gt 0 ]; then
    print_warning "Found $todo_count TODO/FIXME comments"
    grep -r "TODO\|FIXME" src/ --include="*.cs" | head -5
else
    print_success "No TODO/FIXME comments found"
fi

# Dependency vulnerability scan
print_status "Scanning dependencies for vulnerabilities..."
dotnet list "$PROJECT_PATH/WhatShouldIDo.API.csproj" package --vulnerable --include-transitive > "$TEST_RESULTS_DIR/dependency-scan.txt" 2>&1
if grep -q "vulnerable" "$TEST_RESULTS_DIR/dependency-scan.txt"; then
    print_warning "Vulnerable dependencies found - check $TEST_RESULTS_DIR/dependency-scan.txt"
else
    print_success "No vulnerable dependencies found"
fi

# Unit Tests
if [ -d "$TEST_PATH" ]; then
    print_status "Running unit tests..."
    
    dotnet test "$TEST_PATH/WhatShouldIDo.API.IntegrationTests.csproj" \
        --configuration $BUILD_CONFIG \
        --no-build \
        --verbosity normal \
        --logger "trx;LogFileName=unit-test-results.trx" \
        --logger "html;LogFileName=unit-test-results.html" \
        --collect:"XPlat Code Coverage" \
        --results-directory "$TEST_RESULTS_DIR" \
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
    
    if [ $? -eq 0 ]; then
        print_success "Unit tests completed"
        
        # Parse test results
        if [ -f "$TEST_RESULTS_DIR/unit-test-results.trx" ]; then
            # Extract test counts (simplified parsing)
            total_tests=$(grep -o "total=\"[0-9]*\"" "$TEST_RESULTS_DIR/unit-test-results.trx" | grep -o "[0-9]*" || echo "0")
            failed_tests=$(grep -o "failed=\"[0-9]*\"" "$TEST_RESULTS_DIR/unit-test-results.trx" | grep -o "[0-9]*" || echo "0")
            skipped_tests=$(grep -o "skipped=\"[0-9]*\"" "$TEST_RESULTS_DIR/unit-test-results.trx" | grep -o "[0-9]*" || echo "0")
        fi
    else
        print_error "Unit tests failed"
        failed_tests=1
    fi
else
    print_warning "No test project found at $TEST_PATH"
fi

# Integration Tests (using test containers)
print_status "Setting up integration test environment..."

# Start test dependencies using Docker Compose
if [ -f "docker-compose.test.yml" ]; then
    print_status "Starting test dependencies..."
    docker-compose -f docker-compose.test.yml up -d
    
    # Wait for services to be ready
    print_status "Waiting for test services to be ready..."
    sleep 30
    
    # Run integration tests
    print_status "Running integration tests..."
    dotnet test "$TEST_PATH/WhatShouldIDo.API.IntegrationTests.csproj" \
        --configuration $BUILD_CONFIG \
        --logger "trx;LogFileName=integration-test-results.trx" \
        --results-directory "$TEST_RESULTS_DIR" \
        --filter "Category=Integration"
    
    # Clean up test dependencies
    docker-compose -f docker-compose.test.yml down
    
    if [ $? -eq 0 ]; then
        print_success "Integration tests completed"
    else
        print_error "Integration tests failed"
        failed_tests=$((failed_tests + 1))
    fi
else
    print_warning "No integration test configuration found (docker-compose.test.yml)"
fi

# API Contract Tests
print_status "Running API contract tests..."

# Start the API for contract testing
if [ -f "docker-compose.dev.yml" ]; then
    print_status "Starting API for contract testing..."
    docker-compose -f docker-compose.dev.yml up -d api-dev
    
    # Wait for API to be ready
    sleep 45
    
    # Test API endpoints
    api_url="http://localhost:5001"
    
    # Health check
    if curl -f "$api_url/api/health" -m 10 >/dev/null 2>&1; then
        print_success "API health check passed"
    else
        print_error "API health check failed"
        failed_tests=$((failed_tests + 1))
    fi
    
    # Test basic endpoints
    endpoints=(
        "/api/health"
        "/api/discover/random"
    )
    
    for endpoint in "${endpoints[@]}"; do
        print_status "Testing endpoint: $endpoint"
        if curl -f "$api_url$endpoint" -m 10 >/dev/null 2>&1; then
            print_success "Endpoint $endpoint responded correctly"
        else
            print_warning "Endpoint $endpoint failed to respond"
            failed_tests=$((failed_tests + 1))
        fi
    done
    
    # Test API with sample data
    print_status "Testing prompt endpoint with sample data..."
    response=$(curl -s -X POST "$api_url/api/discover/prompt" \
        -H "Content-Type: application/json" \
        -d '{"prompt":"test","latitude":41.0082,"longitude":28.9784}' \
        -w "%{http_code}" -o /tmp/api_response.json)
    
    if [ "$response" = "200" ]; then
        print_success "Prompt endpoint test passed"
    else
        print_warning "Prompt endpoint test failed with HTTP $response"
        failed_tests=$((failed_tests + 1))
    fi
    
    # Clean up
    docker-compose -f docker-compose.dev.yml down
else
    print_warning "No development Docker compose found for API testing"
fi

# Performance Tests (basic)
print_status "Running basic performance tests..."

# Test Docker image build time
print_status "Testing Docker build performance..."
start_time=$(date +%s)
docker build -t whatshouldido-test -f "$PROJECT_PATH/Dockerfile" . >/dev/null 2>&1
end_time=$(date +%s)
build_time=$((end_time - start_time))

if [ $build_time -lt 300 ]; then
    print_success "Docker build completed in ${build_time}s (good)"
elif [ $build_time -lt 600 ]; then
    print_warning "Docker build took ${build_time}s (acceptable)"
else
    print_error "Docker build took ${build_time}s (too slow)"
    failed_tests=$((failed_tests + 1))
fi

# Clean up test image
docker rmi whatshouldido-test >/dev/null 2>&1 || true

# Code Coverage Analysis
print_status "Analyzing code coverage..."
if [ -d "$TEST_RESULTS_DIR" ] && ls "$TEST_RESULTS_DIR"/*.xml >/dev/null 2>&1; then
    # Find coverage files
    coverage_files=$(find "$TEST_RESULTS_DIR" -name "*.xml" | head -1)
    if [ ! -z "$coverage_files" ]; then
        # Extract coverage percentage (simplified)
        coverage=$(grep -o "line-rate=\"[0-9.]*\"" "$coverage_files" | head -1 | grep -o "[0-9.]*" || echo "0")
        coverage_percent=$(echo "$coverage * 100" | bc 2>/dev/null || echo "0")
        
        if (( $(echo "$coverage_percent >= 80" | bc -l 2>/dev/null || echo "0") )); then
            print_success "Code coverage: ${coverage_percent}% (excellent)"
        elif (( $(echo "$coverage_percent >= 60" | bc -l 2>/dev/null || echo "0") )); then
            print_warning "Code coverage: ${coverage_percent}% (acceptable)"
        else
            print_error "Code coverage: ${coverage_percent}% (needs improvement)"
            failed_tests=$((failed_tests + 1))
        fi
    fi
else
    print_warning "No code coverage data found"
fi

# Security Tests
print_status "Running security tests..."

# Check for insecure configurations
insecure_patterns=(
    "AllowAnyOrigin"
    "DisableHttpsRedirection"
    "AllowAnyHeader"
    "AllowAnyMethod"
)

security_issues=false
for pattern in "${insecure_patterns[@]}"; do
    if grep -r "$pattern" src/ --include="*.cs"; then
        print_warning "Potentially insecure configuration found: $pattern"
        security_issues=true
    fi
done

if [ "$security_issues" = false ]; then
    print_success "No obvious security issues found"
fi

# Generate Test Report
print_status "Generating test report..."
report_file="$TEST_RESULTS_DIR/test-summary.html"

cat > "$report_file" << EOF
<!DOCTYPE html>
<html>
<head>
    <title>WhatShouldIDo API - Test Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .success { color: green; }
        .warning { color: orange; }
        .error { color: red; }
        .section { margin: 20px 0; padding: 10px; border-left: 4px solid #ccc; }
    </style>
</head>
<body>
    <h1>WhatShouldIDo API - Test Report</h1>
    <p><strong>Generated:</strong> $(date)</p>
    
    <div class="section">
        <h2>Test Summary</h2>
        <ul>
            <li>Total Tests: $total_tests</li>
            <li>Failed Tests: $failed_tests</li>
            <li>Skipped Tests: $skipped_tests</li>
            <li>Build Time: ${build_time}s</li>
            <li>Coverage: ${coverage_percent:-0}%</li>
        </ul>
    </div>
    
    <div class="section">
        <h2>Test Results</h2>
        <p>Detailed test results are available in the TestResults directory.</p>
    </div>
</body>
</html>
EOF

print_success "Test report generated: $report_file"

# Final Summary
echo ""
echo "🧪 Test Pipeline Summary"
echo "========================"
echo "Total Tests: $total_tests"
echo "Failed Tests: $failed_tests"
echo "Skipped Tests: $skipped_tests"
echo "Build Time: ${build_time}s"
echo "Coverage: ${coverage_percent:-0}%"
echo ""

if [ $failed_tests -eq 0 ]; then
    print_success "🎉 All tests passed! Pipeline is ready for deployment."
    exit 0
else
    print_error "❌ $failed_tests test(s) failed. Please fix issues before deployment."
    exit 1
fi