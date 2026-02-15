# Hybrid AI Implementation Guide

## Status: Phase 1 Complete ✅

This guide provides a complete roadmap for implementing the hybrid AI layer with personalization, route intelligence, and observability.

---

## ✅ Phase 1: Core AI Infrastructure (COMPLETED)

### 1.1 Extended IAIService ✅
**File**: `src/WhatShouldIDo.Application/Interfaces/IAIService.cs`

Added `GetEmbeddingAsync()` method to expose embedding generation for personalization.

### 1.2 Implemented GetEmbeddingAsync in AIService ✅
**File**: `src/WhatShouldIDo.Infrastructure/Services/AI/AIService.cs`

Added embedding generation with caching (24-hour TTL).

### 1.3 Created HybridAIOrchestrator ✅
**File**: `src/WhatShouldIDo.Infrastructure/Services/AI/HybridAIOrchestrator.cs`

Features:
- Multi-provider management
- Priority-based selection (chat vs embedding)
- Automatic failover with health checks
- Circuit breaker pattern (5-minute unhealthy cooldown)
- Comprehensive metrics integration

---

## 📋 Phase 2: Observability & Configuration (TODO)

### 2.1 Extend PrometheusMetricsService

**File**: `src/WhatShouldIDo.Infrastructure/Services/PrometheusMetricsService.cs`

Add these methods:

```csharp
using Prometheus;

public class PrometheusMetricsService : IPrometheusMetricsService
{
    // Existing metrics...

    // AI Provider Metrics
    private static readonly Counter AiProviderSelected = Metrics.CreateCounter(
        "ai_provider_selected_total",
        "Number of times each AI provider was selected",
        new CounterConfiguration { LabelNames = new[] { "provider" } });

    private static readonly Histogram AiCallLatency = Metrics.CreateHistogram(
        "ai_call_latency_seconds",
        "Latency of AI API calls",
        new HistogramConfiguration
        {
            LabelNames = new[] { "provider", "operation" },
            Buckets = Histogram.ExponentialBuckets(0.01, 2, 10)
        });

    private static readonly Counter AiCallSuccess = Metrics.CreateCounter(
        "ai_call_success_total",
        "Number of successful AI API calls",
        new CounterConfiguration { LabelNames = new[] { "provider" } });

    private static readonly Counter AiCallFailures = Metrics.CreateCounter(
        "ai_call_failures_total",
        "Number of failed AI API calls",
        new CounterConfiguration { LabelNames = new[] { "provider", "reason" } });

    private static readonly Histogram RouteGenerationDuration = Metrics.CreateHistogram(
        "route_generation_duration_seconds",
        "Duration of AI route generation",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.1, 2, 10)
        });

    public void RecordAIProviderSelected(string providerName)
    {
        AiProviderSelected.WithLabels(providerName).Inc();
    }

    public void RecordAICallLatency(string providerName, string operation, double seconds)
    {
        AiCallLatency.WithLabels(providerName, operation).Observe(seconds);
    }

    public void IncrementAICallSuccess(string providerName)
    {
        AiCallSuccess.WithLabels(providerName).Inc();
    }

    public void IncrementAICallFailure(string providerName, string reason)
    {
        AiCallFailures.WithLabels(providerName, reason).Inc();
    }

    public void RecordRouteGenerationDuration(double seconds)
    {
        RouteGenerationDuration.Observe(seconds);
    }
}
```

### 2.2 Update AIOptions Configuration

**File**: `src/WhatShouldIDo.Infrastructure/Options/AIOptions.cs`

Add provider priority configuration:

```csharp
public class AIOptions
{
    public bool Enabled { get; set; } = true;
    public string Provider { get; set; } = "OpenAI";
    public double DefaultTemperature { get; set; } = 0.7;
    public bool EnableCaching { get; set; } = true;
    public int CacheTTLMinutes { get; set; } = 60;

    // NEW: Provider Priority
    public ProviderPriorityOptions? ProviderPriority { get; set; }

    // Existing provider configs...
    public OpenAIOptions OpenAI { get; set; } = new();
    public HuggingFaceOptions? HuggingFace { get; set; }
    public OllamaOptions? Ollama { get; set; }
    public AzureAIOptions? AzureAI { get; set; }
}

public class ProviderPriorityOptions
{
    public List<string> Chat { get; set; } = new() { "OpenAI", "Ollama", "NoOp" };
    public List<string> Embedding { get; set; } = new() { "OpenAI", "Ollama", "NoOp" };
}
```

