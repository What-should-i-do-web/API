# WhatShouldIDo – CI/CD & Deployment Plan

## 🎯 Amaç
Projeyi **AWS Lambda Container Image** ve **GitHub Actions + Terraform** tabanlı modern bir CI/CD pipeline ile
hızlı, güvenli ve maliyetsiz şekilde deploy edilebilir hale getirmek.

---

## 🧱 Teknoloji Seçimleri
| Katman | Teknoloji |
|--------|------------|
| CI/CD | GitHub Actions |
| IaC (Altyapı) | Terraform |
| Backend Deploy | AWS Lambda (.NET 8 container image) |
| Container Registry | AWS ECR |
| API Gateway | AWS HTTP API v2 |
| Cache | Upstash Redis (Serverless) |
| Monitoring | CloudWatch (default), opsiyonel Sentry |
| Frontend | Vercel (Next.js) |

---

## ⚙️ Aşamalar

### 1. Kod Temizliği
- Jenkins, docker-compose.monitoring, legacy scriptler → `archive/legacy/`
- `.gitignore` güncellendi (build, monitoring, legacy dışlandı)
- Dockerfile & aktif `docker-compose.yml` korundu

### 2. CI/CD Yapısı
- `.github/workflows/ci.yml` → build + test
- `.github/workflows/deploy-image.yml` → ECR push + Lambda deploy
- CI testleri başlangıçta “build-only”; testler eklendikçe aktifleşecek

### 3. Terraform Altyapısı
- `infra/terraform/` klasörü:
  - `main.tf` → ECR, Lambda, API Gateway, IAM roller
  - `variables.tf` → parametre tanımları
  - `outputs.tf` → API URL, ECR repo, Lambda adı, IAM ARN
- İlk `terraform apply` kaynakları oluşturur ama **henüz ücretli trafik başlatmaz**

### 4. GitHub Secrets & Vars
| Tür | Anahtar | Kaynak |
|-----|----------|---------|
| Secret | `AWS_ROLE_TO_ASSUME` | Terraform output `gha_role_arn` |
| Secret | `PLACES_API_KEY` | Google Places test key |
| Secret | `REDIS_URL` | Upstash test instance |
| Variable | `AWS_REGION` | Terraform’da kullandığın region |
| Variable | `ECR_REPO` | Terraform output `ecr_repo_url` |
| Variable | `LAMBDA_FUNCTION_NAME` | Terraform output `lambda_name` |

### 5. Deploy Süreci
| Branch / Tag | Ortam | Açıklama |
|---------------|-------|----------|
| `develop` push | Staging | Lambda alias=`staging` güncellenir |
| `v*.*.*` tag | Production | Lambda alias=`prod` canary (%10→100) |

### 6. Test & Smoke
- İlk deploy’da `/health` endpoint’i kontrol edilir
- Testler yazıldıkça CI aşamasına eklenir (`dotnet test`)

### 7. Canlıya Geçiş
- Abonelikler aktif edildiğinde (AWS billing, Upstash plan, Google Places),
  `terraform apply` ile environment değişkenleri güncellenir
- Frontend (Vercel) `.env` → `NEXT_PUBLIC_API_BASE=<api_url>`

---

## 💸 Maliyet Yönetimi
| Servis | Ücret | Not |
|--------|-------|-----|
| Lambda | Free tier (1M requests) | 1k kullanıcıda ~0$ |
| API Gateway | Free tier (1M requests) |  |
| ECR | < $0.05 | tek image |
| CloudWatch | ~ $1 | logs |
| Upstash Redis | Free plan | |
| Google Places | pay-as-you-go | test key kullanılacak |

---

## 🚀 Yol Haritası
1. Terraform apply → kaynaklar oluştur
2. GitHub Actions secrets/vars tanımla
3. `develop` branch push → otomatik staging deploy
4. `GET /health` test et
5. `v1.0.0` tag → prod deploy (canary)
6. Abonelikleri aktif etmeden önce load & integration testleri çalıştır
7. Abonelikler açıldığında Terraform’da environment’ı güncelle

---

## 🧭 Notlar
- Testler eklendikçe CI otomatik devreye girecek (şu anda build-only)
- Maliyet minimumda, altyapı üretim için hazır
- Her şey **serverless**, dolayısıyla bakım yükü ≈ 0
