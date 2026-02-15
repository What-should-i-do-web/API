# AI Implementation Guide - WhatShouldIDo Backend

## Overview

This document describes the AI-powered features that have been implemented in the WhatShouldIDo backend, following Clean Architecture principles with a provider-agnostic design.

---

## ✅ What Has Been Implemented

### 1. AI Abstraction Layer

#### Core Interfaces (Application Layer)
- **`IAIService`** - High-level AI service interface
  - `InterpretPromptAsync()` - Converts natural language to structured search filters
  - `RankPlacesByRelevanceAsync()` - Semantic ranking of search results
  - `SummarizePlaceAsync()` - AI-generated place summaries
  - `GenerateDailyItineraryAsync()` - Full-day itinerary generation (interface defined, implementation pending)
  - `ExtractStructuredDataAsync()` - Generic structured data extraction

- **`IAIProvider`** - Low-level provider abstraction
  - `CompleteChatAsync()` - Chat completion
  - `CompleteJsonAsync()` - JSON-structured completion
  - `GenerateEmbeddingAsync()` - Vector embeddings for semantic similarity
  - `IsHealthyAsync()` - Provider health check

#### DTOs (Application Layer)
- **`InterpretedPrompt`** - Structured output from prompt interpretation
  - Categories, dietary restrictions, price preferences
  - Time context, atmosphere, activity types
  - Confidence scoring and AI reasoning

- **`PlaceSummary`** - AI-generated place summary
  - Concise summary, highlights, sentiment score
  - Best suited for (audiences), recommended times

- **`AIItinerary`** & **`AIItineraryRequest`** - Daily itinerary structures
  - Ordered stops with timing and reasoning
  - Travel time and distance calculations
  - Activity type categorization

### 2. AI Provider Implementations (Infrastructure Layer)

#### Implemented Providers
1. **OpenAIProvider** - Full implementation
   - Chat completions with JSON mode
   - Embedding generation (text-embedding-3-small)
   - Configurable models (default: gpt-4o-mini)
   - Retry logic and error handling

2. **NoOpAIProvider** - Testing/fallback provider
   - Returns empty/default responses
   - No external API calls
   - Used when AI is disabled

#### Planned Providers (Interfaces ready, implementation TODO)
- HuggingFaceProvider
- OllamaProvider (local LLM)
- AzureAIProvider

#### Supporting Services
- **AIService** - Main implementation with provider fallback
  - Automatic provider switching on failure
  - Caching support (Redis/in-memory)
  - Comprehensive logging and error handling
  - Cosine similarity calculation for embeddings

- **AIProviderFactory** - Dynamic provider selection
  - Configuration-based provider creation
  - Fallback provider support
  - Validation and health checks

### 3. Configuration System

- **AIOptions** - Complete configuration structure
  - Provider selection (primary and fallback)
  - Per-provider settings (API keys, models, endpoints)
  - Caching configuration
  - Timeout and retry settings

- See `appsettings.AI.json` for full configuration template

### 4. MediatR Integration

#### Commands & Queries
- **`SearchPlacesQuery`** - AI-powered place search
  - Natural language query interpretation
  - Semantic ranking
  - Filtering by categories, price, dietary preferences

- **`CreateRouteCommand`** - User-defined route creation
  - Multiple places with ordering
  - Distance and duration calculation
  - Route optimization options

- **`GenerateDailyItineraryCommand`** - AI itinerary generation
  - (Command defined, handler implementation pending)

#### Handlers
- **`SearchPlacesQueryHandler`** ✅ Complete
  - Orchestrates AI interpretation → API fetch → Ranking
  - Fallback to basic search on AI failure
  - Comprehensive logging

- **`CreateRouteCommandHandler`** ✅ Complete
  - Validates places and creates routes
  - Calculates distances using Haversine formula
  - Estimates travel time by transportation mode
  - Persists to database

---

## 🔧 Setup Instructions

