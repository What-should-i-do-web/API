# 🔗 Git Integration and Webhooks Setup Guide

This guide covers setting up Git integration with Jenkins and configuring webhooks for automated builds.

## 📋 Prerequisites

1. Jenkins server running and accessible
2. Git repository (GitHub, GitLab, Bitbucket)
3. SSH access to Jenkins server
4. Admin access to Git repository

## 🔧 Jenkins Configuration

### 1. Install Required Plugins

Access Jenkins at `http://your-jenkins-server:8080` and install these plugins:

```
- Git Plugin
- GitHub Plugin
- GitHub Branch Source Plugin
- Pipeline: GitHub Groovy Libraries
- Multibranch Pipeline Plugin
- Webhook Step Plugin
```

### 2. Configure Git Credentials

1. Go to **Manage Jenkins** → **Manage Credentials**
2. Click **(global)** → **Add Credentials**
3. Choose **SSH Username with private key**
4. Configure:
   - **ID**: `git-ssh-key`
   - **Username**: `git`
   - **Private Key**: Copy content from `jenkins/ssh-keys/id_rsa`
   - **Description**: `Git SSH Key for Repository Access`

### 3. Create Multibranch Pipeline Job

1. **New Item** → **Multibranch Pipeline**
2. **Name**: `WhatShouldIDo-API-Pipeline`
3. **Branch Sources** → **Add Source** → **Git**
4. Configure:
   - **Project Repository**: `git@github.com:yourusername/WhatShouldIDo.git`
   - **Credentials**: Select `git-ssh-key`
   - **Behaviors**: Add **Discover branches** and **Discover pull requests from origin**

### 4. Configure Branch Discovery

```yaml
Discover Branches:
  - Strategy: All branches
  
Discover Pull Requests:
  - Strategy: Merging the pull request with current target branch revision
  - Trust: Nobody (Recommended for security)
  
Property Strategy:
  - All branches get the same properties
```

## 🐙 GitHub Integration

### 1. Add Deploy Key to Repository

1. Go to your repository → **Settings** → **Deploy keys**
2. Click **Add deploy key**
3. Configure:
   - **Title**: `Jenkins Build Server`
   - **Key**: Content from `jenkins/ssh-keys/id_rsa.pub`
   - **Allow write access**: ☐ (unchecked for security)

### 2. Configure Webhook

1. Go to **Settings** → **Webhooks** → **Add webhook**
2. Configure:
   - **Payload URL**: `http://your-jenkins-server:8080/github-webhook/`
   - **Content type**: `application/json`
   - **Secret**: Generate a random secret and save it
   - **Which events**: 
     - ☑️ Pushes
     - ☑️ Pull requests
     - ☑️ Branch or tag creation
     - ☑️ Branch or tag deletion

### 3. GitHub App (Alternative Method)

For better security and features, create a GitHub App:

1. Go to **Settings** → **Developer settings** → **GitHub Apps**
2. **New GitHub App**
3. Configure:
   - **GitHub App name**: `WhatShouldIDo-CI-CD`
   - **Homepage URL**: `http://your-jenkins-server:8080`
   - **Webhook URL**: `http://your-jenkins-server:8080/github-webhook/`
   - **Repository permissions**:
     - Contents: Read
     - Metadata: Read
     - Pull requests: Read
     - Commit statuses: Write
   - **Subscribe to events**:
     - Push
     - Pull request
     - Create
     - Delete

## 🦊 GitLab Integration

### 1. Add Deploy Key

1. Go to **Project Settings** → **Repository** → **Deploy Keys**
2. **Add key**:
   - **Title**: `Jenkins Build Server`
   - **Key**: Content from `jenkins/ssh-keys/id_rsa.pub`
   - **Write access allowed**: ☐ (unchecked)

### 2. Configure Webhook

1. **Project Settings** → **Webhooks**
2. **Add webhook**:
   - **URL**: `http://your-jenkins-server:8080/project/WhatShouldIDo-API-Pipeline`
   - **Secret Token**: Generate and save
   - **Trigger events**:
     - ☑️ Push events
     - ☑️ Merge request events
     - ☑️ Tag push events

### 3. GitLab Integration Plugin

Install GitLab plugin in Jenkins:

1. **Manage Jenkins** → **Manage Plugins**
2. Install **GitLab Plugin**
3. **Manage Jenkins** → **Configure System**
4. **GitLab** section:
   - **Connection name**: `GitLab`
   - **GitLab host URL**: `https://gitlab.com`
   - **Credentials**: Add GitLab API token

## 📊 Bitbucket Integration

### 1. SSH Keys Setup

1. **Repository settings** → **Access keys**
2. **Add key**:
   - **Label**: `Jenkins Build Server`
   - **Key**: Content from `jenkins/ssh-keys/id_rsa.pub`

### 2. Webhooks

1. **Repository settings** → **Webhooks**
2. **Add webhook**:
   - **Title**: `Jenkins CI/CD`
   - **URL**: `http://your-jenkins-server:8080/bitbucket-hook/`
   - **Status**: Active
   - **Triggers**:
     - Repository push
     - Pull request created
     - Pull request updated

## 🔒 Security Best Practices

