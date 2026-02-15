# WhatShouldIDo API - Build and Test Script
# Run this script in PowerShell to verify all implementations

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "WhatShouldIDo API - Build & Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Clean
Write-Host "[1/5] Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean
if ($LASTEXITCODE -ne 0) {
    Write-Host "Clean failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Clean successful" -ForegroundColor Green
Write-Host ""

# Step 2: Restore packages
Write-Host "[2/5] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Restore failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Restore successful" -ForegroundColor Green
Write-Host ""

# Step 3: Build
Write-Host "[3/5] Building solution..." -ForegroundColor Yellow
dotnet build --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Write-Host "Please check the error messages above and fix any compilation errors." -ForegroundColor Red
    exit 1
}
Write-Host "✓ Build successful" -ForegroundColor Green
Write-Host ""

# Step 4: Run unit tests
Write-Host "[4/5] Running unit tests..." -ForegroundColor Yellow
dotnet test --no-build --filter "FullyQualifiedName~Unit" --logger "console;verbosity=minimal"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Unit tests failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Unit tests passed" -ForegroundColor Green
Write-Host ""

# Step 5: Verify new files exist
Write-Host "[5/5] Verifying new implementations..." -ForegroundColor Yellow

$newFiles = @(
    "src\WhatShouldIDo.Application\UseCases\Handlers\GenerateDailyItineraryCommandHandler.cs",
    "src\WhatShouldIDo.Infrastructure\Services\AI\HuggingFaceProvider.cs",
    "src\WhatShouldIDo.Infrastructure\Services\AI\OllamaProvider.cs",
    "src\WhatShouldIDo.Infrastructure\Services\GoogleDirectionsService.cs",
    "src\WhatShouldIDo.Infrastructure\Services\RouteOptimizationService.cs",
    "src\WhatShouldIDo.Tests\Unit\GenerateDailyItineraryCommandHandlerTests.cs",
    "src\WhatShouldIDo.Tests\Integration\AIProvidersIntegrationTests.cs",
    "src\WhatShouldIDo.Tests\E2E\SearchAndRouteFlowTests.cs",
    "k6-tests\ai-itinerary-load-test.js"
)

$allExist = $true
foreach ($file in $newFiles) {
    if (Test-Path $file) {
        Write-Host "  ✓ $file" -ForegroundColor Green
    } else {
        Write-Host "  ✗ $file (MISSING)" -ForegroundColor Red
        $allExist = $false
    }
}

Write-Host ""

if (-not $allExist) {
    Write-Host "Some files are missing!" -ForegroundColor Red
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✓ ALL CHECKS PASSED!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Configure AI providers in appsettings.json" -ForegroundColor White
Write-Host "2. Set API keys (OpenAI, HuggingFace, Google Maps)" -ForegroundColor White
Write-Host "3. Run integration tests: dotnet test --filter 'FullyQualifiedName~Integration'" -ForegroundColor White
Write-Host "4. Run load tests: k6 run k6-tests/ai-itinerary-load-test.js" -ForegroundColor White
Write-Host "5. Start the API: dotnet run --project src/WhatShouldIDo.API" -ForegroundColor White
Write-Host ""
Write-Host "API Documentation: http://localhost:5000/swagger" -ForegroundColor Cyan
Write-Host ""