### 2.3 Update appsettings.Development.json

```json
{
  "AI": {
    "Enabled": true,
    "Provider": "OpenAI",
    "EnableCaching": true,
    "CacheTTLMinutes": 60,
    "ProviderPriority": {
      "Chat": ["OpenAI", "Ollama", "NoOp"],
      "Embedding": ["OpenAI", "Ollama", "NoOp"]
    },
    "OpenAI": {
      "ApiKey": "${OPENAI_API_KEY}",
      "ChatModel": "gpt-4o-mini",
      "EmbeddingModel": "text-embedding-3-small",
      "Timeout": 30,
      "MaxTokens": 2000
    },
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "ChatModel": "llama2",
      "EmbeddingModel": "llama2",
      "Timeout": 60
    }
  }
}
```

---

## 📋 Phase 3: Database & Personalization (TODO)

### 3.1 Add pgvector Extension Migration

**File**: Create `src/WhatShouldIDo.Infrastructure/Data/Migrations/YYYYMMDDHHMMSS_AddPgvectorSupport.cs`

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

public partial class AddPgvectorSupport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Enable pgvector extension
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

        // Add embedding column to UserProfiles (1536 dimensions for text-embedding-3-small)
        migrationBuilder.AddColumn<string>(
            name: "Embedding",
            table: "UserProfiles",
            type: "vector(1536)",
            nullable: true);

        // Add index for vector similarity search
        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS idx_userprofiles_embedding_cosine
            ON ""UserProfiles"" USING ivfflat (""Embedding"" vector_cosine_ops)
            WITH (lists = 100);
        ");

        // Add LastEmbeddingUpdate tracking
        migrationBuilder.AddColumn<DateTime>(
            name: "LastEmbeddingUpdate",
            table: "UserProfiles",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("idx_userprofiles_embedding_cosine", "UserProfiles");
        migrationBuilder.DropColumn("Embedding", "UserProfiles");
        migrationBuilder.DropColumn("LastEmbeddingUpdate", "UserProfiles");
    }
}
```

### 3.2 Create UserAction Tracking Entity

**File**: `src/WhatShouldIDo.Domain/Entities/UserAction.cs`

```csharp
namespace WhatShouldIDo.Domain.Entities
{
    public class UserAction : EntityBase
    {
        public string UserHash { get; set; } = string.Empty;
        public string PlaceId { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty; // "search", "view", "like", "save_route"
        public DateTime Timestamp { get; set; }
        public double Weight { get; set; } = 1.0;
        public string? Metadata { get; set; } // JSON for additional context
    }
}
```

### 3.3 Add UserActions DbSet

**File**: `src/WhatShouldIDo.Infrastructure/Data/WhatShouldIDoDbContext.cs`

```csharp
public DbSet<UserAction> UserActions { get; set; }
```

### 3.4 Extend PreferenceLearningService

**File**: `src/WhatShouldIDo.Infrastructure/Services/PreferenceLearningService.cs`

Add these methods:

```csharp
private readonly IAIService _aiService;

public PreferenceLearningService(
    WhatShouldIDoDbContext context,
    ILogger<PreferenceLearningService> logger,
    IAIService aiService)
{
    _context = context;
    _logger = logger;
    _aiService = aiService;
}

/// <summary>
/// Generates or updates user embedding based on their action history
/// </summary>
public async Task<float[]> GetOrUpdateUserEmbeddingAsync(
    string userHash,
    CancellationToken cancellationToken = default)
{
    var userProfile = await _context.UserProfiles
        .FirstOrDefaultAsync(p => p.UserId.ToString() == userHash, cancellationToken);

    // Check if embedding needs update (older than 24 hours or doesn't exist)
    if (userProfile?.LastEmbeddingUpdate == null ||
        DateTime.UtcNow - userProfile.LastEmbeddingUpdate > TimeSpan.FromHours(24))
    {
        await RegenerateUserEmbeddingAsync(userHash, cancellationToken);

        userProfile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId.ToString() == userHash, cancellationToken);
    }

    if (userProfile?.Embedding != null)
    {
        return ParseEmbedding(userProfile.Embedding);
    }

    return Array.Empty<float>();
}

