# 🚀 Complete CI/CD Setup Guide for WhatShouldIDo API

This guide provides step-by-step instructions to set up a complete CI/CD pipeline with separate development and production environments.

## 📋 Overview

We've implemented:
- ✅ **Separate Environments**: Development and Production with different configurations
- ✅ **Jenkins CI/CD**: Automated pipeline with build, test, and deploy stages
- ✅ **Docker Containers**: Consistent deployment across environments
- ✅ **Git Integration**: Automatic builds on code changes
- ✅ **Zero-Downtime Deployment**: Blue-green deployment for production
- ✅ **Comprehensive Testing**: Unit, integration, and API contract tests
- ✅ **Security Scanning**: Dependency vulnerabilities and secret detection

## 🏗️ Architecture

```
GitHub/GitLab Repository
         ↓ (webhook)
    Jenkins Server
    ├── Build & Test
    ├── Security Scan
    └── Deploy
        ├── Development (dev.yourdomain.com:5001)
        └── Production (yourdomain.com:5000)
```

## 📦 Files Created

### Environment Configuration
- `.env.development` - Development environment variables
- `.env.production` - Production environment variables
- `src/WhatShouldIDo.API/appsettings.Development.json` - Dev app settings
- `src/WhatShouldIDo.API/appsettings.Production.json` - Prod app settings (existing)

### Jenkins Setup
- `jenkins/Dockerfile` - Jenkins container with .NET SDK
- `jenkins/plugins.txt` - Required Jenkins plugins
- `jenkins/docker-compose.jenkins.yml` - Jenkins deployment
- `jenkins/setup-jenkins.sh` - Automated Jenkins installation
- `jenkins/git-integration-setup.md` - Git webhook configuration guide

### CI/CD Pipeline
- `Jenkinsfile` - Complete CI/CD pipeline definition
- `docker-compose.dev.yml` - Development environment (updated)
- `docker-compose.prod.yml` - Production environment (existing)

### Scripts
- `scripts/deploy-dev.sh` - Development deployment script
- `scripts/deploy-prod.sh` - Production deployment with zero-downtime
- `scripts/test-pipeline.sh` - Comprehensive testing pipeline

### Documentation
- `CI-CD-DEPLOYMENT-GUIDE.md` - Hosting and deployment strategy
- `COMPLETE-CICD-SETUP.md` - This comprehensive guide

## 🚀 Quick Start (30-60 minutes)

### Step 1: Environment Setup (10 minutes)

1. **Configure Environment Variables**
   ```bash
   # Copy and edit development environment
   cp .env.example .env.development
   nano .env.development  # Add your development API keys
   
   # Copy and edit production environment
   cp .env.example .env.production
   nano .env.production   # Add your production API keys
   ```

2. **Make Scripts Executable**
   ```bash
   chmod +x jenkins/setup-jenkins.sh
   chmod +x scripts/*.sh
   ```

### Step 2: Jenkins Installation (15 minutes)

1. **Install Jenkins**
   ```bash
   # Run the automated setup
   ./jenkins/setup-jenkins.sh
   ```

2. **Access Jenkins**
   - Open: `http://localhost:8080`
   - Use credentials: `admin / admin123` (change this!)
   - Install suggested plugins

3. **Configure Git Integration**
   ```bash
   # Add the SSH public key to your Git repository
   cat jenkins/ssh-keys/id_rsa.pub
   # Copy this key to GitHub/GitLab Deploy Keys
   ```

### Step 3: Repository Setup (5 minutes)

1. **Add Webhook to Repository**
   - **GitHub**: Repository → Settings → Webhooks
     - URL: `http://your-jenkins-server:8080/github-webhook/`
     - Content type: `application/json`
     - Events: Push, Pull requests
   
   - **GitLab**: Project → Settings → Webhooks
     - URL: `http://your-jenkins-server:8080/project/WhatShouldIDo-API-Pipeline`
     - Trigger: Push events, Merge request events

