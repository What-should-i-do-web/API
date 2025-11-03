# WhatShouldIDo API - Frontend Developer Guide

**Version:** 2.0.0
**Base URL:** `https://api.whatshouldido.com` (Production) | `http://localhost:5000` (Development)
**Last Updated:** 2025-01-24

---

## Table of Contents

1. [Overview](#overview)
2. [Authentication](#authentication)
3. [Common Patterns](#common-patterns)
4. [Error Handling](#error-handling)
5. [Rate Limiting & Quotas](#rate-limiting--quotas)
6. [API Endpoints](#api-endpoints)
   - [Authentication](#authentication-endpoints)
   - [Discovery](#discovery-endpoints)
   - [Routes](#routes-endpoints)
   - [POIs](#pois-endpoints)
   - [Day Planning](#day-planning-endpoints)
   - [User Feedback](#user-feedback-endpoints)
   - [Analytics](#analytics-endpoints)
   - [Context](#context-endpoints)
   - [Localization](#localization-endpoints)
   - [Health](#health-endpoints)
7. [Request/Response Examples](#request-response-examples)
8. [TypeScript Types](#typescript-types)

---

## Overview

The WhatShouldIDo API provides intelligent location-based suggestions for activities, places, and day plans. It uses AI-powered personalization to adapt recommendations based on user preferences and behavior.

### Key Features

- 🎯 **Smart Discovery**: AI-powered place recommendations based on location and preferences
- 🗺️ **Route Planning**: Create and manage custom routes with multiple points
- 📅 **Day Planning**: Generate full-day itineraries optimized for time and preferences
- 👤 **Personalization**: Learns from user behavior for better suggestions
- 🌍 **Localization**: Multi-language support (10+ languages)
- ⚡ **Real-time Context**: Weather-aware and time-sensitive recommendations

### Architecture

- **Backend**: ASP.NET Core 9.0 with Clean Architecture
- **Database**: PostgreSQL for data persistence
- **Cache**: Redis for high-performance caching
- **Monitoring**: OpenTelemetry with Prometheus/Grafana

---

## Authentication

### JWT Bearer Token

All protected endpoints require a JWT bearer token in the Authorization header.

**Header Format:**
```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Token Lifecycle

- **Expiration**: 60 minutes (production) / 120 minutes (development)
- **Refresh**: Not implemented yet (logout/login required)
- **Claims**: `sub` (user ID), `email`, `subscription` (tier), `role`

### Getting a Token

1. Register a new account: `POST /api/auth/register`
2. Login: `POST /api/auth/login` → Returns `{ token, user }`
3. Store token in localStorage or secure cookie
4. Include token in all subsequent requests

---

## Common Patterns

### Standard Headers

```http
Content-Type: application/json
Accept: application/json
Authorization: Bearer {token}
Accept-Language: en-US (optional, for localization)
X-Correlation-Id: {your-trace-id} (optional, for debugging)
```

### Pagination

Most list endpoints support pagination via query parameters:

```
?page=1&pageSize=20
```

### Filtering

Use query parameters for filtering:

```
?minRating=4.0&maxDistance=5000&category=restaurant
```

### Sorting

```
?sortBy=rating&sortOrder=desc
```

---

## Error Handling

### Standard Error Response (RFC 7807 Problem Details)

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

| Code | Meaning | When It Happens |
|------|---------|-----------------|
| 200 | OK | Request succeeded |
| 201 | Created | Resource created successfully |
| 204 | No Content | Delete/update succeeded, no response body |
| 400 | Bad Request | Invalid input data |
| 401 | Unauthorized | Missing or invalid token |
| 403 | Forbidden | Insufficient permissions or quota exhausted |
| 404 | Not Found | Resource doesn't exist |
| 409 | Conflict | Resource already exists (e.g., email taken) |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Server-side error (report to team) |
| 503 | Service Unavailable | Maintenance or overload |

### Quota Exhausted Response

```json
{
  "type": "quota_exhausted",
  "title": "Quota Exhausted",
  "status": 403,
  "detail": "You have used all 5 free requests. Please subscribe to continue using the service.",
  "instance": "/api/discover",
  "remainingQuota": 0,
  "quotaLimit": 5,
  "subscriptionRequired": true
}
```

---

## Rate Limiting & Quotas

### Rate Limits

**Authenticated Users:**
- 100 requests per minute (configurable)
- Header: `X-RateLimit-Remaining: 95`

**Anonymous Users:**
- 20 requests per minute
- Limited to non-authenticated endpoints only

### Quota System

**Free Users:**
- 5 total requests to feature endpoints (`/api/discover`, `/api/dayplan`)
- Quota does NOT reset daily by default
- Tracked per user across all devices

**Premium Users:**
- Unlimited requests
- No quota checks applied

**Response Headers:**
```http
X-Quota-Remaining: 3
X-Quota-Limit: 5
X-Correlation-Id: a1b2c3d4e5f6...
```

---

## API Endpoints

## Authentication Endpoints

### Register

Create a new user account.

**Endpoint:** `POST /api/auth/register`
**Auth:** None (anonymous)
**Quota:** Not counted

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123!",
  "username": "johndoe",
  "fullName": "John Doe"
}
```

**Validation Rules:**
- `email`: Valid email format, unique
- `password`: Min 8 chars, requires uppercase, lowercase, digit, special char
- `username`: Min 3 chars, alphanumeric + underscore
- `fullName`: Optional

**Success Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "user@example.com",
    "username": "johndoe",
    "fullName": "John Doe",
    "subscriptionTier": "Free",
    "isSubscriptionActive": false,
    "dailyApiUsage": 0,
    "dailyApiLimit": 5,
    "createdAt": "2025-01-24T10:30:00Z"
  }
}
```

**Error Response (409 Conflict):**
```json
{
  "error": "User with this email already exists"
}
```

---

### Login

Authenticate and receive a JWT token.

**Endpoint:** `POST /api/auth/login`
**Auth:** None (anonymous)
**Quota:** Not counted

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123!"
}
```

**Success Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "user@example.com",
    "username": "johndoe",
    "subscriptionTier": "Premium",
    "isSubscriptionActive": true,
    "subscriptionExpiry": "2026-01-24T10:30:00Z"
  }
}
```

**Error Response (401 Unauthorized):**
```json
{
  "error": "Invalid email or password"
}
```

---

### Get Current User

Retrieve the authenticated user's profile.

**Endpoint:** `GET /api/auth/me`
**Auth:** Required (Bearer token)
**Quota:** Not counted

**Success Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "username": "johndoe",
  "fullName": "John Doe",
  "subscriptionTier": "Premium",
  "isSubscriptionActive": true,
  "subscriptionExpiry": "2026-01-24T10:30:00Z",
  "dailyApiUsage": 47,
  "dailyApiLimit": 1000,
  "createdAt": "2024-06-15T08:00:00Z",
  "lastLoginAt": "2025-01-24T09:15:00Z"
}
```

---

### Update Profile

Update user profile information.

**Endpoint:** `PUT /api/auth/profile`
**Auth:** Required
**Quota:** Not counted

**Request Body:**
```json
{
  "username": "johndoe_updated",
  "fullName": "John Michael Doe",
  "bio": "Travel enthusiast and foodie"
}
```

**Success Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "username": "johndoe_updated",
  "fullName": "John Michael Doe",
  "bio": "Travel enthusiast and foodie",
  "updatedAt": "2025-01-24T10:45:00Z"
}
```

---

### Get API Usage

Check current quota/usage statistics.

**Endpoint:** `GET /api/auth/usage`
**Auth:** Required
**Quota:** Not counted

**Success Response (200 OK):**
```json
{
  "dailyUsage": 3,
  "dailyLimit": 5,
  "subscriptionTier": "Free",
  "subscriptionActive": false,
  "subscriptionExpiry": null
}
```

---

### Logout

Logout the user (client-side token removal).

**Endpoint:** `POST /api/auth/logout`
**Auth:** Required
**Quota:** Not counted

**Success Response (200 OK):**
```json
{
  "message": "Logout successful"
}
```

**Note:** With JWT, logout is primarily client-side. Remove the token from storage after calling this endpoint.

---

## Discovery Endpoints

### Discover Nearby Places

Get smart suggestions for places near a location. Returns personalized results for authenticated users.

**Endpoint:** `GET /api/discover`
**Auth:** Optional (personalized if authenticated)
**Quota:** ✅ Counted (1 credit)

**Query Parameters:**
```
lat (required): float - Latitude (-90 to 90)
lng (required): float - Longitude (-180 to 180)
radius (optional): int - Search radius in meters (default: 3000, max: 50000)
```

**Example Request:**
```http
GET /api/discover?lat=41.0082&lng=28.9784&radius=5000
Authorization: Bearer {token}
```

**Success Response (200 OK):**
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
      "userRatingsTotal": 15234,
      "location": {
        "latitude": 41.0054,
        "longitude": 28.9768
      },
      "address": "Sultan Ahmet, Atmeydanı Cd. No:7, 34122 Fatih/İstanbul",
      "distance": 342,
      "distanceText": "342m",
      "photoUrl": "https://places.googleapis.com/v1/places/xyz/media/abc",
      "types": ["mosque", "place_of_worship", "tourist_attraction"],
      "openNow": true,
      "priceLevel": "FREE",
      "personalizedScore": 0.92,
      "reasons": ["Highly rated", "Close to you", "Popular tourist spot"]
    }
  ]
}
```

**For Anonymous Users:**
```json
{
  "personalized": false,
  "suggestions": [ /* same structure */ ]
}
```

---

### Get Random Suggestion

Get a single random place suggestion. Great for "surprise me" features.

**Endpoint:** `GET /api/discover/random`
**Auth:** Optional (personalized if authenticated)
**Quota:** ✅ Counted (1 credit)

**Query Parameters:**
```
lat (required): float
lng (required): float
radius (optional): int - Default: 3000
```

**Example Request:**
```http
GET /api/discover/random?lat=41.0082&lng=28.9784
```

**Success Response (200 OK):**
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
    "distance": 1234,
    "openNow": true,
    "personalizedScore": 0.85
  }
}
```

**Error Response (404 Not Found):**
```json
{
  "error": "Uygun mekan bulunamadı."
}
```

---

### Prompt-Based Discovery

Get suggestions based on natural language prompt (e.g., "romantic dinner place").

**Endpoint:** `POST /api/discover/prompt`
**Auth:** Optional (personalized if authenticated)
**Quota:** ✅ Counted (1 credit)

**Request Body:**
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
- "romantic dinner place"
- "cheap eats near me"
- "museums and historical sites"
- "nightlife and bars"

**Success Response (200 OK):**
```json
{
  "personalized": true,
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "suggestions": [
    {
      "id": "place_123",
      "name": "Sunset Grill & Bar",
      "category": "restaurant",
      "rating": 4.7,
      "priceLevel": "$$$$",
      "location": {
        "latitude": 41.0445,
        "longitude": 29.0347
      },
      "distance": 5234,
      "matchScore": 0.94,
      "matchReasons": ["Sea view", "Romantic ambiance", "High ratings"],
      "cuisineType": "Mediterranean",
      "openNow": true
    }
  ]
}
```

---

## Routes Endpoints

### Get All Routes

Retrieve all saved routes for the authenticated user.

**Endpoint:** `GET /api/routes`
**Auth:** Required
**Quota:** Not counted

**Success Response (200 OK):**
```json
[
  {
    "id": "route_001",
    "name": "Istanbul Day Tour",
    "description": "Historical sites of Istanbul",
    "createdAt": "2025-01-20T10:00:00Z",
    "updatedAt": "2025-01-20T10:00:00Z",
    "totalDistance": 12500,
    "estimatedDuration": "6 hours",
    "pointsCount": 5,
    "isPublic": false
  }
]
```

---

### Get Route by ID

Get detailed information about a specific route.

**Endpoint:** `GET /api/routes/{id}`
**Auth:** Required
**Quota:** Not counted

**Example:** `GET /api/routes/550e8400-e29b-41d4-a716-446655440000`

**Success Response (200 OK):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Istanbul Day Tour",
  "description": "Historical sites of Istanbul",
  "createdAt": "2025-01-20T10:00:00Z",
  "totalDistance": 12500,
  "estimatedDuration": "6 hours",
  "points": [
    {
      "id": "point_001",
      "order": 1,
      "name": "Blue Mosque",
      "latitude": 41.0054,
      "longitude": 28.9768,
      "visitDuration": 60,
      "notes": "Beautiful architecture"
    },
    {
      "id": "point_002",
      "order": 2,
      "name": "Hagia Sophia",
      "latitude": 41.0086,
      "longitude": 28.9802,
      "visitDuration": 90,
      "notes": "Must-see historical site"
    }
  ]
}
```

---

### Create Route

Create a new route.

**Endpoint:** `POST /api/routes`
**Auth:** Required
**Quota:** Not counted

**Request Body:**
```json
{
  "name": "My Custom Route",
  "description": "A weekend exploration route",
  "isPublic": false
}
```

**Success Response (201 Created):**
```json
{
  "id": "new-route-id",
  "name": "My Custom Route",
  "description": "A weekend exploration route",
  "createdAt": "2025-01-24T11:00:00Z",
  "pointsCount": 0,
  "isPublic": false
}
```

---

### Update Route

Update an existing route.

**Endpoint:** `PUT /api/routes/{id}`
**Auth:** Required
**Quota:** Not counted

**Request Body:**
```json
{
  "name": "Updated Route Name",
  "description": "Updated description",
  "isPublic": true
}
```

**Success Response (200 OK):**
```json
{
  "id": "route_001",
  "name": "Updated Route Name",
  "description": "Updated description",
  "updatedAt": "2025-01-24T11:15:00Z",
  "isPublic": true
}
```

---

### Delete Route

Delete a route and all its points.

**Endpoint:** `DELETE /api/routes/{id}`
**Auth:** Required
**Quota:** Not counted

**Success Response:** `204 No Content`

---

## POIs Endpoints

### Get All POIs

Get all Points of Interest.

**Endpoint:** `GET /api/pois`
**Auth:** Optional
**Quota:** Not counted

**Query Parameters:**
```
lat (optional): float - Filter by proximity
lng (optional): float - Filter by proximity
radius (optional): int - Radius in meters
category (optional): string - Filter by category
```

**Success Response (200 OK):**
```json
[
  {
    "id": "poi_001",
    "name": "Central Park",
    "category": "park",
    "latitude": 40.7829,
    "longitude": -73.9654,
    "rating": 4.8,
    "description": "Large urban park",
    "address": "New York, NY",
    "photoUrl": "https://...",
    "isSponsored": false
  }
]
```

---

### Get POI by ID

**Endpoint:** `GET /api/pois/{id}`
**Auth:** Optional
**Quota:** Not counted

**Success Response (200 OK):**
```json
{
  "id": "poi_001",
  "name": "Central Park",
  "category": "park",
  "latitude": 40.7829,
  "longitude": -73.9654,
  "rating": 4.8,
  "description": "Large urban park in Manhattan",
  "address": "New York, NY 10024",
  "photoUrls": ["url1", "url2"],
  "openingHours": {
    "monday": "06:00-00:00",
    "tuesday": "06:00-00:00"
  },
  "isSponsored": false,
  "createdAt": "2024-01-01T00:00:00Z"
}
```

---

### Create POI

**Endpoint:** `POST /api/pois`
**Auth:** Required (Admin only)
**Quota:** Not counted

---

## Day Planning Endpoints

### Generate Day Plan

Create a full-day itinerary based on preferences.

**Endpoint:** `POST /api/dayplan`
**Auth:** Required
**Quota:** ✅ Counted (1 credit)

**Request Body:**
```json
{
  "startLatitude": 41.0082,
  "startLongitude": 28.9784,
  "date": "2025-01-25",
  "startTime": "09:00",
  "endTime": "20:00",
  "preferences": {
    "categories": ["restaurant", "museum", "park"],
    "budget": "medium",
    "pace": "relaxed",
    "maxWalkingDistance": 5000
  }
}
```

**Success Response (200 OK):**
```json
{
  "id": "plan_001",
  "date": "2025-01-25",
  "startTime": "09:00",
  "endTime": "20:00",
  "totalDistance": 8500,
  "totalDuration": "11 hours",
  "activities": [
    {
      "order": 1,
      "startTime": "09:00",
      "endTime": "10:30",
      "place": {
        "name": "Morning Café",
        "category": "cafe",
        "latitude": 41.0082,
        "longitude": 28.9784
      },
      "duration": 90,
      "activity": "breakfast",
      "transportToNext": {
        "mode": "walking",
        "duration": 15,
        "distance": 800
      }
    },
    {
      "order": 2,
      "startTime": "10:45",
      "endTime": "12:30",
      "place": {
        "name": "Istanbul Archaeological Museums",
        "category": "museum",
        "latitude": 41.0117,
        "longitude": 28.9810
      },
      "duration": 105,
      "activity": "sightseeing"
    }
  ],
  "estimatedCost": {
    "min": 50,
    "max": 100,
    "currency": "USD"
  }
}
```

---

## User Feedback Endpoints

### Submit Feedback

Send feedback about a place or suggestion.

**Endpoint:** `POST /api/feedback`
**Auth:** Required
**Quota:** Not counted

**Request Body:**
```json
{
  "placeId": "place_001",
  "rating": 5,
  "visited": true,
  "liked": true,
  "comment": "Great place, highly recommend!",
  "feedbackType": "positive"
}
```

**Success Response (200 OK):**
```json
{
  "id": "feedback_001",
  "placeId": "place_001",
  "userId": "user_001",
  "rating": 5,
  "submittedAt": "2025-01-24T12:00:00Z"
}
```

---

## Analytics Endpoints

### Get User Analytics

Retrieve analytics for the authenticated user.

**Endpoint:** `GET /api/analytics`
**Auth:** Required
**Quota:** Not counted

**Success Response (200 OK):**
```json
{
  "totalVisits": 127,
  "totalFeedback": 43,
  "averageRating": 4.6,
  "topCategories": [
    { "category": "restaurant", "count": 38 },
    { "category": "museum", "count": 22 },
    { "category": "cafe", "count": 18 }
  ],
  "visitsByMonth": {
    "2024-12": 25,
    "2025-01": 15
  }
}
```

---

## Context Endpoints

### Get Current Context

Get context information (weather, time of day, etc.) for better recommendations.

**Endpoint:** `GET /api/context`
**Auth:** Optional
**Quota:** Not counted

**Query Parameters:**
```
lat (required): float
lng (required): float
```

**Success Response (200 OK):**
```json
{
  "location": {
    "latitude": 41.0082,
    "longitude": 28.9784,
    "city": "Istanbul",
    "country": "Turkey"
  },
  "weather": {
    "temperature": 15,
    "condition": "partly_cloudy",
    "humidity": 65,
    "windSpeed": 12
  },
  "timeContext": {
    "currentTime": "14:30",
    "timeOfDay": "afternoon",
    "dayOfWeek": "Wednesday",
    "isWeekend": false
  },
  "recommendations": {
    "suitable": ["indoor_activities", "restaurants"],
    "notRecommended": ["beach", "outdoor_sports"]
  }
}
```

---

## Localization Endpoints

### Get Available Languages

**Endpoint:** `GET /api/localization/languages`
**Auth:** None
**Quota:** Not counted

**Success Response (200 OK):**
```json
{
  "languages": [
    { "code": "en-US", "name": "English", "nativeName": "English" },
    { "code": "tr-TR", "name": "Turkish", "nativeName": "Türkçe" },
    { "code": "es-ES", "name": "Spanish", "nativeName": "Español" },
    { "code": "fr-FR", "name": "French", "nativeName": "Français" },
    { "code": "de-DE", "name": "German", "nativeName": "Deutsch" },
    { "code": "it-IT", "name": "Italian", "nativeName": "Italiano" },
    { "code": "pt-PT", "name": "Portuguese", "nativeName": "Português" },
    { "code": "ru-RU", "name": "Russian", "nativeName": "Русский" },
    { "code": "ja-JP", "name": "Japanese", "nativeName": "日本語" },
    { "code": "ko-KR", "name": "Korean", "nativeName": "한국어" }
  ],
  "default": "en-US"
}
```

**Usage:**
Set `Accept-Language` header in requests:
```http
Accept-Language: tr-TR
```

---

## Health Endpoints

### Simple Health Check

**Endpoint:** `GET /health`
**Auth:** None
**Quota:** Not counted

**Success Response (200 OK):**
```json
{
  "status": "ok"
}
```

---

### Readiness Check

Checks if the API and all dependencies (Redis, Postgres) are ready.

**Endpoint:** `GET /health/ready`
**Auth:** None
**Quota:** Not counted

**Success Response (200 OK):**
```json
{
  "status": "Healthy",
  "duration": "00:00:00.0123456",
  "entries": {
    "redis": {
      "status": "Healthy",
      "description": "Redis is healthy (latency: 3.45ms)",
      "data": {
        "connected": true,
        "latency_ms": 3.45
      }
    },
    "postgres": {
      "status": "Healthy",
      "description": "PostgreSQL is healthy (latency: 12.34ms)",
      "data": {
        "can_connect": true,
        "latency_ms": 12.34
      }
    }
  }
}
```

---

### Liveness Check

**Endpoint:** `GET /health/live`
**Auth:** None
**Quota:** Not counted

---

## Request/Response Examples

### Example 1: Register → Login → Discover

```javascript
// 1. Register
const registerResponse = await fetch('http://localhost:5000/api/auth/register', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    email: 'john@example.com',
    password: 'SecurePass123!',
    username: 'johndoe',
    fullName: 'John Doe'
  })
});
const { token } = await registerResponse.json();

// 2. Get nearby suggestions
const discoverResponse = await fetch(
  'http://localhost:5000/api/discover?lat=41.0082&lng=28.9784&radius=5000',
  {
    headers: {
      'Authorization': `Bearer ${token}`,
      'Accept-Language': 'en-US'
    }
  }
);
const { suggestions } = await discoverResponse.json();

// 3. Submit feedback
const feedbackResponse = await fetch('http://localhost:5000/api/feedback', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    placeId: suggestions[0].id,
    rating: 5,
    visited: true,
    liked: true
  })
});
```

---

### Example 2: Prompt-Based Discovery

```javascript
const response = await fetch('http://localhost:5000/api/discover/prompt', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json',
    'Accept-Language': 'en-US'
  },
  body: JSON.stringify({
    prompt: 'romantic dinner with sea view',
    latitude: 41.0082,
    longitude: 28.9784,
    radius: 10000,
    sortBy: 'rating'
  })
});

const { personalized, suggestions } = await response.json();
```

---

### Example 3: Handling Quota Exhaustion

```javascript
const response = await fetch('http://localhost:5000/api/discover?lat=41&lng=28', {
  headers: { 'Authorization': `Bearer ${token}` }
});

if (response.status === 403) {
  const error = await response.json();
  if (error.type === 'quota_exhausted') {
    // Show upgrade modal
    showUpgradeModal({
      remaining: error.remainingQuota,
      limit: error.quotaLimit,
      message: error.detail
    });
  }
}
```

---

## TypeScript Types

```typescript
// Auth Types
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

// Discovery Types
interface DiscoverRequest {
  lat: number;
  lng: number;
  radius?: number; // default: 3000
}

interface PromptRequest {
  prompt: string;
  latitude?: number;
  longitude?: number;
  radius?: number;
  sortBy?: string;
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
  location: Location;
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

interface Location {
  latitude: number;
  longitude: number;
}

// Route Types
interface Route {
  id: string;
  name: string;
  description?: string;
  createdAt: string;
  updatedAt: string;
  totalDistance: number;
  estimatedDuration: string;
  pointsCount: number;
  isPublic: boolean;
  points?: RoutePoint[];
}

interface RoutePoint {
  id: string;
  order: number;
  name: string;
  latitude: number;
  longitude: number;
  visitDuration: number; // minutes
  notes?: string;
}

interface CreateRouteRequest {
  name: string;
  description?: string;
  isPublic?: boolean;
}

// Feedback Types
interface FeedbackRequest {
  placeId: string;
  rating: number; // 1-5
  visited: boolean;
  liked: boolean;
  comment?: string;
  feedbackType: 'positive' | 'negative' | 'neutral';
}

// Day Plan Types
interface DayPlanRequest {
  startLatitude: number;
  startLongitude: number;
  date: string; // ISO date
  startTime: string; // HH:mm
  endTime: string; // HH:mm
  preferences: {
    categories: string[];
    budget: 'low' | 'medium' | 'high';
    pace: 'relaxed' | 'moderate' | 'fast';
    maxWalkingDistance?: number;
  };
}

interface DayPlan {
  id: string;
  date: string;
  startTime: string;
  endTime: string;
  totalDistance: number;
  totalDuration: string;
  activities: Activity[];
  estimatedCost: {
    min: number;
    max: number;
    currency: string;
  };
}

interface Activity {
  order: number;
  startTime: string;
  endTime: string;
  place: Suggestion;
  duration: number; // minutes
  activity: string;
  transportToNext?: {
    mode: 'walking' | 'transit' | 'driving';
    duration: number;
    distance: number;
  };
}

// Error Types
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
```

---

## Best Practices

### 1. Always Include Authorization Header

```javascript
const headers = {
  'Authorization': `Bearer ${getToken()}`,
  'Content-Type': 'application/json'
};
```

### 2. Handle Token Expiration

```javascript
if (response.status === 401) {
  // Token expired, redirect to login
  clearToken();
  redirectToLogin();
}
```

### 3. Check Quota Before Feature Calls

```javascript
// Check remaining quota
const usage = await fetch('/api/auth/usage', {
  headers: { 'Authorization': `Bearer ${token}` }
});
const { dailyUsage, dailyLimit } = await usage.json();

if (dailyUsage >= dailyLimit) {
  showUpgradePrompt();
}
```

### 4. Use Correlation IDs for Debugging

```javascript
const correlationId = generateUUID();
const response = await fetch('/api/discover', {
  headers: {
    'Authorization': `Bearer ${token}`,
    'X-Correlation-Id': correlationId
  }
});

// If error occurs, provide correlation ID to support
```

### 5. Implement Retry Logic for 429/503

```javascript
async function fetchWithRetry(url, options, maxRetries = 3) {
  for (let i = 0; i < maxRetries; i++) {
    const response = await fetch(url, options);
    if (response.status === 429 || response.status === 503) {
      await sleep(Math.pow(2, i) * 1000); // Exponential backoff
      continue;
    }
    return response;
  }
  throw new Error('Max retries exceeded');
}
```

### 6. Handle Localization

```javascript
// Get user's preferred language
const userLang = navigator.language || 'en-US';

const response = await fetch('/api/discover', {
  headers: {
    'Authorization': `Bearer ${token}`,
    'Accept-Language': userLang
  }
});
```

---

## FAQ

**Q: Do I need authentication for discovery endpoints?**
A: Authentication is optional. Anonymous users get generic suggestions, while authenticated users get personalized recommendations.

**Q: How do I know if my quota is exhausted?**
A: Check the `X-Quota-Remaining` header in responses. When exhausted, you'll receive a 403 error with `type: "quota_exhausted"`.

**Q: Can I increase my rate limit?**
A: Premium users have higher rate limits. Contact support for enterprise limits.

**Q: How long does the JWT token last?**
A: 60 minutes in production, 120 minutes in development. Implement auto-refresh or redirect to login on expiration.

**Q: What happens if Redis or Postgres is down?**
A: The `/health/ready` endpoint will return `Unhealthy`. Feature endpoints may return 503. Implement graceful degradation on the client side.

**Q: How do I test the API locally?**
A: Run `docker-compose up` to start all dependencies, then `dotnet run` in the API directory. Use `http://localhost:5000` as the base URL.

---

## Support & Feedback

- **API Documentation:** [https://api.whatshouldido.com/swagger](https://api.whatshouldido.com/swagger)
- **Issues:** [GitHub Issues](https://github.com/your-org/whatshouldido/issues)
- **Email:** dev-support@whatshouldido.com

**Last Updated:** 2025-01-24
**API Version:** 2.0.0