/// <summary>
/// Regenerates user embedding from weighted action history
/// </summary>
private async Task RegenerateUserEmbeddingAsync(
    string userHash,
    CancellationToken cancellationToken)
{
    // Get recent actions (last 100)
    var recentActions = await _context.UserActions
        .Where(a => a.UserHash == userHash)
        .OrderByDescending(a => a.Timestamp)
        .Take(100)
        .ToListAsync(cancellationToken);

    if (recentActions.Count < 5)
    {
        _logger.LogInformation("Not enough actions for user {UserHash} to generate embedding", userHash);
        return;
    }

    // Generate text representation of user preferences
    var userPreferenceText = BuildUserPreferenceText(recentActions);

    // Generate embedding
    var embedding = await _aiService.GetEmbeddingAsync(userPreferenceText, cancellationToken);

    // Store in database
    var userProfile = await _context.UserProfiles
        .FirstOrDefaultAsync(p => p.UserId.ToString() == userHash, cancellationToken);

    if (userProfile != null)
    {
        userProfile.Embedding = SerializeEmbedding(embedding);
        userProfile.LastEmbeddingUpdate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated embedding for user {UserHash} ({ActionCount} actions)",
            userHash, recentActions.Count);
    }
}

private string BuildUserPreferenceText(List<UserAction> actions)
{
    var likes = actions.Where(a => a.ActionType == "like").Select(a => a.PlaceId);
    var visits = actions.Where(a => a.ActionType == "visit").Select(a => a.PlaceId);
    var saves = actions.Where(a => a.ActionType == "save_route").Select(a => a.PlaceId);

    return $"User preferences: liked {string.Join(", ", likes)}, " +
           $"visited {string.Join(", ", visits)}, " +
           $"saved {string.Join(", ", saves)}";
}

private string SerializeEmbedding(float[] embedding)
{
    return $"[{string.Join(",", embedding)}]";
}

private float[] ParseEmbedding(string embeddingStr)
{
    var cleaned = embeddingStr.Trim('[', ']');
    return cleaned.Split(',').Select(float.Parse).ToArray();
}
```

---

## 📋 Phase 4: AI Route Generation with Diversity (TODO)

### 4.1 Create Diversity Helper

**File**: `src/WhatShouldIDo.Application/Services/DiversityHelper.cs`

```csharp
namespace WhatShouldIDo.Application.Services
{
    public class DiversityHelper
    {
        private readonly Random _random = new();

        /// <summary>
        /// Epsilon-greedy selection: exploit (familiar) vs explore (novel)
        /// </summary>
        public List<T> EpsilonGreedySelection<T>(
            List<(T item, double score)> rankedItems,
            double epsilon,
            int count)
        {
            var selected = new List<T>();
            var exploitCount = (int)((1 - epsilon) * count);
            var exploreCount = count - exploitCount;

            // Exploit: take top-scored items
            selected.AddRange(rankedItems
                .OrderByDescending(x => x.score)
                .Take(exploitCount)
                .Select(x => x.item));

            // Explore: randomly sample from remaining
            var remaining = rankedItems
                .OrderByDescending(x => x.score)
                .Skip(exploitCount)
                .ToList();

            if (remaining.Any())
            {
                for (int i = 0; i < exploreCount && remaining.Any(); i++)
                {
                    var randomIndex = _random.Next(remaining.Count);
                    selected.Add(remaining[randomIndex].item);
                    remaining.RemoveAt(randomIndex);
                }
            }

            return selected;
        }

