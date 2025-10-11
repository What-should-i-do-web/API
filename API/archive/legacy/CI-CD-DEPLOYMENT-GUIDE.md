# 🚀 WhatShouldIDo API - CI/CD Deployment Guide

This guide sets up a complete CI/CD pipeline with separate development and production environments.

## 📋 Environment Strategy

### Development Environment
- **Domain**: `dev.whatshouldido.com` or `api-dev.yourdomain.com`
- **Purpose**: Testing new features, bug fixes, integration testing
- **Branch**: `develop` or `dev` branch
- **Database**: Separate development database
- **Resources**: Lower resource allocation
- **Monitoring**: Basic monitoring with debug logging

### Production Environment
- **Domain**: `api.whatshouldido.com` or `whatshouldido.com`
- **Purpose**: Live application serving real users
- **Branch**: `main` branch only
- **Database**: Production database with backups
- **Resources**: Full resource allocation
- **Monitoring**: Full monitoring, metrics, and alerting

## 🌐 Hosting Options

### Option 1: Cloud VPS (Recommended)
**Development Server:**
- **DigitalOcean Droplet**: $6/month (1GB RAM, 1 vCPU)
- **AWS EC2 t3.micro**: ~$8/month
- **Azure B1s**: ~$8/month

**Production Server:**
- **DigitalOcean Droplet**: $18/month (2GB RAM, 1 vCPU)
- **AWS EC2 t3.small**: ~$15/month
- **Azure B2s**: ~$30/month

**Total Cost**: ~$24-38/month

### Option 2: Container Platforms
**Development:**
- **Railway**: $5/month
- **Render**: Free tier available
- **Fly.io**: $5/month

**Production:**
- **Railway**: $20/month
- **Render**: $25/month
- **Fly.io**: $15/month

**Total Cost**: ~$20-45/month

### Option 3: Single Server with Docker
**One Server for Both Environments:**
- **DigitalOcean Droplet**: $24/month (4GB RAM, 2 vCPUs)
- Uses Docker containers with different ports
- Domain routing via subdomains

**Total Cost**: ~$24/month + domain costs

## 🐳 Docker Container Strategy

### Development Container
- Port: 5001
- Environment: Development
- Debug logging enabled
- Swagger UI enabled
- Lower resource limits

### Production Container
- Port: 5000
- Environment: Production
- Optimized for performance
- Security hardened
- Higher resource limits

## 📦 Subdomain Configuration

### DNS Setup
```
# A Records
api.yourdomain.com    → Production Server IP
api-dev.yourdomain.com → Development Server IP

# CNAME Alternative
dev.api.yourdomain.com → Development Server
```

### Nginx Configuration
```nginx
# Development
server {
    server_name api-dev.yourdomain.com;
    location / {
        proxy_pass http://localhost:5001;
    }
}

# Production
server {
    server_name api.yourdomain.com;
    location / {
        proxy_pass http://localhost:5000;
    }
}
```

## 🔧 Jenkins Server Setup

### Option 1: Dedicated Jenkins Server
- **DigitalOcean Droplet**: $12/month (2GB RAM, 1 vCPU)
- **Purpose**: CI/CD orchestration only
- **Access**: jenkins.yourdomain.com

### Option 2: Jenkins on Existing Server
- Install Jenkins alongside applications
- Uses Docker container for isolation
- Port 8080 for Jenkins UI

### Option 3: Jenkins in Cloud
- **AWS CodePipeline**: Pay per pipeline run
- **Azure DevOps**: $6/month for basic plan
- **GitHub Actions**: Free for public repos, $4/month for private

## 📊 Resource Planning

### Development Environment
```yaml
Resources:
  CPU: 1 vCPU
  RAM: 1-2GB
  Storage: 20GB SSD
  Database: 5GB
  Bandwidth: 100GB/month

Monthly Cost: $6-12
```

### Production Environment
```yaml
Resources:
  CPU: 2 vCPUs
  RAM: 4GB
  Storage: 40GB SSD
  Database: 20GB
  Bandwidth: 1TB/month

Monthly Cost: $18-30
```

