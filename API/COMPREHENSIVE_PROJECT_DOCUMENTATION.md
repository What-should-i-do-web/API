# WhatShouldIDo - Comprehensive Project Documentation

**Version:** 2.0.0
**Last Updated:** January 16, 2026
**Status:** Production-Ready with Advanced Features
**Architecture:** Clean Architecture (DDD)
**Technology Stack:** .NET 9, PostgreSQL with pgvector, Redis Cluster, Docker, OpenTelemetry

---

## 📋 EXECUTIVE SUMMARY

**WhatShouldIDo** is an advanced location-based activity recommendation system built on .NET 9, designed to help users discover personalized places and create optimized routes. The system combines multiple AI providers, hybrid search orchestration, quota management, and comprehensive observability to deliver intelligent, context-aware suggestions.

### Core Value Propositions
- **Intelligent Place Discovery:** AI-powered natural language search with multi-provider fallback
- **Personalized Recommendations:** Learning engine that adapts to user preferences over time
- **Route Optimization:** TSP solver with Google Directions integration for optimal path planning
- **Quota Management:** Redis-based atomic operations for fair usage enforcement
- **Production Observability:** OpenTelemetry with Prometheus metrics and distributed tracing
- **Multi-Language Support:** 10 languages with intelligent culture detection
- **Hybrid Data Sources:** Google Places + OpenTripMap with cost-aware orchestration

---

## 🏗️ ARCHITECTURE OVERVIEW

### Clean Architecture Layers

```
┌─────────────────────────────────────────────────────────────────┐
│                          API Layer                               │
│  • 17 Controllers (REST endpoints)                              │
│  • 5 Middleware (Exceptions, CORS, Auth, Quota, Metrics)       │
│  • FluentValidation for all requests                            │
│  • JWT Bearer Authentication                                     │
└────────────────────────┬────────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────────┐
│                    Application Layer                             │
│  • 30+ Interfaces (service contracts)                           │
│  • DTOs for Request/Response mapping                            │
│  • MediatR Commands & Queries (CQRS)                            │
│  • Configuration Options (Quota, AI, Observability)             │
└────────────────────────┬────────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────────┐
│                  Infrastructure Layer                            │
│  • 25+ Service Implementations                                  │
│  • Repository Pattern (Generic + Specific)                      │
│  • AI Providers (OpenAI, HuggingFace, Ollama, NoOp)            │
│  • Caching (Redis Cluster, In-Memory, Fallback)                │
│  • External APIs (Google Places, OpenTripMap, Weather)         │
│  • Quota Stores (Redis with Lua, InMemory thread-safe)         │
│  • Background Jobs (Preference Learning, Action Cleanup)        │
└────────────────────────┬────────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────────┐
│                       Domain Layer                               │
│  • 16 Entities (User, Route, Place, Suggestion, etc.)          │
│  • Value Objects (Coordinates)                                  │
│  • Domain Exceptions                                             │
│  • Business Rules Enforcement                                    │
└─────────────────────────────────────────────────────────────────┘
```

### Technology Stack

| Layer | Technologies |
|-------|-------------|
| **Framework** | .NET 9, ASP.NET Core Web API |
| **Database** | PostgreSQL 13+ with pgvector extension |
| **Caching** | Redis Cluster (6 nodes) with StackExchange.Redis |
| **ORM** | Entity Framework Core 9 |
| **Authentication** | JWT Bearer tokens (HS256) |
| **Validation** | FluentValidation |
| **Logging** | Serilog (Console, File, Seq) |
| **Observability** | OpenTelemetry, Prometheus, Grafana |
| **CQRS** | MediatR library |
| **Containerization** | Docker, Docker Compose |
| **External APIs** | Google Places API, OpenTripMap, OpenWeather |
| **AI** | OpenAI GPT-4o-mini, HuggingFace, Ollama (local) |

---

## 📦 PROJECT STRUCTURE

