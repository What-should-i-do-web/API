# WhatShouldIDo API - Complete Frontend Developer Guide

**Version:** 3.0.0
**Base URL:** `https://api.whatshouldido.com` (Production) | `http://localhost:5000` (Development)
**Last Updated:** November 10, 2025

---

## 📚 Table of Contents

1. [Quick Start](#quick-start)
2. [Backend Architecture](#backend-architecture)
3. [Authentication Flow](#authentication-flow)
4. [Core Concepts](#core-concepts)
5. [Complete API Reference](#complete-api-reference)
6. [User Journeys & Scenarios](#user-journeys--scenarios)
7. [TypeScript Integration](#typescript-integration)
8. [Error Handling Guide](#error-handling-guide)
9. [Best Practices](#best-practices)
10. [Testing & Debugging](#testing--debugging)

---

## 🚀 Quick Start

### 5-Minute Integration

```javascript
// 1. Register user
const { token } = await fetch('http://localhost:5000/api/auth/register', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    email: 'user@example.com',
    password: 'SecurePass123!',
    username: 'johndoe'
  })
}).then(r => r.json());

// 2. Get personalized suggestions
const { suggestions } = await fetch(
  'http://localhost:5000/api/discover?lat=41.0082&lng=28.9784&radius=5000',
  { headers: { 'Authorization': `Bearer ${token}` } }
).then(r => r.json());

// 3. Generate a Surprise Me route
const surpriseRoute = await fetch('http://localhost:5000/api/routes/surprise', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    targetArea: 'Istanbul',
    latitude: 41.0082,
    longitude: 28.9784,
    radiusMeters: 5000,
    minStops: 3,
    maxStops: 6
  })
}).then(r => r.json());
```

---

## 🏗️ Backend Architecture

### Technology Stack

- **Framework:** ASP.NET Core 9.0
- **Architecture:** Clean Architecture (Domain → Application → Infrastructure → API)
- **Database:** PostgreSQL 13+ with pgvector extension
- **Cache:** Redis (cluster mode)
- **AI:** OpenAI GPT-4o-mini, HuggingFace, Ollama (fallback)
- **Observability:** OpenTelemetry + Prometheus + Grafana

### Architectural Layers

```
┌─────────────────────────────────────┐
│         API Layer (REST)            │  ← Controllers, DTOs, Middleware
├─────────────────────────────────────┤
│      Application Layer (CQRS)       │  ← Use Cases, Commands, Queries
├─────────────────────────────────────┤
│     Infrastructure Layer            │  ← Services, Repositories, AI
├─────────────────────────────────────┤
│        Domain Layer                 │  ← Entities, Value Objects
└─────────────────────────────────────┘
```

### Key Design Patterns

- **Repository Pattern:** Data access abstraction
- **CQRS (MediatR):** Separation of reads and writes
- **Strategy Pattern:** AI provider selection (OpenAI/HuggingFace/Ollama)
- **MRU (Most Recently Used):** Circular buffer for history (max 3 routes, max 20 places)
- **Quota System:** Redis-based with Lua scripts for atomic operations

---

## 🔐 Authentication Flow

### JWT Bearer Tokens

All protected endpoints require a JWT token in the `Authorization` header:

```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Token Lifecycle

| Property | Value |
|----------|-------|
| **Expiration** | 60 minutes (production) / 120 minutes (dev) |
| **Algorithm** | HS256 |
| **Claims** | `sub` (userId), `email`, `subscription`, `role` |
| **Refresh** | Not implemented (re-login required) |

### Authentication Workflow

```mermaid
sequenceDiagram
    Frontend->>API: POST /api/auth/register
    API-->>Frontend: { token, user }
    Frontend->>LocalStorage: Store token
    Frontend->>API: GET /api/discover (with token)
    API-->>Frontend: Personalized suggestions
    Note over API: Token expires after 60min
    API-->>Frontend: 401 Unauthorized
    Frontend->>API: POST /api/auth/login (re-authenticate)
```

---

## 🧠 Core Concepts

### 1. Personalization System

The API learns from user behavior and provides personalized recommendations:

**Data Sources:**
- **Visit History:** Places the user has visited
- **Favorites:** Places user explicitly marked as favorite
- **Exclusions:** Places user marked as "do not recommend"
- **Feedback:** Ratings and reviews
- **Suggestion History:** Recently suggested places (MRU pattern)

**Personalization Scoring:**
```javascript
personalizedScore = baseScore
  + (isFavorite ? 0.5 : 0)
  + (matchesPreferences ? 0.3 : 0)
  + (noveltyBoost ? 0.2 : 0)
  - (recentlyVisited ? 0.3 : 0)
```

### 2. Quota System

**Free Users:**
- 5 total requests to feature endpoints
- Quota does NOT reset daily
- Tracked per user across all devices

**Premium Users:**
- Unlimited requests
- No quota checks

**Checking Quota:**
```javascript
// Response headers
X-Quota-Remaining: 3
X-Quota-Limit: 5
```

### 3. MRU (Most Recently Used) Pattern

The system maintains circular buffers for user history:

| Type | Max Items | Auto-Pruning |
|------|-----------|--------------|
| **Route History** | 3 routes | On insert |
| **Suggestion History** | 20 places | On insert |

**How it works:**
- Items are ordered by `sequenceNumber` (monotonic counter)
- When limit is reached, oldest items are automatically deleted
- Used for "Recently Suggested" exclusion window

### 4. Exclusion Window

Prevents showing the same places repeatedly:

- **Default Size:** 3 suggestions
- **Configurable:** Can be adjusted per request
- **Purpose:** If a place was suggested in the last 3 requests, it won't be suggested again

---

## 📡 Complete API Reference

### Authentication Endpoints

#### **POST** `/api/auth/register`
Create a new user account.

**Request:**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123!",
  "username": "johndoe",
  "fullName": "John Doe"
}
```

**Validation:**
- Email: Valid format, unique
- Password: Min 8 chars, 1 uppercase, 1 lowercase, 1 digit, 1 special char
- Username: Min 3 chars, alphanumeric + underscore

**Response:** `200 OK`
```json
{
  "token": "eyJhbGc...",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "user@example.com",
    "username": "johndoe",
    "subscriptionTier": "Free",
    "dailyApiUsage": 0,
    "dailyApiLimit": 5
  }
}
```

---

#### **POST** `/api/auth/login`
Authenticate and get JWT token.

**Request:**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123!"
}
```

**Response:** `200 OK`
```json
{
  "token": "eyJhbGc...",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "user@example.com",
    "username": "johndoe",
    "subscriptionTier": "Premium",
    "subscriptionExpiry": "2026-01-24T10:30:00Z"
  }
}
```

---

#### **GET** `/api/auth/me`
Get current user profile.

**Headers:** `Authorization: Bearer {token}`

**Response:** `200 OK`
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "username": "johndoe",
  "subscriptionTier": "Premium",
  "dailyApiUsage": 47,
  "dailyApiLimit": 1000,
  "createdAt": "2024-06-15T08:00:00Z"
}
```

---

### Discovery Endpoints

#### **GET** `/api/discover`
Get smart suggestions near a location (personalized for authenticated users).

**Auth:** Optional (personalized if authenticated)
**Quota:** ✅ Counted (1 credit)

**Query Parameters:**
```
lat (required): float - Latitude (-90 to 90)
lng (required): float - Longitude (-180 to 180)
radius (optional): int - Meters (default: 3000, max: 50000)
```

**Example:**
```http
GET /api/discover?lat=41.0082&lng=28.9784&radius=5000
Authorization: Bearer {token}
```

**Response:** `200 OK`
```json
{
  "personalized": true,
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "suggestions": [
    {
      "id": "place_001",
      "name": "Blue Mosque",
      "category": "mosque",
      "rating": 4.8,
      "location": {
        "latitude": 41.0054,
        "longitude": 28.9768
      },
      "address": "Sultan Ahmet, Istanbul",
      "distance": 342,
      "distanceText": "342m",
      "photoUrl": "https://...",
      "openNow": true,
      "priceLevel": "FREE",
      "personalizedScore": 0.92,
      "reasons": ["Highly rated", "Close to you"]
    }
  ]
}
```

---

#### **POST** `/api/discover/prompt`
Get suggestions based on natural language prompt.

**Auth:** Optional
**Quota:** ✅ Counted (1 credit)

**Request:**
```json
{
  "prompt": "romantic restaurant with sea view",
  "latitude": 41.0082,
  "longitude": 28.9784,
  "radius": 5000,
  "sortBy": "rating"
}
```

**Prompt Examples:**
- "coffee shop with wifi"
- "family-friendly restaurant"
- "cheap eats near me"
- "museums and historical sites"
- "nightlife and bars"

**Response:** `200 OK`
```json
{
  "personalized": true,
  "suggestions": [
    {
      "id": "place_123",
      "name": "Sunset Grill & Bar",
      "category": "restaurant",
      "rating": 4.7,
      "priceLevel": "$$$$",
      "matchScore": 0.94,
      "matchReasons": ["Sea view", "Romantic ambiance"],
      "cuisineType": "Mediterranean"
    }
  ]
}
```

---

#### **GET** `/api/discover/random`
Get a single random suggestion ("Surprise Me").

**Auth:** Optional
**Quota:** ✅ Counted (1 credit)

**Query Parameters:**
```
lat (required): float
lng (required): float
radius (optional): int
```

**Response:** `200 OK`
```json
{
  "personalized": true,
  "suggestion": {
    "id": "place_042",
    "name": "Grand Bazaar",
    "category": "shopping_mall",
    "rating": 4.6,
    "location": {
      "latitude": 41.0108,
      "longitude": 28.9680
    },
    "distance": 1234
  }
}
```

---

### Places Endpoints (AI-Powered)

#### **POST** `/api/places/search`
AI-powered place search with natural language.

**Auth:** Optional
**Quota:** Not counted

**Request:**
```json
{
  "query": "best sushi restaurants in downtown",
  "latitude": 41.0082,
  "longitude": 28.9784,
  "radius": 10000,
  "filters": {
    "minRating": 4.0,
    "priceLevel": "$$",
    "openNow": true
  }
}
```

**Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "places": [
      {
        "id": "place_789",
        "name": "Tokyo Sushi Bar",
        "rating": 4.7,
        "priceLevel": "$$",
        "cuisineType": "Japanese"
      }
    ]
  },
  "metadata": {
    "usedAI": true,
    "aiConfidence": 0.95,
    "interpretedQuery": "sushi restaurants, high rating, downtown area"
  }
}
```

---

#### **GET** `/api/places/{id}/summary`
Get AI-generated summary for a place.

**Auth:** Optional
**Quota:** Not counted

**Query Parameters:**
```
style (optional): "brief" | "detailed" | "highlights" (default: "brief")
```

**Example:**
```http
GET /api/places/place_001/summary?style=detailed
```

**Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "placeId": "place_001",
    "placeName": "Blue Mosque",
    "summary": "The Blue Mosque is an iconic Ottoman-era mosque...",
    "highlights": [
      "Stunning blue Iznik tiles",
      "Six minarets - unique architectural feature",
      "Active place of worship"
    ],
    "bestTimeToVisit": "Early morning before crowds",
    "estimatedVisitDuration": "45-60 minutes"
  },
  "metadata": {
    "usedAI": true,
    "aiProvider": "OpenAI",
    "generatedAt": "2025-11-10T10:30:00Z"
  }
}
```

---

#### **POST** `/api/places/{placeId}/favorite`
Add a place to user's favorites.

**Auth:** Required
**Quota:** Not counted

**Request:**
```json
{
  "placeName": "Blue Mosque",
  "category": "mosque",
  "latitude": 41.0054,
  "longitude": 28.9768,
  "notes": "Beautiful architecture, want to visit again"
}
```

**Response:** `200 OK`
```json
{
  "success": true,
  "message": "Place added to favorites",
  "favorite": {
    "id": "fav_001",
    "placeId": "place_001",
    "placeName": "Blue Mosque",
    "addedAt": "2025-11-10T10:30:00Z"
  }
}
```

---

#### **DELETE** `/api/places/{placeId}/favorite`
Remove a place from favorites.

**Auth:** Required
**Quota:** Not counted

**Response:** `204 No Content`

---

#### **POST** `/api/places/{placeId}/exclude`
Exclude a place from recommendations.

**Auth:** Required
**Quota:** Not counted

**Request:**
```json
{
  "placeName": "Crowded Tourist Trap",
  "reason": "Too crowded, bad experience",
  "daysToExpire": 90
}
```

**Notes:**
- `daysToExpire: null` = Permanent exclusion
- `daysToExpire: 30` = Exclude for 30 days

**Response:** `200 OK`
```json
{
  "success": true,
  "message": "Place excluded from recommendations",
  "exclusion": {
    "id": "exc_001",
    "placeId": "place_001",
    "excludedAt": "2025-11-10T10:30:00Z",
    "expiresAt": "2026-02-08T10:30:00Z",
    "isPermanent": false
  }
}
```

---

### Routes Endpoints

#### **POST** `/api/routes/surprise`
Generate a personalized "Surprise Me" route using AI.

**Auth:** Required
**Quota:** ✅ Counted (1 credit)

**Request:**
```json
{
  "targetArea": "Kadıköy, Istanbul",
  "latitude": 40.9880,
  "longitude": 29.0265,
  "radiusMeters": 5000,
  "minStops": 3,
  "maxStops": 6,
  "budgetLevel": "medium",
  "preferredCategories": ["cafe", "museum", "restaurant"],
  "transportationMode": "walking",
  "timeWindowStart": "09:00",
  "timeWindowEnd": "18:00",
  "includeReasoning": true,
  "saveToHistory": true
}
```

**Response:** `200 OK`
```json
{
  "route": {
    "id": "route_001",
    "name": "Surprise Me - Kadıköy",
    "description": "AI-generated personalized route",
    "totalDistance": 4200,
    "estimatedDuration": 180,
    "stopCount": 5,
    "transportationMode": "walking",
    "routeType": "surprise_me"
  },
  "suggestedPlaces": [
    {
      "placeId": "place_123",
      "name": "Çiya Sofrası",
      "category": "restaurant",
      "latitude": 40.9880,
      "longitude": 29.0265,
      "rating": 4.6,
      "priceLevel": 2,
      "routeOrder": 1,
      "reason": "Authentic Turkish cuisine, perfect for lunch",
      "personalizationScore": 0.92,
      "isFavorite": false,
      "previouslyVisited": false,
      "distanceFromPrevious": null,
      "travelTimeFromPrevious": null
    },
    {
      "placeId": "place_456",
      "name": "Moda Sahili",
      "category": "park",
      "latitude": 40.9850,
      "longitude": 29.0300,
      "routeOrder": 2,
      "reason": "Scenic waterfront, great for afternoon walk",
      "personalizationScore": 0.88,
      "distanceFromPrevious": 850,
      "travelTimeFromPrevious": 12
    }
  ],
  "reasoning": "This diverse route includes restaurant, park, cafe and more, offering a well-rounded experience of the area.",
  "diversityScore": 0.8,
  "personalizationScore": 0.85,
  "sessionId": "session_abc123",
  "savedToHistory": true
}
```

**Key Features:**
- ✅ Respects favorites (boosts their score)
- ✅ Respects exclusions (filters them out)
- ✅ Exclusion window (doesn't show last 3 suggested places)
- ✅ Diversity algorithm (max 2 places per category)
- ✅ Route optimization (TSP solver)
- ✅ AI reasoning for each place
- ✅ Saves to MRU history (max 3 routes)

---

#### **GET** `/api/routes`
Get all saved routes.

**Auth:** Required
**Quota:** Not counted

**Response:** `200 OK`
```json
[
  {
    "id": "route_001",
    "name": "Istanbul Day Tour",
    "totalDistance": 12500,
    "estimatedDuration": 360,
    "stopCount": 5,
    "createdAt": "2025-01-20T10:00:00Z"
  }
]
```

---

#### **POST** `/api/routes`
Create a custom route.

**Auth:** Required
**Quota:** Not counted

**Request:**
```json
{
  "name": "My Weekend Route",
  "description": "Exploring local cafes",
  "placeIds": ["place_001", "place_002", "place_003"],
  "optimizeOrder": true,
  "transportationMode": "walking"
}
```

**Response:** `201 Created`
```json
{
  "id": "route_123",
  "name": "My Weekend Route",
  "stopCount": 3,
  "createdAt": "2025-11-10T11:00:00Z"
}
```

---

### User History Endpoints

#### **GET** `/api/users/{userId}/history/routes`
Get user's route history (MRU - last 3 routes).

**Auth:** Required (must be same user)
**Quota:** Not counted

**Query Parameters:**
```
take (optional): int - Number of routes (default: 3, max: 10)
```

**Example:**
```http
GET /api/users/3fa85f64-5717-4562-b3fc-2c963f66afa6/history/routes?take=3
```

**Response:** `200 OK`
```json
{
  "success": true,
  "data": [
    {
      "id": "hist_001",
      "routeName": "Surprise Me - Istanbul",
      "routeId": "route_123",
      "placeCount": 5,
      "source": "surprise_me",
      "createdAt": "2025-11-10T10:00:00Z",
      "sequenceNumber": 3
    },
    {
      "id": "hist_002",
      "routeName": "Custom Route",
      "routeId": "route_456",
      "placeCount": 3,
      "source": "manual",
      "createdAt": "2025-11-09T15:00:00Z",
      "sequenceNumber": 2
    }
  ],
  "metadata": {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "count": 2,
    "mruLimit": 3
  }
}
```

---

#### **GET** `/api/users/{userId}/history/places`
Get user's suggestion history (MRU - last 20 places).

**Auth:** Required
**Quota:** Not counted

**Query Parameters:**
```
take (optional): int - Default: 20, max: 50
```

**Response:** `200 OK`
```json
{
  "success": true,
  "data": [
    {
      "id": "sugg_001",
      "placeId": "place_789",
      "placeName": "Blue Mosque",
      "category": "mosque",
      "source": "surprise_me",
      "suggestedAt": "2025-11-10T10:30:00Z",
      "sessionId": "session_abc123",
      "sequenceNumber": 20
    }
  ],
  "metadata": {
    "count": 15,
    "mruLimit": 20
  }
}
```

---

#### **GET** `/api/users/{userId}/favorites`
Get user's favorite places.

**Auth:** Required
**Quota:** Not counted

**Response:** `200 OK`
```json
{
  "success": true,
  "data": [
    {
      "id": "fav_001",
      "placeId": "place_123",
      "placeName": "Çiya Sofrası",
      "category": "restaurant",
      "latitude": 40.9880,
      "longitude": 29.0265,
      "notes": "Best Turkish food!",
      "addedAt": "2025-11-05T12:00:00Z"
    }
  ],
  "count": 5
}
```

---

#### **GET** `/api/users/{userId}/exclusions`
Get user's excluded places (active only).

**Auth:** Required
**Quota:** Not counted

**Response:** `200 OK`
```json
{
  "success": true,
  "data": [
    {
      "id": "exc_001",
      "placeId": "place_456",
      "placeName": "Tourist Trap",
      "reason": "Too crowded",
      "excludedAt": "2025-11-01T10:00:00Z",
      "expiresAt": "2025-12-01T10:00:00Z",
      "isPermanent": false
    }
  ],
  "count": 3
}
```

---

### Day Planning Endpoints

#### **POST** `/api/dayplan/ai-generate`
Generate AI-driven daily itinerary.

**Auth:** Required
**Quota:** ✅ Counted (1 credit)

**Request:**
```json
{
  "location": "Istanbul, Turkey",
  "latitude": 41.0082,
  "longitude": 28.9784,
  "startTime": "09:00:00",
  "endTime": "20:00:00",
  "preferredActivities": ["cultural", "food", "shopping"],
  "budgetLevel": "medium",
  "maxStops": 6,
  "saveAsRoute": true
}
```

**Response:** `200 OK`
```json
{
  "id": "plan_001",
  "title": "A Perfect Day in Istanbul",
  "description": "AI-curated itinerary",
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

---

### User Feedback Endpoints

#### **POST** `/api/feedback/rate`
Submit rating and feedback for a place.

**Auth:** Required
**Quota:** Not counted

**Request:**
```json
{
  "placeId": "place_001",
  "rating": 5,
  "review": "Amazing place, highly recommend!",
  "wouldRecommend": true,
  "confirmVisit": true,
  "visitDurationMinutes": 90
}
```

**Response:** `200 OK`
```json
{
  "message": "Feedback received successfully",
  "rating": 5
}
```

---

### Analytics Endpoints

#### **GET** `/api/analytics`
Get user analytics.

**Auth:** Required
**Quota:** Not counted

**Response:** `200 OK`
```json
{
  "totalVisits": 127,
  "totalFeedback": 43,
  "averageRating": 4.6,
  "topCategories": [
    { "category": "restaurant", "count": 38 },
    { "category": "museum", "count": 22 }
  ],
  "visitsByMonth": {
    "2025-10": 25,
    "2025-11": 15
  }
}
```

---

### Health & Status Endpoints

#### **GET** `/health`
Simple health check.

**Auth:** None
**Quota:** Not counted

**Response:** `200 OK`
```json
{
  "status": "ok"
}
```

---

#### **GET** `/health/ready`
Readiness check (includes dependencies).

**Auth:** None
**Quota:** Not counted

**Response:** `200 OK`
```json
{
  "status": "Healthy",
  "entries": {
    "redis": {
      "status": "Healthy",
      "latency_ms": 3.45
    },
    "postgres": {
      "status": "Healthy",
      "latency_ms": 12.34
    }
  }
}
```

---

## 🎭 User Journeys & Scenarios

### Scenario 1: New User Onboarding

```javascript
// Step 1: Register
const registerResponse = await fetch('/api/auth/register', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    email: 'newuser@example.com',
    password: 'SecurePass123!',
    username: 'newuser'
  })
});
const { token, user } = await registerResponse.json();

// Step 2: Get nearby suggestions (first-time, no personalization yet)
const suggestionsResponse = await fetch(
  '/api/discover?lat=41.0082&lng=28.9784&radius=5000',
  { headers: { 'Authorization': `Bearer ${token}` } }
);
const { suggestions } = await suggestionsResponse.json();

// Step 3: User likes a place - add to favorites
await fetch(`/api/places/${suggestions[0].id}/favorite`, {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    placeName: suggestions[0].name,
    category: suggestions[0].category,
    latitude: suggestions[0].location.latitude,
    longitude: suggestions[0].location.longitude,
    notes: 'Looks interesting!'
  })
});

// Step 4: Submit feedback after visiting
await fetch('/api/feedback/rate', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    placeId: suggestions[0].id,
    rating: 5,
    review: 'Great place!',
    wouldRecommend: true,
    confirmVisit: true
  })
});
```

---

### Scenario 2: Returning User - Surprise Me

```javascript
// User has existing preferences, favorites, and history

