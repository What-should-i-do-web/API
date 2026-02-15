# Taste Profile System - Integration Strategy

**Created**: 2026-02-11
**Status**: Implementation Ready
**Objective**: Add explicit taste profile system alongside existing implicit preference learning

---

## Executive Summary

Your codebase already has a **sophisticated personalization system** with:
- Implicit learning from visit history (PreferenceLearningService)
- Advanced variability engine for anti-repetition
- Context-aware filtering (weather, time, location)
- Multi-stage scoring pipeline (SmartSuggestionService)
- AI embeddings for semantic search (1536-dim vectors)

**New Requirement**: Add **explicit taste profile** system with:
- Onboarding quiz (5-6 questions, server-driven)
- Bounded weight-based preferences (0-1 scale)
- Deterministic scoring with explainability
- Feedback-driven profile evolution (incremental deltas)
- Anonymous quiz with claim flow (lazy login UX)

**Strategy**: **COMPLEMENT, NOT REPLACE** - Merge both signals for hybrid personalization.

---

## Architecture Decision: Hybrid Personalization

### Two Parallel Systems (Merged at Scoring)

```
┌─────────────────────────────────────────────────────────────┐
│                   Recommendation Pipeline                    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
        ┌─────────────────────────────────────┐
        │   1. Fetch Candidates (Existing)    │
        │   - HybridPlacesOrchestrator        │
        │   - GooglePlaces + OpenTripMap      │
        │   - CostGuard + Caching             │
        └──────────────┬──────────────────────┘
                       │
                       ▼
        ┌─────────────────────────────────────┐
        │   2. Hard Filters (Existing)        │
        │   - UserExclusion (permanent/temp)  │
        │   - UserSuggestionHistory (MRU 20)  │
        │   - Recently visited (7/30 days)    │
        │   - Distance/open now               │
        └──────────────┬──────────────────────┘
                       │
                       ▼
        ┌─────────────────────────────────────┐
        │   3. Context Filtering (Existing)   │
        │   - ContextEngine                   │
        │   - Weather appropriateness         │
        │   - Time of day suitability         │
        └──────────────┬──────────────────────┘
                       │
                       ▼
        ┌─────────────────────────────────────┐
        │   4. HYBRID SCORING (NEW!)          │
        │                                     │
        │   A) Implicit Score (Existing):    │
        │      - PreferenceLearningService    │
        │      - Category frequency           │
        │      - Rating patterns              │
        │      - Temporal preferences         │
        │      - AI embedding similarity      │
        │                                     │
        │   B) Explicit Score (NEW):         │
        │      - TasteProfile weights         │
        │      - Interest match               │
        │      - Preference match             │
        │                                     │
        │   C) Novelty (Enhanced):           │
        │      - VariabilityEngine (existing) │
        │      - TasteProfile.NoveltyTolerance│
        │                                     │
        │   D) Context Score (Existing):     │
        │      - ContextEngine                │
        │                                     │
        │   E) Quality Score:                │
        │      - Rating + review count        │
        │      - Distance penalty             │
        │                                     │
        │   FINAL = w1·Implicit + w2·Explicit │
        │         + w3·Novelty + w4·Context   │
        │         + w5·Quality                │
        └──────────────┬──────────────────────┘
                       │
                       ▼
        ┌─────────────────────────────────────┐
        │   5. Explainability (NEW!)          │
        │   - Map score components to reasons │
        │   - Localize reason messages        │
        │   - Include in response             │
        └──────────────┬──────────────────────┘
                       │
                       ▼
        ┌─────────────────────────────────────┐
        │   6. Final Ranking & Diversity      │
        │   - Sort by score                   │
        │   - Apply max-per-category limits   │
        │   - Sponsorship priority (existing) │
        └──────────────┬──────────────────────┘
                       │
                       ▼
                   Response with
                   reasons[] per item
```

---

## Integration Points (Do NOT Duplicate)

### 1. SmartSuggestionService Extension

**File**: `src/WhatShouldIDo.Infrastructure/Services/SmartSuggestionService.cs`

**Current Method** (line 146-216):
```csharp
private async Task<List<Place>> ApplyPersonalizationAsync(
    int userId, List<Place> places, string prompt)
```

**New Approach**:
- Extract current scoring logic into `IImplicitScorer` interface
- Create new `IExplicitScorer` (taste profile based)
- Create `IHybridScorer` that merges both
- Add `IExplainabilityService` to generate reasons[]
- Keep existing behavior when TasteProfile missing (cold start)

