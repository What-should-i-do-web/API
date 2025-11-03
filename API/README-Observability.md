# WhatShouldIDo API - Production Observability Guide

## Overview

This document describes the production-grade observability, monitoring, and operational excellence infrastructure for the WhatShouldIDo API. The system is built on OpenTelemetry with exports to Prometheus (metrics), Tempo/Jaeger (traces), and Loki (logs).

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                     WhatShouldIDo API                         │
│                                                                │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────┐       │
│  │ Controllers │──│  Middleware  │──│ Infrastructure│       │
│  └─────────────┘  └──────────────┘  └───────────────┘       │
│         │                 │                   │               │
│         ▼                 ▼                   ▼               │
│  ┌──────────────────────────────────────────────────┐       │
│  │         OpenTelemetry SDK (Traces + Metrics)     │       │
│  └──────────────────────────────────────────────────┘       │
│         │                 │                   │               │
└─────────┼─────────────────┼───────────────────┼───────────────┘
          │                 │                   │
          ▼                 ▼                   ▼
   ┌──────────┐      ┌──────────┐       ┌──────────┐
   │ Tempo/   │      │Prometheus│       │  Loki    │
   │ Jaeger   │      │  (OTLP)  │       │ (OTLP)   │
   └──────────┘      └──────────┘       └──────────┘
          │                 │                   │
          └─────────────────┼───────────────────┘
                            ▼
                     ┌──────────┐
                     │ Grafana  │
                     └──────────┘
```

## Key Components

### 1. Observability Context (IObservabilityContext)

**Location:** `Application/Interfaces/IObservabilityContext.cs`

Provides correlation IDs and user traits for consistent span/log enrichment:
- **CorrelationId**: Unique ID per request (propagated to all logs/traces/responses)
- **TraceId**: W3C trace ID for distributed tracing
- **UserIdHash**: SHA256 hash (truncated to 16 chars) for safe metric cardinality
- **UserId**: Raw user ID (use sparingly)
- **IsPremium**: Premium user status
- **Endpoint**: Current endpoint being accessed
- **Baggage**: Additional trace context items

### 2. Metrics Service (IMetricsService)

**Location:** `Application/Interfaces/IMetricsService.cs`

Comprehensive metrics collection service with OpenTelemetry:

#### Product Metrics (SLO/SLI-Driven)

| Metric | Type | Labels | Description |
|--------|------|--------|-------------|
| `requests_total` | Counter | endpoint, method, status_code, authenticated, premium | Total HTTP requests |
| `request_duration_seconds` | Histogram | endpoint, method | Request latency (buckets: 5ms - 10s) |
| `quota_consumed_total` | Counter | - | Total quota credits consumed |
| `quota_blocked_total` | Counter | - | Requests blocked by quota exhaustion |
| `quota_users_with_zero` | Gauge | - | Users currently with zero quota |
| `entitlement_checks_total` | Counter | source, outcome | Entitlement checks (claim/redis/db) |
| `redis_quota_script_latency_seconds` | Histogram | operation, success | Redis Lua script latency |
| `redis_errors_total` | Counter | operation | Redis operation failures |
| `db_subscription_reads_total` | Counter | outcome | Postgres subscription reads |
| `db_latency_seconds` | Histogram | outcome | Database operation latency |
| `webhook_events_total` | Counter | type, outcome | Webhook events processed |
| `webhook_verify_failures_total` | Counter | - | Webhook signature verification failures |
| `rate_limit_blocks_total` | Counter | endpoint | Rate limit blocks |

#### Legacy Metrics (Backward Compatibility)

| Metric | Type | Labels | Description |
|--------|------|--------|-------------|
| `cache_hits_total` | Counter | cache_type | Cache hits |
| `cache_misses_total` | Counter | cache_type | Cache misses |
| `database_query_duration_seconds` | Histogram | - | DB query latency |
| `slow_queries_total` | Counter | - | Slow queries (>threshold) |
| `place_searches_total` | Counter | provider, result_count_bucket | Place searches |
| `place_search_duration_seconds` | Histogram | provider | Place search latency |
| `active_users` | UpDownCounter | - | Current active users |

### 3. Middleware Pipeline

**Execution Order:**
```
Request
  ↓