// Step 1: Generate Surprise Me route
const surpriseResponse = await fetch('/api/routes/surprise', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    targetArea: 'Beyoğlu, Istanbul',
    latitude: 41.0363,
    longitude: 28.9795,
    radiusMeters: 5000,
    minStops: 4,
    maxStops: 7,
    budgetLevel: 'medium',
    preferredCategories: ['cafe', 'museum', 'restaurant'],
    includeReasoning: true,
    saveToHistory: true
  })
});

const {
  route,
  suggestedPlaces,
  reasoning,
  diversityScore,
  personalizationScore
} = await surpriseResponse.json();

// Step 2: User doesn't like one of the suggestions - exclude it
await fetch(`/api/places/${suggestedPlaces[2].placeId}/exclude`, {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    placeName: suggestedPlaces[2].name,
    reason: 'Not my style',
    daysToExpire: 30 // Exclude for 30 days
  })
});

// Step 3: Generate new route (excluded place won't appear)
const newSurprise = await fetch('/api/routes/surprise', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    targetArea: 'Beyoğlu, Istanbul',
    latitude: 41.0363,
    longitude: 28.9795,
    radiusMeters: 5000,
    minStops: 5,
    maxStops: 7
  })
});
```

---

### Scenario 3: AI-Powered Search

```javascript
// Step 1: Search with natural language
const searchResponse = await fetch('/api/places/search', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    query: 'romantic dinner with sea view, not too expensive',
    latitude: 41.0082,
    longitude: 28.9784,
    radius: 10000,
    filters: {
      minRating: 4.0,
      openNow: true
    }
  })
});