        /// <summary>
        /// Softmax-based selection for smoother diversity
        /// </summary>
        public List<T> SoftmaxSelection<T>(
            List<(T item, double score)> rankedItems,
            double temperature,
            int count)
        {
            // Apply softmax with temperature
            var maxScore = rankedItems.Max(x => x.score);
            var probabilities = rankedItems
                .Select(x => Math.Exp((x.score - maxScore) / temperature))
                .ToList();

            var sum = probabilities.Sum();
            var normalizedProbs = probabilities.Select(p => p / sum).ToList();

            // Sample without replacement
            var selected = new List<T>();
            var available = rankedItems.ToList();

            for (int i = 0; i < count && available.Any(); i++)
            {
                var r = _random.NextDouble();
                var cumulative = 0.0;

                for (int j = 0; j < available.Count; j++)
                {
                    cumulative += normalizedProbs[j];
                    if (r <= cumulative)
                    {
                        selected.Add(available[j].item);
                        available.RemoveAt(j);
                        normalizedProbs.RemoveAt(j);

                        // Renormalize
                        var newSum = normalizedProbs.Sum();
                        normalizedProbs = normalizedProbs.Select(p => p / newSum).ToList();
                        break;
                    }
                }
            }

            return selected;
        }
    }
}
```

### 4.2 Extend DayPlanningService with AI

**File**: `src/WhatShouldIDo.Infrastructure/Services/DayPlanningService.cs`

Add this method:

```csharp
public async Task<DayPlanDto> GenerateAIDrivenRouteAsync(
    Guid userId,
    DayPlanRequest request,
    double diversityFactor = 0.2,
    CancellationToken cancellationToken = default)
{
    var stopwatch = Stopwatch.StartNew();

    try
    {
        // Step 1: Get user embedding
        var userEmbedding = await _preferenceLearningService
            .GetOrUpdateUserEmbeddingAsync(userId.ToString(), cancellationToken);

        // Step 2: Fetch candidate places
        var candidates = await _placesProvider.SearchNearbyAsync(
            request.Latitude,
            request.Longitude,
            request.Radius ?? 5000,
            request.Categories?.FirstOrDefault(),
            50);

        // Step 3: Score candidates by similarity to user preferences
        var scoredCandidates = new List<(PlaceDto place, double score)>();

        foreach (var place in candidates)
        {
            var placeText = $"{place.Name} {string.Join(" ", place.Types ?? new List<string>())}";
            var placeEmbedding = await _aiService.GetEmbeddingAsync(placeText, cancellationToken);
            var similarity = CosineSimilarity(userEmbedding, placeEmbedding);
            scoredCandidates.Add((place, similarity));
        }

        // Step 4: Apply ε-greedy diversity selection
        var diversityHelper = new DiversityHelper();
        var selectedPlaces = diversityHelper.EpsilonGreedySelection(
            scoredCandidates,
            diversityFactor,
            request.ActivityCount ?? 5);

        // Step 5: Generate AI-driven itinerary
        var itineraryRequest = new AIItineraryRequest
        {
            UserId = userId,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            StartLocation = new Location { Latitude = request.Latitude, Longitude = request.Longitude },
            AvailablePlaces = selectedPlaces,
            Budget = request.Budget,
            Transportation = request.Transportation ?? "walking"
        };

        var aiItinerary = await _aiService.GenerateDailyItineraryAsync(itineraryRequest, cancellationToken);

        // Step 6: Build DayPlanDto
        var dayPlan = new DayPlanDto
        {
            Id = Guid.NewGuid(),
            Title = aiItinerary.Title,
            Description = aiItinerary.Description,
            Date = DateTime.Today,
            EstimatedDuration = request.EndTime - request.StartTime,
            Budget = request.Budget ?? "medium",
            Activities = MapToActivities(aiItinerary.Activities),
            TotalDistance = aiItinerary.TotalDistance,
            Transportation = request.Transportation ?? "walking",
            IsPersonalized = true,
            CreatedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                { "diversityApplied", diversityFactor },
                { "reasoning", aiItinerary.Reasoning },
                { "aiProvider", _aiService.ProviderName }
            }
        };

        stopwatch.Stop();
        _metrics.RecordRouteGenerationDuration(stopwatch.Elapsed.TotalSeconds);

        return dayPlan;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to generate AI-driven route for user {UserId}", userId);
        throw;
    }
}

