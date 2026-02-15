# Repository Inventory Notes - Intent-First Suggestions Implementation
**Date:** January 16, 2026
**Purpose:** Pre-implementation reconnaissance for intent-first suggestion orchestration

---

## 1. EXISTING SUGGESTION SERVICES (Application Layer)

### Core Interfaces
| Interface | Location | Key Methods | Status |
|-----------|----------|-------------|--------|
| `ISuggestionService` | Application/Interfaces/ | GetNearbySuggestionsAsync, GetRandomSuggestionAsync, GetPromptSuggestionsAsync | ✅ Implemented |
| `ISmartSuggestionService` | Application/Interfaces/ | GetPersonalizedSuggestionsAsync, ApplyPersonalizationAsync, GenerateSurpriseRouteAsync | ✅ Implemented |
| `IVariabilityEngine` | Application/Interfaces/ | FilterForVarietyAsync, ApplyDiscoveryBoostAsync, CalculateNoveltyScoreAsync | ✅ Implemented |
| `IContextEngine` | Application/Interfaces/ | ApplyContextualFiltering, GetContextualInsights, GetContextualReasons | ✅ Implemented |
| `IPreferenceLearningService` | Application/Interfaces/ | UpdateUserPreferencesAsync, GetLearnedPreferencesAsync, TrackUserActionAsync | ✅ Implemented |

### Implementations (Infrastructure Layer)
| Service | Location | Dependencies | Notes |
|---------|----------|--------------|-------|
| `SuggestionService` | Infrastructure/Services/ | IPlacesProvider, IPromptInterpreter, IGeocodingService | Basic non-personalized suggestions |
| `SmartSuggestionService` | Infrastructure/Services/ | IPlacesProvider, IPromptInterpreter, IVariabilityEngine, IContextEngine, IPreferenceLearningService, IRouteOptimizationService, IAIService | 8-step personalization pipeline |
| `VariabilityEngine` | Infrastructure/Services/ | WhatShouldIDoDbContext, IVisitTrackingService | Novelty scoring, category diversity |
| `ContextEngine` | Infrastructure/Services/ | IWeatherService | Weather/time/season/location awareness |
| `PreferenceLearningService` | Infrastructure/Services/ | WhatShouldIDoDbContext, IAIService | ML-based user embeddings |

---

## 2. EXISTING MediatR INFRASTRUCTURE

### Handlers Implemented
| Handler | Command/Query | Status | Location |
|---------|---------------|--------|----------|
| `SearchPlacesQueryHandler` | `SearchPlacesQuery` | ✅ Implemented | Application/UseCases/Handlers/ |
| `CreateRouteCommandHandler` | `CreateRouteCommand` | ✅ Implemented | Application/UseCases/Handlers/ |
| `CreateAIDrivenRouteCommandHandler` | `CreateAIDrivenRouteCommand` | ✅ Implemented | Application/UseCases/Handlers/ |
| `GenerateDailyItineraryCommandHandler` | `GenerateDailyItineraryCommand` | ✅ **COMPLETE** (contrary to docs) | Application/UseCases/Handlers/ |
| `GetPlaceSummaryQueryHandler` | `GetPlaceSummaryQuery` | ✅ Implemented | Application/UseCases/Handlers/ |

### Stubs (Empty Implementation)
| Command/Query | File | Status |
|---------------|------|--------|
| `GetPromptSuggestionsCommand` | UseCases/Commands/ | ⚠️ Stub only |
| `GetNearbySuggestionsQuery` | UseCases/Queries/ | ⚠️ Stub only |
| `GetRandomSuggestionQuery` | UseCases/Queries/ | ⚠️ Stub only (contains wrong code) |

**Note:** MediatR is registered in Program.cs line 423-428, scanning Application assembly.

---

## 3. EXISTING API ENDPOINTS

### DiscoverController (`/api/discover`)
| Endpoint | Method | Purpose | Uses Service |
|----------|--------|---------|--------------|
| `/api/discover` | GET | Nearby suggestions | ISuggestionService, ISmartSuggestionService |
| `/api/discover/random` | GET | Random suggestion | ISuggestionService, ISmartSuggestionService |
| `/api/discover/prompt` | POST | Prompt-based search | ISuggestionService, ISmartSuggestionService |

