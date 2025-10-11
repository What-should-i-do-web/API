# CI/CD Quick Start Guide - WhatShouldIDo

**⚡ Get your CI/CD pipeline running in 30 minutes**

---

## 🎯 What You Have

Your project includes:
- ✅ **Jenkins Pipeline** (fully configured, needs deployment)
- ✅ **GitHub Actions** (ready to use)
- ✅ **Monitoring Stack** (Prometheus, Grafana, Seq)
- ✅ **Deployment Scripts** (blue-green, rolling, rollback)
- ✅ **Docker Infrastructure** (multi-environment support)

## 🚀 Quick Start (30 Minutes)

### Step 1: Start Monitoring (5 minutes)

```bash
# Navigate to project
cd C:\Users\ertan\Desktop\LAB\githubProjects\WhatShouldIDo\NeYapsamWeb\API

# Start monitoring stack
docker-compose -f docker-compose.monitoring.yml up -d

# Wait for services to start (~2 minutes)
docker-compose -f docker-compose.monitoring.yml ps

# Verify all services are "Up"
```

**Access Dashboards:**
- 📊 Grafana: http://localhost:3000 (admin/admin123)
- 📈 Prometheus: http://localhost:9090
- 📝 Seq Logs: http://localhost:5341

---

### Step 2: Get Real API Keys (10 minutes)

You need these API keys for the application to work:

#### 1. Google Places API
```
1. Go to: https://console.cloud.google.com/
2. Create new project or select existing
3. Enable APIs: "Places API (New)"
4. Create credentials → API Key
5. Copy the API key
```

#### 2. OpenTripMap API
```
1. Go to: https://opentripmap.io/
2. Sign up for free account
3. Dashboard → API Keys
4. Copy the API key
```

#### 3. OpenWeather API
```
1. Go to: https://openweathermap.org/api
2. Sign up for free account
3. My API Keys
4. Copy the API key
```

**Update Configuration:**
```bash
# Edit appsettings.Development.json
notepad src/WhatShouldIDo.API/appsettings.Development.json

# Update these values:
"GooglePlaces": { "ApiKey": "YOUR_GOOGLE_KEY_HERE" }
"OpenTripMap": { "ApiKey": "YOUR_OTM_KEY_HERE" }
"OpenWeather": { "ApiKey": "YOUR_WEATHER_KEY_HERE" }

# Enable hybrid search
"HybridPlaces": { "Enabled": true }
```

---

### Step 3: Test Locally (5 minutes)

```bash
# Build and run with Docker Compose
docker-compose up -d

# Wait for services (~1 minute)
docker-compose ps

# Test API health
curl http://localhost:5000/api/health

# Test search functionality
curl -X POST http://localhost:5000/api/discover/prompt \
  -H "Content-Type: application/json" \
  -d '{"prompt":"restaurants","latitude":41.0082,"longitude":28.9784}'
```

If you see results, your API is working! 🎉

---

### Step 4: Setup Jenkins (Optional - 10 minutes)

**For Windows (using Git Bash or WSL):**
```bash
# Navigate to project
cd C:\Users\ertan\Desktop\LAB\githubProjects\WhatShouldIDo\NeYapsamWeb\API

# Make script executable (if using WSL/Git Bash)
chmod +x jenkins/setup-jenkins.sh

# Run Jenkins setup
./jenkins/setup-jenkins.sh

# Alternative: Start Jenkins manually
docker-compose -f jenkins/docker-compose.jenkins.yml up -d

# Wait for Jenkins to start (~2-3 minutes)
```

**Access Jenkins:**
- 🔧 Jenkins: http://localhost:8080

**Initial Setup:**
1. Get initial password:
   ```bash
   docker exec whatshouldido-jenkins cat /var/jenkins_home/secrets/initialAdminPassword
   ```
2. Login with password
3. Click "Install suggested plugins"
4. Create admin user (or continue as admin)

---

## 🎮 What to Do Next

### Option A: Use GitHub Actions (Recommended for Cloud)

**Benefits:**
- ✅ No infrastructure to maintain
- ✅ Free for public repos
- ✅ Automatic on push
- ✅ Built-in container registry

**Setup Steps:**
1. Push code to GitHub
2. Add secrets in GitHub Settings:
   - `DOCKER_REGISTRY_TOKEN`
   - `CODECOV_TOKEN` (optional)
3. Push to `develop` or `main` branch
4. Check Actions tab for pipeline status

---

### Option B: Use Jenkins (Recommended for On-Premise)

**Benefits:**
- ✅ Full control
- ✅ Advanced deployment strategies
- ✅ Manual approval gates
- ✅ More customization

**Setup Steps:**
1. Configure deployment servers (see below)
2. Update Jenkinsfile with server addresses
3. Create Jenkins pipeline job
4. Configure webhooks
5. Push code to trigger build

---

## 🖥️ Deployment Server Setup