**Changes**:
```csharp
// NEW INTERFACE
public interface IHybridScorer
{
    Task<List<ScoredPlace>> ScoreAndExplainAsync(
        int userId,
        List<Place> candidates,
        ScoringContext context,
        CancellationToken ct);
}

public class ScoredPlace
{
    public Place Place { get; set; }
    public double Score { get; set; }
    public List<RecommendationReason> Reasons { get; set; }
    public ScoreBreakdown Debug { get; set; } // behind feature flag
}

// MODIFIED METHOD
private async Task<List<Place>> ApplyPersonalizationAsync(
    int userId, List<Place> places, string prompt)
{
    var context = new ScoringContext { Prompt = prompt, ... };

    // NEW: Use hybrid scorer
    var scored = await _hybridScorer.ScoreAndExplainAsync(
        userId, places, context, CancellationToken.None);

    return scored.OrderByDescending(s => s.Score)
                 .Select(s => s.Place)
                 .ToList();
}
```

**Backward Compatibility**:
- Existing methods stay unchanged
- Scoring enhanced but returns same structure
- Explainability opt-in via request parameter

---

### 2. VariabilityEngine Extension

**File**: `src/WhatShouldIDo.Infrastructure/Services/VariabilityEngine.cs`

**Enhancement**: Read `TasteProfile.NoveltyTolerance` (0-1) to adjust discovery ratio:
- Existing: Fixed 30% discovery / 70% familiar
- New: Dynamic based on user preference
  - NoveltyTolerance = 0.0 → 10% discovery (safe)
  - NoveltyTolerance = 0.5 → 30% discovery (balanced, default)
  - NoveltyTolerance = 1.0 → 60% discovery (exploratory)

**Change**:
```csharp
public async Task<List<Place>> ApplyDiscoveryBoostAsync(
    int userId, List<Place> places)
{
    // NEW: Read taste profile
    var tasteProfile = await _tasteProfileRepo.GetByUserIdAsync(userId);

    var discoveryRatio = tasteProfile != null
        ? 0.1 + (tasteProfile.NoveltyTolerance * 0.5) // 10%-60%
        : 0.3; // Default 30%

    // Existing logic with dynamic ratio...
}
```

---

### 3. Response DTOs Enhancement

**Files**:
- `src/WhatShouldIDo.Application/DTOs/Response/SuggestionDto.cs`
- `src/WhatShouldIDo.Application/DTOs/Response/PoiDto.cs`

**Add Optional Fields** (backward compatible):
```csharp
public class SuggestionDto // or PoiDto
{
    // Existing fields...

    // NEW (optional, only when explainability enabled)
    public double? PersonalizationScore { get; set; }
    public List<RecommendationReasonDto>? Reasons { get; set; }
}

public class RecommendationReasonDto
{
    public string ReasonCode { get; set; } // Stable key
    public string Message { get; set; }    // Localized
    public double? Weight { get; set; }     // Optional debug info
}
```

**Reason Codes** (stable across languages):
- `MATCHES_INTEREST_FOOD` / `MATCHES_INTEREST_CULTURE` / etc.
- `HIGHLY_RATED` / `POPULAR_PLACE` / `HIDDEN_GEM`
- `TRY_SOMETHING_NEW` / `FAMILIAR_CHOICE`
- `CLOSE_TO_YOU` / `WORTH_THE_DISTANCE`
- `GREAT_FOR_NOW` (time/weather context)
- `PREFERS_CALM` / `PREFERS_LIVELY` / etc.

---

### 4. New Domain Entities (No Conflicts)

All new entities are additive:

#### UserTasteProfile (One-to-One with User)
```csharp
public class UserTasteProfile : EntityBase
{
    public int UserId { get; set; }
    public User User { get; set; }

    // Interest Weights (0-1)
    public double CultureWeight { get; set; } = 0.5;
    public double FoodWeight { get; set; } = 0.5;
    public double NatureWeight { get; set; } = 0.5;
    public double NightlifeWeight { get; set; } = 0.5;
    public double ShoppingWeight { get; set; } = 0.5;
    public double ArtWeight { get; set; } = 0.5;
    public double WellnessWeight { get; set; } = 0.5;
    public double SportsWeight { get; set; } = 0.5;

    // Preference Weights (0-1)
    public double TasteQualityWeight { get; set; } = 0.5;
    public double AtmosphereWeight { get; set; } = 0.5;
    public double DesignWeight { get; set; } = 0.5;
    public double CalmnessWeight { get; set; } = 0.5;
    public double SpaciousnessWeight { get; set; } = 0.5;

    // Discovery Style (0-1)
    public double NoveltyTolerance { get; set; } = 0.5; // 0=safe, 1=exploratory

    // Metadata
    public string QuizVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } // Concurrency

    // Methods
    public void ApplyDelta(TasteDelta delta, DateTime utcNow);
    public void EnsureInvariantsOrThrow();
    public Dictionary<string, double> GetInterestWeights();
    public Dictionary<string, double> GetPreferenceWeights();
}
```