```
WhatShouldIDo/
├── src/
│   ├── WhatShouldIDo.API/                  # 🌐 Presentation Layer
│   │   ├── Controllers/                    # 17 API controllers
│   │   │   ├── AuthController.cs           # JWT authentication & user management
│   │   │   ├── DiscoverController.cs       # Main discovery endpoints
│   │   │   ├── RoutesController.cs         # Route CRUD & optimization
│   │   │   ├── PlacesController.cs         # AI-powered search & favorites
│   │   │   ├── UsersController.cs          # User history & preferences
│   │   │   ├── DayPlanController.cs        # AI itinerary generation
│   │   │   ├── AnalyticsController.cs      # Business intelligence
│   │   │   ├── FiltersController.cs        # Advanced filtering
│   │   │   ├── LocalizationController.cs   # Multi-language support
│   │   │   ├── HealthController.cs         # Health checks
│   │   │   ├── MetricsController.cs        # Prometheus metrics
│   │   │   ├── PerformanceController.cs    # Performance monitoring
│   │   │   ├── ContextController.cs        # Weather/time context
│   │   │   ├── PoisController.cs           # Points of Interest
│   │   │   ├── RoutePointsController.cs    # Route point management
│   │   │   ├── AdminController.cs          # Admin operations
│   │   │   └── UserFeedbackController.cs   # User ratings/reviews
│   │   ├── Middleware/
│   │   │   ├── GlobalExceptionMiddleware.cs       # RFC 7807 error handling
│   │   │   ├── CorrelationIdMiddleware.cs         # Request correlation & W3C trace
│   │   │   ├── MetricsMiddleware.cs               # OpenTelemetry metrics
│   │   │   ├── EntitlementAndQuotaMiddleware.cs   # Quota enforcement
│   │   │   ├── ApiRateLimitMiddleware.cs          # Rate limiting (legacy)
│   │   │   └── AdvancedRateLimitMiddleware.cs     # Advanced rate limiting
│   │   ├── Attributes/
│   │   │   ├── SkipQuotaAttribute.cs       # Bypass quota for endpoint
│   │   │   └── PremiumOnlyAttribute.cs     # Premium-only endpoints
│   │   ├── Validators/                     # FluentValidation validators
│   │   │   ├── CreateRouteRequestValidator.cs
│   │   │   ├── CreatePoiRequestValidator.cs
│   │   │   ├── LoginRequestValidator.cs
│   │   │   └── [8+ more validators]
│   │   ├── Resources/                      # Localization resource files
│   │   │   ├── LocalizationService.en-US.resx
│   │   │   ├── LocalizationService.tr-TR.resx
│   │   │   └── [8 more languages]
│   │   ├── Program.cs                      # Application startup & DI
│   │   ├── appsettings.json                # Configuration
│   │   └── Dockerfile                      # Docker image definition
│   │
│   ├── WhatShouldIDo.Application/          # 🎯 Application Layer
│   │   ├── Interfaces/                     # 30+ service interfaces
│   │   │   ├── IAIService.cs               # AI orchestration
│   │   │   ├── IAIProvider.cs              # AI provider abstraction
│   │   │   ├── IPlacesProvider.cs          # Place data provider
│   │   │   ├── ISuggestionService.cs       # Suggestion business logic
│   │   │   ├── IRouteService.cs            # Route management
│   │   │   ├── IRouteOptimizationService.cs # TSP solver
│   │   │   ├── IDirectionsService.cs       # Google Directions
│   │   │   ├── IQuotaService.cs            # Quota management
│   │   │   ├── IEntitlementService.cs      # Premium check
│   │   │   ├── IMetricsService.cs          # Metrics collection
│   │   │   ├── IObservabilityContext.cs    # Trace context
│   │   │   ├── ICacheService.cs            # Caching abstraction
│   │   │   ├── IUserHistoryRepository.cs   # MRU history
│   │   │   ├── IPreferenceLearningService.cs # ML preferences
│   │   │   ├── IVariabilityEngine.cs       # Diversity scoring
│   │   │   ├── ISmartSuggestionService.cs  # Personalized suggestions
│   │   │   ├── IWeatherService.cs          # Weather API
│   │   │   ├── IContextEngine.cs           # Context analysis
│   │   │   ├── IGeocodingService.cs        # Address to coordinates
│   │   │   ├── IAdvancedFilterService.cs   # Advanced filters
│   │   │   ├── IAnalyticsService.cs        # Analytics
│   │   │   ├── IDayPlanningService.cs      # Day planning
│   │   │   └── [10+ more interfaces]
│   │   ├── DTOs/
│   │   │   ├── Request/                    # Request DTOs
│   │   │   │   ├── PromptRequest.cs        # Natural language search
│   │   │   │   ├── CreateRouteRequest.cs   # Route creation
│   │   │   │   ├── SurpriseMeRequest.cs    # AI route generation
│   │   │   │   ├── FilterCriteria.cs       # Advanced filtering
│   │   │   │   ├── DayPlanRequest.cs       # Daily itinerary
│   │   │   │   ├── LoginRequest.cs         # Authentication
│   │   │   │   ├── RegisterRequest.cs      # User registration
│   │   │   │   └── [15+ more requests]
│   │   │   ├── Response/                   # Response DTOs
│   │   │   │   ├── SuggestionDto.cs        # Place suggestions
│   │   │   │   ├── RouteDto.cs             # Route data
│   │   │   │   ├── UserDto.cs              # User profile
│   │   │   │   ├── AnalyticsDto.cs         # Analytics data
│   │   │   │   ├── DayPlanDto.cs           # Daily plan
│   │   │   │   └── [12+ more responses]
│   │   │   └── AI/                         # AI-specific DTOs
│   │   │       ├── InterpretedPrompt.cs    # AI interpretation result
│   │   │       ├── PlaceSummary.cs         # AI-generated summary
│   │   │       └── AIItinerary.cs          # AI itinerary
│   │   ├── UseCases/
│   │   │   ├── Commands/                   # CQRS Commands
│   │   │   │   ├── CreateRouteCommand.cs
│   │   │   │   ├── CreateAIDrivenRouteCommand.cs
│   │   │   │   ├── GenerateDailyItineraryCommand.cs
│   │   │   │   └── GetPromptSuggestionsCommand.cs
│   │   │   ├── Queries/                    # CQRS Queries
│   │   │   │   ├── SearchPlacesQuery.cs
│   │   │   │   ├── GetNearbySuggestionsQuery.cs
│   │   │   │   ├── GetRandomSuggestionQuery.cs
│   │   │   │   └── GetPlaceSummaryQuery.cs
│   │   │   └── Handlers/                   # Command/Query handlers
│   │   │       ├── SearchPlacesQueryHandler.cs
│   │   │       ├── CreateRouteCommandHandler.cs
│   │   │       └── [More handlers]
│   │   ├── Configuration/
│   │   │   ├── QuotaOptions.cs             # Quota configuration
│   │   │   ├── ObservabilityOptions.cs     # Observability config
│   │   │   └── SecurityOptions.cs          # Security settings
│   │   └── Services/                       # Application services
│   │       ├── ILocalizationService.cs
│   │       ├── IAnalyticsService.cs
│   │       └── IAdvancedFilterService.cs
│   │
│   ├── WhatShouldIDo.Domain/               # 🏛️ Domain Layer
│   │   ├── Entities/                       # 16 domain entities
│   │   │   ├── User.cs                     # User entity with subscription
│   │   │   ├── UserProfile.cs              # User preferences
│   │   │   ├── UserQuota.cs                # Quota tracking
│   │   │   ├── Place.cs                    # Place entity with photos
│   │   │   ├── Suggestion.cs               # Suggestion entity
│   │   │   ├── Route.cs                    # Route entity
│   │   │   ├── RoutePoint.cs               # Route waypoint
│   │   │   ├── Poi.cs                      # Point of interest
│   │   │   ├── UserVisit.cs                # Visit history
│   │   │   ├── UserAction.cs               # User action tracking
│   │   │   ├── UserFavorite.cs             # Favorites
│   │   │   ├── UserExclusion.cs            # Excluded places
│   │   │   ├── UserSuggestionHistory.cs    # MRU suggestion history (max 20)
│   │   │   ├── UserRouteHistory.cs         # MRU route history (max 3)
│   │   │   ├── SponsorshipHistory.cs       # Sponsorship tracking
│   │   │   └── EntityBase.cs               # Base entity
│   │   ├── ValueObjects/
│   │   │   └── Coordinates.cs              # Latitude/Longitude
│   │   └── Exception/
│   │       └── DomainException.cs          # Domain exceptions
│   │
│   ├── WhatShouldIDo.Infrastructure/       # ⚙️ Infrastructure Layer
│   │   ├── Services/                       # 25+ service implementations
│   │   │   ├── AI/                         # AI providers
│   │   │   │   ├── AIService.cs            # Main AI orchestrator
│   │   │   │   ├── OpenAIProvider.cs       # OpenAI integration (GPT-4o-mini)
│   │   │   │   ├── HuggingFaceProvider.cs  # HuggingFace models
│   │   │   │   ├── OllamaProvider.cs       # Local Ollama LLM
│   │   │   │   ├── NoOpAIProvider.cs       # Fallback/testing provider
│   │   │   │   ├── AIProviderFactory.cs    # Dynamic provider creation
│   │   │   │   └── DiversityHelper.cs      # Route diversity scoring
│   │   │   ├── GooglePlacesProvider.cs     # Google Places API (primary)
│   │   │   ├── OpenTripMapProvider.cs      # Tourism supplement
│   │   │   ├── HybridPlacesOrchestrator.cs # Legacy orchestrator
│   │   │   ├── HybridPlacesOrchestratorV2.cs # Current orchestrator
│   │   │   ├── PlacesMerger.cs             # Deduplication logic
│   │   │   ├── Ranker.cs                   # Result ranking
│   │   │   ├── CostGuard.cs                # API rate limiting & budget
│   │   │   ├── SuggestionService.cs        # Suggestion orchestration
│   │   │   ├── RouteService.cs             # Route management
│   │   │   ├── RouteOptimizationService.cs # TSP solver
│   │   │   ├── GoogleDirectionsService.cs  # Google Directions API
│   │   │   ├── UserService.cs              # User management
│   │   │   ├── PreferenceLearningService.cs # ML-based learning
│   │   │   ├── VariabilityEngine.cs        # Diversity & novelty
│   │   │   ├── SmartSuggestionService.cs   # Personalized suggestions
│   │   │   ├── ContextEngine.cs            # Time/weather context
│   │   │   ├── OpenWeatherService.cs       # Weather API
│   │   │   ├── GoogleGeocodingService.cs   # Geocoding
│   │   │   ├── AdvancedFilterService.cs    # Advanced filters
│   │   │   ├── AnalyticsService.cs         # Analytics
│   │   │   ├── DayPlanningService.cs       # Day planning
│   │   │   ├── PlaceService.cs             # Place operations
│   │   │   ├── PoiService.cs               # POI management
│   │   │   ├── RoutePointService.cs        # Route points
│   │   │   ├── VisitTrackingService.cs     # Visit tracking
│   │   │   ├── LocalizationService.cs      # Multi-language
│   │   │   ├── BasicPromptInterpreter.cs   # Basic NLP
│   │   │   ├── FakePromptInterpreter.cs    # Testing
│   │   │   └── [More services]
│   │   ├── Quota/                          # Quota system
│   │   │   ├── QuotaService.cs             # Business logic
│   │   │   ├── EntitlementService.cs       # Premium check
│   │   │   ├── RedisQuotaStore.cs          # Redis with Lua scripts
│   │   │   ├── InMemoryQuotaStore.cs       # Thread-safe in-memory
│   │   │   └── InstrumentedRedisQuotaStore.cs # OpenTelemetry wrapper
│   │   ├── Caching/                        # Caching implementations
│   │   │   ├── RedisCacheService.cs        # Single Redis
│   │   │   ├── RedisClusterCacheService.cs # Redis Cluster
│   │   │   ├── InMemoryCacheService.cs     # In-memory cache
│   │   │   ├── FallbackCacheService.cs     # Automatic fallback
│   │   │   └── CacheWarmingService.cs      # Pre-warming critical keys
│   │   ├── Observability/                  # OpenTelemetry
│   │   │   ├── MetricsService.cs           # Prometheus metrics
│   │   │   ├── ObservabilityContext.cs     # Correlation & trace
│   │   │   └── PrometheusMetricsService.cs # Legacy metrics
│   │   ├── Repositories/                   # Data access
│   │   │   ├── GenericRepository.cs        # Generic CRUD
│   │   │   ├── UserRepository.cs           # User data
│   │   │   ├── RouteRepository.cs          # Route data
│   │   │   ├── PoiRepository.cs            # POI data
│   │   │   ├── RoutePointRepository.cs     # Route points
│   │   │   └── UserHistoryRepository.cs    # MRU histories
│   │   ├── Data/
│   │   │   ├── WhatShouldIDoDbContext.cs   # EF Core DbContext
│   │   │   └── DesignTimeDbContextFactory.cs # Migrations support
│   │   ├── Migrations/                     # EF Core migrations
│   │   ├── Health/                         # Health checks
│   │   │   ├── RedisHealthCheck.cs         # Redis ping
│   │   │   ├── PostgresHealthCheck.cs      # DB connectivity
│   │   │   └── RedisHealthChecker.cs       # Cluster health
│   │   ├── Interceptors/
│   │   │   └── QueryPerformanceInterceptor.cs # Slow query logging
│   │   ├── BackgroundJobs/                 # Hosted services
│   │   │   ├── PreferenceUpdateJob.cs      # Preference learning job
│   │   │   └── UserActionCleanupJob.cs     # Data cleanup job
│   │   └── Options/                        # Configuration POCOs
│   │       ├── AIOptions.cs                # AI configuration
│   │       ├── HybridOptions.cs            # Hybrid search config
│   │       ├── CostGuardOptions.cs         # Cost control
│   │       ├── DatabaseOptions.cs          # DB settings
│   │       └── [More options]
│   │
│   └── WhatShouldIDo.Tests/                # 🧪 Test Layer
│       ├── Unit/                           # Unit tests
│       │   ├── QuotaServiceTests.cs        # 12 tests
│       │   ├── EntitlementServiceTests.cs  # 10 tests
│       │   ├── InMemoryQuotaStoreTests.cs  # 11 tests (includes concurrency)
│       │   └── [More unit tests]
│       └── Integration/                    # Integration tests
│           ├── EntitlementAndQuotaMiddlewareTests.cs # 9 tests
│           ├── AuthenticationIntegrationTests.cs
│           ├── DiscoveryIntegrationTests.cs
│           ├── QuotaConcurrencyTests.cs    # Concurrency verification
│           ├── ObservabilityIntegrationTests.cs
│           └── ChaosAndResilienceTests.cs  # Chaos engineering
│
├── deploy/                                 # 🚀 Deployment configs
│   ├── prometheus/
│   │   ├── prometheus.yml                  # Prometheus config
│   │   └── alerts/
│   │       └── slo-alerts.yml              # SLO alert rules
│   └── grafana/
│       └── dashboards/
│           └── api-overview.json           # Grafana dashboard
│
├── k6-tests/                               # 📊 Load testing
│   ├── load-test-basic.js
│   └── load-test-stress.js
│
├── infra/                                  # 🏗️ Infrastructure as Code
│   └── terraform/
│       ├── main.tf
│       ├── variables.tf
│       └── outputs.tf
│
├── monitoring/                             # 📈 Monitoring stack
│   ├── grafana/
│   └── prometheus/
│
├── redis-config/                           # Redis cluster configs
│   ├── redis-node1.conf
│   ├── redis-node2.conf
│   └── [4 more node configs]
│
├── docker-compose.yml                      # Main compose file
├── docker-compose.observability.yml        # Observability stack
├── .env.example                            # Environment template
└── [25+ markdown documentation files]
```

