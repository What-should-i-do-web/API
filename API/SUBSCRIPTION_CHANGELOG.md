# Subscription System Implementation - CHANGELOG

## Overview

This implementation adds a complete subscription domain with API surface and policy integration that is ready for future Apple/Google IAP integration, while keeping the web app fully functional today without any subscription/payments.

**Key Features:**
- Provider-agnostic subscription model (supports Apple App Store, Google Play, manual grants)
- Trial support with configurable trial periods
- Database-backed subscription storage
- Seamless integration with existing quota system
- Safe defaults (verification disabled by default)
- Development test receipts for local testing

---

## Files Added

### Domain Layer
| File | Description |
|------|-------------|
| `src/WhatShouldIDo.Domain/Enums/SubscriptionEnums.cs` | Enums: SubscriptionProvider, SubscriptionPlan, SubscriptionStatus |
| `src/WhatShouldIDo.Domain/Entities/UserSubscription.cs` | Main subscription entity with behavior methods |

### Application Layer
| File | Description |
|------|-------------|
| `src/WhatShouldIDo.Application/Configuration/SubscriptionOptions.cs` | Configuration options class |
| `src/WhatShouldIDo.Application/DTOs/Response/SubscriptionDto.cs` | DTOs for subscription and verification results |
| `src/WhatShouldIDo.Application/DTOs/Requests/VerifyReceiptRequest.cs` | Request DTO for receipt verification |
| `src/WhatShouldIDo.Application/Interfaces/ISubscriptionService.cs` | Service interface |
| `src/WhatShouldIDo.Application/Interfaces/IReceiptVerifier.cs` | Receipt verifier interface |
| `src/WhatShouldIDo.Application/Interfaces/ISubscriptionRepository.cs` | Repository interface |
| `src/WhatShouldIDo.Application/UseCases/Queries/GetMySubscriptionQuery.cs` | CQRS query |
| `src/WhatShouldIDo.Application/UseCases/Commands/VerifySubscriptionReceiptCommand.cs` | CQRS command |
| `src/WhatShouldIDo.Application/UseCases/Handlers/GetMySubscriptionQueryHandler.cs` | Query handler |
| `src/WhatShouldIDo.Application/UseCases/Handlers/VerifySubscriptionReceiptCommandHandler.cs` | Command handler |

### Infrastructure Layer
| File | Description |
|------|-------------|
| `src/WhatShouldIDo.Infrastructure/Services/Subscription/SubscriptionService.cs` | Main service implementation |
| `src/WhatShouldIDo.Infrastructure/Services/Subscription/DisabledReceiptVerifier.cs` | Disabled verifier (production default) |
| `src/WhatShouldIDo.Infrastructure/Services/Subscription/DevTestReceiptVerifier.cs` | Development test verifier |
| `src/WhatShouldIDo.Infrastructure/Repositories/SubscriptionRepository.cs` | Repository implementation |

### API Layer
| File | Description |
|------|-------------|
| `src/WhatShouldIDo.API/Controllers/SubscriptionsController.cs` | REST endpoints |
| `src/WhatShouldIDo.API/Validators/VerifyReceiptRequestValidator.cs` | FluentValidation validator |

### Tests
| File | Description |
|------|-------------|
| `src/WhatShouldIDo.Tests/Unit/UserSubscriptionTests.cs` | Domain entity unit tests |
| `src/WhatShouldIDo.Tests/Integration/SubscriptionIntegrationTests.cs` | API integration tests |

---

## Files Modified

### Infrastructure Layer
| File | Changes |
|------|---------|
| `src/WhatShouldIDo.Infrastructure/Data/WhatShouldIDoDbContext.cs` | Added `DbSet<UserSubscription>` and entity configuration |
| `src/WhatShouldIDo.Infrastructure/Services/EntitlementService.cs` | Added `ISubscriptionRepository` dependency, DB-first entitlement check |

### API Layer
| File | Changes |
|------|---------|
| `src/WhatShouldIDo.API/Program.cs` | Added subscription service registrations (lines ~308-348) |

### Configuration
| File | Changes |
|------|---------|
| `src/WhatShouldIDo.API/appsettings.json` | Added `Feature:Subscription` configuration section |
| `src/WhatShouldIDo.API/appsettings.Development.json` | Added `Feature:Subscription` with `AllowDevTestReceipts: true` |

### Tests
| File | Changes |
|------|---------|
| `src/WhatShouldIDo.Tests/Unit/EntitlementServiceTests.cs` | Added `ISubscriptionRepository` mock, new subscription-based tests |

---

## Database Migration Required

Run the following command to create the migration:

```bash
cd src/WhatShouldIDo.Infrastructure
dotnet ef migrations add AddUserSubscription --context WhatShouldIDoDbContext
```

Then apply:

```bash
dotnet ef database update
```

