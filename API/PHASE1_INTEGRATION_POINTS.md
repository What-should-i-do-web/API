# Phase 1 - Exact Integration Points Report

**Date**: 2026-02-12
**Status**: Ready for Implementation

---

## 🎯 EXACT INTEGRATION POINTS IDENTIFIED

### 1. SmartSuggestionService - Main Personalization Pipeline

**File**: `src/WhatShouldIDo.Infrastructure/Services/SmartSuggestionService.cs`

**Method to Extend**: `CalculatePersonalizedScoreAsync` (lines 236-305)

**Current Scoring Logic** (Implicit Only):
```csharp
score = 0.5f (base)
     + 0.3f (if category matches favorite cuisines)
     + 0.2f (if category matches favorite activities)
     - 0.4f (if category in avoided types)
     + noveltyScore * 0.2f
     - avoidanceScore * 0.3f
     + (rating / 5f) * 0.1f
```

**Problems with Current Approach**:
- Hardcoded weights (0.3f, 0.2f, etc.)
- Simple additive scoring without normalization
- No explainability
- No support for explicit preferences

**Integration Strategy**:
1. **Extract current logic** into `ImplicitScorer` service
2. **Create new `IExplicitScorer`** for TasteProfile-based scoring
3. **Create `IHybridScorer`** that combines both:
   ```csharp
   finalScore = (wImplicit * implicitScore)
              + (wExplicit * explicitScore)
              + (wNovelty * noveltyScore)
              + (wQuality * qualityScore)
              + (wDistance * distanceScore)
   ```
4. **Inject `IHybridScorer`** into SmartSuggestionService constructor
5. **Replace line 178** from:
   ```csharp
   var personalizedScore = await CalculatePersonalizedScoreAsync(userId, place, userPreferences, cancellationToken);
   ```
   To:
   ```csharp
   var scoredPlace = await _hybridScorer.ScoreAndExplainAsync(userId, place, scoringContext, cancellationToken);
   suggestion.Score = scoredPlace.Score;
   suggestion.Reasons = scoredPlace.Reasons; // NEW FIELD
   ```

**No Parallel Pipeline**: We modify the existing pipeline, not create a new one.

---

### 2. Configuration Pattern - IOptions<T>

**Existing Pattern** (QuotaOptions.cs):
```csharp
namespace WhatShouldIDo.Application.Configuration
{
    public class QuotaOptions
    {
        [Range(1, 1000)]
        public int DefaultFreeQuota { get; set; } = 5;
        public bool DailyResetEnabled { get; set; } = false;
        // ...
    }
}
```

**New Options Classes to Add**:

#### A) TasteQuizOptions
**File**: `src/WhatShouldIDo.Application/Configuration/TasteQuizOptions.cs`
```csharp
public class TasteQuizOptions
{
    public string Version { get; set; } = "v1";
    public int DraftTtlHours { get; set; } = 24;
    public List<QuizStep> Steps { get; set; } = new();
}

public class QuizStep
{
    public string Id { get; set; }
    public string Type { get; set; } // multi-select, single-select, rating
    public string TitleKey { get; set; }
    public string DescriptionKey { get; set; }
    public List<QuizOption> Options { get; set; }
}

public class QuizOption
{
    public string Id { get; set; }
    public string LabelKey { get; set; }
    public Dictionary<string, double> Deltas { get; set; } // e.g., {"CultureWeight": 0.3}
}
```

#### B) RecommendationScoringOptions
**File**: `src/WhatShouldIDo.Application/Configuration/RecommendationScoringOptions.cs`
```csharp
public class RecommendationScoringOptions
{
    // Scoring weights
    public double ImplicitWeight { get; set; } = 0.25;
    public double ExplicitWeight { get; set; } = 0.30;
    public double NoveltyWeight { get; set; } = 0.20;
    public double ContextWeight { get; set; } = 0.15;
    public double QualityWeight { get; set; } = 0.10;

    // Caching
    public int CandidateCacheTtlMinutes { get; set; } = 15;
    public int PlaceDetailsCacheTtlMinutes { get; set; } = 60;

    // Limits
    public int MaxCandidates { get; set; } = 100;
    public int MaxResults { get; set; } = 20;
    public int DefaultRadius { get; set; } = 3000;

    // Quality scoring
    public int ReviewCountSmoothingFactor { get; set; } = 50;

    // Debug
    public bool EnableDebugFields { get; set; } = false;
}
```

**Registration in Program.cs** (after line 124):
```csharp
builder.Services.Configure<TasteQuizOptions>(builder.Configuration.GetSection("Feature:TasteQuiz"));
builder.Services.Configure<RecommendationScoringOptions>(builder.Configuration.GetSection("Feature:Recommendations"));
```

---

### 3. Dependency Injection Pattern

**Existing Pattern** (Program.cs lines 23-47):
```csharp
builder.Services.AddScoped<IVisitTrackingService, VisitTrackingService>();
builder.Services.AddScoped<IPreferenceLearningService, PreferenceLearningService>();
// ...
```