#### UserTasteEvent (Audit Trail)
```csharp
public class UserTasteEvent : EntityBase
{
    public int UserId { get; set; }
    public string EventType { get; set; } // QuizCompleted, FeedbackLike, etc.
    public string Payload { get; set; }   // JSON
    public DateTime OccurredAtUtc { get; set; }
    public string CorrelationId { get; set; }
}
```

#### TasteQuizFeedback (Replaces generic RecommendationFeedback)
**Note**: Existing `UserAction` entity already tracks actions. We'll reuse it and extend:

```csharp
// REUSE EXISTING UserAction entity
// Just add new ActionType values:
// - "taste_like"
// - "taste_dislike"
// - "taste_skip"
```

**No new entity needed** - existing UserAction is perfect!

---

### 5. Quiz Configuration (Server-Driven)

**File**: `appsettings.json` (or `appsettings.Development.json`)

```json
{
  "Feature": {
    "TasteQuiz": {
      "Version": "v1",
      "DraftTtlHours": 24,
      "Steps": [
        {
          "Id": "interests",
          "Type": "multi-select",
          "TitleKey": "quiz.step1.title",
          "DescriptionKey": "quiz.step1.description",
          "Options": [
            {
              "Id": "culture",
              "LabelKey": "quiz.interests.culture",
              "Deltas": {
                "CultureWeight": 0.3
              }
            },
            {
              "Id": "food",
              "LabelKey": "quiz.interests.food",
              "Deltas": {
                "FoodWeight": 0.3
              }
            }
            // ... more interests
          ]
        },
        {
          "Id": "place_preferences",
          "Type": "rating",
          "TitleKey": "quiz.step2.title",
          "Options": [
            {
              "Id": "taste_quality",
              "LabelKey": "quiz.pref.taste",
              "Deltas": {
                "TasteQualityWeight": 0.2
              }
            }
            // ... more preferences
          ]
        },
        {
          "Id": "discovery_style",
          "Type": "single-select",
          "TitleKey": "quiz.step3.title",
          "Options": [
            {
              "Id": "safe",
              "LabelKey": "quiz.style.safe",
              "Deltas": {
                "NoveltyTolerance": -0.2
              }
            },
            {
              "Id": "balanced",
              "LabelKey": "quiz.style.balanced",
              "Deltas": {}
            },
            {
              "Id": "exploratory",
              "LabelKey": "quiz.style.exploratory",
              "Deltas": {
                "NoveltyTolerance": 0.3
              }
            }
          ]
        }
        // ... more steps
      ]
    },
    "Recommendations": {
      "Weights": {
        "Implicit": 0.25,
        "Explicit": 0.30,
        "Novelty": 0.20,
        "Context": 0.15,
        "Quality": 0.10
      },
      "CandidateCacheTtlMinutes": 15,
      "MaxCandidates": 100,
      "MaxResults": 20,
      "ReviewCountSmoothingFactor": 50
    },
    "Debug": {
      "EnableRecommendationDebugFields": false
    }
  }
}
```

---

## New Endpoints (No Breaking Changes)

### OnboardingController

```
GET  /api/onboarding/taste-quiz
     → Returns quiz definition (localized)
     → Anonymous allowed
     → [SkipQuota]

POST /api/onboarding/taste-quiz/submit
     → Body: QuizAnswers + optional UserId
     → If authenticated: persists profile immediately
     → If anonymous: returns claimToken + draft profile
     → [SkipQuota]

POST /api/onboarding/taste-quiz/claim
     → Body: claimToken
     → [Authorize] required
     → Retrieves draft from Redis and persists
     → [SkipQuota]
```

### TasteProfileController