### Jenkins Server
```yaml
Resources:
  CPU: 1 vCPU
  RAM: 2GB
  Storage: 20GB SSD

Monthly Cost: $12
```

## 🛠️ Implementation Steps

### Phase 1: Infrastructure Setup (Day 1)
1. **Provision Servers**
   - Development server
   - Production server
   - Jenkins server (optional)

2. **Domain Configuration**
   - Purchase domain
   - Configure DNS records
   - Set up subdomains

3. **Basic Security**
   - SSH key authentication
   - Firewall configuration
   - User account setup

### Phase 2: Environment Setup (Day 1-2)
1. **Install Dependencies**
   - Docker & Docker Compose
   - Nginx
   - SSL certificates (Let's Encrypt)

2. **Database Setup**
   - SQL Server containers
   - Development database
   - Production database
   - Redis instances

3. **Initial Deployment**
   - Manual deployment test
   - Verify both environments work
   - Test database connections

### Phase 3: Jenkins Setup (Day 2-3)
1. **Install Jenkins**
   - Docker-based installation
   - Initial configuration
   - Plugin installation

2. **Security Configuration**
   - User accounts
   - Role-based access
   - API tokens

3. **Git Integration**
   - Repository connection
   - Webhook configuration
   - Branch strategies

### Phase 4: Pipeline Creation (Day 3-4)
1. **Build Pipeline**
   - Jenkinsfile creation
   - Multi-stage builds
   - Testing integration

2. **Deployment Pipeline**
   - Environment-specific deployments
   - Database migrations
   - Health checks

3. **Monitoring Setup**
   - Deployment notifications
   - Error reporting
   - Performance monitoring

## 💰 Total Cost Breakdown

### Minimum Setup
```
Development Server: $6/month
Production Server: $18/month
Domain: $12/year (~$1/month)
Total: ~$25/month
```

### Recommended Setup
```
Development Server: $12/month
Production Server: $24/month
Jenkins Server: $12/month
Domain: $12/year (~$1/month)
SSL Certificate: Free (Let's Encrypt)
Total: ~$49/month
```

### Enterprise Setup
```
Development Server: $18/month
Production Server: $50/month
Jenkins Server: $24/month
Monitoring: $10/month
Backups: $5/month
Domain: $12/year (~$1/month)
Total: ~$108/month
```

## 📈 Scaling Considerations

### Traffic Growth
- Start with minimum setup
- Monitor resource usage
- Scale vertically first (more RAM/CPU)
- Scale horizontally when needed (load balancing)

### Feature Growth
- Database optimization
- Caching strategies (Redis)
- CDN for static content
- Microservices architecture (future)

## 🔒 Security Checklist

### Server Security
- [ ] SSH key authentication only
- [ ] Firewall configured (UFW)
- [ ] Regular security updates
- [ ] Non-root user accounts
- [ ] Fail2ban for brute force protection

### Application Security
- [ ] Environment variables for secrets
- [ ] HTTPS/SSL certificates
- [ ] Database access restrictions
- [ ] API rate limiting
- [ ] CORS configuration

### CI/CD Security
- [ ] Secure Jenkins installation
- [ ] Repository webhook secrets
- [ ] Deployment key management
- [ ] Build artifact scanning
- [ ] Dependency vulnerability checks

## 🚨 Monitoring & Alerting

### Development Monitoring
- Basic health checks
- Build failure notifications
- Error logging

### Production Monitoring
- Uptime monitoring
- Performance metrics
- Error alerting
- Resource utilization
- Database monitoring

## 📝 Next Steps

1. **Choose hosting provider** based on budget and requirements
2. **Set up development environment** first
3. **Test manual deployment** to both environments
4. **Install and configure Jenkins**
5. **Create and test CI/CD pipeline**
6. **Set up monitoring and alerting**
7. **Document operational procedures**

This setup provides a professional, scalable CI/CD pipeline that grows with your application needs.