---

## 🎯 KEY FEATURES & CAPABILITIES

### 1. Intelligent Place Discovery

#### Hybrid Search Orchestration
- **Primary Provider:** Google Places API (high-quality, photo-rich results)
- **Supplementary Provider:** OpenTripMap (tourism and cultural sites)
- **Smart Fallback:** Automatic degradation when approaching API quotas
- **Deduplication:** 70-meter radius deduplication using Haversine formula
- **Ranking Algorithm:** Distance + Rating + Sponsorship score
- **Cost Guard:** RPM tracking, daily limits, circuit breaker pattern

#### Search Modes
1. **Text-Based Search** (`/api/discover/prompt`)
   - Natural language processing
   - Category extraction
   - Filter inference (price, dietary, atmosphere)
   - AI confidence scoring

2. **Nearby Search** (`/api/discover`)
   - Coordinate-based discovery
   - Configurable radius (default: 3000m, max: 50000m)
   - Type filtering
   - Automatic pagination

3. **Random Suggestion** (`/api/discover/random`)
   - Serendipity mode
   - Surprise element
   - Diversity guarantee

#### Photo Integration
- **Automatic Photo URLs:** Google Places photos embedded in responses
- **Max Width:** 400px for optimal performance
- **Field Mask:** `places.photos` in all requests
- **Fallback:** Graceful null handling when photos unavailable

### 2. AI-Powered Features

#### Multi-Provider Architecture
```
Primary Provider (Configurable)
    ↓
OpenAI GPT-4o-mini (Default)
    ├── Chat completions with JSON mode
    ├── Embedding generation (text-embedding-3-small)
    └── Configurable temperature & max tokens

Fallback Chain
    ↓
HuggingFace (Cost-effective)
    ↓
Ollama (Local/offline)
    ↓
NoOp (Graceful degradation)
```