The migration will create:
- Table: `usersubscriptions`
- Columns: Id, UserId, Provider, Plan, Status, TrialEndsAtUtc, CurrentPeriodEndsAtUtc, AutoRenew, ExternalSubscriptionId, LastVerifiedAtUtc, RowVersion, CreatedAtUtc, UpdatedAtUtc
- Indexes: Unique on UserId, Composite on (Status, CurrentPeriodEndsAtUtc)
- Foreign key: UserId -> Users.Id (CASCADE DELETE)

---

## Configuration Options

### appsettings.json

```json
{
  "Feature": {
    "Subscription": {
      "VerificationEnabled": false,         // Enable for mobile IAP
      "AllowDevTestReceipts": false,        // Enable in Development only
      "AppleSharedSecret": "${APPLE_SHARED_SECRET}",
      "GoogleServiceAccountJson": "${GOOGLE_SERVICE_ACCOUNT_JSON}",
      "MonthlyTrialDays": 7,
      "YearlyTrialDays": 30,
      "ReverificationIntervalHours": 24,
      "GracePeriodHours": 24
    }
  }
}
```

---

## API Endpoints

### GET /api/subscriptions/me
Returns current user's subscription status.

**Response (200 OK):**
```json
{
  "plan": "Free",
  "status": "None",
  "provider": "None",
  "trialEndsAtUtc": null,
  "currentPeriodEndsAtUtc": null,
  "autoRenew": false,
  "hasEntitlement": false,
  "effectivePlan": "Free",
  "planDisplayName": "Free",
  "statusDisplayName": "Free Tier"
}
```

### POST /api/subscriptions/verify
Verifies a receipt and updates subscription status.

**Request:**
```json
{
  "provider": "AppleAppStore",
  "plan": "Monthly",
  "receipt": "receipt_data_here",
  "isTrialRequested": false
}
```

**Response when disabled (501 Not Implemented):**
```json
{
  "type": "https://errors.whatshouldido.app/verification-disabled",
  "title": "Verification Not Implemented",
  "status": 501,
  "errorCode": "VERIFICATION_DISABLED",
  "detail": "Subscription verification is disabled on this environment."
}
```

### GET /api/subscriptions/status
Returns verification service configuration.

**Response (200 OK):**
```json
{
  "verificationEnabled": false,
  "devTestReceiptsAllowed": true,
  "supportedProviders": ["AppleAppStore", "GooglePlay"],
  "supportedPlans": ["Monthly", "Yearly"]
}
```

---

## Development Testing

### Test Receipts (Development Only)

When `VerificationEnabled: true` and `AllowDevTestReceipts: true` in Development:

| Receipt | Result |
|---------|--------|
| `TEST_MONTHLY` | Active Monthly subscription (1 month) |
| `TEST_YEARLY` | Active Yearly subscription (1 year) |
| `TEST_TRIAL_MONTHLY` | Trialing Monthly (7 days trial) |
| `TEST_TRIAL_YEARLY` | Trialing Yearly (30 days trial) |

### Example Dev Test:

```bash
# Enable verification in appsettings.Development.json:
# "VerificationEnabled": true, "AllowDevTestReceipts": true

# Call verify endpoint:
curl -X POST http://localhost:5000/api/subscriptions/verify \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"provider":"AppleAppStore","plan":"Monthly","receipt":"TEST_TRIAL_MONTHLY"}'
```

---

## Manual Test Checklist

### 1. Web User Flow (No Mobile, No Subscription)
- [ ] Register new user via POST /api/auth/register
- [ ] Login via POST /api/auth/login
- [ ] GET /api/subscriptions/me → Returns Free/None
- [ ] Call discover/route endpoints → Works with quota limits
- [ ] Exhaust quota (5 requests) → Returns 403 with quota headers
- [ ] Verify X-Quota-Remaining and X-Quota-Limit headers present

### 2. Subscription Endpoints (Verification Disabled)
- [ ] GET /api/subscriptions/me → 200 OK with Free tier
- [ ] POST /api/subscriptions/verify → 501 Not Implemented
- [ ] GET /api/subscriptions/status → Shows verificationEnabled: false

### 3. Premium Bypass via Manual DB Update
```sql
-- Grant premium to a user manually
INSERT INTO usersubscriptions (id, userid, provider, plan, status, currentperiodendatutc, autorenew, createdatutc, updatedatutc)
VALUES (gen_random_uuid(), '<user_id>', 'None', 'Monthly', 'Active', NOW() + INTERVAL '30 days', true, NOW(), NOW());
```
- [ ] After manual grant, quota bypass works
- [ ] GET /api/subscriptions/me → Shows Active/Monthly