**Key Behavior:**
- Extracts user ID from claims ("sub" or NameIdentifier)
- Falls back to basic ISuggestionService if smart service fails
- Returns `{ personalized: bool, suggestions: SuggestionDto[], userId?: Guid }`

### RoutesController (`/api/routes`)
| Endpoint | Method | Purpose | Uses MediatR |
|----------|--------|---------|--------------|
| `/api/routes` | POST | Create route | ✅ CreateRouteCommand |
| `/api/routes/ai/generate` | POST | AI-driven route | ✅ CreateAIDrivenRouteCommand |
| `/api/routes/surprise` | POST | Surprise Me route | ❌ Direct service call |
| `/api/routes/{id}` | GET | Get route | ❌ Direct service call |
| `/api/routes` | GET | List routes | ❌ Direct service call |
| `/api/routes/{id}` | PUT | Update route | ❌ Direct service call |
| `/api/routes/{id}` | DELETE | Delete route | ❌ Direct service call |

---

## 4. DOMAIN ENTITIES (Route History)

### UserRouteHistory (MRU Pattern, N=3)
**Location:** Domain/Entities/UserRouteHistory.cs
**Properties:**
- `Id`, `UserId`, `RouteId` (nullable), `RouteName`
- `RouteDataJson` (JSON-serialized snapshot)
- `CreatedAt`, `SequenceNumber` (for MRU ordering)
- `Source` (e.g., "surprise_me", "manual", "ai_itinerary")
- `PlaceCount`

**Purpose:** Tracks last 3 routes per user with MRU pattern.

### UserSuggestionHistory (MRU Pattern, N=20)
**Location:** Domain/Entities/UserSuggestionHistory.cs
**Properties:**
- `Id`, `UserId`, `PlaceId`, `PlaceName`, `Category`
- `SuggestedAt`, `SequenceNumber`
- `Source`, `SessionId` (groups places in one request)

**Purpose:** Exclusion window logic for "Surprise Me" feature.

### Route
**Location:** Domain/Entities/Route.cs
**Properties:**
- `Id`, `Name`, `Description`, `UserId`, `CreatedAt`, `UpdatedAt`
- `IsPublic`, `Tags`, `TotalDistance`, `EstimatedDuration`
- `Points` (IReadOnlyCollection<RoutePoint>)

**Domain Methods:**
- `AddPoint()`, `UpdateName()`, `UpdateDescription()`
- `UpdateDistanceAndDuration()`, `SetTags()`, `SetPublic()`

---

## 5. OBSERVABILITY INFRASTRUCTURE

### OpenTelemetry Setup (Program.cs lines 166-253)
- **Tracing:** OTLP exporter to Tempo/Jaeger, 5% sampling
- **Metrics:** Prometheus exporter at `/metrics`
- **Logging:** Serilog to Console, Seq, File

### Middleware Pipeline (Program.cs lines 567-570)
1. `GlobalExceptionMiddleware` - Exception handling
2. `CorrelationIdMiddleware` - W3C trace context, correlation ID
3. `MetricsMiddleware` - Request metrics collection
4. `AdvancedRateLimitMiddleware` - Rate limiting
5. `EntitlementAndQuotaMiddleware` - After auth/authz

### IMetricsService
**Location:** Infrastructure/Observability/MetricsService.cs
**30+ Metrics Including:**
- `requests_total` (counter with labels: endpoint, method, status_code, authenticated, premium)
- `request_duration_seconds` (histogram, SLO-aligned buckets)
- `quota_consumed_total`, `quota_blocked_total`, `quota_users_with_zero`
- `redis_quota_script_latency_seconds`, `redis_errors_total`
- `db_latency_seconds`, `db_subscription_reads_total`
- `place_searches_total`, `active_users`
- `ai_provider_selected_total`, `ai_call_success_total`, `ai_call_failures_total`, `ai_call_latency_seconds`
- `route_generation_duration_seconds`

### Health Checks
- **Endpoints:** `/health/ready`, `/health/live`, `/health/startup`, `/health`
- **Implementations:** `RedisHealthCheck`, `PostgresHealthCheck`
- **Tags:** "ready" (Redis + Postgres), "live" (self-check)

### Alert Rules (deploy/prometheus/alerts/slo-alerts.yml)
**20+ alerts covering:**
- Availability SLO (error rates)
- Latency SLO (p95 < 300ms, p99 < 800ms)
- Quota system health
- Database health
- Webhook health
- Rate limiting
- Infrastructure (K8s)

