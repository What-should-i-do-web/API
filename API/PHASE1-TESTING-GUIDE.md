# Phase 1 Testing Guide - OpenTelemetry Integration

This guide walks you through testing the newly integrated observability stack for WhatShouldIDo API.

## Prerequisites

- Docker and Docker Compose installed
- .NET 9.0 SDK installed
- Terminal/PowerShell access

## Step 1: Restore NuGet Packages

```bash
cd src/WhatShouldIDo.API
dotnet restore
```

Expected output:
```
Restored C:\Users\ertan\Desktop\LAB\...\WhatShouldIDo.API.csproj (in X ms).
```

## Step 2: Build the Solution

```bash
dotnet build
```

Expected output:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

If you see any errors about missing references, ensure all packages were restored correctly.

## Step 3: Start the Observability Stack

From the API directory:

```bash
cd C:\Users\ertan\Desktop\LAB\githubProjects\WhatShouldIDo\NeYapsamWeb\API
docker-compose -f docker-compose.observability.yml up -d
```

This starts:
- Prometheus (metrics) on port 9090
- Grafana (dashboards) on port 3000
- Tempo (traces) on port 3200
- Loki (logs) on port 3100
- Redis (quota store) on port 6379
- Postgres (database) on port 5432
- Exporters for Redis, Postgres, and Node metrics

Wait for all containers to be healthy (about 30 seconds):

```bash
docker-compose -f docker-compose.observability.yml ps
```

All services should show "Up" status.

## Step 4: Run Database Migrations

```bash
cd src/WhatShouldIDo.API
dotnet ef database update
```

This ensures your Postgres database has all the required tables.

## Step 5: Start the API

```bash
dotnet run
```

Watch for these log messages:
```
[INFO] OpenTelemetry initialized: ServiceName=whatshouldido-api, TraceSampling=100%, Prometheus=True, OTLP=True
[INFO] Quota System Initialized: DefaultFreeQuota=5, DailyReset=False, Backend=InMemory
[INFO] Health checks registered: Redis, PostgreSQL, Self
[INFO] WhatShouldIDo API started successfully
[INFO] Health endpoints: /health/ready, /health/live, /health/startup
[INFO] Metrics endpoint: /metrics
```

## Step 6: Verify Health Endpoints

Open a new terminal and test each health endpoint:

### 6.1 Simple Health Check (Legacy)
```bash
curl http://localhost:5000/health
```

Expected response:
```json
{"status":"ok"}
```

### 6.2 Readiness Check
```bash
curl http://localhost:5000/health/ready
```

Expected response (formatted):
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
        "latency_ms": 12.34,
        "database": "Wisido"
      }
    }
  }
}
```

### 6.3 Liveness Check
```bash
curl http://localhost:5000/health/live
```

Expected response:
```json
{
  "status": "Healthy",
  "entries": {
    "self": {
      "status": "Healthy",
      "description": "API is running"
    }
  }
}
```

### 6.4 Startup Check
```bash
curl http://localhost:5000/health/startup
```

Should return the same as `/health/ready`.

## Step 7: Verify Metrics Endpoint

```bash
curl http://localhost:5000/metrics
```

Expected output (OpenMetrics format):
```
# HELP requests_total Total number of HTTP requests
# TYPE requests_total counter
requests_total{endpoint="/health",method="GET",status_code="200",authenticated="False",premium="unknown"} 1

# HELP request_duration_seconds Request duration in seconds
# TYPE request_duration_seconds histogram
request_duration_seconds_bucket{endpoint="/health",method="GET",le="0.005"} 1
...

# HELP quota_consumed_total Total quota credits consumed
# TYPE quota_consumed_total counter
quota_consumed_total 0
...
```

## Step 8: Generate Some Traffic

Run these commands to generate test traffic:

```bash
# Make 10 requests to health endpoint
for i in {1..10}; do curl -s http://localhost:5000/health > /dev/null; done

# Check metrics again
curl -s http://localhost:5000/metrics | grep requests_total
```

You should see the counter increase.

## Step 9: Verify Prometheus Scraping

1. Open Prometheus UI: http://localhost:9090
2. Go to **Status > Targets**
3. Verify all targets are "UP":
   - whatshouldido-api
   - prometheus
   - node
   - redis
   - postgres

4. Run a test query:
   - Go to **Graph** tab
   - Enter query: `requests_total`
   - Click **Execute**
   - You should see your API metrics

5. Test another query:
   - Query: `rate(requests_total[1m])`
   - This shows requests per second

## Step 10: Verify Grafana

1. Open Grafana: http://localhost:3000
2. Login with: **admin / admin** (skip password change for now)
3. Go to **Connections > Data Sources**
4. Verify these data sources exist:
   - Prometheus (should be green/connected)
   - Tempo (should be green/connected)
   - Loki (should be green/connected)

5. Go to **Explore**
6. Select **Prometheus** data source
7. Run query: `requests_total`
8. You should see a graph of your API metrics

## Step 11: Verify Distributed Tracing

Generate a request with trace context:

```bash
curl -H "traceparent: 00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" \
     http://localhost:5000/health