CorrelationIdMiddleware (sets correlation ID, W3C context, ObservabilityContext)
  ↓
MetricsMiddleware (records request metrics, duration, status codes)
  ↓
UseAuthentication() (JWT validation)
  ↓
UseAuthorization()
  ↓
EntitlementAndQuotaMiddleware (enforces quota, checks premium status)
  ↓
Controllers
  ↓
Response (includes X-Correlation-Id header, X-Quota-Remaining if applicable)
```

**Key Files:**
- `API/Middleware/CorrelationIdMiddleware.cs`
- `API/Middleware/MetricsMiddleware.cs`
- `API/Middleware/EntitlementAndQuotaMiddleware.cs`

### 4. Instrumented Data Stores

#### InstrumentedRedisQuotaStore

**Location:** `Infrastructure/Quota/InstrumentedRedisQuotaStore.cs`

Decorator for RedisQuotaStore that adds:
- OpenTelemetry spans for all Redis operations (GET, SET, CONSUME)
- Metrics for latency and error rates
- Automatic quota consumption tracking

**Span Attributes:**
- `redis.operation`: Operation type
- `user.id`: User ID
- `quota.amount`: Amount to consume
- `redis.consume_success`: Success/failure
- `redis.quota_value`: Current value

#### Postgres Health Check

**Location:** `Infrastructure/Health/PostgresHealthCheck.cs`

Health check with latency monitoring:
- Executes `SELECT 1` query
- Reports latency in health check data
- **Degraded** if latency > 100ms
- **Unhealthy** if cannot connect

### 5. Health Endpoints

| Endpoint | Purpose | Dependencies Checked |
|----------|---------|---------------------|
| `/health/ready` | Readiness probe | Redis (ping), Postgres (SELECT 1) |
| `/health/live` | Liveness probe | Internal process health |
| `/health/startup` | Startup probe | Migrations, config validation |

**Response Format:**
```json
{
  "status": "Healthy|Degraded|Unhealthy",
  "duration": "00:00:00.0234567",
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
        "latency_ms": 12.34,
        "database": "whatshouldido"
      }
    }
  }
}
```

## Configuration

### appsettings.Production.json

```json
{
  "Observability": {
    "Enabled": true,
    "ServiceName": "whatshouldido-api",
    "ServiceVersion": "1.0.0",
    "TraceSamplingRatio": 0.05,
    "PrometheusEnabled": true,
    "PrometheusEndpoint": "/metrics",
    "OtlpTracesEnabled": true,
    "OtlpTracesEndpoint": "http://tempo:4317",
    "OtlpLogsEnabled": true,
    "OtlpLogsEndpoint": "http://loki:4317",
    "LogLevel": "Information",
    "IncludeSensitiveData": false,
    "IncludeExceptionStackTrace": true,
    "CorrelationIdHeader": "X-Correlation-Id"
  },
  "Security": {
    "Jwt": {
      "ValidIssuer": "https://your-issuer.com",
      "ValidAudience": "whatshouldido-api",
      "ValidateSignature": true,
      "ValidateIssuer": true,
      "ValidateAudience": true,
      "ValidateLifetime": true,
      "ClockSkewSeconds": 300,
      "MaxTokenLifetimeSeconds": 3600
    },
    "RateLimit": {
      "Enabled": true,
      "WindowSeconds": 60,
      "MaxRequestsPerWindow": 100,
      "MaxRequestsPerWindowAnonymous": 20,
      "PremiumBypass": false,
      "StatusCode": 429
    },
    "Webhook": {
      "VerifySignature": true,
      "SigningSecret": "${WEBHOOK_SIGNING_SECRET}",
      "SignatureHeader": "X-Webhook-Signature",
      "TimestampHeader": "X-Webhook-Timestamp",
      "MaxTimestampAgeSeconds": 300,
      "UseIdempotencyKeys": true
    },
    "Redis": {
      "UseTls": true,
      "AclUsername": "quota-service",
      "ValidateCertificate": true,
      "MinTlsVersion": "1.2"
    }
  },
  "Feature": {
    "Quota": {
      "DefaultFreeQuota": 5,
      "DailyResetEnabled": false,
      "DailyResetAtUtc": "00:00:00",
      "StorageBackend": "Redis"
    }
  }
}
```

### Environment Variables

Production secrets should be injected via environment variables:

```bash
export WEBHOOK_SIGNING_SECRET="your-webhook-secret-min-32-chars"
export REDIS__PASSWORD="your-redis-password"
export JWT__KEY="your-jwt-signing-key"
```

## Service Level Objectives (SLOs)

### Availability SLO: 99.9% Monthly

**Measurement:**
- Error Budget: 0.1% (43 minutes/month)
- Alert Thresholds:
  - **Warning**: Error rate > 0.05% over 10 minutes
  - **Critical**: Error rate > 0.1% over 1 hour

**Prometheus Query:**
```promql
(
  sum(rate(requests_total{status_code=~"5.."}[10m]))
  /
  sum(rate(requests_total[10m]))
) > 0.001
```

### Latency SLO

| Percentile | Target | Measurement Window |
|------------|--------|-------------------|
| p95 | < 300ms | 10 minutes |
| p99 | < 800ms | 10 minutes |
| p99.9 | < 2s | 1 hour |

**Prometheus Query (p95):**
```promql
histogram_quantile(0.95,
  sum(rate(request_duration_seconds_bucket[10m])) by (le, endpoint)
) > 0.3
```

### Quota System Health SLOs

| Metric | Target | Alert Threshold |
|--------|--------|----------------|
| Redis script latency p95 | < 20ms | > 50ms for 10m (warn), > 100ms (critical) |
| Redis error rate | < 0.1% | > 0.1% for 5m |
| Quota blocked surge | Normal variance | Surge without consumption increase |
| DB read latency p95 | < 100ms | > 200ms for 10m |

## Dashboards

### API Golden Signals Dashboard

**File:** `deploy/grafana/dashboards/api-golden-signals.json`

**Panels:**
1. **RPS (Requests Per Second)** by endpoint, status code
2. **Error Rate %** (4xx, 5xx) with SLO threshold line
3. **Latency Heatmap** (p50, p95, p99, p99.9)
4. **Top 5 Slowest Endpoints** (p95 latency)
5. **Saturation**: CPU %, Memory %, Pod count
6. **Active Users** gauge

### Quota & Entitlements Dashboard

**File:** `deploy/grafana/dashboards/quota-entitlements.json`

**Panels:**
1. **Quota Consumption Rate** (credits/sec)
2. **Quota Blocks** count over time
3. **Users with Zero Quota** gauge
4. **Premium vs Free Split** (pie chart)
5. **Redis Script Latency** (p50, p95, p99)
6. **Redis Errors** count
7. **Entitlement Check Sources** (claim/redis/db breakdown)

### Persistence Dashboard

**File:** `deploy/grafana/dashboards/persistence.json`

**Panels:**
1. **Postgres Query Latency** (p95, p99)
2. **Postgres Errors** count
3. **Postgres Connection Pool Usage**
4. **Redis Operations/sec**
5. **Redis Latency** (p95, p99)
6. **Redis Memory Usage**
7. **Redis Keyspace Hits/Misses**

## Alerts

### Critical Alerts (Page Immediately)

**File:** `deploy/prometheus/alerts/critical.yaml`

```yaml
groups:
  - name: critical
    interval: 30s
    rules:
      - alert: HighErrorRate
        expr: |
          (
            sum(rate(requests_total{status_code=~"5.."}[10m]))
            /
            sum(rate(requests_total[10m]))
          ) > 0.01
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "High error rate detected"
          description: "Error rate is {{ $value | humanizePercentage }} (threshold: 1%)"
          runbook: "https://docs.internal/runbooks/high-error-rate"

      - alert: HighLatency
        expr: |
          histogram_quantile(0.95,
            sum(rate(request_duration_seconds_bucket[10m])) by (le)
          ) > 0.3
        for: 10m
        labels:
          severity: critical
        annotations:
          summary: "API latency p95 exceeds SLO"
          description: "p95 latency is {{ $value }}s (SLO: 300ms)"
          runbook: "https://docs.internal/runbooks/high-latency"

      - alert: RedisDown
        expr: up{job="redis"} == 0
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "Redis is down"
          description: "Redis has been down for 2 minutes"
          runbook: "https://docs.internal/runbooks/redis-down"

      - alert: PostgresDown
        expr: up{job="postgres"} == 0
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "PostgreSQL is down"
          description: "PostgreSQL has been down for 2 minutes"
          runbook: "https://docs.internal/runbooks/postgres-down"