### 4. Development Test Receipts (Dev Environment)
Enable in appsettings.Development.json:
```json
"Subscription": {
  "VerificationEnabled": true,
  "AllowDevTestReceipts": true
}
```
- [ ] POST /api/subscriptions/verify with TEST_MONTHLY → Success
- [ ] Subscription becomes Active
- [ ] Quota bypass now works
- [ ] POST /api/subscriptions/verify with TEST_TRIAL_MONTHLY → Success
- [ ] Subscription becomes Trialing

### 5. Security Checks
- [ ] Unauthenticated requests to /api/subscriptions/* → 401
- [ ] Receipt content is NOT logged (check logs)
- [ ] Correlation ID header preserved in responses

### 6. Backward Compatibility
- [ ] Existing endpoints still work
- [ ] JWT claims-based premium still works as fallback
- [ ] No breaking changes to existing APIs

---

## Architecture Notes

### Entitlement Check Priority
1. **Database subscription** (checked first)
   - If Active with valid period → Premium
   - If Trialing with valid trial → Premium
2. **JWT claims** (fallback)
   - `subscription: premium` claim → Premium
   - `role: premium` claim → Premium
3. **Default** → Free tier

### Security Considerations
- Receipts are never logged (security)
- Raw receipts are not stored in database
- Test receipts only work in Development environment
- Feature flag controls all verification functionality
- Rate limiting applies to verify endpoint

### Future Mobile Integration
When ready for mobile IAP:
1. Set `VerificationEnabled: true` in production
2. Implement real Apple/Google receipt verifiers
3. Configure Apple shared secret and Google service account
4. Mobile apps call POST /api/subscriptions/verify after purchase
5. Server validates with Apple/Google and updates subscription

---

## Breaking Changes

**None.** All changes are backward compatible:
- Existing APIs unchanged
- JWT-based premium still works
- Default configuration disables new features
- Free users continue to work with quota limits

---

## Dependencies

No new external packages required. Uses existing:
- MediatR (CQRS)
- FluentValidation
- Entity Framework Core
- Microsoft.Extensions.Options

---

## Phase 2: Security & Testability Enhancements (2025-06)

### Summary

This phase adds security-focused improvements, testable time handling, domain invariants, and admin-only manual grant capabilities.

**Key Changes:**
- `IClock` interface for deterministic testing (no more `DateTime.UtcNow` calls in domain/services)
- `Manual` provider enum for admin-granted subscriptions (distinct from `None`)
- Domain invariants with validation methods
- Receipt hash logging (never log raw receipts)
- Admin-only manual grant endpoints with role-based authorization

---

### New Files Added

| File | Description |
|------|-------------|
| `src/WhatShouldIDo.Application/Interfaces/IClock.cs` | Clock abstraction for testable time handling |
| `src/WhatShouldIDo.Infrastructure/Services/SystemClock.cs` | Production IClock implementation |
| `src/WhatShouldIDo.Application/DTOs/Requests/ManualGrantRequest.cs` | Request DTO for admin manual grants |
| `src/WhatShouldIDo.API/Validators/ManualGrantRequestValidator.cs` | Validator with PII detection |

---

### Files Modified

| File | Changes |
|------|---------|
| `src/WhatShouldIDo.Domain/Enums/SubscriptionEnums.cs` | Added `Manual = 1` to SubscriptionProvider enum |
| `src/WhatShouldIDo.Domain/Entities/UserSubscription.cs` | Added `Notes` property, `GrantManual()` method, `ValidateInvariants()`, `EnsureInvariantsOrThrow()`, all methods now accept `DateTime utcNow` |
| `src/WhatShouldIDo.Application/Interfaces/ISubscriptionService.cs` | Added `ManualGrantAsync()` and `RevokeManualGrantAsync()` |
| `src/WhatShouldIDo.Application/DTOs/Requests/VerifyReceiptRequest.cs` | Renamed `Receipt` to `ReceiptData` with security comment |
| `src/WhatShouldIDo.Infrastructure/Services/Subscription/SubscriptionService.cs` | Injected IClock, added receipt hash logging, implemented manual grant methods |
| `src/WhatShouldIDo.Infrastructure/Services/Subscription/DevTestReceiptVerifier.cs` | Injected IClock, updated property reference |
| `src/WhatShouldIDo.Infrastructure/Services/EntitlementService.cs` | Injected IClock instead of using DateTime.UtcNow |
| `src/WhatShouldIDo.Infrastructure/Data/WhatShouldIDoDbContext.cs` | Added Notes field configuration (max 500 chars) |
| `src/WhatShouldIDo.API/Controllers/AdminController.cs` | Added manual grant/revoke endpoints with `[Authorize(Roles = "Admin")]` |
| `src/WhatShouldIDo.API/Validators/VerifyReceiptRequestValidator.cs` | Updated property name, added Manual provider validation |
| `src/WhatShouldIDo.API/Program.cs` | Registered IClock as singleton |
| `src/WhatShouldIDo.Tests/Unit/UserSubscriptionTests.cs` | Updated all tests for new API, added invariant and manual grant tests |

---

### Domain Model Changes

#### SubscriptionProvider Enum
```csharp
public enum SubscriptionProvider
{
    None = 0,            // Free tier (no subscription)
    Manual = 1,          // Admin-granted (internal use)
    AppleAppStore = 2,   // Apple IAP
    GooglePlay = 3       // Google IAP
}
```

#### UserSubscription Entity - New Members
```csharp
// New property for manual grant notes
public string? Notes { get; set; }

// Manual grant method
public void GrantManual(SubscriptionPlan plan, DateTime currentPeriodEndsAtUtc, DateTime utcNow, string? notes = null)

// Invariant validation
public IReadOnlyList<string> ValidateInvariants()
public void EnsureInvariantsOrThrow()

// All behavior methods now require utcNow parameter:
public void Activate(..., DateTime utcNow, ...)
public void StartTrial(..., DateTime utcNow, ...)
public void Cancel(DateTime utcNow)
public void Expire(DateTime utcNow)
public void ResetToFree(DateTime utcNow)
public static UserSubscription CreateDefault(Guid userId, DateTime utcNow)
```

#### Domain Invariants Enforced
1. **Provider=None** → Status must be None AND Plan must be Free
2. **Provider=Manual** → ExternalSubscriptionId must be null
3. **Status=Trialing** → TrialEndsAtUtc must be set
4. **Status=Active with IAP** → CurrentPeriodEndsAtUtc must be set
5. **Notes field** → Only allowed when Provider=Manual

---

### New API Endpoints

#### POST /api/admin/subscriptions/grant
**Authorization:** `[Authorize(Roles = "Admin")]`

Manually grants a subscription to a user.

**Request:**
```json
{
  "userId": "guid",
  "plan": "Monthly",
  "expiresAtUtc": "2025-09-15T00:00:00Z",
  "notes": "Beta tester reward"
}
```

**Response (200 OK):**
```json
{
  "message": "Manual grant applied successfully.",
  "subscription": {
    "plan": "Monthly",
    "status": "Active",
    "provider": "Manual",
    "currentPeriodEndsAtUtc": "2025-09-15T00:00:00Z",
    "hasEntitlement": true
  }
}
```

#### DELETE /api/admin/subscriptions/grant/{userId}
**Authorization:** `[Authorize(Roles = "Admin")]`

Revokes a manual grant, resetting user to free tier.

**Response (200 OK):**
```json
{
  "message": "Manual grant revoked successfully. User reset to free tier."
}
```

#### GET /api/admin/subscriptions/{userId}
**Authorization:** `[Authorize(Roles = "Admin")]`

Gets any user's subscription details (for support).

---

### Security Improvements

1. **Receipt Hash Logging**
   - Raw receipt data is NEVER logged
   - SHA256 hash (first 8 chars) logged for audit correlation
   ```
   Processing receipt verification for user {UserId}, provider {Provider}, receiptHash={ReceiptHash}
   ```

2. **PII Prevention in Notes**
   - Validator rejects notes containing email patterns (@, .)
   - Validator rejects notes with 10+ consecutive digits (phone numbers)
   - Notes truncated to 500 chars max

3. **Provider Validation**
   - `SubscriptionProvider.Manual` cannot be used via receipt verification endpoint
   - Only admins can grant Manual subscriptions

---

### IClock Interface

For testable time handling throughout the subscription system:

```csharp
// Application layer interface
public interface IClock
{
    DateTime UtcNow { get; }
}

// Infrastructure implementation (DI registered as singleton)
public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();
    public DateTime UtcNow => DateTime.UtcNow;
}

// Test implementation
public class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
}
```

---

### Database Migration Required

The `Notes` column needs to be added to `usersubscriptions` table:

```bash
cd src/WhatShouldIDo.Infrastructure
dotnet ef migrations add AddUserSubscriptionNotes --context WhatShouldIDoDbContext
dotnet ef database update
```

**New Column:**
- `Notes` (varchar(500), nullable)

---

### Breaking Changes

**API Breaking Change:**
- `VerifyReceiptRequest.Receipt` renamed to `VerifyReceiptRequest.ReceiptData`
- Mobile clients must update request body property name

**Internal Breaking Changes (no external impact):**
- All `UserSubscription` behavior methods now require `DateTime utcNow` parameter
- Services must inject `IClock` instead of using `DateTime.UtcNow`

---

### Test Updates

Updated `UserSubscriptionTests.cs` with:
- Fixed timestamp (`TestUtcNow`) for deterministic testing
- Tests for all new invariant validations
- Tests for `GrantManual()` method
- Tests for exception cases (invalid provider, past dates, etc.)