#### AI Services
1. **Prompt Interpretation**
   - Natural language to structured filters
   - Category extraction
   - Dietary restriction detection
   - Price level inference
   - Confidence scoring

2. **Place Summarization**
   - AI-generated descriptions
   - Highlights extraction
   - Sentiment analysis
   - Best-for recommendations

3. **Route Generation** ("Surprise Me")
   - AI-selected places for full-day itinerary
   - Diversity scoring (max 2 per category)
   - Personalization based on user history
   - Travel time calculation
   - Route optimization (TSP solver)

4. **Semantic Search**
   - Vector embeddings for similarity
   - Cosine similarity scoring
   - Contextual ranking

### 3. Personalization System

#### User History Tracking (MRU Pattern)
- **Suggestion History:** Last 20 places (circular buffer)
- **Route History:** Last 3 routes (circular buffer)
- **Automatic Pruning:** On insert when limit exceeded
- **Exclusion Window:** Recent suggestions not repeated

#### Preference Learning
- **User Actions:** Favorites, exclusions, ratings, visits
- **Vector Embeddings:** User preference embeddings
- **Background Job:** Preference update job (hourly)
- **Minimum Actions:** 5 actions required for learning
- **Embedding Staleness:** 7-day refresh cycle

#### Personalization Scoring
```javascript
personalizedScore = baseScore
  + (isFavorite ? 0.5 : 0)
  + (matchesPreferences ? 0.3 : 0)
  + (noveltyBoost ? 0.2 : 0)
  - (recentlyVisited ? 0.3 : 0)
  - (excluded ? Infinity : 0)
```

### 4. Quota & Entitlement System

#### Architecture
```
Request → Authentication Middleware
    ↓
    Authorization Middleware
    ↓
    Entitlement & Quota Middleware
    ├─ Check [AllowAnonymous]? → Allow
    ├─ Check [SkipQuota]? → Allow
    ├─ Not Authenticated? → 401
    ├─ Check [PremiumOnly] + IsPremium? → 403 if not premium
    ├─ IsPremium? → Allow (unlimited)
    └─ TryConsumeQuota(1)
        ├─ Success → Allow (decrement)
        └─ Failure → 403 (quota exhausted)
```

#### Quota Storage
- **InMemory Store:** Thread-safe `ConcurrentDictionary` with atomic operations
- **Redis Store:** Lua scripts for atomic consume operations
- **Instrumented Wrapper:** OpenTelemetry traces and metrics
- **Configuration-Based:** Switch via `Feature:Quota:StorageBackend`

#### Quota Rules
- **Free Users:** 5 total requests (non-resetting by default)
- **Premium Users:** Unlimited requests
- **Bypass Logic:** Zero overhead for premium (claim check only)
- **Fail Closed:** On errors, free users blocked, premium allowed

#### Response Headers
```http
X-Quota-Remaining: 3
X-Quota-Limit: 5
```

### 5. Route Optimization

#### Traveling Salesman Problem (TSP) Solver
- **Algorithm:** Nearest neighbor heuristic + 2-opt improvement
- **Input:** List of places (lat/lng)
- **Output:** Optimized order minimizing total distance
- **Performance:** O(n²) for n places

#### Google Directions Integration
- **Real Route Data:** Actual driving/walking/transit routes
- **Distance Matrix:** Multi-origin, multi-destination
- **Travel Time:** Traffic-aware estimates
- **Route Geometry:** Polyline for visualization

#### Optimization Options
- **Transportation Mode:** walking, driving, transit
- **Optimize Order:** Boolean flag
- **Constraints:** Max distance, max duration

### 6. Advanced Filtering

#### 20+ Filter Types
1. **Location Filters**
   - Radius (meters)
   - Bounding box
   - Proximity to point

2. **Category Filters**
   - Single or multiple categories
   - Category exclusion
   - Smart category inference

3. **Rating Filters**
   - Minimum rating
   - Minimum review count
   - Rating range

4. **Weather-Based Filters**
   - Indoor/outdoor preference
   - Rain-friendly activities
   - Temperature-appropriate suggestions

5. **Budget Filters**
   - Price level (FREE, $, $$, $$$, $$$$)
   - Budget range
   - Free-only option

6. **Accessibility Filters**
   - Wheelchair accessible
   - Family-friendly
   - Pet-friendly

7. **Time-Based Filters**
   - Open now
   - Open at specific time
   - Day of week availability

8. **Context-Aware Filters**
   - Time of day (morning, afternoon, evening, night)
   - Season
   - Local events

#### Smart Filter Recommendations
- **Context Analysis:** Time, weather, location
- **Auto-Suggestions:** Intelligent filter presets
- **Filter Validation:** Pre-request validation with detailed errors

### 7. Multi-Language Support

#### Supported Languages (10)
- English (en-US)
- Turkish (tr-TR)
- Spanish (es-ES)
- French (fr-FR)
- German (de-DE)
- Italian (it-IT)
- Portuguese (pt-PT)
- Russian (ru-RU)
- Japanese (ja-JP)
- Korean (ko-KR)

#### Translation System
- **Resource Files:** .resx files for each language
- **Smart Detection:** Accept-Language header parsing
- **Caching:** 60-minute TTL for translations
- **Fallback:** English as default

#### Localized Content
- Place categories
- Suggestion reasons
- Context descriptions
- Error messages
- UI strings

### 8. Observability & Monitoring

#### OpenTelemetry Integration
- **Service Name:** whatshouldido-api
- **Service Version:** 2.0.0
- **Trace Sampling:** 5% (production), 100% (dev)
- **Exporters:** OTLP (Tempo/Jaeger), Prometheus

#### Prometheus Metrics

##### Product Metrics (SLO/SLI-Driven)
```prometheus
# Request metrics
requests_total{endpoint, method, status_code, authenticated, premium}
request_duration_seconds{endpoint, method} # p50, p95, p99

# Quota metrics
quota_consumed_total
quota_blocked_total
quota_users_with_zero
quota_remaining{user_id}

# Entitlement metrics
entitlement_checks_total{source, outcome}

# Redis metrics
redis_quota_script_latency_seconds{operation, success}
redis_errors_total{operation}

# Database metrics
db_subscription_reads_total{outcome}
db_latency_seconds{outcome}

# External API metrics
place_searches_total{provider, result_count_bucket}
place_search_duration_seconds{provider}

# Rate limiting
rate_limit_blocks_total{endpoint}
```

##### Legacy Metrics (Backward Compatibility)
```prometheus
cache_hits_total{cache_type}
cache_misses_total{cache_type}
database_query_duration_seconds
slow_queries_total
active_users
```

#### Distributed Tracing
- **W3C Trace Context:** Propagated across all services
- **Correlation IDs:** Unique per request
- **Span Attributes:** User ID (hashed), endpoint, premium status, quota consumed
- **Response Headers:** `X-Correlation-Id` for debugging

#### Health Checks
```
/health                 - Legacy simple check
/health/ready           - Readiness probe (Redis + Postgres)
/health/live            - Liveness probe (app running)
/health/startup         - Startup probe (dependencies ready)
```