### 1. Install Required NuGet Packages

Run these commands in the respective project directories:

```bash
# In WhatShouldIDo.Application
dotnet add package MediatR --version 12.4.0
dotnet add package FluentValidation --version 11.9.2
dotnet add package FluentValidation.DependencyInjectionExtensions --version 11.9.2

# In WhatShouldIDo.Infrastructure
# (Already has required packages: HttpClient, JSON, etc.)
```

### 2. Update Program.cs (Dependency Injection)

Add this configuration to your `Program.cs` after existing service registrations:

```csharp
// -------------------------------------
// MediatR Configuration
// -------------------------------------
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(SearchPlacesQuery).Assembly);
});

// -------------------------------------
// AI Configuration
// -------------------------------------
builder.Services.Configure<AIOptions>(builder.Configuration.GetSection("AI"));

// Register AI providers
builder.Services.AddHttpClient<OpenAIProvider>();
builder.Services.AddScoped<NoOpAIProvider>();
builder.Services.AddSingleton<AIProviderFactory>();

// Register AI service with provider factory
builder.Services.AddScoped<IAIService>(provider =>
{
    var factory = provider.GetRequiredService<AIProviderFactory>();
    var cacheService = provider.GetService<ICacheService>();
    var logger = provider.GetRequiredService<ILogger<AIService>>();
    var options = provider.GetRequiredService<IOptions<AIOptions>>();

    var primaryProvider = factory.CreatePrimaryProvider();
    var fallbackProvider = factory.CreateFallbackProvider();

    return new AIService(primaryProvider, options, logger, cacheService, fallbackProvider);
});

Log.Information("AI service registered with provider: {Provider}",
    builder.Configuration["AI:Provider"] ?? "OpenAI");
```

### 3. Configure appsettings.json

Merge the content of `appsettings.AI.json` into your `appsettings.json` or `appsettings.Development.json`.

**Important:** Set environment variables for sensitive data:

```bash
# Linux/Mac
export OPENAI_API_KEY="sk-..."

# Windows
set OPENAI_API_KEY=sk-...

# Or use dotnet user-secrets
dotnet user-secrets set "AI:OpenAI:ApiKey" "sk-..."
```

### 4. Update Controllers to Use MediatR

Modify `DiscoverController.cs` to use MediatR:

```csharp
using MediatR;
using WhatShouldIDo.Application.UseCases.Queries;

[ApiController]
[Route("api/[controller]")]
public class DiscoverController : ControllerBase
{
    private readonly IMediator _mediator;

    public DiscoverController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchPlacesQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
```

Similar updates needed for `RoutesController.cs`:

```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateRouteCommand command)
{
    var result = await _mediator.Send(command);
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
}
```

---

## 📋 Remaining Implementation Tasks

### High Priority

1. **Complete Daily Itinerary Generation**
   - Implement `GenerateDailyItineraryCommandHandler`
   - Logic:
     1. Fetch user preferences and search history
     2. Get available places in radius (cached)
     3. Use AI to select and order places for full-day plan
     4. Include diverse categories (landmarks → café → lunch → museum → dinner)
     5. Calculate timing and distances
     6. Save as Route entity with type "ai-generated"

2. **Enhance Route Repository**
   - Add methods: GetByUserIdAsync, GetByIdWithPointsAsync, UpdateAsync, DeleteAsync
   - Include route points in queries
   - Add pagination support

3. **Complete API Controllers**
   - Add `/api/places/search` endpoint (uses SearchPlacesQuery)
   - Add `/api/routes/ai/generate` endpoint (uses GenerateDailyItineraryCommand)
   - Add `/api/places/{id}/summary` endpoint
   - Update existing controllers to use MediatR

4. **Add Place Details Fetching**
   - Ensure `IPlacesProvider.GetPlaceDetailsAsync()` is implemented
   - Required by CreateRouteCommandHandler

