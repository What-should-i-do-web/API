import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');
const spatialQueryDuration = new Trend('spatial_query_duration');

// Stress test configuration - Higher load to test limits
export const options = {
  stages: [
    { duration: '1m', target: 50 },   // Ramp up to 50 users
    { duration: '2m', target: 100 },  // Ramp up to 100 users
    { duration: '5m', target: 100 },  // Stay at 100 users (stress level)
    { duration: '2m', target: 200 },  // Ramp up to 200 users (breaking point test)
    { duration: '3m', target: 200 },  // Stay at 200 users
    { duration: '2m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<1000'],    // 95% under 1 second (relaxed for stress test)
    http_req_failed: ['rate<0.2'],        // Allow higher error rate in stress test
    errors: ['rate<0.2'],
    spatial_query_duration: ['p(95)<500'], // Spatial queries should still be fast
  },
};

const BASE_URL = 'http://localhost:5296';

// Cities in Turkey for realistic location testing
const testLocations = [
  { name: 'Istanbul', lat: 41.0082, lng: 28.9784 },
  { name: 'Ankara', lat: 39.9334, lng: 32.8597 },
  { name: 'Izmir', lat: 38.4192, lng: 27.1287 },
  { name: 'Antalya', lat: 36.8841, lng: 30.7056 },
  { name: 'Bursa', lat: 40.1826, lng: 29.0665 },
  { name: 'Adana', lat: 37.0000, lng: 35.3213 },
];

export default function () {
  // Select random location
  const location = testLocations[Math.floor(Math.random() * testLocations.length)];
  
  // Test the core discover endpoint with spatial queries (most important)
  const discoverUrl = `${BASE_URL}/api/discover?lat=${location.lat}&lng=${location.lng}&radius=${Math.floor(Math.random() * 5000) + 1000}`;
  
  const headers = {
    'User-Agent': 'k6-stress-test/1.0.0',
    'Accept': 'application/json',
    'Accept-Language': ['en-US', 'tr-TR', 'de-DE'][Math.floor(Math.random() * 3)]
  };

  // Add API key occasionally to test rate limiting under stress
  if (Math.random() > 0.8) {
    headers['X-API-Key'] = Math.random() > 0.5 ? 'premium_key_456' : 'basic_key_789';
  }

  const startTime = Date.now();
  const response = http.get(discoverUrl, { headers });
  const duration = Date.now() - startTime;
  
  // Record spatial query performance
  spatialQueryDuration.add(duration);

  const checkResult = check(response, {
    'status is 200, 429, or 503': (r) => [200, 429, 503].includes(r.status),
    'response time reasonable': (r) => r.timings.duration < 5000, // More lenient for stress test
    'has response body': (r) => r.body && r.body.length > 0,
  });

  errorRate.add(!checkResult);

  // Log interesting responses
  if (response.status === 429) {
    console.log(`⚡ Rate limited at ${location.name} (tier: ${response.headers['X-RateLimit-Tier']})`);
  } else if (response.status === 503) {
    console.log(`🔥 Service overloaded at ${location.name}`);
  } else if (response.status === 200) {
    try {
      const data = JSON.parse(response.body);
      if (data.suggestions && data.suggestions.length > 0) {
        console.log(`✅ Found ${data.suggestions.length} places in ${location.name} (${duration}ms)`);
      }
    } catch (e) {
      // Ignore parsing errors in stress test
    }
  }

  // Reduced sleep time for stress testing
  sleep(Math.random() * 0.5 + 0.1); // 0.1-0.6 second pause
}

export function setup() {
  console.log('🔥 Starting WhatShouldIDo API Stress Test');
  console.log('⚠️  This test will push the API to its limits');
  
  // Check if Redis cluster is running
  const healthResponse = http.get(`${BASE_URL}/api/performance/health`);
  console.log(`📊 Initial health check: ${healthResponse.status}`);
  
  return { startTime: Date.now() };
}

export function teardown(data) {
  const duration = (Date.now() - data.startTime) / 1000;
  console.log(`🏁 Stress test completed in ${duration.toFixed(1)} seconds`);
  console.log('📈 Check Grafana dashboards for performance metrics');
}