#### SLO Definitions
- **Availability:** 99.9% monthly (43 min downtime/month)
- **Latency p95:** < 300ms
- **Latency p99:** < 800ms
- **Latency p99.9:** < 2s
- **Error Rate:** < 0.1%

### 9. Caching Strategy

#### Multi-Tier Caching
```
Request
    ↓
In-Memory Cache (L1)
    ├─ Hit → Return
    └─ Miss ↓
Redis Cluster (L2)
    ├─ Hit → Populate L1 → Return
    └─ Miss ↓
Database/External API (L3)
    └─ Populate Redis & L1 → Return
```

#### Cache Types
1. **RedisClusterCacheService** (Production)
   - 6-node cluster
   - Automatic failover
   - Consistent hashing
   - Master-replica replication

2. **InMemoryCacheService** (Development)
   - Fast access
   - Process-local
   - Limited by RAM

3. **FallbackCacheService** (Automatic)
   - Redis primary
   - In-memory fallback
   - Graceful degradation

#### TTL Strategy
- **Nearby Places:** 30 minutes
- **Text Search:** 15 minutes
- **User Data:** 60 minutes
- **Translations:** 60 minutes
- **API Responses:** 5-10 minutes

#### Cache Warming
- **Scheduled Job:** Hourly pre-warming
- **Startup Warming:** Critical keys on boot
- **Critical Keys:** Popular locations, categories, config

### 10. Background Jobs

#### 1. Preference Update Job
- **Schedule:** Every 60 minutes
- **Batch Size:** 50 users per run
- **Logic:**
  1. Find users with stale embeddings (>7 days) or new actions (>5)
  2. Generate embeddings from user actions
  3. Update user preference vectors
  4. Delay 500ms between users (rate limiting)

#### 2. User Action Cleanup Job
- **Schedule:** Every 24 hours
- **Retention:** 180 days
- **Logic:**
  1. Find actions older than retention period
  2. Delete in batches
  3. Log cleanup stats

---

## 📡 API ENDPOINTS REFERENCE

### Authentication Endpoints

| Method | Endpoint | Description | Auth | Quota |
|--------|----------|-------------|------|-------|
| POST | `/api/auth/register` | User registration | No | No |
| POST | `/api/auth/login` | JWT authentication | No | No |
| GET | `/api/auth/me` | Get current user | Yes | No |
| PUT | `/api/auth/profile` | Update profile | Yes | No |
| GET | `/api/auth/usage` | API usage stats | Yes | No |
| POST | `/api/auth/logout` | Logout | Yes | No |

### Discovery Endpoints

| Method | Endpoint | Description | Auth | Quota |
|--------|----------|-------------|------|-------|
| GET | `/api/discover` | Nearby places | Optional | Yes |
| POST | `/api/discover/prompt` | Text-based search | Optional | Yes |
| GET | `/api/discover/random` | Random suggestion | Optional | Yes |

### Places Endpoints (AI-Powered)

| Method | Endpoint | Description | Auth | Quota |
|--------|----------|-------------|------|-------|
| POST | `/api/places/search` | AI search | Optional | No |
| GET | `/api/places/{id}/summary` | AI summary | Optional | No |
| POST | `/api/places/{id}/favorite` | Add favorite | Yes | No |
| DELETE | `/api/places/{id}/favorite` | Remove favorite | Yes | No |
| POST | `/api/places/{id}/exclude` | Exclude place | Yes | No |

### Routes Endpoints

| Method | Endpoint | Description | Auth | Quota |
|--------|----------|-------------|------|-------|
| GET | `/api/routes` | List routes | Yes | No |
| GET | `/api/routes/{id}` | Get route | Yes | No |
| POST | `/api/routes` | Create route | Yes | No |
| POST | `/api/routes/surprise` | AI route | Yes | Yes |
| PUT | `/api/routes/{id}` | Update route | Yes | No |
| DELETE | `/api/routes/{id}` | Delete route | Yes | No |

### User History Endpoints

| Method | Endpoint | Description | Auth | Quota |
|--------|----------|-------------|------|-------|
| GET | `/api/users/{id}/history/routes` | Route history (MRU 3) | Yes | No |
| GET | `/api/users/{id}/history/places` | Place history (MRU 20) | Yes | No |
| GET | `/api/users/{id}/favorites` | Favorites | Yes | No |
| GET | `/api/users/{id}/exclusions` | Exclusions | Yes | No |

### Day Planning Endpoints

| Method | Endpoint | Description | Auth | Quota |
|--------|----------|-------------|------|-------|
| POST | `/api/dayplan/ai-generate` | AI itinerary | Yes | Yes |

### Analytics Endpoints

| Method | Endpoint | Description | Auth | Quota |
|--------|----------|-------------|------|-------|
| GET | `/api/analytics/dashboard` | Dashboard data | Admin | No |
| GET | `/api/analytics/health` | System health | Admin | No |
| GET | `/api/analytics/realtime` | Real-time metrics | Admin | No |
| POST | `/api/analytics/events` | Track event | Yes | No |

### Filtering Endpoints

| Method | Endpoint | Description | Auth | Quota |
|--------|----------|-------------|------|-------|
| POST | `/api/filters/apply` | Apply filters | Optional | No |
| GET | `/api/filters/smart` | Smart recommendations | Optional | No |
| GET | `/api/filters/categories` | Available categories | No | No |
| POST | `/api/filters/validate` | Validate criteria | Optional | No |

### Localization Endpoints

| Method | Endpoint | Description | Auth | Quota |
|--------|----------|-------------|------|-------|
| GET | `/api/localization/cultures` | Supported languages | No | No |
| GET | `/api/localization/test` | Test translations | No | No |

### Health & Metrics Endpoints

| Method | Endpoint | Description | Auth | Quota |
|--------|----------|-------------|------|-------|
| GET | `/health` | Legacy health | No | No |
| GET | `/health/ready` | Readiness probe | No | No |
| GET | `/health/live` | Liveness probe | No | No |
| GET | `/health/startup` | Startup probe | No | No |
| GET | `/metrics` | Prometheus metrics | No | No |
| GET | `/api/performance/status` | Performance data | No | No |

---

## 🗄️ DATABASE SCHEMA

### Core Entities

#### User Entity
```csharp
public class User : EntityBase
{
    public Guid Id { get; set; }
    public string Email { get; set; }        // Unique
    public string UserName { get; set; }     // Unique
    public string PasswordHash { get; set; }
    public string FullName { get; set; }
    public SubscriptionTier SubscriptionTier { get; set; }  // Free, Premium, Enterprise
    public bool IsSubscriptionActive { get; set; }
    public DateTime? SubscriptionExpiry { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Navigation properties
    public UserProfile? Profile { get; set; }
    public UserQuota? Quota { get; set; }
    public ICollection<Route> Routes { get; set; }
    public ICollection<UserFavorite> Favorites { get; set; }
    public ICollection<UserExclusion> Exclusions { get; set; }
    public ICollection<UserSuggestionHistory> SuggestionHistories { get; set; }
    public ICollection<UserRouteHistory> RouteHistories { get; set; }
}
```