const { data: { places }, metadata } = await searchResponse.json();

// Step 2: Get AI summary for top result
const summaryResponse = await fetch(
  `/api/places/${places[0].id}/summary?style=detailed`,
  { headers: { 'Authorization': `Bearer ${token}` } }
);

const { data: { summary, highlights, bestTimeToVisit } } = await summaryResponse.json();

// Display to user
console.log(`Summary: ${summary}`);
console.log(`Best time: ${bestTimeToVisit}`);
console.log(`Highlights: ${highlights.join(', ')}`);
```

---

### Scenario 4: Handling Quota Exhaustion

```javascript
// Free user with 5 request quota

// Make discovery request
const response = await fetch('/api/discover?lat=41&lng=28', {
  headers: { 'Authorization': `Bearer ${token}` }
});

// Check if quota exhausted
if (response.status === 403) {
  const error = await response.json();

  if (error.type === 'quota_exhausted') {
    // Show upgrade modal
    showUpgradeModal({
      remaining: error.remainingQuota,
      limit: error.quotaLimit,
      message: error.detail,
      // Show upgrade options
      upgradeUrl: '/subscribe/premium'
    });
  }
}

// Alternative: Check quota before request
const usageResponse = await fetch('/api/auth/usage', {
  headers: { 'Authorization': `Bearer ${token}` }
});
const { dailyUsage, dailyLimit } = await usageResponse.json();