### 1. Webhook Security

```bash
# Generate webhook secret
openssl rand -hex 20

# Store in Jenkins credentials
# ID: github-webhook-secret
# Type: Secret text
```

### 2. Network Security

```nginx
# Nginx configuration for webhook endpoint
location /github-webhook/ {
    # Limit to GitHub IPs
    allow 192.30.252.0/22;
    allow 185.199.108.0/22;
    allow 140.82.112.0/20;
    deny all;
    
    proxy_pass http://jenkins:8080;
}
```

### 3. Jenkins Security

```groovy
// Jenkinsfile security
pipeline {
    agent any
    
    options {
        // Prevent concurrent builds
        disableConcurrentBuilds()
        
        // Build timeout
        timeout(time: 30, unit: 'MINUTES')
        
        // Keep only last 10 builds
        buildDiscarder(logRotator(numToKeepStr: '10'))
    }
    
    // Only allow certain branches to deploy to production
    when {
        anyOf {
            branch 'main'
            branch 'master'
        }
    }
}
```

## 🧪 Testing Webhooks

### 1. Manual Test

```bash
# Test GitHub webhook
curl -X POST \
  http://your-jenkins-server:8080/github-webhook/ \
  -H 'Content-Type: application/json' \
  -H 'X-GitHub-Event: push' \
  -d '{
    "ref": "refs/heads/main",
    "repository": {
      "clone_url": "https://github.com/yourusername/WhatShouldIDo.git"
    }
  }'
```

### 2. Webhook Validation

```bash
# Check Jenkins logs
docker-compose -f jenkins/docker-compose.jenkins.yml logs -f jenkins

# Check webhook delivery in GitHub
# Repository → Settings → Webhooks → Recent Deliveries
```

## 🔄 Branch Strategy

### Recommended Git Flow

```
main/master     → Production deployments
develop/dev     → Development deployments
feature/*       → Development deployments (for testing)
hotfix/*        → Production deployments (urgent fixes)
release/*       → Staging deployments (before production)
```

### Jenkins Branch Configuration

```groovy
// In Jenkinsfile
script {
    if (env.BRANCH_NAME == 'main' || env.BRANCH_NAME == 'master') {
        env.TARGET_ENV = 'production'
    } else if (env.BRANCH_NAME == 'develop' || env.BRANCH_NAME == 'dev') {
        env.TARGET_ENV = 'development'
    } else if (env.BRANCH_NAME.startsWith('feature/')) {
        env.TARGET_ENV = 'development'
    } else if (env.BRANCH_NAME.startsWith('hotfix/')) {
        env.TARGET_ENV = 'production'
    } else {
        env.TARGET_ENV = 'development'  // Default
    }
}
```

## 📱 Notifications Setup

### 1. Slack Integration

```groovy
// Add to Jenkinsfile post section
slackSend(
    color: 'good',
    message: "✅ Build #${env.BUILD_NUMBER} successful on ${env.BRANCH_NAME}",
    channel: '#ci-cd'
)
```

### 2. Email Notifications

```groovy
emailext(
    subject: "Build ${currentBuild.currentResult}: ${env.JOB_NAME} - ${env.BUILD_NUMBER}",
    body: "Build ${env.BUILD_NUMBER} of ${env.JOB_NAME} ${currentBuild.currentResult.toLowerCase()}",
    recipientProviders: [culprits(), developers(), requestor()]
)
```

### 3. Teams/Discord

```groovy
// Use webhook step
httpRequest(
    httpMode: 'POST',
    url: 'https://hooks.slack.com/services/YOUR/WEBHOOK/URL',
    requestBody: '{"text": "Build completed: ' + env.BUILD_NUMBER + '"}'
)
```

## 🔍 Troubleshooting

### Common Issues

1. **Webhook not triggering builds**
   - Check webhook URL format
   - Verify network connectivity
   - Check Jenkins logs
   - Validate webhook secret

2. **Authentication failures**
   - Verify SSH key is added to repository
   - Check Jenkins credentials configuration
   - Ensure SSH key has proper permissions

3. **Build not starting automatically**
   - Check branch discovery configuration
   - Verify Jenkinsfile is in repository root
   - Check multibranch scan logs

### Debug Commands

```bash
# Check Jenkins webhook logs
grep "github-webhook" /var/jenkins_home/logs/jenkins.log

# Test SSH connection
ssh -T git@github.com

# Validate Jenkinsfile syntax
curl -X POST \
  http://jenkins:8080/pipeline-model-converter/validate \
  -F "jenkinsfile=@Jenkinsfile"
```

## 📚 Additional Resources

- [Jenkins Pipeline Syntax](https://www.jenkins.io/doc/book/pipeline/syntax/)
- [GitHub Webhooks Documentation](https://docs.github.com/en/developers/webhooks-and-events/webhooks)
- [GitLab CI/CD Integration](https://docs.gitlab.com/ee/integration/jenkins.html)
- [Bitbucket Jenkins Integration](https://confluence.atlassian.com/bitbucket/jenkins-with-bitbucket-cloud-671277536.html)

This completes the Git integration and webhooks setup. The pipeline will now automatically trigger on code changes and deploy to the appropriate environment based on the branch.