private double CosineSimilarity(float[] v1, float[] v2)
{
    if (v1.Length != v2.Length) return 0;

    double dot = 0, mag1 = 0, mag2 = 0;
    for (int i = 0; i < v1.Length; i++)
    {
        dot += v1[i] * v2[i];
        mag1 += v1[i] * v1[i];
        mag2 += v2[i] * v2[i];
    }
    return dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
}
```

---

## 📋 Phase 5: New API Endpoints (TODO)

### 5.1 Create AI Route Generation Endpoint

**File**: `src/WhatShouldIDo.API/Controllers/RoutesController.cs`

Add this endpoint:

```csharp
/// <summary>
/// Generate AI-driven route with personalization and diversity
/// </summary>
[HttpPost("ai/generate")]
[Authorize]
[ProducesResponseType(typeof(DayPlanDto), 200)]
public async Task<IActionResult> GenerateAIRoute(
    [FromQuery] double? diversity = 0.2,
    [FromBody] DayPlanRequest request = null,
    CancellationToken cancellationToken = default)
{
    var userId = GetCurrentUserId();
    if (!userId.HasValue)
        return Unauthorized();

    request ??= new DayPlanRequest
    {
        Latitude = 41.0082,
        Longitude = 28.9784,
        Radius = 5000,
        StartTime = 9,
        EndTime = 18,
        ActivityCount = 5
    };

    var result = await _dayPlanningService.GenerateAIDrivenRouteAsync(
        userId.Value,
        request,
        diversity ?? 0.2,
        cancellationToken);

    return Ok(new
    {
        success = true,
        data = result,
        metadata = new
        {
            diversityFactor = diversity,
            provider = result.Metadata?["aiProvider"],
            processingTime = "measured by metrics"
        }
    });
}
```

### 5.2 Create Place Summary Endpoint

**File**: `src/WhatShouldIDo.API/Controllers/PlacesController.cs`

Add this endpoint:

```csharp
/// <summary>
/// Get AI-generated summary of a place
/// </summary>
[HttpGet("{id}/summary")]
[ProducesResponseType(typeof(PlaceSummary), 200)]
public async Task<IActionResult> GetPlaceSummary(
    string id,
    CancellationToken cancellationToken = default)
{
    var cacheKey = $"place:summary:{id}";

    // Try cache first
    var cached = await _cacheService.GetAsync<PlaceSummary>(cacheKey);
    if (cached != null)
    {
        return Ok(new { success = true, data = cached, cached = true });
    }

    // Get place details
    var place = await _placesProvider.GetPlaceDetailsAsync(id);
    if (place == null)
        return NotFound();

    // Generate AI summary
    var summary = await _aiService.SummarizePlaceAsync(place, cancellationToken);

    // Cache for 1 hour
    await _cacheService.SetAsync(cacheKey, summary, TimeSpan.FromHours(1));

    return Ok(new { success = true, data = summary, cached = false });
}
```

---

## 📋 Phase 6: Background Jobs (TODO)

### 6.1 Create Background Job Service

**File**: `src/WhatShouldIDo.Infrastructure/Services/BackgroundJobs/PreferenceUpdateJob.cs`

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WhatShouldIDo.Infrastructure.Services.BackgroundJobs
{
    public class PreferenceUpdateJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PreferenceUpdateJob> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromHours(1);

        public PreferenceUpdateJob(
            IServiceProvider serviceProvider,
            ILogger<PreferenceUpdateJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Preference Update Job started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await UpdateUserPreferencesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Preference Update Job");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task UpdateUserPreferencesAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var preferenceLearningService = scope.ServiceProvider
                .GetRequiredService<IPreferenceLearningService>();
            var context = scope.ServiceProvider
                .GetRequiredService<WhatShouldIDoDbContext>();

            // Get users with stale embeddings (> 24 hours old)
            var staleProfiles = await context.UserProfiles
                .Where(p => p.LastEmbeddingUpdate == null ||
                           p.LastEmbeddingUpdate < DateTime.UtcNow.AddHours(-24))
                .Take(10) // Process 10 at a time
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Processing {Count} stale user embeddings", staleProfiles.Count);

            foreach (var profile in staleProfiles)
            {
                try
                {
                    await preferenceLearningService.GetOrUpdateUserEmbeddingAsync(
                        profile.UserId.ToString(),
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update embedding for user {UserId}", profile.UserId);
                }
            }
        }
    }
}
```

### 6.2 Register Background Job

**File**: `src/WhatShouldIDo.API/Program.cs`

Add to DI registration:

```csharp
// Background Jobs
builder.Services.AddHostedService<PreferenceUpdateJob>();
```

---

## 📋 Phase 7: Testing (TODO)

### 7.1 Unit Tests

**File**: `tests/WhatShouldIDo.Tests/Services/HybridAIOrchestratorTests.cs`