2. **Create Pipeline Job in Jenkins**
   - New Item → Multibranch Pipeline
   - Name: `WhatShouldIDo-API-Pipeline`
   - Branch Sources → Git
   - Repository URL: `git@github.com:yourusername/WhatShouldIDo.git`
   - Credentials: Select the git-ssh-key

### Step 4: Test the Pipeline (15 minutes)

1. **Test Development Deployment**
   ```bash
   # Deploy to development manually first
   ./scripts/deploy-dev.sh
   
   # Verify it's working
   curl http://localhost:5001/api/health
   ```

2. **Test Production Deployment**
   ```bash
   # Deploy to production manually
   ./scripts/deploy-prod.sh
   
   # Verify it's working
   curl http://localhost:5000/api/health
   ```

3. **Test CI/CD Pipeline**
   ```bash
   # Make a small change and push to trigger the pipeline
   echo "# Test change" >> README.md
   git add README.md
   git commit -m "Test CI/CD pipeline"
   git push origin main  # Should trigger production deployment
   git push origin dev   # Should trigger development deployment
   ```

## 🏠 Hosting Options

### Option 1: Single Server (Recommended for starting)
- **Cost**: $24-48/month
- **Setup**: One VPS running both Jenkins and applications
- **Pros**: Simple, cost-effective
- **Cons**: Single point of failure

```bash
# Server requirements
CPU: 2-4 cores
RAM: 4-8GB
Storage: 40-80GB SSD
OS: Ubuntu 22.04 LTS
```

### Option 2: Separate Servers
- **Cost**: $50-100/month
- **Setup**: Jenkins server + Dev server + Prod server
- **Pros**: Better isolation, scalable
- **Cons**: More complex, higher cost

### Option 3: Cloud Container Services
- **Providers**: Railway, Render, Fly.io
- **Cost**: $20-60/month
- **Pros**: Managed infrastructure
- **Cons**: Less control, vendor lock-in

## 🔒 Security Checklist

### Repository Security
- [ ] Remove any hardcoded secrets from code
- [ ] Use environment variables for all sensitive data
- [ ] Add `.env*` files to `.gitignore`
- [ ] Set up deploy keys (read-only) instead of full access keys

### Jenkins Security
- [ ] Change default admin password
- [ ] Enable HTTPS for Jenkins UI
- [ ] Restrict Jenkins network access
- [ ] Use role-based access control
- [ ] Regularly update Jenkins and plugins

### Server Security
- [ ] Use SSH keys instead of passwords
- [ ] Configure firewall (UFW)
- [ ] Enable automatic security updates
- [ ] Set up fail2ban for brute force protection
- [ ] Use non-root user for deployments

### Application Security
- [ ] Enable HTTPS/SSL certificates
- [ ] Configure CORS properly
- [ ] Use strong JWT secrets
- [ ] Enable rate limiting
- [ ] Regular security scans

## 📊 Monitoring and Alerting

### Built-in Monitoring
- **Health Checks**: `http://your-domain/api/health`
- **Seq Logging**: `http://localhost:5341` (development)
- **Docker Stats**: `docker stats`
- **Jenkins Build History**: Job status and logs

### Recommended Additional Monitoring
1. **Uptime Monitoring**: UptimeRobot, Pingdom
2. **Application Performance**: Application Insights, New Relic
3. **Infrastructure**: Prometheus + Grafana
4. **Log Aggregation**: ELK Stack, Splunk

### Alerting Setup
```bash
# Example Slack webhook for notifications
export SLACK_WEBHOOK_URL="https://hooks.slack.com/services/YOUR/WEBHOOK/URL"
```

## 🔄 Branch Strategy

### Recommended Git Flow
```
main/master     → Production deployments (auto)
develop/dev     → Development deployments (auto)
feature/*       → Development deployments (manual)
hotfix/*        → Production deployments (manual)
release/*       → Staging → Production (manual)
```

### Pipeline Behavior
- **Push to main**: Automatic production deployment
- **Push to develop**: Automatic development deployment
- **Pull requests**: Build and test only
- **Feature branches**: Deploy to development on demand