if (dailyUsage >= dailyLimit) {
  // Show "upgrade to continue" message
  showUpgradePrompt();
} else {
  // Proceed with feature request
  const remaining = dailyLimit - dailyUsage;
  console.log(`${remaining} requests remaining`);
}
```

---

### Scenario 5: Managing History

```javascript
// View recent routes
const routeHistoryResponse = await fetch(
  `/api/users/${userId}/history/routes?take=3`,
  { headers: { 'Authorization': `Bearer ${token}` } }
);
const { data: recentRoutes } = await routeHistoryResponse.json();

// View recently suggested places
const placeHistoryResponse = await fetch(
  `/api/users/${userId}/history/places?take=20`,
  { headers: { 'Authorization': `Bearer ${token}` } }
);
const { data: recentPlaces } = await placeHistoryResponse.json();

// View all favorites
const favoritesResponse = await fetch(
  `/api/users/${userId}/favorites`,
  { headers: { 'Authorization': `Bearer ${token}` } }
);
const { data: favorites } = await favoritesResponse.json();

// View active exclusions
const exclusionsResponse = await fetch(
  `/api/users/${userId}/exclusions`,
  { headers: { 'Authorization': `Bearer ${token}` } }
);
const { data: exclusions } = await exclusionsResponse.json();

// Display summary
console.log(`Recent routes: ${recentRoutes.length}/3 (MRU limit)`);
console.log(`Recent suggestions: ${recentPlaces.length}/20 (MRU limit)`);
console.log(`Favorites: ${favorites.length}`);
console.log(`Exclusions: ${exclusions.length}`);
```

---

## 💻 TypeScript Integration

### Complete Type Definitions

```typescript
// ============== Auth Types ==============
interface RegisterRequest {
  email: string;
  password: string;
  username: string;
  fullName?: string;
}