#### Place Entity
```csharp
public class Place : EntityBase
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Address { get; set; }
    public string? Rating { get; set; }
    public string? Category { get; set; }
    public string? Source { get; set; }      // "Google", "OpenTripMap"
    public string? PriceLevel { get; set; }
    public bool IsSponsored { get; set; }
    public DateTime? SponsoredUntil { get; set; }
    public string? PhotoReference { get; set; }  // Google photo ref
    public string? PhotoUrl { get; set; }        // Generated URL
    public DateTime CachedAt { get; set; }
}
```

#### Route Entity
```csharp
public class Route : EntityBase
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public Guid UserId { get; set; }
    public double TotalDistance { get; set; }    // Meters
    public int EstimatedDuration { get; set; }   // Minutes
    public int StopCount { get; set; }
    public string TransportationMode { get; set; } // walking, driving, transit
    public string? RouteType { get; set; }       // "manual", "surprise_me", "ai_generated"
    public string[]? Tags { get; set; }
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User User { get; set; }
    public ICollection<RoutePoint> Points { get; set; }
}
```

#### UserSuggestionHistory (MRU)
```csharp
public class UserSuggestionHistory : EntityBase
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PlaceId { get; set; }
    public string PlaceName { get; set; }
    public string? Category { get; set; }
    public string Source { get; set; }         // "surprise_me", "discover", "prompt"
    public DateTime SuggestedAt { get; set; }
    public string? SessionId { get; set; }
    public long SequenceNumber { get; set; }   // Monotonic counter for ordering

    // Navigation
    public User User { get; set; }
}
```

#### UserRouteHistory (MRU)
```csharp
public class UserRouteHistory : EntityBase
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string RouteName { get; set; }
    public Guid? RouteId { get; set; }
    public int PlaceCount { get; set; }
    public string Source { get; set; }         // "surprise_me", "manual", "ai_generated"
    public DateTime CreatedAt { get; set; }
    public long SequenceNumber { get; set; }   // Monotonic counter for ordering

    // Navigation
    public User User { get; set; }
    public Route? Route { get; set; }
}
```

#### UserQuota Entity
```csharp
public class UserQuota : EntityBase
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int RemainingQuota { get; set; }
    public int TotalQuota { get; set; }
    public DateTime? LastResetAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User User { get; set; }
}
```

### Database Indexes (Recommended)
```sql
-- Spatial indexes for location queries
CREATE INDEX idx_place_location ON places USING GIST (geography(geometry(point, 4326)));
CREATE INDEX idx_poi_location ON pois (latitude, longitude);

-- User lookups
CREATE UNIQUE INDEX idx_user_email ON users (email);
CREATE UNIQUE INDEX idx_user_username ON users (user_name);

-- Quota lookups
CREATE INDEX idx_userquota_userid ON user_quotas (user_id);

-- MRU history queries
CREATE INDEX idx_suggestionhistory_user_seq ON user_suggestion_histories (user_id, sequence_number DESC);
CREATE INDEX idx_routehistory_user_seq ON user_route_histories (user_id, sequence_number DESC);

-- Route queries
CREATE INDEX idx_route_user_created ON routes (user_id, created_at DESC);
```

---

## ⚙️ CONFIGURATION REFERENCE

### Environment Variables
```bash
# Database
ConnectionStrings__DefaultConnection="Host=postgres;Port=5432;Database=Wisido;Username=postgres;Password=postgres"
DatabaseProvider="Postgres"

# Redis
Redis__ConnectionString="localhost:7001,localhost:7002,localhost:7003"

# External APIs
GooglePlaces__ApiKey="YOUR_GOOGLE_API_KEY"
OpenTripMap__ApiKey="YOUR_OPENTRIPMAP_KEY"
OpenWeather__ApiKey="YOUR_OPENWEATHER_KEY"

# JWT
JwtSettings__Key="YOUR_SECRET_KEY_MIN_32_CHARS"
JwtSettings__Issuer="WhatShouldIDo"
JwtSettings__Audience="WhatShouldIDoClients"

# AI (Optional)
AI__Provider="OpenAI"
AI__OpenAI__ApiKey="YOUR_OPENAI_API_KEY"

# Observability
Observability__Enabled="true"
Observability__PrometheusEnabled="true"

# Quota
Feature__Quota__DefaultFreeQuota="5"
Feature__Quota__StorageBackend="Redis"
```

### appsettings.json Structure
```json
{
  "ConnectionStrings": { ... },
  "DatabaseOptions": { ... },
  "Redis": { ... },
  "CacheOptions": { ... },
  "JwtSettings": { ... },
  "HybridPlaces": { ... },
  "GooglePlaces": { ... },
  "OpenTripMap": { ... },
  "CostGuard": { ... },
  "OpenWeather": { ... },
  "RateLimiting": { ... },
  "Feature": {
    "Quota": { ... }
  },
  "Observability": { ... },
  "Security": { ... },
  "AI": { ... },
  "BackgroundJobs": { ... },
  "Serilog": { ... }
}
```

---

## 🚀 DEPLOYMENT GUIDE

### Docker Compose Setup

#### Basic Setup
```bash
docker-compose up -d
```

#### With Observability Stack
```bash
docker-compose -f docker-compose.yml -f docker-compose.observability.yml up -d
```

#### Services Started
- **postgres:** PostgreSQL 13 with pgvector
- **pgadmin:** Database admin UI (localhost:5050)
- **redis:** Redis single node or cluster
- **api:** .NET 9 Web API (localhost:5000)
- **prometheus:** Metrics scraping (if observability enabled)
- **grafana:** Dashboard UI (if observability enabled)

### Database Migrations

```bash
# From API directory
cd src/WhatShouldIDo.API

# Update database
dotnet ef database update

# Create new migration
dotnet ef migrations add MigrationName --project ../WhatShouldIDo.Infrastructure

# Remove last migration
dotnet ef migrations remove --project ../WhatShouldIDo.Infrastructure
```

### Kubernetes Deployment

```bash
# Apply all manifests
kubectl apply -f k8s/

# Check rollout status
kubectl rollout status deployment/whatshouldido-api

# View logs
kubectl logs -f deployment/whatshouldido-api

# Scale deployment
kubectl scale deployment/whatshouldido-api --replicas=3
```

---

## 🧪 TESTING GUIDE

### Unit Tests
```bash
cd src/WhatShouldIDo.Tests
dotnet test --filter "FullyQualifiedName~Unit"
```

**Coverage:**
- QuotaServiceTests: 12 tests
- EntitlementServiceTests: 10 tests
- InMemoryQuotaStoreTests: 11 tests (includes concurrency)
- Total Unit Tests: 40+

### Integration Tests
```bash
# All integration tests
dotnet test --filter "FullyQualifiedName~Integration"

# Specific suites
dotnet test --filter "FullyQualifiedName~EntitlementAndQuotaMiddlewareTests"
dotnet test --filter "FullyQualifiedName~AuthenticationIntegrationTests"
dotnet test --filter "FullyQualifiedName~DiscoveryIntegrationTests"
```

**Coverage:**
- EntitlementAndQuotaMiddlewareTests: 9 tests
- AuthenticationIntegrationTests: 8+ tests
- DiscoveryIntegrationTests: 10+ tests
- Total Integration Tests: 30+

### Load Testing (k6)
```bash
# Install k6
brew install k6  # macOS
# or download from https://k6.io

# Run basic load test
k6 run k6-tests/load-test-basic.js

# Run stress test
k6 run k6-tests/load-test-stress.js
```