### Medium Priority

5. **Additional AI Providers**
   - Implement HuggingFaceProvider (for cost-effective alternative)
   - Implement OllamaProvider (for local/offline support)
   - Implement AzureAIProvider (for enterprise scenarios)

6. **Route Optimization**
   - Integrate with Google Directions API for real route optimization
   - Implement traveling salesman problem (TSP) solver for optimal ordering
   - Add route visualization data

7. **User Preferences System**
   - Track user search patterns
   - Store activity preferences
   - Learn dietary restrictions
   - Use for personalization in itinerary generation

8. **Caching Strategy**
   - Cache AI interpretations (prompt → structured data)
   - Cache place summaries (long TTL)
   - Cache embeddings for frequently searched places
   - Invalidation on place updates

### Low Priority

9. **Validation**
   - Add FluentValidation validators for all commands/queries
   - Validate coordinates, radius limits, place ID formats
   - Sanitize user inputs

10. **Testing**
    - Unit tests for handlers (mock AI service)
    - Integration tests for AI providers
    - E2E tests for search and route creation flows

11. **Documentation**
    - Update README with AI features
    - Add API documentation (Swagger annotations)
    - Create usage examples
    - Document provider switching scenarios

---

## 🚀 Usage Examples

### 1. Search Places with Natural Language

```http
POST /api/places/search
Content-Type: application/json

{
  "query": "romantic italian restaurants with outdoor seating",
  "latitude": 41.0082,
  "longitude": 28.9784,
  "radius": 3000,
  "maxResults": 10,
  "useAIRanking": true
}
```

Response:
```json
{
  "places": [...],
  "totalCount": 10,
  "interpretedQuery": "italian restaurants outdoor seating romantic",
  "extractedCategories": ["restaurant", "italian_restaurant"],
  "usedAI": true,
  "aiConfidence": 0.92
}
```

### 2. Create a Custom Route

```http
POST /api/routes
Content-Type: application/json

{
  "name": "Istanbul Cultural Tour",
  "description": "Historic sites and museums",
  "userId": "user-guid-here",
  "placeIds": [
    "ChIJCZZmh...",
    "ChIJXTZqw...",
    "ChIJKZTps..."
  ],
  "optimizeOrder": true,
  "transportationMode": "walking",
  "tags": ["cultural", "history", "museums"]
}
```

### 3. Generate AI Itinerary (TODO)

```http
POST /api/routes/ai/generate
Content-Type: application/json

{
  "location": "Istanbul, Kadıköy",
  "latitude": 40.9907,
  "longitude": 29.0257,
  "targetDate": "2025-11-10",
  "startTime": "09:00",
  "endTime": "22:00",
  "preferredActivities": ["cultural", "food", "scenic"],
  "budgetLevel": "medium",
  "maxStops": 6,
  "saveAsRoute": true
}
```

---

## 🏗️ Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     API Layer (Controllers)                  │
│  DiscoverController, RoutesController, ItineraryController   │
└────────────────────────┬────────────────────────────────────┘
                         │ MediatR
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              Application Layer (Handlers)                     │
│  SearchPlacesQueryHandler, CreateRouteCommandHandler, etc.   │
└──────────┬──────────────────────────────────┬───────────────┘
           │                                    │
           ▼                                    ▼
┌──────────────────────┐            ┌─────────────────────────┐
│   IAIService         │            │  IPlacesProvider        │
│   (Orchestration)    │            │  (Google Places)        │
└──────────┬───────────┘            └─────────────────────────┘
           │
           ▼
