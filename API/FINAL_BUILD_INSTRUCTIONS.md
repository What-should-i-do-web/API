# 🚀 Final Build Instructions

**Last Updated:** November 6, 2025
**Status:** Ready to Build

---

## ⚡ QUICK BUILD (Recommended)

### Option 1: Using Build Script

```bash
cd /mnt/c/Users/ertan/Desktop/LAB/githubProjects/WhatShouldIDo/NeYapsamWeb/API

# Set OpenAI API Key (optional but recommended)
export OPENAI_API_KEY="sk-your-actual-key-here"

# Run the build script
./BUILD.sh
```

### Option 2: Manual Build

```bash
cd /mnt/c/Users/ertan/Desktop/LAB/githubProjects/WhatShouldIDo/NeYapsamWeb/API

# Clean
dotnet clean

# Restore
dotnet restore

# Build
dotnet build WhatShouldIDo.sln --configuration Release

# Run
dotnet run --project src/WhatShouldIDo.API
```

---

## 🔧 IF BUILD FAILS

### Common Issue: ICacheService Implementations

The caching implementations may need updates. Here's how to fix:

#### 1. Update FallbackCacheService

Edit: `src/WhatShouldIDo.Infrastructure/Caching/FallbackCacheService.cs`

Add these methods if missing:

```csharp
public async Task<T?> GetAsync<T>(string key) where T : class
{
    // Implementation for get
    var result = await GetOrSetAsync(key, () => Task.FromResult(default(T)!));
    return result;
}

public async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
{
    // Implementation for set
    await GetOrSetAsync(key, () => Task.FromResult(value), expiration);
}

public async Task<bool> ExistsAsync(string key)
{
    try
    {
        var result = await GetAsync<object>(key);
        return result != null;
    }
    catch
    {
        return false;
    }
}
```

#### 2. Update RedisClusterCacheService

Edit: `src/WhatShouldIDo.Infrastructure/Caching/RedisClusterCacheService.cs`

Add the same methods as above.

---

## 🎯 ALTERNATIVE: Use NoOp Provider Temporarily

If you want to skip the cache fixes temporarily:

### Edit Program.cs

Replace the ICacheService registration (around line 130):

```csharp
// Temporary: Register a simple in-memory cache
builder.Services.AddSingleton<ICacheService>(provider =>
{
    return new SimpleCacheService(); // We'll create this
});
```

### Create SimpleCacheService

Create: `src/WhatShouldIDo.Infrastructure/Caching/SimpleCacheService.cs`

```csharp
using System.Collections.Concurrent;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Caching
{
    public class SimpleCacheService : ICacheService
    {
        private readonly ConcurrentDictionary<string, object> _cache = new();

        public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> acquire, TimeSpan? absoluteExpiration = null)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                return Task.FromResult((T)cached);
            }

            var result = acquire().Result;
            _cache[key] = result!;
            return Task.FromResult(result);
        }

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            _cache.TryGetValue(key, out var value);
            return Task.FromResult(value as T);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
        {
            _cache[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            _cache.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key)
        {
            return Task.FromResult(_cache.ContainsKey(key));
        }
    }
}
```

---

## ✅ VERIFICATION

After successful build, verify with:

```bash
# 1. Check health
curl http://localhost:5000/health

# 2. Check AI health
curl http://localhost:5000/api/places/ai/health

# Expected response:
# {
#   "success": true,
#   "healthy": true,
#   "provider": "OpenAI",  // or "NoOp" if key not set
#   "timestamp": "2025-11-06T..."
# }
```

---

## 📋 PRE-BUILD CHECKLIST

Before building, ensure:

- [ ] .NET 9 SDK installed (`dotnet --version`)
- [ ] All project files are saved
- [ ] OPENAI_API_KEY set (optional)
- [ ] PostgreSQL running (for full functionality)
- [ ] Redis running (optional, will use in-memory fallback)

---

## 🐛 TROUBLESHOOTING

### Error: "ICacheService does not implement..."

**Solution:** The existing cache implementations need to be updated. Use the SimpleCacheService approach above or update the existing implementations.

### Error: "dotnet command not found"

**Solution:** You're in WSL but .NET isn't installed in WSL. Either:
1. Install .NET in WSL: `wget https://dot.net/v1/dotnet-install.sh && bash dotnet-install.sh --channel 9.0`
2. Or build from Windows PowerShell/CMD instead

### Error: "Cannot find project file"

**Solution:** Make sure you're in the correct directory:
```bash
pwd  # Should show: .../WhatShouldIDo/NeYapsamWeb/API
ls   # Should show: WhatShouldIDo.sln
```

---

## 🎯 SUCCESS CRITERIA

Build is successful if you see:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Then run and test:

```bash
dotnet run --project src/WhatShouldIDo.API

# In another terminal:
curl http://localhost:5000/health
# Should return: {"status":"ok"}
```

---

## 📞 NEED HELP?

1. Check **BUILD_FIXES_APPLIED.md** for detailed fixes
2. Check **AI_IMPLEMENTATION_GUIDE.md** for architecture
3. Check logs in `logs/api-.txt`

---

## 🎉 AFTER SUCCESSFUL BUILD

Once built and running:

1. **Test AI Search:**
```bash
curl -X POST http://localhost:5000/api/places/search \
  -H "Content-Type: application/json" \
  -d '{
    "query": "coffee shops near me",
    "latitude": 41.0082,
    "longitude": 28.9784,
    "radius": 2000,
    "maxResults": 5
  }'
```

2. **Access Swagger UI:**
   Open browser: `http://localhost:5000/swagger`

3. **Check Metrics:**
   Open browser: `http://localhost:5000/metrics`

---

**Ready to build! Run `./BUILD.sh` or follow manual steps above.**

Good luck! 🚀
