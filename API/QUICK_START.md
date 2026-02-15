# 🚀 Quick Start Guide - WhatShouldIDo API

This guide will help you get started with the newly implemented features.

---

## 📋 Prerequisites

- ✅ .NET 9 SDK installed
- ✅ PostgreSQL running
- ✅ Redis running
- ✅ (Optional) Ollama installed for local AI
- ✅ (Optional) k6 installed for load testing

---

## ⚙️ Configuration

### Step 1: Update `appsettings.json`

Add the following sections to your `appsettings.json`:

```json
{
  "AI": {
    "Enabled": true,
    "Provider": "OpenAI",
    "FallbackProvider": "Ollama",
    "DefaultTemperature": 0.7,
    "DefaultMaxTokens": 1000,
    "EnableCaching": true,
    "CacheTTLMinutes": 60,

    "OpenAI": {
      "ApiKey": "sk-YOUR_OPENAI_KEY_HERE",
      "ChatModel": "gpt-4o-mini",
      "EmbeddingModel": "text-embedding-3-small"
    },

    "HuggingFace": {
      "ApiKey": "hf_YOUR_HUGGINGFACE_KEY_HERE",
      "ChatModel": "mistralai/Mixtral-8x7B-Instruct-v0.1",
      "EmbeddingModel": "sentence-transformers/all-MiniLM-L6-v2"
    },

    "Ollama": {
      "BaseUrl": "http://localhost:11434/api/",
      "ChatModel": "llama2",
      "EmbeddingModel": "nomic-embed-text"
    }
  },

  "GoogleMaps": {
    "ApiKey": "AIzaYOUR_GOOGLE_MAPS_KEY_HERE"
  }
}
```

### Step 2: Set Environment Variables (Alternative)

Instead of hardcoding API keys, use environment variables:

**Windows (PowerShell):**
```powershell
$env:AI__OPENAI__APIKEY="sk-..."
$env:AI__HUGGINGFACE__APIKEY="hf_..."
$env:GOOGLEMAPS__APIKEY="AIza..."
```

**Linux/macOS:**
```bash
export AI__OPENAI__APIKEY="sk-..."
export AI__HUGGINGFACE__APIKEY="hf_..."
export GOOGLEMAPS__APIKEY="AIza..."
```

### Step 3: Run Database Migrations

```bash
cd src/WhatShouldIDo.API
dotnet ef database update
```

---

## 🏃 Running the Application

### Option 1: Run Directly

```bash
cd src/WhatShouldIDo.API
dotnet run
```

### Option 2: Run with Docker Compose

```bash
docker-compose up -d
```

### Option 3: Run with Hot Reload (Development)

```bash
dotnet watch run --project src/WhatShouldIDo.API
```

---

## 🧪 Testing the New Features

### 1. **Test AI-Driven Itinerary Generation**

```bash
# First, get an auth token
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "YourPassword123!"
  }'

# Use the token to generate an itinerary
curl -X POST http://localhost:5000/api/dayplan/ai-generate \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE" \
  -d '{
    "location": "Istanbul, Turkey",
    "latitude": 41.0082,
    "longitude": 28.9784,
    "startTime": "09:00:00",
    "endTime": "20:00:00",
    "preferredActivities": ["cultural", "food"],
    "budgetLevel": "medium",
    "maxStops": 6
  }'
```

**Expected Response:**
```json
{
  "id": "guid",
  "title": "A Perfect Day in Istanbul",
  "description": "Explore the best of Istanbul with this AI-curated itinerary",
  "stops": [
    {
      "order": 1,
      "place": {
        "name": "Blue Mosque",
        "latitude": 41.0054,
        "longitude": 28.9768
      },
      "arrivalTime": "09:00:00",
      "durationMinutes": 90,
      "activityType": "sightseeing",
      "reason": "Iconic landmark with stunning architecture"
    }
  ],
  "totalDurationMinutes": 600,
  "totalDistanceMeters": 15000,
  "reasoning": "This itinerary balances culture, food, and sightseeing"
}
```

### 2. **Test Different AI Providers**

**Switch to HuggingFace:**
```json
{
  "AI": {
    "Provider": "HuggingFace"
  }
}
```

**Switch to Ollama (Local):**
```bash
# First, install and run Ollama
curl -fsSL https://ollama.com/install.sh | sh
ollama pull llama2
ollama serve

# Then update config
{
  "AI": {
    "Provider": "Ollama"
  }
}
```

