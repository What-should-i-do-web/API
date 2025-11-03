# Runbook: Redis Issues

## Overview

This runbook covers common Redis connectivity, latency, and error scenarios for the WhatShouldIDo quota system.

**Related Alerts:**
- `RedisDown`
- `RedisHighLatency`
- `RedisHighErrorRate`
- `RedisQuotaScriptHighLatency_Critical`
- `RedisQuotaScriptHighLatency_Warning`

**Dashboards:**
- [Quota & Entitlements Dashboard](https://grafana.local/d/quota-entitlements)
- [Persistence Dashboard](https://grafana.local/d/persistence)

---

## Severity Classification

| Severity | Impact | Response Time |
|----------|--------|---------------|
| **Critical** | Quota system down, all free users blocked | < 15 minutes |
| **High** | Elevated latency (>100ms), some requests timing out | < 30 minutes |
| **Medium** | Degraded performance (50-100ms latency) | < 2 hours |
| **Low** | Minor issues, no user impact | Next business day |

---

## Scenario 1: Redis Completely Down

### Symptoms
- Alert: `RedisDown`
- All `/health/ready` checks failing
- Free users receiving 403 "Quota exhausted" (fail-closed behavior)
- Premium users unaffected (DB fallback)
- Metrics: `redis_errors_total` spiking

### Diagnosis

1. **Check Redis pod status:**
```bash
kubectl get pods -l app=redis -n production
# Look for: CrashLoopBackOff, Error, Pending

kubectl logs redis-0 --tail=100
# Check for: OOM, config errors, disk full
```

2. **Check Redis connectivity:**
```bash
kubectl exec -it redis-0 -- redis-cli PING
# Expected: PONG
# If timeout: network issue
# If "NOAUTH": ACL/password issue
```

3. **Check resource usage:**
```bash
kubectl top pod redis-0
# Look for: Memory near limit, CPU throttling
```

### Resolution

**Option A: Redis Pod Restart (Quick Fix)**
```bash
# If pod is in bad state, restart it
kubectl delete pod redis-0 -n production
# Kubernetes will recreate it

# Watch for recovery
kubectl get pods -l app=redis -w
```

**Option B: Redis Memory Full**
```bash
# Connect to Redis
kubectl exec -it redis-0 -- redis-cli

# Check memory usage
INFO memory
# Look at: used_memory_human, maxmemory_human

# If at limit, flush least-recently-used keys (safe for quota cache)
MEMORY PURGE

# Or increase memory limit (requires pod restart)
```

**Option C: Redis Config Issue**
```bash
# Check ConfigMap
kubectl get configmap redis-config -o yaml

# Common issues:
# - Invalid ACL rules
# - Wrong maxmemory setting
# - Broken replication config

# Fix and apply
kubectl edit configmap redis-config
kubectl rollout restart statefulset/redis
```

### Verification
```bash
# 1. Check health endpoint
curl https://api.whatshouldido.com/health/ready | jq '.entries.redis'

# 2. Test quota consumption (requires auth token)
curl -H "Authorization: Bearer $TOKEN" \
     https://api.whatshouldido.com/api/discover

# 3. Check metrics
curl https://api.whatshouldido.com/metrics | grep redis_errors_total
```

### Communication Template
```
INCIDENT: Redis quota store is down
IMPACT: Free users unable to make requests (fail-closed)
STATUS: Investigating / Mitigating / Resolved
NEXT UPDATE: [Time]
WORKAROUND: Premium users unaffected. Free users: please wait.
```

---

## Scenario 2: High Redis Latency

### Symptoms
- Alert: `RedisQuotaScriptHighLatency_Warning` or `_Critical`
- Requests completing but slow (p95 > 50ms, p99 > 100ms)
- Quota operations timing out intermittently
- Metrics: `redis_quota_script_latency_seconds` elevated

### Diagnosis

1. **Check Redis latency:**
```bash
kubectl exec -it redis-0 -- redis-cli

# Intrinsic latency test (baseline)
redis-cli --intrinsic-latency 60
# Should be < 1ms on good hardware

# Client latency
redis-cli --latency-history
# Look for spikes > 10ms
```

2. **Check for slow commands:**
```bash
redis-cli SLOWLOG GET 10
# Look for: KEYS *, SCAN with large counts, blocking commands

# If found, identify the source and fix
```

3. **Check CPU and memory:**
```bash
kubectl top pod redis-0

# If CPU > 80%, Redis may be overloaded
# If memory near limit, evictions may be slow
```

4. **Check network:**
```bash
# From API pod to Redis
kubectl exec -it whatshouldido-api-xxx -- sh
time redis-cli -h redis PING
# Should be < 5ms

# If > 20ms, network issue (check CNI plugin, network policies)
```

### Resolution

**Option A: Clear Slow Operations**
```bash
redis-cli SLOWLOG RESET

# If KEYS * found, ban it via ACL
redis-cli ACL SETUSER default -@dangerous +@all
```

**Option B: Increase Redis Resources**
```bash
# Edit StatefulSet
kubectl edit statefulset redis

# Increase CPU/memory requests and limits
resources:
  requests:
    cpu: 1000m      # was 500m
    memory: 512Mi   # was 256Mi
  limits:
    cpu: 2000m
    memory: 1Gi

kubectl rollout status statefulset/redis
```

**Option C: Optimize Lua Scripts**
```bash
# Our CompareExchangeConsumeAsync script is already optimized
# but verify via SCRIPT DEBUG
redis-cli SCRIPT DEBUG YES
# Run operation
# Check logs for script execution time
```

**Option D: Add Read Replicas (Long-term)**
```yaml
# redis-statefulset.yaml
replicas: 3  # was 1

# Configure read/write split in application
```

### Verification
```bash
# Check latency after changes
redis-cli --latency-history

# Check metrics
curl https://api.whatshouldido.com/metrics | \
  grep redis_quota_script_latency_seconds | \
  grep quantile

# Should show p95 < 50ms, p99 < 100ms
```

---

## Scenario 3: Redis Connection Timeouts

### Symptoms
- Intermittent `redis_errors_total` spikes
- Logs: "Timeout performing EVAL"
- Free users occasionally blocked incorrectly
- Metrics: `entitlement_checks_total{outcome="error"}` elevated

### Diagnosis

1. **Check connection pool:**
```bash
# API logs
kubectl logs whatshouldido-api-xxx | grep "Redis timeout"

# Look for:
# - "No connection is available"
# - "Timeout performing EVAL"
# - "Unable to connect to Redis"
```

2. **Check Redis connection count:**
```bash
redis-cli INFO clients
# Look at: connected_clients, maxclients

# If connected_clients near maxclients:
redis-cli CONFIG GET maxclients
redis-cli CONFIG SET maxclients 10000
```

3. **Check Redis password/ACL:**
```bash
# From API pod
kubectl exec -it whatshouldido-api-xxx -- sh
redis-cli -h redis -a $REDIS_PASSWORD PING

# If NOAUTH: password wrong
# If NOPERM: ACL issue
```

### Resolution

**Option A: Increase Connection Timeout**
```json
// appsettings.Production.json
{
  "Redis": {
    "ConnectionString": "redis:6379,password=xxx,connectTimeout=5000,syncTimeout=5000"
    // Increase timeouts if network latency is high
  }
}
```

**Option B: Fix ACL Permissions**
```bash
redis-cli ACL LIST
# Check quota-service user has: +GET +SET +EVAL +EVALSHA

# If missing:
redis-cli ACL SETUSER quota-service on >password ~quota:* +GET +SET +EVAL +EVALSHA
redis-cli ACL SAVE
```

**Option C: Restart Connection Pool**
```bash
# Sometimes stale connections cause issues
# Restart API pods to reset connection pools
kubectl rollout restart deployment/whatshouldido-api
```

---

## Scenario 4: Redis Memory Eviction

### Symptoms
- Quota values unexpectedly reset to null
- Users getting fresh quota after exhaustion
- Metrics: `quota_consumed_total` not matching expected
- Redis logs: "evicted keys"

### Diagnosis

1. **Check eviction policy:**
```bash
redis-cli CONFIG GET maxmemory-policy
# Should be: allkeys-lru or volatile-lru

# If "noeviction", Redis will start rejecting writes when full
```

2. **Check memory usage:**
```bash
redis-cli INFO memory
# Look at: used_memory, maxmemory, evicted_keys

# If evicted_keys > 0, memory is full
```

3. **Check key TTL:**
```bash
# Quota keys should NOT have TTL (unless daily reset enabled)
redis-cli TTL quota:user-id-here
# Should return: -1 (no expiry)
```

### Resolution

**Option A: Increase Redis Memory**
```bash
# Edit StatefulSet or ConfigMap
kubectl edit statefulset redis

# Increase memory limit
resources:
  limits:
    memory: 1Gi  # was 512Mi

# Or adjust maxmemory
redis-cli CONFIG SET maxmemory 1gb
redis-cli CONFIG REWRITE
```

**Option B: Set Appropriate Eviction Policy**
```bash
# For quota cache, use allkeys-lru (evict least-recently-used)
redis-cli CONFIG SET maxmemory-policy allkeys-lru
redis-cli CONFIG REWRITE

# This ensures old user quotas are evicted first
```

**Option C: Monitor and Alert on Evictions**
```promql
# Add alert for high eviction rate
rate(redis_evicted_keys_total[5m]) > 10
```

---

## Scenario 5: Redis TLS/ACL Issues

### Symptoms
- Alert: `RedisDown` or `RedisHighErrorRate`
- API logs: "NOAUTH Authentication required"
- API logs: "NOPERM User has no permissions"
- Premium users also affected

### Diagnosis

1. **Check TLS configuration:**
```bash
# From API pod
kubectl exec -it whatshouldido-api-xxx -- sh

# Test TLS connection
openssl s_client -connect redis:6380 -tls1_2
# Should show: "Verify return code: 0 (ok)"

# If cert error: check cert validity
```

2. **Check ACL permissions:**
```bash
redis-cli ACL LIST
# Look for quota-service user

redis-cli ACL GETUSER quota-service
# Should have: +GET +SET +EVAL +EVALSHA on ~quota:* keys
```

3. **Check secrets:**
```bash
kubectl get secret redis-credentials -o yaml
# Decode and verify password matches app config
```

### Resolution

**Option A: Fix TLS Certificates**
```bash
# Regenerate cert if expired
kubectl delete secret redis-tls-cert
./scripts/generate-redis-cert.sh
kubectl create secret tls redis-tls-cert --cert=redis.crt --key=redis.key

# Restart Redis
kubectl rollout restart statefulset/redis
```

**Option B: Fix ACL Configuration**
```bash
# Create correct ACL for quota-service
redis-cli ACL SETUSER quota-service on >$REDIS_PASSWORD ~quota:* +GET +SET +EVAL +EVALSHA
redis-cli ACL SAVE

# Update app secret
kubectl create secret generic redis-credentials \
  --from-literal=username=quota-service \
  --from-literal=password=$REDIS_PASSWORD \
  --dry-run=client -o yaml | kubectl apply -f -

# Restart API to pick up new secret
kubectl rollout restart deployment/whatshouldido-api
```

---

## Prevention

### Monitoring
- Set up alerts for all Redis metrics listed above
- Monitor: latency, error rate, memory usage, connection count
- Dashboard: [Persistence Dashboard](https://grafana.local/d/persistence)

### Best Practices
1. **Always use Redis with TLS in production**
2. **Set appropriate memory limits and eviction policy**
3. **Use ACLs with least-privilege (quota-service user)**
4. **Monitor quota key count** (should grow linearly with users)
5. **Set up Redis replication for high availability**
6. **Regular backups** (though quota is ephemeral, helps with debugging)
7. **Load test quota system** before traffic spikes

### Useful Commands
```bash
# Quick health check
redis-cli PING

# Check all quota keys
redis-cli --scan --pattern 'quota:*' | wc -l

# Get specific user quota
redis-cli GET quota:USER-GUID-HERE

# Monitor commands in real-time
redis-cli MONITOR

# Check replication lag (if using replicas)
redis-cli INFO replication

# Export all quota keys (for debugging)
redis-cli --scan --pattern 'quota:*' | xargs redis-cli MGET
```

---

## Escalation

If issue persists after these steps:
1. **Slack**: #oncall-engineering
2. **PagerDuty**: Escalate to Infrastructure team
3. **Email**: oncall@whatshouldido.com

**Include:**
- Alert name and time
- Steps attempted
- Relevant logs/metrics screenshots
- Impact assessment (how many users affected)

---

## Post-Incident

After resolving:
1. Update incident timeline in StatusPage
2. Write post-mortem if >5 min outage
3. Add any new learnings to this runbook
4. Review and update alerts if false positive/negative

**Post-Mortem Template:** [TEMPLATE.md](./TEMPLATE.md)