interface LoginRequest {
  email: string;
  password: string;
}

interface AuthResponse {
  token: string;
  user: User;
}

interface User {
  id: string;
  email: string;
  username: string;
  fullName?: string;
  subscriptionTier: 'Free' | 'Premium' | 'Enterprise';
  isSubscriptionActive: boolean;
  subscriptionExpiry?: string;
  dailyApiUsage: number;
  dailyApiLimit: number;
  createdAt: string;
  lastLoginAt?: string;
}

// ============== Discovery Types ==============
interface DiscoverRequest {
  lat: number;
  lng: number;
  radius?: number;
}

interface PromptRequest {
  prompt: string;
  latitude?: number;
  longitude?: number;
  radius?: number;
  sortBy?: 'rating' | 'distance' | 'popularity';
}

interface DiscoverResponse {
  personalized: boolean;
  userId?: string;
  suggestions: Suggestion[];
}

interface Suggestion {
  id: string;
  name: string;
  category: string;
  rating: number;
  userRatingsTotal: number;
  location: {
    latitude: number;
    longitude: number;
  };
  address: string;
  distance: number;
  distanceText: string;
  photoUrl?: string;
  types: string[];
  openNow?: boolean;
  priceLevel?: 'FREE' | '$' | '$$' | '$$$' | '$$$$';
  personalizedScore?: number;
  reasons?: string[];
}

// ============== Surprise Me Types ==============
interface SurpriseMeRequest {
  targetArea: string;
  latitude: number;
  longitude: number;
  radiusMeters?: number;
  timeWindowStart?: string;
  timeWindowEnd?: string;
  isFullDay?: boolean;
  preferredCategories?: string[];
  budgetLevel?: 'low' | 'medium' | 'high';
  minStops?: number;
  maxStops?: number;
  transportationMode?: 'walking' | 'driving' | 'transit';
  includeReasoning?: boolean;
  saveToHistory?: boolean;
}

interface SurpriseMeResponse {
  route: RouteDto;
  suggestedPlaces: SurpriseMePlaceDto[];
  reasoning?: string;
  diversityScore: number;
  personalizationScore: number;
  sessionId: string;
  savedToHistory: boolean;
}

interface SurpriseMePlaceDto {
  placeId: string;
  name: string;
  category?: string;
  latitude: number;
  longitude: number;
  address?: string;
  rating?: number;
  priceLevel?: number;
  photoUrl?: string;
  routeOrder: number;
  reason?: string;
  personalizationScore: number;
  isFavorite: boolean;
  previouslyVisited: boolean;
  distanceFromPrevious?: number;
  travelTimeFromPrevious?: number;
}

// ============== Route Types ==============
interface RouteDto {
  id: string;
  name: string;
  description?: string;
  userId: string;
  totalDistance: number;
  estimatedDuration: number;
  stopCount: number;
  transportationMode: string;
  routeType?: string;
  tags?: string[];
  isPublic: boolean;
  createdAt: string;
  updatedAt: string;
  points?: RoutePointDto[];
}