```

1. Open Grafana Explore: http://localhost:3000/explore
2. Select **Tempo** data source
3. Click **Search**
4. You should see recent traces
5. Click on a trace to see the span details

Note: Traces may take 10-30 seconds to appear in Tempo.

## Step 12: Test Correlation IDs

Every response should include `X-Correlation-Id` header:

```bash
curl -v http://localhost:5000/health 2>&1 | grep X-Correlation-Id
```

Expected:
```
< X-Correlation-Id: a1b2c3d4e5f6789012345678901234567
```

You can also provide your own:

```bash
curl -H "X-Correlation-Id: my-custom-id-123" http://localhost:5000/health -v 2>&1 | grep X-Correlation-Id
```

Expected:
```
< X-Correlation-Id: my-custom-id-123
```

## Step 13: Test Quota System with Instrumentation

Create a test request to trigger quota consumption (requires authentication setup):

```bash
# This will fail with 401 if auth isn't set up yet - that's expected
curl -v http://localhost:5000/api/discover

# Check metrics for quota
curl -s http://localhost:5000/metrics | grep quota
```

You should see:
```
quota_consumed_total 0
quota_blocked_total 0
quota_users_with_zero 0
```

## Step 14: Test Redis Instrumentation

Check Redis operation metrics:

```bash
curl -s http://localhost:5000/metrics | grep redis
```

Expected output:
```
redis_quota_script_latency_seconds_bucket{operation="get",success="true",le="0.001"} 0
...
redis_errors_total{operation="get"} 0
```

## Step 15: Verify Logs in Loki

1. Open Grafana Explore: http://localhost:3000/explore
2. Select **Loki** data source
3. Use query: `{job="whatshouldido-api"}`
4. You should see application logs

Note: Logs appear immediately if using Promtail, or within 30 seconds if using OTLP.

## Troubleshooting

### Issue: "Could not load file or assembly 'OpenTelemetry'"

Solution:
```bash
dotnet restore --force
dotnet build --no-restore
```

### Issue: Health check shows "Unhealthy" for Redis

Solution:
```bash
# Check if Redis container is running
docker ps | grep redis

# Check Redis logs
docker logs redis-quota

# Test Redis connection manually
docker exec -it redis-quota redis-cli PING
# Should return: PONG
```

### Issue: Health check shows "Unhealthy" for Postgres

Solution:
```bash
# Check if Postgres container is running
docker ps | grep postgres

# Check Postgres logs
docker logs postgres-app

# Test Postgres connection manually
docker exec -it postgres-app psql -U postgres -c "SELECT 1;"
# Should return: 1
```

### Issue: No metrics showing in Prometheus

Solution:
1. Check Prometheus targets: http://localhost:9090/targets
2. If `whatshouldido-api` shows "DOWN", check:
   - API is running on port 5000
   - Try `host.docker.internal:5000` instead of `localhost:5000` in `prometheus.yml`

### Issue: Traces not appearing in Tempo

Solution:
1. Check Tempo is running: `docker logs tempo`
2. Verify OTLP endpoint in appsettings: `http://tempo:4317`
3. Generate more traffic - traces have sampling rate (100% in dev, 5% in prod)
4. Wait 30 seconds for traces to be flushed

### Issue: API fails to start with "Port 5000 already in use"

Solution:
```bash
# Find and kill the process using port 5000
# Windows:
netstat -ano | findstr :5000
taskkill /PID <PID> /F

# Linux/Mac:
lsof -ti:5000 | xargs kill -9

# Or change the port in launchSettings.json
```

## Success Criteria Checklist

Phase 1 is complete when you can check all these boxes:

- ✅ API builds without errors
- ✅ All health endpoints return Healthy status
- ✅ `/metrics` endpoint returns OpenMetrics data
- ✅ Prometheus scrapes API metrics successfully
- ✅ Grafana can query metrics from Prometheus
- ✅ Correlation IDs appear in all responses
- ✅ Redis operations generate metrics
- ✅ All Docker containers are running and healthy
- ✅ Logs appear in Loki (if configured)
- ✅ Traces appear in Tempo (after traffic generation)

## Next Steps (Phase 2)

Once Phase 1 is validated:

1. Create Grafana dashboards (API Golden Signals, Quota, Persistence)
2. Import Prometheus alert rules
3. Configure Alertmanager for notifications
4. Write additional runbooks
5. Deploy to Kubernetes/production environment

## Cleanup

To stop all containers:

```bash
docker-compose -f docker-compose.observability.yml down
```

To stop and remove all data (careful!):

```bash
docker-compose -f docker-compose.observability.yml down -v
```

## Support

If you encounter issues not covered here:
1. Check the main README-Observability.md
2. Check the RUNBOOKS/ directory
3. Review application logs: `logs/dev-api-*.txt`
4. Check Docker logs: `docker-compose -f docker-compose.observability.yml logs`

---

**Last Updated:** 2025-01-24
**Phase:** 1 - OpenTelemetry Integration
**Status:** Complete