**New Services to Register**:
```csharp
// Taste Profile Domain Services
builder.Services.AddScoped<ITasteProfileRepository, TasteProfileRepository>();
builder.Services.AddScoped<ITasteEventRepository, TasteEventRepository>();
builder.Services.AddScoped<ITasteDraftStore, TasteDraftStore>();

// Scoring Services (NO PARALLEL PIPELINE - EXTENDS EXISTING)
builder.Services.AddScoped<IImplicitScorer, ImplicitScorer>(); // Extracted from SmartSuggestionService
builder.Services.AddScoped<IExplicitScorer, ExplicitScorer>(); // NEW - TasteProfile based
builder.Services.AddScoped<IHybridScorer, HybridScorer>(); // NEW - Combines both

// Explainability
builder.Services.AddScoped<IExplainabilityService, ExplainabilityService>();

// Quiz Service
builder.Services.AddScoped<ITasteQuizService, TasteQuizService>();
builder.Services.AddScoped<ITasteProfileService, TasteProfileService>();

// Mapper Services (Reusable)
builder.Services.AddSingleton<IPlaceCategoryMapper, PlaceCategoryMapper>(); // Maps place types -> interests
```

---

### 4. Database Context Extension

**File**: `src/WhatShouldIDo.Infrastructure/Data/WhatShouldIDoDbContext.cs`

**Add DbSets**:
```csharp
public DbSet<UserTasteProfile> UserTasteProfiles { get; set; }
public DbSet<UserTasteEvent> UserTasteEvents { get; set; }
```

**OnModelCreating Additions**:
```csharp
// UserTasteProfile
modelBuilder.Entity<UserTasteProfile>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.UserId).IsUnique();
    entity.Property(e => e.RowVersion).IsRowVersion();
    entity.HasOne(e => e.User).WithOne(u => u.TasteProfile).HasForeignKey<UserTasteProfile>(e => e.UserId);

    // All weights default to 0.5
    entity.Property(e => e.CultureWeight).HasDefaultValue(0.5);
    entity.Property(e => e.FoodWeight).HasDefaultValue(0.5);
    // ... etc
});

// UserTasteEvent
modelBuilder.Entity<UserTasteEvent>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => new { e.UserId, e.OccurredAtUtc });
    entity.Property(e => e.Payload).HasColumnType("jsonb");
});
```

---

### 5. VariabilityEngine Extension Point

**File**: `src/WhatShouldIDo.Infrastructure/Services/VariabilityEngine.cs`

**Method to Modify**: `ApplyDiscoveryBoostAsync` (currently uses fixed 30% discovery ratio)

**Change**:
```csharp
public async Task<List<Place>> ApplyDiscoveryBoostAsync(int userId, List<Place> places, CancellationToken ct)
{
    // NEW: Read TasteProfile to get NoveltyTolerance
    var tasteProfile = await _tasteProfileRepository.GetByUserIdAsync(userId, ct);

    // Dynamic discovery ratio based on user preference
    var discoveryRatio = tasteProfile != null
        ? 0.1 + (tasteProfile.NoveltyTolerance * 0.5) // 10%-60% range
        : 0.3; // Default 30% if no profile

    // Existing logic continues with dynamic discoveryRatio...
}
```

**Inject ITasteProfileRepository** into VariabilityEngine constructor.

---

### 6. Response DTO Extensions (Backward Compatible)

**File**: `src/WhatShouldIDo.Application/DTOs/Response/SuggestionDto.cs`

**Add Optional Fields**:
```csharp
public class SuggestionDto
{
    // Existing fields...
    public Guid Id { get; set; }
    public string PlaceName { get; set; }
    public string Reason { get; set; }
    public double Score { get; set; }
    // ...

    // NEW - Optional (null when explainability not requested)
    public List<RecommendationReasonDto>? Reasons { get; set; }
}
```

**New DTO**:
```csharp
public class RecommendationReasonDto
{
    public string ReasonCode { get; set; } // Stable: MATCHES_INTEREST_FOOD
    public string Message { get; set; }    // Localized: "Yemek ilgi alanınıza uyuyor"
    public double? Weight { get; set; }    // Optional debug: 0.85
}
```

---

### 7. Caching Strategy

**Cache Keys to Implement**:

#### A) Candidate Set Cache
```csharp
// Key format
string key = $"candidates:{latBucket}:{lngBucket}:{radius}:{types}:{openNow}:{timeBucket}";

// Example
"candidates:41.008:28.978:3000:restaurant,cafe:true:2026021214"

// TTL: 15 minutes (configurable)
```

#### B) TasteProfile Draft Cache (Redis)
```csharp
// Key format (hashed token)
string key = $"taste:draft:{SHA256(claimToken)}";

// Value: JSON serialized UserTasteProfile
// TTL: 24 hours (configurable)
```

**Implementation**: Reuse existing `ICacheService` abstraction (already handles Redis/InMemory fallback).

---

### 8. Observability Integration Points

**File**: `src/WhatShouldIDo.Infrastructure/Observability/MetricsService.cs`