### Quick DigitalOcean Setup Example

```bash
# 1. Create droplet
# - Choose Ubuntu 22.04
# - 4GB RAM / 2 vCPUs
# - Add SSH key

# 2. SSH into server
ssh root@your-droplet-ip

# 3. Run setup script
curl -fsSL https://get.docker.com -o get-docker.sh
sh get-docker.sh

# Install docker-compose
curl -L "https://github.com/docker/compose/releases/download/v2.20.0/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
chmod +x /usr/local/bin/docker-compose

# Create deploy user
useradd -m -s /bin/bash deploy
usermod -aG docker deploy
mkdir -p /home/deploy/.ssh
chmod 700 /home/deploy/.ssh

# Add your SSH public key
echo "YOUR_PUBLIC_KEY" >> /home/deploy/.ssh/authorized_keys
chown -R deploy:deploy /home/deploy/.ssh
chmod 600 /home/deploy/.ssh/authorized_keys

# Create app directory
mkdir -p /home/deploy/whatshouldido
chown -R deploy:deploy /home/deploy/whatshouldido

# Test SSH access
# From your machine:
ssh deploy@your-droplet-ip
```

---

## 📊 Monitoring Quick Check

### Grafana Setup

```bash
# 1. Access Grafana
open http://localhost:3000

# 2. Login: admin/admin123

# 3. Add Prometheus Data Source
#    Configuration → Data Sources → Add data source
#    Type: Prometheus
#    URL: http://prometheus:9090
#    Click: Save & Test

# 4. Import Dashboards
#    Create → Import → Enter ID:
#    - 1860 (Node Exporter)
#    - 179 (Docker Overview)
#    - 2115 (PostgreSQL)
```

---

## 🔍 Troubleshooting

### Issue: Docker compose fails
```bash
# Check Docker is running
docker --version
docker ps

# Restart Docker Desktop (Windows)
# Or restart Docker service (Linux):
sudo systemctl restart docker
```

### Issue: Port already in use
```bash
# Check what's using the port
netstat -ano | findstr :5000  # Windows
lsof -i :5000                 # Linux/Mac

# Kill the process or change port in docker-compose.yml
```

### Issue: API returns 500 errors
```bash
# Check logs
docker-compose logs api

# Common causes:
# - Missing API keys
# - Database not ready
# - Redis not connected

# Restart services
docker-compose restart api
```

### Issue: Can't access Grafana
```bash
# Check if running
docker ps | grep grafana

# Check logs
docker logs grafana

# Restart
docker-compose -f docker-compose.monitoring.yml restart grafana
```

---

## 📈 Success Checklist

After following this guide, you should have:

- [ ] Monitoring stack running (Grafana, Prometheus, Seq)
- [ ] Real API keys configured
- [ ] Application running locally with Docker
- [ ] API health endpoint responding
- [ ] Search functionality working
- [ ] Grafana showing metrics
- [ ] Jenkins installed (optional)

---

## 🎯 Next Steps After Quick Start

1. **Read Full Analysis:** `CI_CD_INFRASTRUCTURE_ANALYSIS.md`
2. **Configure Notifications:** Setup Slack/Email
3. **Deploy to Cloud:** Follow Phase 4 in main document
4. **Setup Webhooks:** Automate deployments
5. **Configure Backup:** Setup automated backups
6. **Load Testing:** Test under load
7. **Security Hardening:** SSL, firewall rules, secrets management

---

## 🆘 Getting Help

**Common Questions:**

**Q: Do I need Jenkins AND GitHub Actions?**
A: No, choose one. GitHub Actions is easier to start with. Jenkins gives more control.

**Q: Can I skip monitoring for now?**
A: Not recommended, but yes. Comment out monitoring in docker-compose files.

**Q: Where do I get a domain name?**
A: Namecheap, GoDaddy, or Google Domains (~$12/year)

**Q: How much does hosting cost?**
A: $20-50/month for basic setup (DigitalOcean, Linode, or Vultr)

**Q: Can I use Azure/AWS free tier?**
A: Yes! Both offer 12 months free for basic compute resources.

---

## 💡 Pro Tips

1. **Start Simple:** Get monitoring working first, then add CI/CD
2. **Use Free Tiers:** Most cloud providers offer free tiers for testing
3. **Automate Gradually:** Manual deployment → Automated deployment → Blue-green
4. **Monitor Everything:** If you can't measure it, you can't improve it
5. **Document As You Go:** Future you will thank present you

---

## ✅ You're Ready!

You now have:
- ✅ Understanding of what CI/CD infrastructure exists
- ✅ Monitoring stack running
- ✅ Application running locally
- ✅ Clear path to production deployment

**Next Action:** Choose Jenkins OR GitHub Actions and follow the setup guide in `CI_CD_INFRASTRUCTURE_ANALYSIS.md`

---

**Last Updated:** 2025-10-11
**Version:** 1.0
