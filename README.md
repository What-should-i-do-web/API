# WhatShouldIDo Backend API

A comprehensive .NET 9 Web API for location-based activity suggestions, built with Clean Architecture principles.

---

## 🏗️ Project Structure

```
├── src/
│   ├── WhatShouldIDo.API/          # Web API layer (Controllers, Middleware)
│   ├── WhatShouldIDo.Application/  # Application services & DTOs
│   ├── WhatShouldIDo.Domain/       # Domain entities & business logic
│   ├── WhatShouldIDo.Infrastructure/ # Data access & external services
│   └── WhatShouldIDo.Tests/        # Unit & integration tests
├── docker-compose*.yml            # Docker configurations
├── monitoring/                    # Grafana & Prometheus configs
└── k6-tests/                     # Load testing scripts
```

---

## 🚀 Quick Start

### Prerequisites
- **Docker Desktop** with Docker Compose
- **.NET 9 SDK** (for local development)
- **Git**

### 1. Clone the Repository
```bash
git clone https://github.com/What-should-i-do-web/NeYapsamWeb.git
cd NeYapsamWeb
```

### 2. Start with Docker
```bash
# Basic setup
docker-compose up --build -d

# With monitoring (Grafana + Prometheus)
docker-compose -f docker-compose.yml -f docker-compose.monitoring.yml up --build -d

# With Redis cluster
docker-compose -f docker-compose.yml -f docker-compose.redis-cluster.yml up --build -d
```

### 3. Apply Database Migrations
```bash
# Via Docker
docker-compose exec api dotnet ef database update

# Or locally
cd src/WhatShouldIDo.API
dotnet ef database update
```

### 4. Verify Setup
- **API**: http://localhost:5000
- **Swagger**: http://localhost:5000/swagger
- **Health Check**: http://localhost:5000/health
- **Grafana** (if enabled): http://localhost:3000 (admin/admin)

---

## 🔧 Local Development

### Setup
```bash
# Install EF Core tools
dotnet tool install --global dotnet-ef

# Restore packages
cd src/WhatShouldIDo.API
dotnet restore

# Run locally
dotnet run
```

### Database Management
```bash
# Add new migration
dotnet ef migrations add MigrationName

# Update database
dotnet ef database update

# Remove last migration
dotnet ef migrations remove
```

---

## 📚 API Documentation

### 🔐 Authentication Endpoints
| Method | URL | Description |
|--------|-----|-------------|
| POST | `/api/auth/register` | User registration |
| POST | `/api/auth/login` | User login |
| GET | `/api/auth/me` | Get current user |
| PUT | `/api/auth/profile` | Update user profile |
| GET | `/api/auth/usage` | Get API usage stats |
| POST | `/api/auth/logout` | User logout |

### 📍 Points of Interest (POI) Endpoints
| Method | URL | Description |
|--------|-----|-------------|
| GET | `/api/pois` | List all POIs |
| GET | `/api/pois/{id}` | Get POI by ID |
| GET | `/api/pois/nearby` | Get nearby POIs by location & type |
| POST | `/api/pois` | Create new POI |
| PUT | `/api/pois/{id}` | Update POI |
| DELETE | `/api/pois/{id}` | Delete POI |

### 🗺️ Routes Endpoints
| Method | URL | Description |
|--------|-----|-------------|
| GET | `/api/routes` | List all routes |
| GET | `/api/routes/{id}` | Get route by ID |
| POST | `/api/routes` | Create new route |
| PUT | `/api/routes/{id}` | Update route |
| DELETE | `/api/routes/{id}` | Delete route |

### 🎯 Discovery & Suggestions
| Method | URL | Description |
|--------|-----|-------------|
| POST | `/api/discover/prompt` | Get AI-powered suggestions |
| GET | `/api/discover/random` | Get random suggestions |
| GET | `/api/discover/nearby` | Get nearby suggestions |

