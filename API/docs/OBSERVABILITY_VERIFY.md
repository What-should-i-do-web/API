# Observability Verification Checklist

This document provides a verification checklist for the WhatShouldIDo API observability stack.

---

## Prerequisites

Ensure you have the following running:
- Docker / Docker Compose
- API service
- Prometheus
- Grafana
- Jaeger/Tempo (optional for tracing)

Start the full observability stack:
```bash
docker compose -f docker-compose.yml -f docker-compose.observability.yml up -d
```

---

## Health Check Verification

### 1. Liveness Check
```bash
curl -s http://localhost:5000/health/live | jq
```

**Expected output:**
```json
{
  "status": "Healthy",
  "results": {}
}
```

### 2. Readiness Check
```bash
curl -s http://localhost:5000/health/ready | jq
```

**Expected output:**
```json
{
  "status": "Healthy",
  "results": {
    "postgresql": { "status": "Healthy" },
    "redis": { "status": "Healthy" }
  }
}
```

### 3. Startup Check
```bash
curl -s http://localhost:5000/health/startup | jq
```

---

## Metrics Verification

### 1. Prometheus Metrics Endpoint
```bash
curl -s http://localhost:5000/metrics | head -50
```

**Expected:** Prometheus text format with metrics like:
- `requests_total`
- `request_duration_seconds`
- `quota_consumed_total`
- `cache_hits_total`

### 2. Key Metrics Presence Check
```bash
curl -s http://localhost:5000/metrics | grep -E "^(requests_total|request_duration_seconds|quota_|cache_|place_search)" | head -20
```

### 3. Prometheus Targets
Open Prometheus UI: http://localhost:9090/targets

Verify target `whatshouldido-api` is UP.

### 4. Sample Prometheus Queries

**Request Rate:**
```promql
sum(rate(requests_total[5m])) by (endpoint)
```

**Error Rate:**
```promql
sum(rate(requests_total{status_code=~"5.."}[5m])) / sum(rate(requests_total[5m]))
```

**P95 Latency:**
```promql
histogram_quantile(0.95, sum(rate(request_duration_seconds_bucket[5m])) by (le))
```

---

## Tracing Verification

### 1. Generate Trace
```bash
TRACE_ID=$(uuidgen | tr -d '-' | head -c 32)
curl -v -H "X-Correlation-Id: test-$TRACE_ID" \
  http://localhost:5000/api/suggestions \
  -H "Content-Type: application/json" \
  -d '{"intent": 0, "latitude": 41.0082, "longitude": 28.9784, "radiusMeters": 1000}'
```

### 2. Verify Correlation ID in Response
Check for `X-Correlation-Id` header in response.

### 3. Find Trace in Jaeger/Tempo

1. Open Jaeger UI: http://localhost:16686
2. Service: `whatshouldido-api`
3. Search by correlation ID tag

### 4. Expected Spans

For `/api/suggestions` request, expect these spans:
- `HTTP POST /api/suggestions`
- `Suggestions.Orchestrate`
- `Providers.SearchPlaces`
- `Cache.Get` / `Cache.Set`
- `Personalization.Apply` (if authenticated)
- `Route.Build` (if ROUTE_PLANNING)

---

## Grafana Dashboard Verification

### 1. Access Grafana
Open: http://localhost:3000

Default credentials: `admin` / `admin`

### 2. Check Dashboards

Navigate to Dashboards and verify:
- **API Overview** dashboard exists
- **Quota & Entitlements** dashboard exists
- **Persistence** dashboard exists

### 3. Dashboard Panels Working

On API Overview dashboard, verify these panels:
- Request Rate (showing data)
- Error Rate (showing data)
- Latency Percentiles (showing data)
- Active endpoints (showing data)

---

## Alert Verification

### 1. Check Alert Rules in Prometheus
Open: http://localhost:9090/alerts

Verify these alert groups exist:
- `availability-slo`
- `latency-slo`
- `quota-system-health`
- `database-health`
- `provider-health`
- `rate-limiting`
- `infrastructure`

### 2. Test Alert Rule Syntax
```bash
promtool check rules deploy/prometheus/alerts/slo-alerts.yml
```

### 3. Simulate Alert (Manual)

Temporarily set an alert threshold very low:
```promql
# In Prometheus, check if this would fire
(sum(rate(requests_total{status_code=~"5.."}[5m])) / sum(rate(requests_total[5m]))) > 0.0001
```

---

## Log Verification

### 1. Check Log Output
```bash
# View recent logs
docker logs whatshouldido-api --tail 50

# Or read from file
tail -50 src/WhatShouldIDo.API/logs/api-*.txt
```

### 2. Verify Structured Logging

Logs should be JSON formatted with fields:
- `@t` (timestamp)
- `@l` (level)
- `@m` (message)
- `CorrelationId`
- `RequestPath`
- `StatusCode`

### 3. Correlation ID in Logs

```bash
# Search for specific correlation ID
grep "test-abc123" src/WhatShouldIDo.API/logs/*.txt
```

---

## Integration Test Commands

### 1. Quick Smoke Test
```bash
# Health
curl -s http://localhost:5000/health/ready

# Metrics
curl -s http://localhost:5000/metrics | wc -l

# API endpoint
curl -s http://localhost:5000/api/suggestions/intents
```

### 2. Full Integration Test
```bash
dotnet test --filter "Category=Integration"
```

---

## Provider Failure Simulation

### 1. Simulate Google Places Failure

Set invalid API key temporarily:
```bash
# In appsettings.Development.json
"GooglePlaces": {
  "ApiKey": "INVALID_KEY"
}
```

Then make request and verify:
- Alert fires (if configured)
- Fallback to OpenTripMap works
- Error logged with correlation ID

### 2. Simulate Redis Failure

```bash
# Stop Redis
docker stop redis-node-1 redis-node-2 redis-node-3

# Make request
curl http://localhost:5000/api/health/ready

# Verify fallback to in-memory cache
# Restart Redis
docker start redis-node-1 redis-node-2 redis-node-3
```

---

## Checklist Summary

| Check | Command | Expected |
|-------|---------|----------|
| Liveness | `curl /health/live` | 200 OK, Healthy |
| Readiness | `curl /health/ready` | 200 OK, all dependencies healthy |
| Metrics | `curl /metrics` | Prometheus format metrics |
| Prometheus | UI targets page | Target UP |
| Grafana | Dashboard panels | Data displayed |
| Alerts | Prometheus alerts | Rules loaded |
| Tracing | Jaeger search | Spans visible |
| Logs | Container/file logs | Structured JSON |
| Correlation | Request with X-Correlation-Id | Same ID in response |

---

## Troubleshooting

### Metrics Not Showing

1. Check API is running: `docker ps`
2. Check metrics endpoint: `curl http://localhost:5000/metrics`
3. Check Prometheus config: `prometheus.yml` has correct target
4. Check Prometheus logs: `docker logs prometheus`

### Traces Not Appearing

1. Check OTLP exporter config in `appsettings.json`
2. Verify Jaeger/Tempo is running: `docker ps | grep jaeger`
3. Check for OTLP errors in API logs
4. Verify sampling rate is not 0%

### Alerts Not Firing

1. Verify alert rules are loaded: Prometheus UI > Alerts
2. Check rule syntax: `promtool check rules`
3. Test query manually in Prometheus
4. Check Alertmanager configuration (if using)

---

*Last Updated: 2026-01-16*