```csharp
public class HybridAIOrchestratorTests
{
    [Fact]
    public async Task SelectProviderForChat_ShouldReturnFirstHealthyProvider()
    {
        // Arrange
        var mockProvider1 = new Mock<IAIProvider>();
        mockProvider1.Setup(p => p.Name).Returns("Provider1");
        mockProvider1.Setup(p => p.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var providers = new List<IAIProvider> { mockProvider1.Object };
        var options = Options.Create(new AIOptions
        {
            ProviderPriority = new ProviderPriorityOptions
            {
                Chat = new List<string> { "Provider1" }
            }
        });

        var orchestrator = new HybridAIOrchestrator(
            providers,
            options,
            Mock.Of<ILogger<HybridAIOrchestrator>>(),
            Mock.Of<IPrometheusMetricsService>());

        // Act
        var result = await orchestrator.SelectProviderForChatAsync();

        // Assert
        Assert.Equal("Provider1", result.Name);
    }

    [Fact]
    public async Task ExecuteWithFailover_ShouldTryFallback_WhenPrimaryFails()
    {
        // Test failover logic
    }
}
```

### 7.2 Integration Tests

**File**: `tests/WhatShouldIDo.IntegrationTests/AI/RouteGenerationTests.cs`

```csharp
public class RouteGenerationTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GenerateAIRoute_ShouldReturnPersonalizedRoute()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new DayPlanRequest
        {
            Latitude = 41.0082,
            Longitude = 28.9784,
            Radius = 5000,
            StartTime = 9,
            EndTime = 18
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/routes/ai/generate?diversity=0.3", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<DayPlanDto>>();
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsPersonalized);
    }
}
```

---

## 📋 Implementation Checklist

### Phase 2: Observability
- [ ] Add AI metrics to PrometheusMetricsService
- [ ] Update AIOptions with ProviderPriority
- [ ] Update appsettings.json

### Phase 3: Database & Personalization
- [ ] Create pgvector migration
- [ ] Add UserAction entity
- [ ] Extend PreferenceLearningService with embeddings
- [ ] Test pgvector queries

### Phase 4: AI Route Generation
- [ ] Create DiversityHelper
- [ ] Extend DayPlanningService
- [ ] Implement ε-greedy selection
- [ ] Test diversity algorithms

### Phase 5: API Endpoints
- [ ] Add /api/routes/ai/generate endpoint
- [ ] Add /api/places/{id}/summary endpoint
- [ ] Update Swagger documentation
- [ ] Add endpoint tests

### Phase 6: Background Jobs
- [ ] Create PreferenceUpdateJob
- [ ] Register as HostedService
- [ ] Add job metrics
- [ ] Test job execution

### Phase 7: Testing
- [ ] Write unit tests for orchestrator
- [ ] Write integration tests for endpoints
- [ ] Test diversity algorithms
- [ ] Test failover scenarios

---

## 🚀 Quick Start

1. **Run migrations**:
   ```bash
   dotnet ef migrations add AddPgvectorSupport --project src/WhatShouldIDo.Infrastructure
   dotnet ef database update
   ```

2. **Configure providers**:
   ```bash
   export OPENAI_API_KEY="sk-your-key"
   ```

3. **Run application**:
   ```bash
   dotnet run --project src/WhatShouldIDo.API
   ```

4. **Test AI route generation**:
   ```bash
   curl -X POST http://localhost:5000/api/routes/ai/generate?diversity=0.3 \
     -H "Authorization: Bearer YOUR_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
       "latitude": 41.0082,
       "longitude": 28.9784,
       "radius": 5000,
       "startTime": 9,
       "endTime": 18,
       "activityCount": 5
     }'
   ```

---

## 📊 Metrics to Monitor

- `ai_provider_selected_total{provider="OpenAI"}` - Provider usage
- `ai_call_latency_seconds` - API latency
- `ai_call_failures_total` - Failure rate
- `route_generation_duration_seconds` - Generation performance

---

## 🎯 Acceptance Criteria

✅ Phase 1 Complete:
- [x] IAIService exposes GetEmbeddingAsync
- [x] AIService implements GetEmbeddingAsync with caching
- [x] HybridAIOrchestrator manages multi-provider selection

🔄 Remaining:
- [ ] pgvector enabled and user embeddings stored
- [ ] AI route generation with ε-greedy diversity
- [ ] /api/routes/ai/generate endpoint functional
- [ ] Background job updates embeddings
- [ ] All metrics emitted correctly
- [ ] Tests passing

---

**Next Steps**: Continue with Phase 2 (Observability) to add metrics and configuration.