```

### Warning Alerts

**File:** `deploy/prometheus/alerts/warning.yaml`

```yaml
groups:
  - name: warning
    interval: 1m
    rules:
      - alert: RedisHighLatency
        expr: |
          histogram_quantile(0.95,
            sum(rate(redis_quota_script_latency_seconds_bucket[10m])) by (le)
          ) > 0.05
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "Redis quota script latency is high"
          description: "Redis script p95 latency is {{ $value }}s (threshold: 50ms)"

      - alert: QuotaBlockSurge
        expr: |
          (
            rate(quota_blocked_total[5m])
            /
            rate(quota_consumed_total[5m] offset 1h)
          ) > 1.5
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "Unusual surge in quota blocks"
          description: "Quota blocks increased by {{ $value | humanizePercentage }} compared to 1h ago"

      - alert: WebhookVerificationFailures
        expr: |
          (
            rate(webhook_verify_failures_total[10m])
            /
            rate(webhook_events_total[10m])
          ) > 0.01
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Webhook verification failure rate is high"
          description: "{{ $value | humanizePercentage }} of webhooks failing verification"
```

## Testing

### Local Development Setup

**Docker Compose File:** `docker-compose.observability.yml`

```bash
# Start observability stack locally
docker-compose -f docker-compose.observability.yml up -d