┌──────────────────────────────────────────────────────────────┐
│           Infrastructure Layer (AI Providers)                 │
│  ┌────────────┐  ┌──────────────┐  ┌───────────────┐        │
│  │  OpenAI    │  │ HuggingFace  │  │    Ollama     │        │
│  │  Provider  │  │   Provider   │  │   Provider    │        │
│  └────────────┘  └──────────────┘  └───────────────┘        │
│                   AIProviderFactory                           │
└──────────────────────────────────────────────────────────────┘
```

---

## 🔒 Security Considerations

1. **API Key Management**
   - Never commit API keys to source control
   - Use environment variables or Azure Key Vault
   - Rotate keys regularly

2. **Rate Limiting**
   - AI calls are expensive - implement quota per user
   - Cache aggressively to reduce API calls
   - Consider usage limits for free tier users

3. **Input Validation**
   - Sanitize all user inputs before sending to AI
   - Limit prompt length (max 500 chars)
   - Validate coordinates and radius

4. **Cost Controls**
   - Set monthly budget alerts for AI providers
   - Monitor token usage per request
   - Implement circuit breakers for failing providers

---

## 📊 Monitoring & Observability

The system already has OpenTelemetry instrumentation. Add these custom metrics:

```csharp
// In AIService
_metricsService.RecordCounter("ai_requests_total", 1, new[] {
    new KeyValuePair<string, object?>("provider", _primaryProvider.Name),
    new KeyValuePair<string, object?>("operation", "interpret_prompt")
});

_metricsService.RecordHistogram("ai_request_duration_seconds", elapsed, new[] {
    new KeyValuePair<string, object?>("provider", _primaryProvider.Name)
});
```

Grafana queries:
- AI request rate: `rate(ai_requests_total[5m])`
- AI latency p95: `histogram_quantile(0.95, ai_request_duration_seconds_bucket)`
- Provider failure rate: `rate(ai_provider_failures_total[5m])`

---

## ✅ Testing Checklist

- [ ] Test prompt interpretation with various queries
- [ ] Test search with AI ranking enabled/disabled
- [ ] Test route creation with valid/invalid place IDs
- [ ] Test AI provider fallback scenarios
- [ ] Test caching behavior
- [ ] Test with AI disabled (NoOp provider)
- [ ] Load test AI endpoints (100 concurrent users)
- [ ] Test OpenAI API failures (simulate with chaos engineering)

---

## 📚 References

- [OpenAI API Documentation](https://platform.openai.com/docs/api-reference)
- [MediatR Wiki](https://github.com/jbogard/MediatR/wiki)
- [Clean Architecture by Uncle Bob](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Semantic Kernel (Microsoft AI orchestration)](https://github.com/microsoft/semantic-kernel)

---

## 🎯 Next Steps for Developer

1. **Immediate Actions:**
   - Run `dotnet restore` in all projects
   - Add AI configuration to appsettings
   - Update Program.cs with DI setup
   - Test basic prompt interpretation

2. **Within 1 Week:**
   - Implement GenerateDailyItineraryCommandHandler
   - Complete all API endpoints
   - Add validation
   - Write integration tests

3. **Within 2 Weeks:**
   - Implement additional AI providers
   - Add route optimization
   - Complete user preferences system
   - Deploy to staging

4. **Within 1 Month:**
   - Full test coverage
   - Production deployment
   - Monitor and optimize costs
   - Gather user feedback

---

## 🐛 Troubleshooting

### AI Service Not Working
- Check `AI:Enabled` is true in configuration
- Verify OPENAI_API_KEY environment variable is set
- Check logs for provider initialization errors
- Test provider health: `GET /health/ready`

### High AI Costs
- Enable caching: `AI:EnableCaching = true`
- Reduce max tokens: `AI:DefaultMaxTokens = 500`
- Use cheaper model: `AI:OpenAI:ChatModel = "gpt-4o-mini"`
- Implement user quotas

### Slow Response Times
- Increase timeout: `AI:TimeoutSeconds = 60`
- Enable caching
- Use fallback provider
- Consider local Ollama for non-critical requests

---

**Implementation Status:** 🟢 Core Complete | 🟡 Itinerary Generation Pending | 🟡 Additional Providers Pending

Last Updated: 2025-11-06