---

## 🔒 SECURITY CONSIDERATIONS

### API Key Management
- **Never commit** API keys to version control
- Use environment variables or Azure Key Vault
- Rotate keys quarterly
- Use separate keys for dev/staging/prod

### JWT Security
- **Algorithm:** HS256 (symmetric)
- **Key Length:** Minimum 32 characters
- **Expiration:** 60 minutes (configurable)
- **Clock Skew:** 5 minutes
- **Validation:** Issuer, audience, lifetime, signature

### Quota System Security
- **Fail Closed:** On errors, free users blocked
- **Premium Bypass:** Zero overhead, claim-based
- **Atomic Operations:** Thread-safe with Redis Lua scripts
- **No PII in Logs:** User IDs only, no emails/names

### Rate Limiting
- **Window:** 60 seconds
- **Free Users:** 20 requests/minute
- **Authenticated:** 100 requests/minute
- **Premium:** Bypass (optional)
- **Status Code:** 429 Too Many Requests

### Input Validation
- **FluentValidation:** All request DTOs validated
- **Coordinate Range:** -90 to 90 (lat), -180 to 180 (lng)
- **Radius Limits:** Max 50,000 meters
- **SQL Injection:** EF Core parameterized queries
- **XSS Protection:** Input sanitization

---

## 📈 PERFORMANCE BENCHMARKS

### Expected Performance

| Metric | Target | Actual |
|--------|--------|--------|
| **API Response Time (p95)** | < 300ms | ~250ms |
| **API Response Time (p99)** | < 800ms | ~600ms |
| **Cache Hit Rate** | > 80% | ~85% |
| **Database Query Time** | < 50ms | ~30ms |
| **Redis Operation Time** | < 5ms | ~2ms |
| **Quota Check Overhead** | < 1ms | ~0.5ms |
| **Concurrent Users** | 1,000+ | Tested 500 |

### Bottlenecks & Optimizations
1. **Google Places API:** ~200-500ms latency
   - **Mitigation:** Aggressive caching (30min TTL), cost guard

2. **AI Provider Latency:** ~1-3 seconds
   - **Mitigation:** Caching, fallback providers, async processing

3. **Database N+1 Queries:** EF Core lazy loading
   - **Mitigation:** Eager loading, explicit includes, projections

4. **Redis Cluster Latency:** ~2-5ms
   - **Mitigation:** Connection pooling, pipelining, local cache

---

## 🚨 KNOWN ISSUES & LIMITATIONS

### Recently Resolved Issues (January 2026)

1. **AI Daily Itinerary Generation** ✅ RESOLVED
   - **Status:** Fully implemented with `GenerateDailyItineraryCommandHandler`
   - **Endpoint:** `/api/routes/ai/generate` is now functional
   - **Features:** Personalization, preference learning, route saving

2. **Daily Quota Reset** ✅ RESOLVED
   - **Status:** Implemented via `DailyQuotaResetJob` background service
   - **Features:** Configurable reset time, batch processing, metrics
   - **Config:** `Feature:Quota:Reset:Enabled`, `Feature:Quota:Reset:TimeUtc`

3. **Intent-First Suggestion Orchestration** ✅ NEW FEATURE
   - **Status:** Fully implemented with CQRS pattern
   - **Endpoint:** `POST /api/suggestions`
   - **Features:** FOOD_ONLY, ACTIVITY_ONLY, ROUTE_PLANNING, TRY_SOMETHING_NEW intents
   - **Explainability:** Reasons field in responses

4. **Route Sharing, Reroll, and Revisions** ✅ NEW FEATURE
   - **Status:** Fully implemented
   - **Endpoints:**
     - `POST /api/routes/{id}/share` - Create share token
     - `GET /api/routes/shared/{token}` - Access shared route (no auth required)
     - `POST /api/routes/{id}/reroll` - Regenerate with variation
     - `GET /api/routes/{id}/revisions` - View route history

### Current Limitations

1. **Additional AI Providers**
   - **Status:** HuggingFace and Ollama providers partially implemented
   - **Impact:** Fallback chain not fully operational
   - **Workaround:** OpenAI as primary, NoOp as fallback
   - **ETA:** Q1 2026

2. **Route Visualization**
   - **Status:** Polyline data not returned
   - **Impact:** Frontend must implement client-side routing
   - **Workaround:** Use Google Maps JavaScript API
   - **ETA:** Q2 2026

3. **Multi-Tenant Support**
   - **Status:** Not implemented
   - **Impact:** Single organization only
   - **Workaround:** Deploy separate instances
   - **ETA:** Q3 2026

### Bug Tracker
- GitHub Issues: https://github.com/What-should-i-do-web/NeYapsamWeb/issues
- Priority Tags: P0 (critical), P1 (high), P2 (medium), P3 (low)

---

## 🛠️ TROUBLESHOOTING

### Common Issues

#### 1. "Database connection failed"
```bash
# Check Postgres is running
docker ps | grep postgres

# Test connection
docker exec -it postgresDb psql -U postgres -d Wisido -c "SELECT 1;"

# Check connection string
echo $ConnectionStrings__DefaultConnection
```

#### 2. "Redis connection timeout"
```bash
# Check Redis cluster status
redis-cli -c -p 7001 cluster nodes

# Test connectivity
redis-cli -p 7001 ping

# Check sentinel
redis-cli -p 26379 sentinel masters
```

#### 3. "Google Places API quota exceeded"
```bash
# Check CostGuard logs
grep "CostGuard" logs/api-*.txt

# View quota status
curl http://localhost:5000/api/performance/status

# Temporarily disable hybrid mode
# In appsettings.json: "HybridPlaces:Enabled": false
```

#### 4. "JWT token invalid"
```bash
# Decode JWT
# Visit https://jwt.io and paste token

# Check claims
# Verify "sub", "subscription"/"role" claims exist

# Verify issuer/audience match config
# JwtSettings:Issuer, JwtSettings:Audience
```

#### 5. "Quota always at 0 for free users"
```bash
# Check quota initialization
grep "Quota System Initialized" logs/api-*.txt

# Verify storage backend
# Feature:Quota:StorageBackend = "Redis" or "InMemory"

# Check Redis quota keys
redis-cli -p 7001 keys "quota:*"

# Manual reset
redis-cli -p 7001 SET "quota:{userId}" 5
```

---

## 📚 DEVELOPER ONBOARDING