# Endpoints:
# - Prometheus: http://localhost:9090
# - Grafana: http://localhost:3000 (admin/admin)
# - Tempo: http://localhost:3200
# - Loki: http://localhost:3100
```

### Running Tests

```bash
# Unit tests
dotnet test src/WhatShouldIDo.Tests/Unit/

# Integration tests (requires Redis + Postgres)
dotnet test src/WhatShouldIDo.Tests/Integration/

# Concurrency test (100 parallel requests, only 5 succeed)
dotnet test src/WhatShouldIDo.Tests/Integration/ --filter "FullyQualifiedName~ConcurrencyTest"

# Chaos tests (Redis down, DB slow, etc.)
dotnet test src/WhatShouldIDo.Tests/Chaos/
```

### Load Testing

```bash
# Install k6
brew install k6  # macOS
# or download from https://k6.io/

# Run load test
k6 run tests/load/api-load-test.js

# Expected results:
# - p95 latency < 300ms
# - p99 latency < 800ms
# - Error rate < 0.1%
# - Quota enforcement working correctly
```

## Monitoring Best Practices

### 1. Correlation IDs

Every request/response includes `X-Correlation-Id` header:

```bash
curl -H "Authorization: Bearer $TOKEN" \
     https://api.whatshouldido.com/api/discover \
     -v | grep X-Correlation-Id