```
GET  /api/taste-profile/me
     → Returns user's taste profile
     → [Authorize]
     → [SkipQuota]

PATCH /api/taste-profile/me
      → Allows manual weight adjustments
      → [Authorize]
      → [SkipQuota]

POST /api/taste-profile/feedback
     → Body: placeId, feedbackType (like/dislike/skip)
     → Incremental profile update
     → [Authorize]
     → [SkipQuota]
```

### Existing Endpoints (Enhanced)

```
POST /api/suggestions (ENHANCED, NOT BREAKING)
     → Request: add optional "includeReasons: true"
     → Response: add optional reasons[] per suggestion
     → Behavior: uses hybrid scoring when TasteProfile exists
     → Backward compatible: existing clients see no change
```

---

## Implementation Order (18 Steps)

### Phase 1: Foundation (Steps 1-4)
1. ✅ **Analyze** existing system (DONE)
2. **Domain**: Add entities + invariants + unit tests
3. **EF Core**: Add mappings + migration
4. **Application**: Add DTOs + interfaces + CQRS structure

### Phase 2: Infrastructure (Steps 5-7)
5. **Repos**: Implement TasteProfileRepository + TasteDraftStore (Redis)
6. **Quiz**: Implement TasteQuizService + configuration parsing
7. **Scoring**: Implement HybridScorer + ExplainabilityService

### Phase 3: API Layer (Steps 8-11)
8. **Onboarding**: Add quiz endpoints + validators + tests
9. **Profile**: Add taste profile endpoints + tests
10. **Feedback**: Add feedback endpoint + evolution logic + tests
11. **Enhancement**: Extend SuggestionsController for reasons[]

### Phase 4: Polish (Steps 12-15)
12. **Localization**: Add .resx keys for quiz + reasons
13. **Observability**: Add metrics + tracing spans
14. **Tests**: Write unit + integration + resilience tests
15. **Performance**: Add caching + optimize queries

### Phase 5: Finalization (Steps 16-18)
16. **Integration**: Wire everything into SmartSuggestionService
17. **Documentation**: Update all docs
18. **Validation**: Full build + test + manual testing

---

## Risk Mitigation

### Risk 1: Breaking Existing Behavior
**Mitigation**:
- All changes are additive (new entities, new endpoints)
- Existing SmartSuggestionService gets enhanced, not replaced
- TasteProfile optional - system works without it (cold start)
- Response DTOs use optional fields for reasons[]

### Risk 2: Performance Regression
**Mitigation**:
- Profile fetch: single indexed query by UserId
- Scoring: in-memory calculations (no DB calls in loop)
- Caching: candidate sets cached (existing pattern)
- Measured: add metrics for scoring duration

### Risk 3: Inconsistent Personalization
**Mitigation**:
- Hybrid scoring merges both signals (implicit + explicit)
- Weights configurable - can tune balance
- Cold start: defaults to implicit learning
- Evolution: bounded deltas prevent wild swings

### Risk 4: Complexity Creep
**Mitigation**:
- Clear interfaces (IHybridScorer, IExplainabilityService)
- Single responsibility services
- Comprehensive tests
- Feature flags for debug fields

---

## Success Metrics

### User Experience
- [ ] Quiz completion rate >70%
- [ ] Draft claim rate >40% (lazy login UX)
- [ ] Feedback submission rate >10%
- [ ] User satisfaction with recommendations (survey)

### Technical Performance
- [ ] p95 recommendation latency <300ms (cached)
- [ ] p95 recommendation latency <1000ms (uncached)
- [ ] Quiz submit <100ms
- [ ] Feedback update <50ms
- [ ] Zero unhandled exceptions in production

### Personalization Quality
- [ ] CTR on recommendations increase by >15%
- [ ] Visit confirmation rate increase by >20%
- [ ] Category diversity improvement (Shannon entropy)
- [ ] Repeat visit reduction by >30%

---

## Configuration Example (Full)

See `appsettings.TasteProfile.json` (to be created) for complete quiz definition with:
- 5 steps (interests, place preferences, discovery style, avoided types, valued aspects)
- En + Tr localizations
- Delta mappings per option
- Validation rules

---

## Next Steps

1. **Get User Approval** on this integration strategy
2. **Start Phase 1**: Domain entities + tests
3. **Incremental PRs**: Each phase = one PR
4. **Continuous Testing**: Run tests after each phase
5. **Documentation**: Update docs with each PR

---

**Questions? Concerns? Let's discuss before implementation!**