### Correlation ID Handling
- **CorrelationIdMiddleware:** Generates/extracts correlation ID, sets W3C trace context
- **ObservabilityContext:** Scoped service with CorrelationId, TraceId, UserIdHash, IsPremium

---

## 6. BACKGROUND JOBS

### Existing Pattern (BackgroundService)
**Example:** `PreferenceUpdateJob.cs`
**Pattern:**
```csharp
public class MyJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MyJob> _logger;
    private readonly MyJobOptions _options;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for initial delay
        // Loop with interval
        // Create scope per iteration
        // Process batch
        // Handle errors
    }
}
```

**Options Pattern:** `MyJobOptions` with `Enabled`, `IntervalMinutes/Hours`, `InitialDelayMinutes/Hours`, `BatchSize`

**Registration:** Program.cs conditionally adds `AddHostedService<MyJob>()` if `options.Enabled == true`

### Existing Jobs
1. **PreferenceUpdateJob** - Updates user preference embeddings hourly
2. **UserActionCleanupJob** - Cleans old user actions daily

---

## 7. IDENTIFIED GAPS (From Documentation)

### ✅ RESOLVED: AI Daily Itinerary Generation
**Status:** `GenerateDailyItineraryCommandHandler` IS implemented (contrary to documentation)
**Location:** Application/UseCases/Handlers/GenerateDailyItineraryCommandHandler.cs
**Dependencies:** IAIService, IPlacesProvider, IRouteService, IPreferenceLearningService
**Evidence:** Full implementation found, merges preferences, saves route, tracks actions

### ❌ UNRESOLVED: Daily Quota Reset Job
**Status:** Not implemented
**Required Implementation:**
- Create `QuotaResetJob : BackgroundService`
- Create `QuotaResetJobOptions` with schedule config
- Use existing `IQuotaStore` to reset quotas
- Use Lua scripts for atomic operations in Redis
- Register conditionally in Program.cs

---

## 8. CLASSES WE WILL TOUCH

### NEW FILES TO CREATE

#### Application Layer
- `Application/UseCases/Commands/CreateSuggestionsCommand.cs` - Intent-first orchestration
- `Application/UseCases/Handlers/CreateSuggestionsCommandHandler.cs` - Handler implementation
- `Application/DTOs/Request/CreateSuggestionsRequest.cs` - Request DTO
- `Application/DTOs/Response/SuggestionsResponse.cs` - Response DTO with reasons
- `Application/ValueObjects/SuggestionIntent.cs` - Intent enum/value object
- `Application/Services/ISuggestionPolicy.cs` - Policy interface
- `Application/UseCases/Commands/CreateRouteShareTokenCommand.cs` - Share token creation
- `Application/UseCases/Commands/RerollRouteCommand.cs` - Route re-roll
- `Application/UseCases/Commands/SaveRouteRevisionCommand.cs` - Route revision save
- `Application/UseCases/Queries/GetSharedRouteQuery.cs` - Shared route retrieval
- `Application/UseCases/Queries/GetRouteRevisionsQuery.cs` - Route revisions retrieval
- `Application/Interfaces/IRouteShareService.cs` - Route sharing abstraction

#### Infrastructure Layer
- `Infrastructure/Services/SuggestionPolicyService.cs` - Policy implementation
- `Infrastructure/Services/RouteShareService.cs` - Share token service
- `Infrastructure/BackgroundJobs/QuotaResetJob.cs` - Daily quota reset
- `Infrastructure/BackgroundJobs/QuotaResetJobOptions.cs` - Config options

#### Domain Layer
- `Domain/Entities/RouteShareToken.cs` - Share token entity
- `Domain/Entities/RouteRevision.cs` - Route revision entity

#### API Layer
- `API/Controllers/SuggestionsController.cs` - New unified endpoint
- `API/Validators/CreateSuggestionsRequestValidator.cs` - Request validation

#### Documentation
- `docs/INCIDENT_PLAYBOOK.md` - Observability runbook
- `docs/OBSERVABILITY_VERIFY.md` - Verification checklist

#### Alerts & Config
- `deploy/prometheus/alerts/observability-alerts.yml` - Additional alert rules

### FILES TO MODIFY

#### Application Layer
- `Application/DTOs/Response/SuggestionDto.cs` - Add `Reasons: string[]` optional field