### 3. **Test Route Optimization**

```bash
# Create a route
curl -X POST http://localhost:5000/api/routes \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Istanbul Highlights",
    "description": "Optimized tour route"
  }'

# Add waypoints to the route
# The route optimization service will automatically optimize the order
```

---

## 🧪 Running Tests

### Unit Tests
```bash
dotnet test --filter "FullyQualifiedName~Unit"
```

### Integration Tests (Requires API Keys)
```bash
export OPENAI_API_KEY="sk-..."
export HUGGINGFACE_API_KEY="hf_..."
dotnet test --filter "FullyQualifiedName~Integration"
```

### End-to-End Tests
```bash
dotnet test --filter "FullyQualifiedName~E2E"
```

### Load Tests
```bash
# Install k6
brew install k6  # macOS
choco install k6  # Windows

# Run load test
k6 run k6-tests/ai-itinerary-load-test.js

# With custom base URL
BASE_URL=https://api.whatshouldido.com k6 run k6-tests/ai-itinerary-load-test.js
```

---

## 📊 Monitoring & Observability

### Access Swagger UI
```
http://localhost:5000/swagger
```

### Check Health Endpoints
```bash
# Simple health check
curl http://localhost:5000/health

# Readiness check (includes dependencies)
curl http://localhost:5000/health/ready

# Prometheus metrics
curl http://localhost:5000/metrics
```

### Grafana Dashboards
If using the observability stack:
```
http://localhost:3000
Username: admin
Password: admin
```

---

## 🐛 Troubleshooting

### Issue: "AI provider not configured"

**Solution:** Ensure API keys are set in `appsettings.json` or environment variables.

### Issue: "Ollama connection refused"

**Solution:**
```bash
# Check if Ollama is running
curl http://localhost:11434/api/tags

# Start Ollama if not running
ollama serve
```

### Issue: "Quota exhausted"

**Solution:** Free users have 5 requests. Either:
1. Upgrade to premium (set `SubscriptionTier = "Premium"` in database)
2. Reset quota manually
3. Create a new test user

### Issue: "Google Directions API error"

**Solution:** Verify your Google Maps API key has these APIs enabled:
- Directions API
- Distance Matrix API

### Issue: "Database migration failed"

**Solution:**
```bash
# Check connection string
# Delete existing database and recreate
dotnet ef database drop
dotnet ef database update
```

---

## 📚 API Documentation

### New Endpoints

#### 1. Generate AI Itinerary
```
POST /api/dayplan/ai-generate
Authorization: Bearer {token}
```

#### 2. Search Places (AI-Powered)
```
POST /api/discover/prompt
Authorization: Bearer {token}
```

#### 3. Optimize Route
```
GET /api/routes/{id}/optimize
Authorization: Bearer {token}
```

### Full API Documentation
Visit: `http://localhost:5000/swagger`

---

## 🎯 Feature Highlights

### 1. AI-Driven Itinerary Generation
- Generates 1-8 stops based on time range
- Considers dietary preferences and restrictions
- Balances activity types (no 6 museums in a row!)
- Accounts for travel time between stops
- Provides AI reasoning for each recommendation

### 2. Multiple AI Provider Support
- **OpenAI** (GPT-4o-mini): Best quality, paid
- **HuggingFace** (Mixtral-8x7B): Good quality, paid
- **Ollama** (Llama 2/Mistral): Free, runs locally
- Automatic fallback if primary provider fails

### 3. Route Optimization
- **TSP Solver**: Nearest Neighbor + 2-Opt
- Minimizes total travel distance/time
- Supports multiple transport modes
- Google Directions API integration
- Haversine fallback for offline use

### 4. Comprehensive Testing
- 30+ test cases
- Unit + Integration + E2E + Load tests
- k6 performance validation
- SLO monitoring

---

## 🚀 Next Steps

1. ✅ Configure API keys
2. ✅ Run build and tests (`BUILD_AND_TEST.ps1`)
3. ✅ Start the application
4. ✅ Test AI itinerary generation
5. ✅ Explore Swagger documentation
6. ✅ Set up Grafana dashboards
7. ✅ Run load tests to validate performance

---

## 📞 Support

For issues or questions:
- Check `IMPLEMENTATION_COMPLETE.md` for detailed documentation
- Review API documentation at `/swagger`
- Check logs in `/logs/` directory
- Verify observability metrics at `/metrics`

---

**Happy Coding! 🎉**