interface RoutePointDto {
  id: string;
  order: number;
  name: string;
  latitude: number;
  longitude: number;
  visitDuration?: number;
  notes?: string;
}

interface CreateRouteRequest {
  name: string;
  description?: string;
  placeIds?: string[];
  optimizeOrder?: boolean;
  transportationMode?: string;
  tags?: string[];
  isPublic?: boolean;
}

// ============== Favorites & Exclusions ==============
interface AddFavoriteRequest {
  placeName: string;
  category?: string;
  latitude?: number;
  longitude?: number;
  notes?: string;
}

interface ExcludePlaceRequest {
  placeName: string;
  reason?: string;
  daysToExpire?: number; // null = permanent
}

interface UserFavorite {
  id: string;
  placeId: string;
  placeName: string;
  category?: string;
  latitude?: number;
  longitude?: number;
  notes?: string;
  addedAt: string;
}

interface UserExclusion {
  id: string;
  placeId: string;
  placeName: string;
  reason?: string;
  excludedAt: string;
  expiresAt?: string;
  isPermanent: boolean;
}

// ============== History Types ==============
interface RouteHistoryItem {
  id: string;
  routeName: string;
  routeId?: string;
  placeCount: number;
  source: string;
  createdAt: string;
  sequenceNumber: number;
}

interface SuggestionHistoryItem {
  id: string;
  placeId: string;
  placeName: string;
  category?: string;
  source: string;
  suggestedAt: string;
  sessionId?: string;
  sequenceNumber: number;
}

// ============== Feedback Types ==============
interface FeedbackRequest {
  placeId: string;
  rating: number;
  review?: string;
  wouldRecommend: boolean;
  confirmVisit?: boolean;
  visitDurationMinutes?: number;
}

// ============== AI Search Types ==============
interface PlaceSearchRequest {
  query: string;
  latitude?: number;
  longitude?: number;
  radius?: number;
  filters?: {
    minRating?: number;
    priceLevel?: string;
    openNow?: boolean;
  };
}

interface PlaceSearchResponse {
  success: boolean;
  data: {
    places: PlaceResult[];
  };
  metadata: {
    usedAI: boolean;
    aiConfidence: number;
    interpretedQuery: string;
    extractedCategories?: string[];
  };
}

interface PlaceResult {
  id: string;
  name: string;
  rating: number;
  priceLevel?: string;
  cuisineType?: string;
  category: string;
  location: {
    latitude: number;
    longitude: number;
  };
}

// ============== Day Plan Types ==============
interface AIItineraryRequest {
  location: string;
  latitude: number;
  longitude: number;
  startTime: string;
  endTime: string;
  preferredActivities?: string[];
  budgetLevel?: 'low' | 'medium' | 'high';
  maxStops?: number;
  saveAsRoute?: boolean;
}

interface AIItinerary {
  id: string;
  title: string;
  description?: string;
  stops: ItineraryStop[];
  totalDurationMinutes: number;
  totalDistanceMeters: number;
  reasoning?: string;
}

interface ItineraryStop {
  order: number;
  place: {
    name: string;
    latitude: number;
    longitude: number;
  };
  arrivalTime: string;
  durationMinutes: number;
  activityType: string;
  reason?: string;
}

// ============== Error Types ==============
interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail: string;
  instance: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

interface QuotaExhaustedError extends ProblemDetails {
  remainingQuota: number;
  quotaLimit: number;
  subscriptionRequired: boolean;
}

// ============== Analytics Types ==============
interface UserAnalytics {
  totalVisits: number;
  totalFeedback: number;
  averageRating: number;
  topCategories: Array<{
    category: string;
    count: number;
  }>;
  visitsByMonth: Record<string, number>;
}
```

---

### React Hook Example

```typescript
// useAPI.ts
import { useState, useEffect } from 'react';

interface UseAPIOptions {
  token?: string;
  onError?: (error: Error) => void;
}