# Response:
# < X-Correlation-Id: a1b2c3d4e5f6...
```

Use this ID to:
- Search logs in Grafana Loki
- Find traces in Tempo/Jaeger
- Debug user-reported issues

### 2. Trace Sampling

- **Production**: 5% sampling (configurable via `TraceSamplingRatio`)
- **Staging**: 100% sampling
- **Development**: 100% sampling

### 3. High Cardinality Guard

**❌ DON'T:**
```csharp
// Raw user IDs create unbounded cardinality
metricsService.RecordQuotaRemaining(userId.ToString(), remaining);
```

**✅ DO:**
```csharp
// Use hashed IDs for metrics
metricsService.RecordQuotaRemaining(observabilityContext.UserIdHash, remaining);
```

### 4. Structured Logging

**❌ DON'T:**
```csharp
_logger.LogInformation($"User {userId} consumed {amount} credits");
```

**✅ DO:**
```csharp
_logger.LogInformation(
    "User consumed quota credits",
    userId,  // Automatically filtered from logs (PII)
    amount,
    observabilityContext.CorrelationId
);
```

## Troubleshooting

### Symptoms → Runbooks

| Symptom | Runbook |
|---------|---------|
| High error rate (5xx) | [RUNBOOKS/high-error-rate.md](RUNBOOKS/high-error-rate.md) |
| High latency | [RUNBOOKS/high-latency.md](RUNBOOKS/high-latency.md) |
| Redis connectivity issues | [RUNBOOKS/redis-issues.md](RUNBOOKS/redis-issues.md) |
| Postgres slow queries | [RUNBOOKS/postgres-issues.md](RUNBOOKS/postgres-issues.md) |
| Quota anomalies | [RUNBOOKS/quota-anomalies.md](RUNBOOKS/quota-anomalies.md) |
| Webhook failures | [RUNBOOKS/webhook-failures.md](RUNBOOKS/webhook-failures.md) |

### Quick Diagnostics

```bash
# Check health endpoints
curl https://api.whatshouldido.com/health/ready

# Check metrics endpoint
curl https://api.whatshouldido.com/metrics

# Query recent errors
kubectl logs -l app=whatshouldido-api --tail=100 | grep "ERROR"

# Check Redis connectivity
kubectl exec -it redis-0 -- redis-cli PING

# Check Postgres connectivity
kubectl exec -it postgres-0 -- psql -U postgres -c "SELECT 1;"
```

## Deployment

### Kubernetes Deployment

```bash
# Apply manifests
kubectl apply -f deploy/k8s/

# Verify deployment
kubectl rollout status deployment/whatshouldido-api

# Check pods
kubectl get pods -l app=whatshouldido-api

# Check metrics scraping
kubectl get servicemonitor whatshouldido-api -o yaml
```

### Helm Deployment

```bash
# Install/upgrade
helm upgrade --install whatshouldido-api ./deploy/helm/whatshouldido \
  --namespace production \
  --values deploy/helm/values.production.yaml

# Verify
helm test whatshouldido-api
```

## References

- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/instrumentation/net/)
- [Prometheus Best Practices](https://prometheus.io/docs/practices/naming/)
- [Grafana Dashboard Best Practices](https://grafana.com/docs/grafana/latest/best-practices/best-practices-for-creating-dashboards/)
- [SRE Book - Monitoring Distributed Systems](https://sre.google/sre-book/monitoring-distributed-systems/)
- [Site Reliability Workbook](https://sre.google/workbook/table-of-contents/)

## Support

For questions or issues:
- **Runbooks**: `RUNBOOKS/` directory
- **Dashboards**: Grafana at https://grafana.whatshouldido.com
- **Metrics**: Prometheus at https://prometheus.whatshouldido.com
- **Traces**: Tempo/Jaeger at https://tempo.whatshouldido.com
- **On-call**: PagerDuty integration via Alertmanager
