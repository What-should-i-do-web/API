# 🚀 WhatShouldIDo API - Complete Deployment Guide

This guide covers multiple deployment options for the WhatShouldIDo API project.

## 📋 Prerequisites

### Required API Keys
1. **Google Places API Key**
   - Go to [Google Cloud Console](https://console.cloud.google.com/)
   - Enable Google Places API (New) and Places Photos API
   - Create API key and restrict it to your domain

2. **OpenTripMap API Key** (Optional but recommended)
   - Register at [OpenTripMap](https://opentripmap.io/docs)
   - Get free API key (10,000 requests/month)

3. **OpenWeather API Key** (Optional)
   - Register at [OpenWeatherMap](https://openweathermap.org/api)
   - Get free API key for weather context features

### System Requirements
- **.NET 9 SDK** (for development)
- **Docker & Docker Compose** (for production deployment)
- **Domain name** (for production)
- **SSL Certificate** (Let's Encrypt recommended)

---

## 🏗️ Deployment Options

### Option 1: Cloud VPS (Recommended for beginners)

#### 1.1 Choose a Cloud Provider
**Recommended providers:**
- **DigitalOcean Droplet** ($12-25/month)
- **AWS EC2** (t3.small: ~$15/month)
- **Azure VM** (B2s: ~$15/month)
- **Google Cloud VM** (e2-small: ~$15/month)
- **Linode** ($10-20/month)

#### 1.2 VPS Setup (Ubuntu 22.04 LTS)
```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker $USER

# Install Docker Compose
sudo apt install docker-compose-plugin -y

# Install Nginx (for reverse proxy)
sudo apt install nginx certbot python3-certbot-nginx -y
```

#### 1.3 Deploy Your Application
```bash
# Clone your repository
git clone https://github.com/yourusername/WhatShouldIDo.git
cd WhatShouldIDo/NeYapsamWeb/API

# Copy environment file
cp .env.example .env
nano .env  # Fill in your API keys and settings

# Start the application
docker-compose -f docker-compose.prod.yml up -d

# Setup SSL certificate
sudo certbot --nginx -d yourdomain.com
```

### Option 2: Platform-as-a-Service (PaaS)

#### 2.1 Railway (Easiest)
1. Connect GitHub repository to [Railway](https://railway.app/)
2. Add environment variables in Railway dashboard
3. Deploy with one click
4. **Cost**: ~$5-20/month

#### 2.2 Azure Container Apps
1. Create Azure Container App
2. Configure container image
3. Set environment variables
4. **Cost**: ~$10-30/month

#### 2.3 Google Cloud Run
1. Build container image
2. Deploy to Cloud Run
3. Configure custom domain
4. **Cost**: Pay per request (~$5-15/month)

### Option 3: Traditional Web Hosting

#### 3.1 Shared Hosting with .NET Support
- **SmarterASP.NET** ($2.95/month)
- **WinHost** ($3.95/month)
- **DiscountASP.NET** ($5/month)

---

## 🔧 Step-by-Step VPS Deployment (Detailed)

### Step 1: Prepare Your Environment

```bash
# 1. Copy environment template
cp .env.example .env

# 2. Edit with your actual values
nano .env
```

**Required Environment Variables:**
```env
DB_CONNECTION_STRING=Server=db;Database=WhatShouldIDo;User Id=sa;Password=YourStrongPassword123!;TrustServerCertificate=True;
SQL_SA_PASSWORD=YourStrongPassword123!
REDIS_PASSWORD=YourRedisPassword123!
GOOGLE_PLACES_API_KEY=your_google_api_key_here
OPENTRIPMAP_API_KEY=your_opentripmap_key_here
JWT_SECRET_KEY=your_super_secret_jwt_key_minimum_32_chars_here
DOMAIN_NAME=yourdomain.com
SSL_EMAIL=your-email@domain.com
```

### Step 2: Domain Configuration

1. **Buy a domain** (Namecheap, GoDaddy, Cloudflare)
2. **Point DNS to your VPS**:
   ```
   A Record: @ → Your VPS IP
   A Record: www → Your VPS IP
   ```

### Step 3: Deploy Application

```bash
# Build and start all services
docker-compose -f docker-compose.prod.yml up -d

# Check if services are running
docker-compose -f docker-compose.prod.yml ps

# View logs
docker-compose -f docker-compose.prod.yml logs api
```

### Step 4: SSL Certificate Setup

```bash
# Install Certbot
sudo apt install certbot python3-certbot-nginx

# Get SSL certificate
sudo certbot --nginx -d yourdomain.com -d www.yourdomain.com

# Test auto-renewal
sudo certbot renew --dry-run
```

### Step 5: Database Migration

```bash
# Run database migrations
docker-compose -f docker-compose.prod.yml exec api dotnet ef database update
```

---

## 🛠️ Production Configuration

### Nginx Configuration
Create `/etc/nginx/sites-available/whatshouldido`:

```nginx
server {
    listen 80;
    server_name yourdomain.com www.yourdomain.com;
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name yourdomain.com www.yourdomain.com;

    ssl_certificate /etc/letsencrypt/live/yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/yourdomain.com/privkey.pem;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }

    location /api/health {
        proxy_pass http://localhost:5000;
        access_log off;
    }
}
```

Enable the site:
```bash
sudo ln -s /etc/nginx/sites-available/whatshouldido /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

---

## 💰 Cost Breakdown

### Budget Option (~$15-25/month)
- **VPS**: DigitalOcean Droplet ($12/month)
- **Domain**: $12/year ($1/month)
- **Total**: ~$13-15/month

### Production Option (~$30-50/month)
- **VPS**: Larger instance ($25/month)
- **Domain**: $12/year ($1/month)
- **Backups**: $5/month
- **Monitoring**: $10/month
- **Total**: ~$40-50/month

### Enterprise Option (~$100+/month)
- **Managed Database**: $50/month
- **Load Balancer**: $20/month
- **CDN**: $10/month
- **Enhanced Monitoring**: $20/month
- **Total**: ~$100+/month

---

## 📊 Monitoring & Maintenance

### Health Checks
```bash
# Check API health
curl https://yourdomain.com/api/health

# Check database
docker-compose -f docker-compose.prod.yml exec db sqlcmd -S localhost -U sa -P YourPassword -Q "SELECT 1"

# Check Redis
docker-compose -f docker-compose.prod.yml exec redis redis-cli ping
```

### Backup Strategy
```bash
# Database backup
docker-compose -f docker-compose.prod.yml exec db /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P YourPassword -Q "BACKUP DATABASE WhatShouldIDo TO DISK='/var/backups/whatshouldido_backup.bak'"

# Redis backup
docker-compose -f docker-compose.prod.yml exec redis redis-cli save
```

### Log Management
```bash
# View application logs
docker-compose -f docker-compose.prod.yml logs api -f

# Rotate logs
docker-compose -f docker-compose.prod.yml exec api find /app/logs -name "*.txt" -mtime +7 -delete
```

---

## 🔒 Security Checklist

- [ ] **Strong passwords** for database and Redis
- [ ] **SSL certificate** configured and auto-renewing  
- [ ] **Firewall** configured (only ports 22, 80, 443 open)
- [ ] **API keys** stored in environment variables, not code
- [ ] **Regular backups** automated
- [ ] **System updates** scheduled
- [ ] **Rate limiting** configured
- [ ] **CORS** properly configured for your frontend domains

---

## 🚨 Troubleshooting

### Common Issues

#### 1. API not responding
```bash
# Check if container is running
docker-compose -f docker-compose.prod.yml ps

# Check logs
docker-compose -f docker-compose.prod.yml logs api

# Restart if needed
docker-compose -f docker-compose.prod.yml restart api
```

#### 2. Database connection issues
```bash
# Check if database is running
docker-compose -f docker-compose.prod.yml exec db sqlcmd -S localhost -U sa -P YourPassword -Q "SELECT 1"

# Check connection string in logs
docker-compose -f docker-compose.prod.yml logs api | grep -i "connection"
```

#### 3. SSL certificate issues
```bash
# Check certificate status
sudo certbot certificates

# Renew if needed
sudo certbot renew --force-renewal
```

---

## 📞 Need Help?

- **Documentation**: Check the project documentation
- **Logs**: Always check application and system logs first
- **Community**: Stack Overflow, Reddit r/dotnet
- **Professional Help**: Consider hiring a DevOps consultant for complex setups

---

**Next Steps After Deployment:**
1. Test all API endpoints
2. Set up monitoring and alerting
3. Configure automated backups
4. Plan for scaling (load balancing, CDN)
5. Implement CI/CD pipeline for updates