# Clean Build Instructions

The "Argument 5" error and metadata file errors are likely cascading from the first compilation failure. Here's how to do a clean build:

## Windows (PowerShell)

```powershell
# 1. Clean all bin and obj folders
Get-ChildItem -Path . -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force

# 2. Restore packages
dotnet restore WhatShouldIDo.sln

# 3. Build in order
dotnet build src/WhatShouldIDo.Domain/WhatShouldIDo.Domain.csproj
dotnet build src/WhatShouldIDo.Application/WhatShouldIDo.Application.csproj
dotnet build src/WhatShouldIDo.Infrastructure/WhatShouldIDo.Infrastructure.csproj
dotnet build src/WhatShouldIDo.API/WhatShouldIDo.API.csproj
dotnet build src/WhatShouldIDo.Tests/WhatShouldIDo.API.IntegrationTests.csproj

# 4. Or build entire solution
dotnet build WhatShouldIDo.sln --configuration Release
```

## Linux/WSL (Bash)

```bash
# 1. Clean all bin and obj folders
find . -type d \( -name bin -o -name obj \) -exec rm -rf {} +

# 2. Restore packages
dotnet restore WhatShouldIDo.sln

# 3. Build in order
dotnet build src/WhatShouldIDo.Domain/WhatShouldIDo.Domain.csproj
dotnet build src/WhatShouldIDo.Application/WhatShouldIDo.Application.csproj
dotnet build src/WhatShouldIDo.Infrastructure/WhatShouldIDo.Infrastructure.csproj
dotnet build src/WhatShouldIDo.API/WhatShouldIDo.API.csproj
dotnet build src/WhatShouldIDo.Tests/WhatShouldIDo.API.IntegrationTests.csproj

# 4. Or build entire solution
dotnet build WhatShouldIDo.sln --configuration Release
```

## Quick Clean Build Script

Create a file named `clean-build.sh`:

```bash
#!/bin/bash
echo "=== Cleaning bin and obj folders ==="
find . -type d \( -name bin -o -name obj \) -exec rm -rf {} + 2>/dev/null

echo ""
echo "=== Restoring packages ==="
dotnet restore WhatShouldIDo.sln

echo ""
echo "=== Building solution ==="
dotnet build WhatShouldIDo.sln --configuration Release --no-incremental

if [ $? -eq 0 ]; then
    echo ""
    echo "✓ BUILD SUCCESSFUL"
else
    echo ""
    echo "✗ BUILD FAILED"
    exit 1
fi
```

Make it executable:
```bash
chmod +x clean-build.sh
./clean-build.sh
```

## Why Clean Build is Needed

The errors you're seeing are likely caused by:
1. **Stale metadata files** - Previous compilation left incomplete metadata in obj folders
2. **Cascading errors** - One compilation error causes dependent projects to fail
3. **Incremental build issues** - MSBuild trying to reuse old artifacts

A clean build will:
- Remove all compiled artifacts (bin/obj folders)
- Force fresh compilation of all projects
- Rebuild dependency metadata correctly
- Resolve cascading error chains

## Expected Result

After clean build, you should see:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## If Errors Persist

If you still see the "GetUserPreferencesAsync" error after clean build, please share the exact error message and file name so I can fix the specific occurrence.

## All Fixes Applied

✅ GenerateDailyItineraryCommandHandler.cs - Line 72: GetUserPreferencesAsync → GetLearnedPreferencesAsync
✅ GenerateDailyItineraryCommandHandler.cs - Lines 107-134: Fixed Route creation logic
✅ GenerateDailyItineraryCommandHandlerTests.cs - 3 occurrences fixed
✅ AIProvidersIntegrationTests.cs - Fixed WebApplicationFactory<Program>
✅ SearchAndRouteFlowTests.cs - Fixed WebApplicationFactory<Program>
✅ All interface files - Added System using directives