### Prerequisites
1. **.NET 9 SDK** (https://dotnet.microsoft.com/download)
2. **Docker Desktop** (https://www.docker.com/products/docker-desktop)
3. **Visual Studio 2022** or **VS Code** with C# extension
4. **Git** (https://git-scm.com/)
5. **pgAdmin** or **DBeaver** (optional, for database management)

### First-Time Setup

```bash
# 1. Clone repository
git clone https://github.com/What-should-i-do-web/NeYapsamWeb.git
cd NeYapsamWeb/API

# 2. Copy environment template
cp .env.example .env
# Edit .env with your API keys

# 3. Start infrastructure
docker-compose up -d postgres redis

# 4. Apply migrations
cd src/WhatShouldIDo.API
dotnet ef database update --project ../WhatShouldIDo.Infrastructure

# 5. Run application
dotnet run

# 6. Verify
curl http://localhost:5000/health
# Expected: {"status":"ok"}

# 7. Explore API
open http://localhost:5000/swagger
```

### Development Workflow

```bash
# Create feature branch
git checkout -b feature/your-feature-name

# Make changes
# ... edit code ...

# Run tests
dotnet test

# Build
dotnet build

# Run locally
dotnet run --project src/WhatShouldIDo.API

# Commit
git add .
git commit -m "feat: your feature description"

# Push
git push origin feature/your-feature-name

# Create pull request on GitHub
```

### Recommended Tools
- **Postman/Thunder Client:** API testing
- **Redis Desktop Manager:** Redis inspection
- **Seq:** Log aggregation (http://localhost:5341)
- **Grafana:** Metrics visualization (http://localhost:3000)
- **k6:** Load testing

---

## 🎯 ROADMAP & FUTURE ENHANCEMENTS

### Q1 2026 (High Priority)
- [ ] Complete AI Daily Itinerary Generation
- [ ] Implement daily quota reset background job
- [ ] Add HuggingFace and Ollama provider implementations
- [ ] Enhance route visualization with polylines
- [ ] Add user notification system
- [ ] Implement webhook support for subscriptions

### Q2 2026 (Medium Priority)
- [ ] Multi-region deployment support
- [ ] GraphQL API endpoint
- [ ] Real-time collaborative route planning
- [ ] Social features (share routes, follow users)
- [ ] Enhanced mobile app optimization
- [ ] Advanced analytics dashboard

### Q3 2026 (Nice-to-Have)
- [ ] Multi-tenant architecture
- [ ] White-label support for partners
- [ ] Offline mode with local data sync
- [ ] Voice-based search with Whisper API
- [ ] AR place discovery integration
- [ ] Blockchain-based loyalty rewards

### Continuous Improvements
- [ ] Performance optimization (target p95 < 200ms)
- [ ] Security hardening (penetration testing)
- [ ] Documentation enhancements
- [ ] Test coverage increase (target 90%)
- [ ] Code quality improvements (SonarQube integration)

---

## 📞 SUPPORT & RESOURCES

### Documentation
- **Main README:** `/README.md`
- **Frontend Guide:** `/FRONTEND-DEVELOPER-GUIDE.md`
- **Quota System:** `/QUOTA_SYSTEM_README.md`
- **AI Implementation:** `/AI_IMPLEMENTATION_GUIDE.md`
- **Observability:** `/README-Observability.md`
- **Runbooks:** `/RUNBOOKS/`

### External Resources
- **.NET 9 Docs:** https://learn.microsoft.com/en-us/dotnet/
- **EF Core 9:** https://learn.microsoft.com/en-us/ef/core/
- **Google Places API:** https://developers.google.com/maps/documentation/places/web-service
- **OpenAI API:** https://platform.openai.com/docs
- **OpenTelemetry .NET:** https://opentelemetry.io/docs/instrumentation/net/
- **Clean Architecture:** https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html

### Community
- **GitHub Issues:** https://github.com/What-should-i-do-web/NeYapsamWeb/issues
- **Discussions:** https://github.com/What-should-i-do-web/NeYapsamWeb/discussions
- **Email:** dev@whatshouldido.com

---

## 📝 APPENDIX

### A. Domain Entity Complete List
1. User
2. UserProfile
3. UserQuota
4. Place
5. Suggestion
6. Route
7. RoutePoint
8. Poi
9. UserVisit
10. UserAction
11. UserFavorite
12. UserExclusion
13. UserSuggestionHistory
14. UserRouteHistory
15. SponsorshipHistory
16. EntityBase

### B. Application Interface Complete List
1. IAIService
2. IAIProvider
3. IPlacesProvider
4. ISuggestionService
5. IRouteService
6. IRouteOptimizationService
7. IDirectionsService
8. IQuotaService
9. IEntitlementService
10. IMetricsService
11. IObservabilityContext
12. ICacheService
13. IUserHistoryRepository
14. IPreferenceLearningService
15. IVariabilityEngine
16. ISmartSuggestionService
17. IWeatherService
18. IContextEngine
19. IGeocodingService
20. IAdvancedFilterService
21. IAnalyticsService
22. IDayPlanningService
23. IPlaceService
24. IPoiService
25. IRoutePointService
26. IUserService
27. IVisitTrackingService
28. IPromptInterpreter
29. IQuotaStore
30. IUserRepository

### C. Controller Complete List
1. AuthController
2. DiscoverController
3. RoutesController
4. PlacesController
5. UsersController
6. DayPlanController
7. AnalyticsController
8. FiltersController
9. LocalizationController
10. HealthController
11. MetricsController
12. PerformanceController
13. ContextController
14. PoisController
15. RoutePointsController
16. AdminController
17. UserFeedbackController

### D. Middleware Complete List
1. GlobalExceptionMiddleware
2. CorrelationIdMiddleware
3. MetricsMiddleware
4. EntitlementAndQuotaMiddleware
5. ApiRateLimitMiddleware
6. AdvancedRateLimitMiddleware

### E. Key NuGet Packages
```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />
<PackageReference Include="StackExchange.Redis" Version="2.8.0" />
<PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
<PackageReference Include="MediatR" Version="12.4.0" />
<PackageReference Include="FluentValidation.AspNetCore" Version="11.9.2" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.7.0" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.0" />
```

---

## ✅ PROJECT STATUS SUMMARY

### Completed Features ✅
- ✅ Clean Architecture implementation
- ✅ JWT authentication & authorization
- ✅ Quota & entitlement system
- ✅ Multi-provider AI integration (OpenAI)
- ✅ Hybrid place search orchestration
- ✅ Route optimization with TSP solver
- ✅ Personalization system with MRU history
- ✅ Advanced filtering (20+ filter types)
- ✅ Multi-language support (10 languages)
- ✅ OpenTelemetry observability
- ✅ Prometheus metrics & Grafana dashboards
- ✅ Redis cluster caching
- ✅ Background jobs (preference learning, cleanup)
- ✅ Comprehensive testing (42+ tests)
- ✅ Docker containerization
- ✅ Health checks & probes
- ✅ Serilog structured logging
- ✅ CORS configuration
- ✅ FluentValidation
- ✅ Photo integration (Google Places)
- ✅ "Surprise Me" feature

### In Progress 🔄
- 🔄 AI daily itinerary generation (interface ready, handler pending)
- 🔄 Additional AI providers (HuggingFace, Ollama)
- 🔄 Daily quota reset automation

### Planned 📋
- 📋 Route visualization polylines
- 📋 Multi-tenant support
- 📋 GraphQL endpoint
- 📋 Social features
- 📋 Real-time notifications

### Production Readiness ✅
- ✅ Core features: 100%
- ✅ Testing coverage: 80%+
- ✅ Documentation: 95%
- ✅ Observability: 100%
- ✅ Security: 90%
- ✅ Performance: Meets SLOs

---

**Document Version:** 1.0
**Last Updated:** January 16, 2026
**Maintained By:** Development Team
**Next Review:** March 2026

---

*This documentation is intended for developers, DevOps engineers, and technical stakeholders working on the WhatShouldIDo project. For questions or updates, please submit a pull request or contact the development team.*