**New Metrics to Add**:
```csharp
// Counters
private Counter<long> _recommendationRequestsCounter;
private Counter<long> _quizSubmissionsCounter;
private Counter<long> _tasteProfileUpdatesCounter;
private Counter<long> _feedbackEventsCounter;

// Histograms
private Histogram<double> _recommendationDurationHistogram;
private Histogram<long> _candidatesCountHistogram;

// Init in constructor
_recommendationRequestsCounter = _meter.CreateCounter<long>(
    "recommendation_requests_total",
    description: "Total recommendation requests",
    unit: "requests");

// Usage in HybridScorer
_recommendationRequestsCounter.Add(1, new KeyValuePair<string, object?>("profile_state", profileState));
```

**Tracing Spans to Add**:
```csharp
using var activity = _activitySource.StartActivity("Recommendations.HybridScoring");
activity?.SetTag("user_id_hash", HashUserId(userId));
activity?.SetTag("profile_state", profileState);
activity?.SetTag("candidates_count", candidates.Count);
```

---

## 🚀 IMPLEMENTATION ORDER (Phase 1 Only)

### Step 1: Domain Layer (Foundation)
- [ ] UserTasteProfile entity with invariants
- [ ] UserTasteEvent entity
- [ ] TasteDelta value object
- [ ] Unit tests for domain logic

### Step 2: EF Core Integration
- [ ] Add DbSets to WhatShouldIDoDbContext
- [ ] Add entity configurations (OnModelCreating)
- [ ] Create and apply migration
- [ ] Verify migration with `dotnet ef migrations add TasteProfile`

### Step 3: Application Layer
- [ ] Configuration options (TasteQuizOptions, RecommendationScoringOptions)
- [ ] DTOs (quiz, profile, reasons)
- [ ] Interfaces (repositories, services, scorers)
- [ ] CQRS commands/queries/handlers

### Step 4: Infrastructure - Repositories
- [ ] TasteProfileRepository (EF Core)
- [ ] TasteEventRepository (EF Core)
- [ ] TasteDraftStore (Redis-backed)

### Step 5: Infrastructure - Scoring Services
- [ ] PlaceCategoryMapper (place type -> interest dimension)
- [ ] ImplicitScorer (extract existing logic from SmartSuggestionService)
- [ ] ExplicitScorer (TasteProfile-based scoring)
- [ ] HybridScorer (combines both + generates reasons)
- [ ] ExplainabilityService (score components -> localized reasons)

### Step 6: Infrastructure - Business Services
- [ ] TasteQuizService (quiz orchestration, draft management)
- [ ] TasteProfileService (profile CRUD, validation)

### Step 7: API Layer
- [ ] OnboardingController (GET quiz, POST submit, POST claim)
- [ ] FluentValidation validators
- [ ] Update Program.cs DI registrations

### Step 8: SmartSuggestionService Integration
- [ ] Inject IHybridScorer
- [ ] Replace CalculatePersonalizedScoreAsync usage
- [ ] Handle cold start (no profile → default)
- [ ] Preserve existing fallback behavior

### Step 9: VariabilityEngine Integration
- [ ] Inject ITasteProfileRepository
- [ ] Read NoveltyTolerance in ApplyDiscoveryBoostAsync
- [ ] Dynamic discovery ratio

### Step 10: Candidate Caching
- [ ] Implement cache key builder
- [ ] Wrap candidate fetching with caching
- [ ] Add cache metrics

### Step 11: Observability
- [ ] Add metrics to MetricsService
- [ ] Add tracing spans to scoring services
- [ ] Update Prometheus config (if needed)

### Step 12: Localization
- [ ] Add quiz keys to .resx files (en-US, tr-TR)
- [ ] Add reason message keys
- [ ] Test localization service

### Step 13: Tests
- [ ] Unit tests (domain, scoring, explainability)
- [ ] Integration tests (quiz endpoints, scoring)
- [ ] Test cold start behavior
- [ ] Test fallback on errors

### Step 14: Documentation
- [ ] Update COMPREHENSIVE_PROJECT_DOCUMENTATION.md
- [ ] Add API documentation
- [ ] Update README if needed

### Step 15: Build & Validate
- [ ] `dotnet build`
- [ ] `dotnet test`
- [ ] Manual smoke test

---

## ✅ INTEGRATION CHECKLIST

- [x] Identified exact scoring integration point (CalculatePersonalizedScoreAsync)
- [x] Identified configuration pattern (IOptions<T>)
- [x] Identified DI registration pattern
- [x] Identified DbContext extension pattern
- [x] Identified caching strategy (reuse ICacheService)
- [x] Identified observability pattern (MetricsService + ActivitySource)
- [x] Identified localization pattern (LocalizationService + .resx)
- [x] Confirmed no parallel pipeline needed (extend existing)
- [x] Confirmed backward compatibility approach (optional fields)
- [x] Confirmed fail-safe patterns (degraded results, not crashes)

**Ready to proceed with implementation!** 🚀