#### Infrastructure Layer
- `Infrastructure/Observability/MetricsService.cs` - Add intent, provider, cache_layer labels to existing metrics

#### API Layer
- `API/Controllers/RoutesController.cs` - Add share, reroll, revisions endpoints
- `API/Middleware/MetricsMiddleware.cs` - Add intent labeling where applicable

#### Configuration
- `appsettings.json` - Add QuotaResetJobOptions section

#### Database
- EF Core migration for RouteShareToken and RouteRevision entities

#### Documentation
- `COMPREHENSIVE_PROJECT_DOCUMENTATION.md` - Update "Known Issues" section to mark AI itinerary as complete

---

## 9. IMPLEMENTATION STRATEGY

### Phase 1: Intent-First Orchestration
1. Create `SuggestionIntent` value object
2. Create `ISuggestionPolicy` and implementation
3. Create `CreateSuggestionsCommand` and handler
4. Add `SuggestionsController` with POST `/api/suggestions`
5. Extend `SuggestionDto` with `Reasons` field
6. Add intent-based metrics labels
7. Write unit tests for policy enforcement
8. Write integration tests for endpoint

### Phase 2: Route History UX
1. Create `RouteShareToken` and `RouteRevision` entities
2. Create migration
3. Create `IRouteShareService` and implementation
4. Create commands/queries for share, reroll, revisions
5. Add endpoints to `RoutesController`
6. Add metrics for share and reroll actions
7. Write unit tests for token uniqueness
8. Write integration tests for sharing flow

### Phase 3: Daily Quota Reset
1. Create `QuotaResetJob` and options
2. Add configuration to appsettings.json
3. Register conditionally in Program.cs
4. Add metrics for reset operations
5. Write unit tests against in-memory quota store
6. Provide manual verification instructions

### Phase 4: Observability Hardening
1. Enrich existing metrics with new labels
2. Add tracing spans for orchestration stages
3. Create additional Prometheus alert rules
4. Write `INCIDENT_PLAYBOOK.md`
5. Write `OBSERVABILITY_VERIFY.md`
6. Add integration test for metrics endpoint

### Phase 5: Documentation & Verification
1. Update COMPREHENSIVE_PROJECT_DOCUMENTATION.md
2. Create final verification checklist
3. Document migration steps
4. Note risks and mitigations

---

## 10. BACKWARD COMPATIBILITY GUARANTEES

### Existing Endpoints (MUST NOT BREAK)
- ✅ GET `/api/discover` - No changes
- ✅ GET `/api/discover/random` - No changes
- ✅ POST `/api/discover/prompt` - No changes
- ✅ POST `/api/routes` - No changes
- ✅ POST `/api/routes/ai/generate` - No changes
- ✅ POST `/api/routes/surprise` - No changes
- ✅ All other routes endpoints - No changes

### Response DTOs (MUST REMAIN VALID)
- ✅ `SuggestionDto` - Add optional `Reasons: string[]` field (default: empty array)
- ✅ `RouteDto` - No changes to existing fields
- ✅ All other DTOs - No changes

### New Endpoints (ADDITIVE ONLY)
- ✅ POST `/api/suggestions` - New unified intent-first endpoint
- ✅ POST `/api/routes/{id}/share` - New share token creation
- ✅ GET `/api/routes/shared/{token}` - New shared route retrieval
- ✅ POST `/api/routes/{id}/reroll` - New route re-roll
- ✅ GET `/api/routes/{id}/revisions` - New route revisions

---

## 11. TESTING STRATEGY

### Unit Tests
- Intent policy enforcement (FOOD_ONLY, ROUTE_PLANNING, TRY_SOMETHING_NEW)
- Token generation uniqueness
- Quota reset logic
- Novelty scoring for TRY_SOMETHING_NEW

### Integration Tests
- `/api/suggestions` endpoint with different intents
- `/api/routes/shared/{token}` read-only access
- `/api/routes/{id}/reroll` variation guarantee
- `/metrics` endpoint response validation
- `/health/ready` dependency checks

### Manual Verification
- Docker compose up
- Database migrations
- Curl scenarios for each intent
- Share token creation and access
- Quota reset trigger
- Grafana dashboard inspection
- Trace correlation ID lookup

---

**Inventory Complete. Ready for Implementation.**