### 📊 Analytics & Monitoring
| Method | URL | Description |
|--------|-----|-------------|
| GET | `/api/analytics/overview` | Get analytics overview |
| GET | `/api/metrics` | Prometheus metrics |
| GET | `/api/performance/status` | Performance metrics |
| GET | `/health` | Health check endpoint |

---

## 🛠️ Configuration

### Environment Variables
Create `.env` file in the root directory:

```env
# Database
ConnectionStrings__DefaultConnection=Server=localhost,1433;Database=WhatShouldIDo;User Id=sa;Password=YourPassword;TrustServerCertificate=True;

# External APIs
GooglePlaces__ApiKey=your_google_places_api_key
Geoapify__ApiKey=your_geoapify_api_key

# Redis (optional)
Redis__ConnectionString=localhost:6379

# JWT
JWT__SecretKey=your_jwt_secret_key
JWT__Issuer=WhatShouldIDo
JWT__Audience=WhatShouldIDoClients
```

### Database Configuration
- **Provider**: SQL Server
- **Connection**: localhost:1433 (Docker) or LocalDB (local dev)
- **Migrations**: Auto-applied on startup in Docker

---

## 🧪 Testing

### Demo User (for frontend testing)
- **Email**: `demo@example.com`
- **Password**: `Demo123!`
- **Username**: `demouser`

### Run Tests
```bash
# Unit tests
cd src/WhatShouldIDo.Tests
dotnet test

# Load testing with K6
k6 run k6-tests/load-test-basic.js
```

### Sample API Calls
```bash
# Register new user
curl -X POST "http://localhost:5000/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","userName":"testuser","password":"Test123!","confirmPassword":"Test123!"}'

# Login
curl -X POST "http://localhost:5000/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"demo@example.com","password":"Demo123!"}'

# Get nearby parks
curl -X GET "http://localhost:5000/api/pois/nearby?lat=41.0082&lng=28.9784&radius=5000&types=park&maxResults=10"
```

---

## 🔍 Features

- ✅ **Clean Architecture** (Domain, Application, Infrastructure, API)
- ✅ **JWT Authentication** with user management
- ✅ **Multiple POI Providers** (Google Places, OpenTripMap, Geoapify)
- ✅ **Smart Caching** (Redis, In-Memory, Hybrid)
- ✅ **Rate Limiting** (API protection)
- ✅ **Performance Monitoring** (Prometheus metrics)
- ✅ **Health Checks** (Database, Redis, External APIs)
- ✅ **Docker Support** (Multi-container setup)
- ✅ **Load Testing** (K6 scripts)
- ✅ **Monitoring Stack** (Grafana dashboards)

---

## 📈 Monitoring & Observability

### Metrics Available
- API response times
- Request counts by endpoint
- Error rates
- Database performance
- Cache hit rates
- External API usage

### Dashboards
Access Grafana at http://localhost:3000 with `admin/admin` to view:
- API Overview Dashboard
- Performance Metrics
- Error Tracking
- Resource Usage

---

## 🚨 Important Notes

### Security
- **Never commit** `appsettings.json` with real API keys
- Use **environment variables** for sensitive data
- **JWT tokens expire** after 1 hour (configurable)
- **Rate limiting** is enabled (5 requests/hour for free tier)

### External Dependencies
- **Google Places API** (primary POI provider)
- **SQL Server** (database)
- **Redis** (caching - optional)
- **Prometheus/Grafana** (monitoring - optional)

---

## 🆘 Troubleshooting

### Common Issues
1. **Database connection failed** → Check SQL Server is running
2. **Google API quota exceeded** → Check API key and billing
3. **Redis connection failed** → Verify Redis is running or disable caching
4. **Port conflicts** → Change ports in docker-compose.yml

### Logs Location
- **API logs**: `src/WhatShouldIDo.API/logs/`
- **Docker logs**: `docker-compose logs api`

---

## 🤝 Contributing

1. Follow Clean Architecture principles
2. Write unit tests for new features
3. Update documentation
4. Use conventional commit messages
5. Ensure Docker builds succeed

---

## 📞 Support

For questions or issues, contact the backend development team or create an issue in the repository.