# Incident Playbook - WhatShouldIDo API

This document provides actionable guidance for responding to incidents in the WhatShouldIDo API.

---

## First 5 Minutes Checklist

When an incident occurs, follow these steps immediately:

1. **Acknowledge the alert** - Prevent duplicate pages
2. **Check `/health/ready`** - Is the service healthy?
3. **Check Grafana dashboard** - Look at api-golden-signals
4. **Get correlation ID** - From client or logs
5. **Identify scope** - Is it one user, one endpoint, or all traffic?

---

## Finding Correlation IDs

### From Client Request
Look for the `X-Correlation-Id` response header:
```bash
curl -v https://api.whatshouldido.com/api/health/ready 2>&1 | grep -i correlation
```

### From Logs
Search in logs by correlation ID:
```bash
# In Seq/Loki
correlation_id = "abc123"

# In raw logs
grep "abc123" logs/*.txt
```

### Generating a Correlation ID
If missing, request will auto-generate one. To trace your own:
```bash
curl -H "X-Correlation-Id: manual-test-$(date +%s)" \
  https://api.whatshouldido.com/api/suggestions
```

---

## Finding Traces

### Via Jaeger/Tempo

1. Go to Jaeger UI: `https://jaeger.local/search`
2. Select Service: `whatshouldido-api`
3. Search by:
   - Correlation ID (tag: `correlation_id`)
   - User ID hashed (tag: `user_id_hashed`)
   - Intent (tag: `intent`)
   - Operation name

### Key Span Attributes

| Attribute | Description |
|-----------|-------------|
| `correlation_id` | Request correlation ID |
| `intent` | Suggestion intent (FOOD_ONLY, ROUTE_PLANNING, etc.) |
| `provider` | External provider (google, opentrip, openai) |
| `cache_hit` | Whether cache was hit (true/false) |
| `cache_layer` | Cache layer (L1, L2, L3) |
| `premium` | Is user premium |
| `authenticated` | Is request authenticated |
| `quota_consumed` | Quota credits consumed |
| `user_id_hashed` | Hashed user ID (for privacy) |

### Span Names to Look For

- `Suggestions.Orchestrate` - Main orchestration
- `Providers.SearchPlaces` - External place search
- `Cache.Get` / `Cache.Set` - Cache operations
- `Route.Build` - Route optimization
- `Personalization.Apply` - User personalization
- `Explainability.GenerateReasons` - Reason generation

---

## Common Failure Scenarios

### 1. External Provider Down (Google Places / OpenTripMap)

**Symptoms:**
- Alert: `PlaceSearchHighErrorRate`
- Slow responses or timeouts on `/api/suggestions`
- Increased cache misses

**Investigation:**
1. Check provider-specific metrics:
   ```promql
   rate(place_search_errors_total[5m]) by (provider)
   ```
2. Look at trace spans for `Providers.SearchPlaces`
3. Test provider directly:
   ```bash
   curl "https://maps.googleapis.com/maps/api/place/nearbysearch/json?key=$KEY&location=41,29&radius=1000"
   ```

**Mitigation:**
- Hybrid orchestrator will fallback to secondary provider
- Increase cache TTLs if prolonged outage
- Consider enabling OpenTripMap-only mode

### 2. Redis Down / Slow

**Symptoms:**
- Alert: `RedisHighErrorRate` or `RedisQuotaScriptHighLatency`
- Quota checks failing
- Cache operations timing out

**Investigation:**
1. Check Redis metrics:
   ```promql
   rate(redis_errors_total[5m])
   histogram_quantile(0.95, rate(redis_quota_script_latency_seconds_bucket[5m]))
   ```
2. Check Redis cluster health:
   ```bash
   redis-cli -c -h redis-node-1 cluster info
   redis-cli -c -h redis-node-1 cluster nodes
   ```

**Mitigation:**
- Fallback cache (InMemory) should activate automatically
- Quota store has InMemory fallback
- Check Redis node resource usage
- Restart unhealthy Redis nodes

### 3. Database (PostgreSQL) Issues

**Symptoms:**
- Alert: `PostgresHighLatency` or `PostgresHighErrorRate`
- Slow authentication / user queries
- Connection pool exhaustion

**Investigation:**
1. Check DB metrics:
   ```promql
   histogram_quantile(0.95, rate(db_latency_seconds_bucket[5m]))
   ```
2. Check active connections:
   ```sql
   SELECT count(*) FROM pg_stat_activity WHERE state = 'active';
   ```
3. Look for slow queries in logs

**Mitigation:**
- Check for long-running queries and kill if needed
- Scale connection pool if needed
- Check disk I/O on database server

### 4. Quota System Blocking Requests

**Symptoms:**
- Alert: `QuotaBlockSurge`
- Users reporting 403 errors
- High `quota_blocked_total`

**Investigation:**
1. Check quota metrics:
   ```promql
   rate(quota_blocked_total[5m])
   quota_users_with_zero
   ```
2. Verify quota reset job ran:
   ```promql
   quota_reset_total
   ```

**Mitigation:**
- Check if daily reset job is running
- Manually trigger reset if needed via admin endpoint
- Temporarily increase quota limit in config

### 5. AI Provider Issues (OpenAI)

**Symptoms:**
- Alert: `AIProviderHighErrorRate`
- Day planning / itinerary generation failing
- Slow AI responses

**Investigation:**
1. Check AI metrics:
   ```promql
   rate(ai_request_errors_total[5m]) by (provider)
   ```
2. Check OpenAI status page
3. Look at trace spans for AI operations

**Mitigation:**
- Fallback to HuggingFace or NoOp provider
- Disable AI features temporarily
- Check API key validity and quota

---

## Escalation Matrix

| Severity | Response Time | Escalate To |
|----------|---------------|-------------|
| Critical | 5 min | On-call engineer + Tech Lead |
| Warning | 30 min | On-call engineer |
| Info | Next business day | Team lead review |

---

## Useful Queries

### Error Rate by Endpoint
```promql
sum(rate(requests_total{status_code=~"5.."}[5m])) by (endpoint)
/
sum(rate(requests_total[5m])) by (endpoint)
```

### P95 Latency by Intent
```promql
histogram_quantile(0.95,
  sum(rate(suggestion_orchestration_duration_seconds_bucket[5m])) by (le, intent)
)
```

### Cache Hit Rate
```promql
sum(rate(cache_hits_total[5m])) by (layer)
/
(sum(rate(cache_hits_total[5m])) by (layer) + sum(rate(cache_misses_total[5m])) by (layer))
```

### Active Premium Users
```promql
sum(rate(entitlement_checks_total{outcome="premium"}[1h]))
```

---

## Rollback Procedures

### Application Rollback
```bash
# Kubernetes
kubectl rollout undo deployment/whatshouldido-api -n production

# Docker Compose
docker compose down
docker compose pull
docker compose up -d
```

### Database Rollback
1. Identify the migration to rollback
2. Run: `dotnet ef database update <previous-migration>`
3. Verify data integrity

---

## Post-Incident

After resolving an incident:

1. **Update status page** (if applicable)
2. **Write incident report** within 24 hours
3. **Create follow-up tickets** for improvements
4. **Update this playbook** if new scenarios discovered

---

## Contact Information

- **On-Call Schedule**: PagerDuty / Opsgenie
- **Slack Channel**: #whatshouldido-incidents
- **Status Page**: status.whatshouldido.com

---

*Last Updated: 2026-01-16*
