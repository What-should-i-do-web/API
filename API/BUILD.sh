#!/bin/bash

# Build Script for WhatShouldIDo Backend
# This script will build the entire solution

set -e  # Exit on error

echo "=================================="
echo "WhatShouldIDo Backend Build Script"
echo "=================================="
echo ""

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Check if .NET is installed
if ! command -v dotnet &> /dev/null
then
    echo -e "${RED}ERROR: dotnet command not found${NC}"
    echo "Please install .NET 9 SDK from https://dotnet.microsoft.com/download"
    exit 1
fi

echo -e "${GREEN}✓ .NET SDK found:${NC} $(dotnet --version)"
echo ""

# Check if OpenAI API key is set
if [ -z "$OPENAI_API_KEY" ]; then
    echo -e "${YELLOW}WARNING: OPENAI_API_KEY environment variable not set${NC}"
    echo "AI features will use NoOp provider (no actual AI)"
    echo "To set: export OPENAI_API_KEY='sk-your-key-here'"
    echo ""
fi

# Navigate to solution directory
cd "$(dirname "$0")"

echo "=================================="
echo "Step 1: Cleaning solution"
echo "=================================="
dotnet clean WhatShouldIDo.sln --verbosity quiet
echo -e "${GREEN}✓ Clean complete${NC}"
echo ""

echo "=================================="
echo "Step 2: Restoring NuGet packages"
echo "=================================="
dotnet restore WhatShouldIDo.sln --verbosity quiet
echo -e "${GREEN}✓ Restore complete${NC}"
echo ""

echo "=================================="
echo "Step 3: Building solution"
echo "=================================="
dotnet build WhatShouldIDo.sln --configuration Release --no-restore

if [ $? -eq 0 ]; then
    echo ""
    echo -e "${GREEN}=================================="
    echo "✓ BUILD SUCCESSFUL"
    echo "==================================${NC}"
    echo ""
    echo "To run the application:"
    echo "  dotnet run --project src/WhatShouldIDo.API"
    echo ""
    echo "Or:"
    echo "  cd src/WhatShouldIDo.API"
    echo "  dotnet run"
    echo ""
else
    echo ""
    echo -e "${RED}=================================="
    echo "✗ BUILD FAILED"
    echo "==================================${NC}"
    echo ""
    echo "Please check the errors above and:"
    echo "  1. Ensure all NuGet packages are restored"
    echo "  2. Check for missing files or references"
    echo "  3. Review BUILD_FIXES_APPLIED.md for common issues"
    exit 1
fi