## 🧪 Testing Strategy

### Automated Tests
1. **Unit Tests**: Test individual components
2. **Integration Tests**: Test API endpoints with database
3. **Contract Tests**: Verify API responses
4. **Security Tests**: Scan for vulnerabilities
5. **Performance Tests**: Basic load and response time

### Manual Testing
1. **Smoke Tests**: Verify critical functionality after deployment
2. **User Acceptance**: Test user scenarios
3. **Security Review**: Regular security audits
4. **Performance Review**: Monitor metrics and optimize

## 🚨 Troubleshooting

### Common Issues

1. **Jenkins Build Fails**
   ```bash
   # Check Jenkins logs
   docker-compose -f jenkins/docker-compose.jenkins.yml logs -f
   
   # Check workspace permissions
   docker exec -it whatshouldido-jenkins bash
   ```

2. **Webhook Not Triggering**
   - Check webhook URL format
   - Verify network connectivity
   - Check repository webhook settings
   - Validate webhook secret

3. **Deployment Fails**
   ```bash
   # Check deployment logs
   ./scripts/deploy-dev.sh  # for development
   ./scripts/deploy-prod.sh # for production
   
   # Check Docker containers
   docker-compose -f docker-compose.dev.yml ps
   docker-compose -f docker-compose.prod.yml ps
   ```

4. **Health Check Fails**
   ```bash
   # Check API logs
   docker-compose -f docker-compose.dev.yml logs api-dev
   
   # Check database connectivity
   docker-compose -f docker-compose.dev.yml exec db-dev /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P PASSWORD -Q "SELECT 1"
   ```

### Debug Commands
```bash
# View all containers
docker ps -a

# Check logs
docker logs CONTAINER_NAME

# Execute commands in container
docker exec -it CONTAINER_NAME bash

# Check resource usage
docker stats

# Clean up resources
docker system prune -f
```

## 📈 Scaling Considerations

### Horizontal Scaling
- Load balancer (Nginx, HAProxy)
- Multiple API instances
- Database read replicas
- Redis clustering

### Vertical Scaling
- Increase server resources
- Optimize database queries
- Implement caching
- Code optimization

### Performance Monitoring
- Response times
- Error rates
- Resource utilization
- Database performance
- Cache hit rates

## 💡 Next Steps

### Immediate (Week 1)
1. Set up basic CI/CD pipeline
2. Deploy to development environment
3. Configure monitoring and alerting
4. Test the deployment process

### Short-term (Month 1)
1. Set up production environment
2. Implement zero-downtime deployment
3. Add comprehensive test coverage
4. Set up backup and disaster recovery

### Long-term (Months 2-3)
1. Implement advanced monitoring
2. Set up multi-region deployment
3. Add performance optimization
4. Implement advanced security measures

## 📞 Support

### Documentation
- [Jenkins Pipeline Syntax](https://www.jenkins.io/doc/book/pipeline/syntax/)
- [Docker Compose Reference](https://docs.docker.com/compose/compose-file/)
- [ASP.NET Core Deployment](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/)

### Community
- Stack Overflow
- Jenkins Community Forum
- Docker Community
- GitHub Issues

## 🎯 Success Metrics

### Pipeline Metrics
- **Build Success Rate**: >95%
- **Build Time**: <10 minutes
- **Test Coverage**: >80%
- **Deployment Time**: <5 minutes
- **Zero Failed Deployments**: Per week

### Application Metrics
- **Uptime**: >99.9%
- **Response Time**: <500ms (95th percentile)
- **Error Rate**: <1%
- **Security Issues**: 0 critical, <5 high

---

## 🚀 You're Ready!

Your WhatShouldIDo API now has a complete, professional CI/CD pipeline that:
- ✅ Automatically builds and tests code changes
- ✅ Deploys to appropriate environments based on branches
- ✅ Provides zero-downtime production deployments
- ✅ Includes comprehensive monitoring and alerting
- ✅ Follows security best practices
- ✅ Scales with your application needs

**Happy Deploying!** 🎉