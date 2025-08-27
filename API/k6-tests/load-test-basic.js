import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');

// Test configuration
export const options = {
  stages: [
    { duration: '2m', target: 20 }, // Ramp up to 20 users
    { duration: '5m', target: 20 }, // Stay at 20 users
    { duration: '2m', target: 50 }, // Ramp up to 50 users  
    { duration: '5m', target: 50 }, // Stay at 50 users
    { duration: '2m', target: 0 },  // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95% of requests should be below 500ms
    http_req_failed: ['rate<0.1'],    // Error rate should be less than 10%
    errors: ['rate<0.1'],             // Custom error rate threshold
  },
};

const BASE_URL = 'http://localhost:5296';

// Test scenarios
const scenarios = [
  {
    name: 'discover_places',
    weight: 40,
    url: '/api/discover',
    params: {
      lat: 41.0082,
      lng: 28.9784,
      radius: 5000
    }
  },
  {
    name: 'performance_health',
    weight: 20,
    url: '/api/performance/health'
  },
  {
    name: 'performance_summary',
    weight: 15,
    url: '/api/performance/summary'
  },
  {
    name: 'analytics_dashboard',
    weight: 15,
    url: '/api/analytics/dashboard'
  },
  {
    name: 'localization_cultures',
    weight: 10,
    url: '/api/localization/cultures'
  }
];

// Weighted random scenario selection
function selectScenario() {
  const random = Math.random() * 100;
  let cumulative = 0;
  
  for (const scenario of scenarios) {
    cumulative += scenario.weight;
    if (random <= cumulative) {
      return scenario;
    }
  }
  
  return scenarios[0]; // Fallback
}

export default function () {
  const scenario = selectScenario();
  let url = `${BASE_URL}${scenario.url}`;
  
  // Add query parameters for discover endpoint
  if (scenario.params) {
    const params = new URLSearchParams(scenario.params);
    url += `?${params.toString()}`;
  }

  // Add some headers to simulate real usage
  const headers = {
    'User-Agent': 'k6-load-test/1.0.0',
    'Accept': 'application/json',
    'Accept-Language': Math.random() > 0.5 ? 'en-US' : 'tr-TR'
  };

  // Add API key for some requests to test rate limiting tiers
  if (Math.random() > 0.7) {
    const apiKeys = [
      'basic_key_789',
      'premium_key_456', 
      'enterprise_key_123'
    ];
    headers['X-API-Key'] = apiKeys[Math.floor(Math.random() * apiKeys.length)];
  }

  const response = http.get(url, { headers });

  // Validation checks
  const checkResult = check(response, {
    'status is 200 or 429': (r) => r.status === 200 || r.status === 429,
    'response time OK': (r) => r.timings.duration < 2000,
    'content is JSON': (r) => {
      try {
        JSON.parse(r.body);
        return true;
      } catch (e) {
        return false;
      }
    }
  });

  // Record errors
  errorRate.add(!checkResult);

  // Log rate limiting responses
  if (response.status === 429) {
    console.log(`Rate limited on ${scenario.name}: ${response.headers['X-RateLimit-Tier']}`);
  }

  // Simulate realistic user behavior
  sleep(Math.random() * 2 + 0.5); // 0.5-2.5 second pause
}

// Setup function - called once at the beginning
export function setup() {
  console.log('🚀 Starting WhatShouldIDo API Load Test');
  console.log(`📊 Testing ${BASE_URL}`);
  
  // Warm up the application
  const response = http.get(`${BASE_URL}/api/performance/health`);
  if (response.status !== 200) {
    console.warn('⚠️  Application may not be ready for testing');
  }
  
  return { startTime: Date.now() };
}

// Teardown function - called once at the end
export function teardown(data) {
  const duration = (Date.now() - data.startTime) / 1000;
  console.log(`✅ Load test completed in ${duration.toFixed(1)} seconds`);
}