export function useAPI<T>(
  endpoint: string,
  options: UseAPIOptions = {}
): {
  data: T | null;
  loading: boolean;
  error: Error | null;
  refetch: () => Promise<void>;
} {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  const fetchData = async () => {
    setLoading(true);
    setError(null);

    try {
      const headers: Record<string, string> = {
        'Content-Type': 'application/json',
      };

      if (options.token) {
        headers['Authorization'] = `Bearer ${options.token}`;
      }

      const response = await fetch(`http://localhost:5000${endpoint}`, {
        headers,
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.detail || 'API request failed');
      }

      const jsonData = await response.json();
      setData(jsonData);
    } catch (err) {
      const error = err as Error;
      setError(error);
      options.onError?.(error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, [endpoint]);

  return { data, loading, error, refetch: fetchData };
}

// Usage in component
function NearbyPlaces() {
  const token = useAuth(); // Your auth hook
  const { data, loading, error } = useAPI<DiscoverResponse>(
    '/api/discover?lat=41.0082&lng=28.9784&radius=5000',
    { token }
  );

  if (loading) return <div>Loading...</div>;
  if (error) return <div>Error: {error.message}</div>;

  return (
    <div>
      {data?.suggestions.map(place => (
        <PlaceCard key={place.id} place={place} />
      ))}
    </div>
  );
}
```

---

### API Client Class

```typescript
// apiClient.ts
class WhatShouldIDoAPI {
  private baseURL: string;
  private token: string | null = null;

  constructor(baseURL: string = 'http://localhost:5000') {
    this.baseURL = baseURL;
  }

  setToken(token: string) {
    this.token = token;
    localStorage.setItem('auth_token', token);
  }

  clearToken() {
    this.token = null;
    localStorage.removeItem('auth_token');
  }

  private async request<T>(
    endpoint: string,
    options: RequestInit = {}
  ): Promise<T> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      ...((options.headers as Record<string, string>) || {}),
    };

    if (this.token) {
      headers['Authorization'] = `Bearer ${this.token}`;
    }

    const response = await fetch(`${this.baseURL}${endpoint}`, {
      ...options,
      headers,
    });

    if (!response.ok) {
      if (response.status === 401) {
        this.clearToken();
        throw new Error('Authentication required');
      }

      const error = await response.json();
      throw new Error(error.detail || 'Request failed');
    }

    if (response.status === 204) {
      return {} as T;
    }

    return response.json();
  }

  // Auth methods
  async register(data: RegisterRequest): Promise<AuthResponse> {
    const response = await this.request<AuthResponse>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify(data),
    });
    this.setToken(response.token);
    return response;
  }

  async login(data: LoginRequest): Promise<AuthResponse> {
    const response = await this.request<AuthResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify(data),
    });
    this.setToken(response.token);
    return response;
  }

  async logout(): Promise<void> {
    await this.request('/api/auth/logout', { method: 'POST' });
    this.clearToken();
  }

  // Discovery methods
  async discover(params: DiscoverRequest): Promise<DiscoverResponse> {
    const query = new URLSearchParams({
      lat: params.lat.toString(),
      lng: params.lng.toString(),
      ...(params.radius && { radius: params.radius.toString() }),
    });
    return this.request<DiscoverResponse>(`/api/discover?${query}`);
  }

  async searchByPrompt(data: PromptRequest): Promise<DiscoverResponse> {
    return this.request<DiscoverResponse>('/api/discover/prompt', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  // Surprise Me
  async generateSurpriseRoute(
    data: SurpriseMeRequest
  ): Promise<SurpriseMeResponse> {
    return this.request<SurpriseMeResponse>('/api/routes/surprise', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  // Favorites
  async addFavorite(
    placeId: string,
    data: AddFavoriteRequest
  ): Promise<{ success: boolean; favorite: UserFavorite }> {
    return this.request(`/api/places/${placeId}/favorite`, {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async removeFavorite(placeId: string): Promise<void> {
    return this.request(`/api/places/${placeId}/favorite`, {
      method: 'DELETE',
    });
  }

  async getFavorites(userId: string): Promise<UserFavorite[]> {
    const response = await this.request<{
      data: UserFavorite[];
    }>(`/api/users/${userId}/favorites`);
    return response.data;
  }

  // Exclusions
  async excludePlace(
    placeId: string,
    data: ExcludePlaceRequest
  ): Promise<{ success: boolean; exclusion: UserExclusion }> {
    return this.request(`/api/places/${placeId}/exclude`, {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  // History
  async getRouteHistory(
    userId: string,
    take: number = 3
  ): Promise<RouteHistoryItem[]> {
    const response = await this.request<{
      data: RouteHistoryItem[];
    }>(`/api/users/${userId}/history/routes?take=${take}`);
    return response.data;
  }

  async getPlaceHistory(
    userId: string,
    take: number = 20
  ): Promise<SuggestionHistoryItem[]> {
    const response = await this.request<{
      data: SuggestionHistoryItem[];
    }>(`/api/users/${userId}/history/places?take=${take}`);
    return response.data;
  }

  // Feedback
  async submitFeedback(data: FeedbackRequest): Promise<void> {
    return this.request('/api/feedback/rate', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }
}

// Export singleton instance
export const api = new WhatShouldIDoAPI();

// Usage
await api.register({
  email: 'user@example.com',
  password: 'SecurePass123!',
  username: 'johndoe',
});

const suggestions = await api.discover({
  lat: 41.0082,
  lng: 28.9784,
  radius: 5000,
});

const surpriseRoute = await api.generateSurpriseRoute({
  targetArea: 'Istanbul',
  latitude: 41.0082,
  longitude: 28.9784,
  radiusMeters: 5000,
  minStops: 3,
  maxStops: 6,
});
```

---

## 🚨 Error Handling Guide

### Standard Error Response Format (RFC 7807)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "The Prompt field is required.",
  "instance": "/api/discover/prompt",
  "traceId": "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
  "errors": {
    "Prompt": ["The Prompt field is required."]
  }
}
```

### HTTP Status Codes

| Code | Meaning | Action |
|------|---------|--------|
| 200 | OK | Success |
| 201 | Created | Resource created |
| 204 | No Content | Success (no response body) |
| 400 | Bad Request | Fix request data |
| 401 | Unauthorized | Login required |
| 403 | Forbidden | Quota exhausted or insufficient permissions |
| 404 | Not Found | Resource doesn't exist |
| 409 | Conflict | Resource already exists |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Report to team |
| 503 | Service Unavailable | Retry later |

### Error Handling Pattern

```typescript
async function makeAPICall<T>(
  endpoint: string,
  options: RequestInit
): Promise<T> {
  try {
    const response = await fetch(endpoint, options);

    // Handle specific status codes
    switch (response.status) {
      case 401:
        // Token expired or invalid
        redirectToLogin();
        throw new Error('Authentication required');

      case 403:
        // Check if quota exhausted
        const error = await response.json();
        if (error.type === 'quota_exhausted') {
          showUpgradeModal({
            remaining: error.remainingQuota,
            limit: error.quotaLimit,
            message: error.detail,
          });
        }
        throw new Error(error.detail);

      case 429:
        // Rate limit exceeded
        const retryAfter = response.headers.get('Retry-After');
        showToast(`Too many requests. Please retry after ${retryAfter}s`);
        throw new Error('Rate limit exceeded');

      case 500:
      case 503:
        // Server error - retry with exponential backoff
        return retryWithBackoff(() => makeAPICall(endpoint, options));

      default:
        if (!response.ok) {
          const error = await response.json();
          throw new Error(error.detail || 'Request failed');
        }
    }

    return response.json();
  } catch (error) {
    // Network error or timeout
    if (error instanceof TypeError) {
      showToast('Network error. Please check your connection.');
    }
    throw error;
  }
}

// Retry with exponential backoff
async function retryWithBackoff<T>(
  fn: () => Promise<T>,
  maxRetries: number = 3,
  baseDelay: number = 1000
): Promise<T> {
  for (let i = 0; i < maxRetries; i++) {
    try {
      return await fn();
    } catch (error) {
      if (i === maxRetries - 1) throw error;

      const delay = baseDelay * Math.pow(2, i);
      await new Promise(resolve => setTimeout(resolve, delay));
    }
  }
  throw new Error('Max retries exceeded');
}
```

---

## ✅ Best Practices

### 1. Always Include Authorization Header

```javascript
const headers = {
  'Authorization': `Bearer ${getToken()}`,
  'Content-Type': 'application/json',
  'Accept': 'application/json'
};
```

### 2. Check Quota Before Feature Calls

```javascript
// Before making expensive API call
const { dailyUsage, dailyLimit } = await api.getUsage();

if (dailyUsage >= dailyLimit) {
  showUpgradePrompt();
} else {
  const remaining = dailyLimit - dailyUsage;
  if (remaining <= 2) {
    showLowQuotaWarning(remaining);
  }
  // Proceed with request
}
```

### 3. Use Correlation IDs for Debugging

```javascript
import { v4 as uuidv4 } from 'uuid';

const correlationId = uuidv4();
const response = await fetch('/api/discover', {
  headers: {
    'Authorization': `Bearer ${token}`,
    'X-Correlation-Id': correlationId
  }
});

// If error, provide correlation ID to support
if (!response.ok) {
  console.error(`Request failed. Correlation ID: ${correlationId}`);
}
```

### 4. Handle Token Expiration Gracefully

```javascript
// API interceptor
async function apiCall(url, options) {
  let response = await fetch(url, options);

  if (response.status === 401) {
    // Try to refresh token (if refresh endpoint exists)
    // Or redirect to login
    clearToken();
    redirectToLogin();
    return;
  }

  return response;
}
```

### 5. Implement Request Deduplication

```javascript
// Prevent duplicate requests
const pendingRequests = new Map();

async function deduplicatedFetch(key, fn) {
  if (pendingRequests.has(key)) {
    return pendingRequests.get(key);
  }

  const promise = fn();
  pendingRequests.set(key, promise);

  try {
    return await promise;
  } finally {
    pendingRequests.delete(key);
  }
}

// Usage
const suggestions = await deduplicatedFetch(
  'discover-41.0082-28.9784',
  () => api.discover({ lat: 41.0082, lng: 28.9784 })
);
```

### 6. Cache Responses Appropriately

```javascript
// Simple cache with TTL
class APICache {
  private cache = new Map();

  set(key, value, ttlMs = 5 * 60 * 1000) {
    this.cache.set(key, {
      value,
      expiresAt: Date.now() + ttlMs
    });
  }

  get(key) {
    const item = this.cache.get(key);
    if (!item) return null;

    if (Date.now() > item.expiresAt) {
      this.cache.delete(key);
      return null;
    }

    return item.value;
  }
}

const cache = new APICache();

// Use cache for discovery requests
const cacheKey = `discover-${lat}-${lng}`;
let suggestions = cache.get(cacheKey);

if (!suggestions) {
  suggestions = await api.discover({ lat, lng });
  cache.set(cacheKey, suggestions, 5 * 60 * 1000); // 5 min TTL
}
```

### 7. Localization Support

```javascript
// Detect user's preferred language
const userLang = navigator.language || 'en-US';

const response = await fetch('/api/discover', {
  headers: {
    'Authorization': `Bearer ${token}`,
    'Accept-Language': userLang
  }
});
```

---

## 🧪 Testing & Debugging

### Testing with Postman/Thunder Client

**Environment Variables:**
```json
{
  "baseURL": "http://localhost:5000",
  "token": "{{auth_token}}"
}
```

**Test Flow:**
1. Register: `POST {{baseURL}}/api/auth/register`
2. Save token from response
3. Test endpoints with `Authorization: Bearer {{token}}`

### cURL Examples

```bash
# Register
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "SecurePass123!",
    "username": "testuser"
  }'

# Discover (with token)
curl -X GET "http://localhost:5000/api/discover?lat=41.0082&lng=28.9784" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"

# Surprise Me
curl -X POST http://localhost:5000/api/routes/surprise \
  -H "Authorization: Bearer YOUR_TOKEN_HERE" \
  -H "Content-Type: application/json" \
  -d '{
    "targetArea": "Istanbul",
    "latitude": 41.0082,
    "longitude": 28.9784,
    "radiusMeters": 5000,
    "minStops": 3,
    "maxStops": 6
  }'
```

### Debugging Response Headers

```javascript
const response = await fetch('/api/discover?lat=41&lng=28', {
  headers: { 'Authorization': `Bearer ${token}` }
});

// Check quota headers
console.log('Quota Remaining:', response.headers.get('X-Quota-Remaining'));
console.log('Quota Limit:', response.headers.get('X-Quota-Limit'));
console.log('Correlation ID:', response.headers.get('X-Correlation-Id'));

// Check rate limit
console.log('Rate Limit Remaining:', response.headers.get('X-RateLimit-Remaining'));
```

### Health Check

```javascript
// Check API health before starting
const health = await fetch('http://localhost:5000/health/ready');
const status = await health.json();

if (status.status !== 'Healthy') {
  console.error('API not ready:', status);
  showMaintenanceMode();
}
```

---

## 📞 Support & Resources

- **Swagger UI:** [http://localhost:5000/swagger](http://localhost:5000/swagger) (Development)
- **Production API:** [https://api.whatshouldido.com](https://api.whatshouldido.com)
- **GitHub Issues:** [https://github.com/your-org/whatshouldido/issues](https://github.com/your-org/whatshouldido/issues)
- **Email Support:** dev-support@whatshouldido.com

---

## 📄 Changelog

### Version 3.0.0 (November 10, 2025)
- ✅ Added "Surprise Me" personalized route generation
- ✅ Added AI-powered place search
- ✅ Added favorites and exclusions management
- ✅ Added MRU-based history tracking (routes & places)
- ✅ Added exclusion window logic
- ✅ Enhanced personalization scoring

### Version 2.0.0 (January 24, 2025)
- ✅ Added AI-driven daily itinerary generation
- ✅ Multiple AI provider support (OpenAI, HuggingFace, Ollama)
- ✅ Route optimization with TSP solver
- ✅ Comprehensive testing suite

### Version 1.0.0
- ✅ Initial release
- ✅ Basic discovery and route planning
- ✅ User authentication

---

**Last Updated:** November 10, 2025
**API Version:** 3.0.0
**Maintained by:** WhatShouldIDo Development Team
