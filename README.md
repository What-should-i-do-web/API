# WhatShouldIDo Solution

Bu doküman, öncelikle **frontend** geliştiriciler için gerekli en basit adımları içerir. Backend’in (API) kullanıma hazır hale getirilmesi, Docker üzerinden ayağa kaldırılması ve endpoint’lerin test edilmesi anlatılmaktadır.

---

## Frontend Geliştirici Kılavuzu

### 1. Önbildirim

* **Docker Desktop** ve **Docker Compose** kurulu olmalı.
* **Git** kurulu olmalı.

### 2. Projeyi Klonlayın

```bash
git clone https://github.com/What-should-i-do-web/NeYapsamWeb.git
```

### 3. Backend’i Çalıştırın

```bash
docker-compose up --build -d
```

* Bu komut, SQL Server ve API konteynerlerini ayağa kaldırır.
* Çıktıda `api_1 | Now listening on: http://[::]:80` görmelisiniz.

### 4. Veritabanı Migration’larını Uygulayın

```bash
docker-compose exec api dotnet ef database update --context WhatShouldIDoDbContext --project WhatShouldIDo.Infrastructure/WhatShouldIDo.Infrastructure.csproj --startup-project WhatShouldIDo.API/WhatShouldIDo.API.csproj
```

* Bu adım, `WhatShouldIDo` veritabanını otomatik olarak oluşturur ve tabloları kurar.

---

## Geliştirme Ortamı (Opsiyonel)

Aşağıdaki adımları yalnızca backend üzerinde geliştirme veya hata ayıklama yapacaksanız uygulayın.

### Gerekenler

* **.NET 9 SDK**
* **EF Core Tools** (`dotnet tool install --global dotnet-ef`)

### Migration ve Veritabanı

1. `src/WhatShouldIDo.Infrastructure` içinde:

   ```bash
   dotnet ef migrations add InitialCreate --project . --startup-project ../WhatShouldIDo.API --output-dir Migrations
   dotnet ef database update --project . --startup-project ../WhatShouldIDo.API
   ```
2. Uygulamayı yerel çalıştırmak isterseniz:

   ```bash
   cd src/WhatShouldIDo.API
   dotnet run
   ```

---

## Endpoint Özeti

| Method | URL                | Açıklama               |
| ------ | ------------------ | ---------------------- |
| GET    | `/api/routes`      | Tüm rotaları listeler  |
| GET    | `/api/routes/{id}` | Belirli rotayı getirir |
| POST   | `/api/routes`      | Yeni rota oluşturur    |
| PUT    | `/api/routes/{id}` | Rota adını günceller   |
| DELETE | `/api/routes/{id}` | Rotayı siler           |

---

Herhangi bir sorunuz veya ihtiyacınız olursa, backend ekibiyle iletişime geçebilirsiniz.
