# Production deployment plan — TenXEmpires

## Overview

**Architecture:**
- Frontend: React 19 + Vite (Nginx) — Separate App Platform app — $5/month (512 MiB)
- Backend: .NET 8 API — Separate App Platform app — $10/month (1 GiB)
- Database: PostgreSQL 16 Managed Database — $10/month (1 GiB)

**Total cost:** $25/month

**Note:** Frontend and backend are deployed as separate App Platform apps to enable different domain assignments (`yourdomain.com` for frontend, `api.yourdomain.com` for backend), since DigitalOcean manages domains at the app level, not per component.

---

## Phase 1: Code changes

### 1.1 Create frontend Dockerfile

**File:** `tenxempires.client/Dockerfile`

```dockerfile
# Build stage
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files
COPY package*.json ./
RUN npm ci

# Copy source and build
COPY . .
RUN npm run build

# Production stage with nginx
FROM nginx:alpine
WORKDIR /usr/share/nginx/html

# Copy built files
COPY --from=build /app/dist /usr/share/nginx/html

# Copy nginx config (optional, for SPA routing)
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Expose port 80
EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
```

### 1.2 Create nginx configuration for frontend

**File:** `tenxempires.client/nginx.conf`

```nginx
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;

    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_min_length 1024;
    gzip_types text/plain text/css text/xml text/javascript application/x-javascript application/xml+rss application/json;

    # SPA routing - serve index.html for all routes
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Cache static assets
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }

    # Health check endpoint
    location /health {
        access_log off;
        return 200 "healthy\n";
        add_header Content-Type text/plain;
    }
}
```

### 1.3 Update backend Dockerfile (remove client build)

**File:** `TenXEmpires.Server/Dockerfile`

```dockerfile
# Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build stage (no Node.js needed)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy and restore server project
COPY ["TenXEmpires.Server/TenXEmpires.Server.csproj", "TenXEmpires.Server/"]
RUN dotnet restore "./TenXEmpires.Server/TenXEmpires.Server.csproj"

# Copy domain and infrastructure projects
COPY ["TenXEmpires.Server.Domain/TenXEmpires.Server.Domain.csproj", "TenXEmpires.Server.Domain/"]
COPY ["TenXEmpires.Server.Infrastructure/TenXEmpires.Server.Infrastructure.csproj", "TenXEmpires.Server.Infrastructure/"]
RUN dotnet restore "./TenXEmpires.Server/TenXEmpires.Server.csproj"

# Copy all source and build
COPY . .
WORKDIR "/src/TenXEmpires.Server"
RUN dotnet build "./TenXEmpires.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TenXEmpires.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TenXEmpires.Server.dll"]
```

### 1.4 Remove SPA proxy from backend project

**File:** `TenXEmpires.Server/TenXEmpires.Server.csproj`

Remove these lines:
```xml
<SpaRoot>..\tenxempires.client</SpaRoot>
<SpaProxyLaunchCommand>npm run dev</SpaProxyLaunchCommand>
<SpaProxyServerUrl>http://localhost:5173</SpaProxyServerUrl>
```

Remove this package reference:
```xml
<PackageReference Include="Microsoft.AspNetCore.SpaProxy" Version="8.0.21" />
```

Remove this project reference (or keep it but it won't be used):
```xml
<ProjectReference Include="..\tenxempires.client\tenxempires.client.esproj">
  <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
</ProjectReference>
```

### 1.5 Update Program.cs to remove static file serving (optional)

**File:** `TenXEmpires.Server/Program.cs`

Remove or comment out these lines (lines 335-336, 365):
```csharp
app.UseDefaultFiles();
app.UseStaticFiles();
// ...
app.MapFallbackToFile("/index.html");
```

Or make them conditional:
```csharp
// Only serve static files if in development (for local testing)
if (app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("/index.html");
}
```

### 1.6 Add health check endpoint to backend

**File:** `TenXEmpires.Server/Controllers/HealthController.cs` (create new)

```csharp
using Microsoft.AspNetCore.Mvc;

namespace TenXEmpires.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
```

### 1.7 Update appsettings.json for production CORS

**File:** `TenXEmpires.Server/appsettings.json`

Update CORS section:
```json
"Cors": {
  "AllowedOrigins": [
    "https://yourdomain.com",
    "https://www.yourdomain.com"
  ],
  "AllowCredentials": true,
  "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
  "AllowedHeaders": ["*"],
  "ExposedHeaders": ["ETag", "X-Tenx-Total-Count"]
}
```

### 1.8 Create production appsettings

**File:** `TenXEmpires.Server/appsettings.Production.json` (create new)

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "Cors": {
    "AllowedOrigins": []
  }
}
```

Note: Connection string and CORS will be set via environment variables.

### 1.9 Update vite.config.ts for production API URL

**File:** `tenxempires.client/vite.config.ts`

```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: process.env.VITE_API_BASE_URL || 'http://localhost:5019',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, '/v1'),
        secure: false,
      },
    },
  },
  // Build-time environment variable
  define: {
    'import.meta.env.VITE_API_BASE_URL': JSON.stringify(process.env.VITE_API_BASE_URL || ''),
  },
})
```

### 1.10 Create Docker Compose for E2E tests

**File:** `docker-compose.e2e.yml` (in repo root)

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: tenxempires_test
      POSTGRES_USER: tenxempires_test
      POSTGRES_PASSWORD: test_password
    ports:
      - "5433:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U tenxempires_test"]
      interval: 5s
      timeout: 5s
      retries: 5

  backend:
    build:
      context: .
      dockerfile: TenXEmpires.Server/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_HTTP_PORTS: 8080
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=tenxempires_test;Username=tenxempires_test;Password=test_password;SslMode=Prefer;"
      Cors__AllowedOrigins__0: "http://localhost:5173"
    ports:
      - "5019:8080"
    depends_on:
      postgres:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/api/health"]
      interval: 10s
      timeout: 5s
      retries: 5

  frontend:
    build:
      context: ./tenxempires.client
      dockerfile: Dockerfile
    environment:
      VITE_API_BASE_URL: "http://localhost:5019"
    ports:
      - "5173:80"
    depends_on:
      - backend
```

---

## Phase 2: GitHub Actions workflows

**Workflow Strategy:** We use a two-workflow approach for optimal CI/CD:
- **PR Workflow**: Fast feedback with unit tests, linting, and builds (~2-5 minutes)
- **Main Workflow**: Full validation including E2E tests when code is merged (~5-10 minutes)

This approach saves CI minutes, reduces flakiness noise on PRs, and ensures thorough validation before deployment.

### 2.1 PR pipeline (fast feedback - unit tests only)

**File:** `.github/workflows/pr.yml`

**Purpose:** Fast feedback on PRs with unit tests, linting, and builds. E2E tests run only on main branch to save CI time and reduce flakiness.

```yaml
name: PR Build and Test

on:
  pull_request:
    types: [opened, synchronize, reopened]
  workflow_dispatch:

jobs:
  build-backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      
      - name: Restore dependencies
        run: dotnet restore TenXEmpires.Server/TenXEmpires.Server.csproj
      
      - name: Build
        run: dotnet build TenXEmpires.Server/TenXEmpires.Server.csproj --configuration Release --no-restore
      
      - name: Run unit tests
        run: dotnet test --configuration Release --no-build --verbosity normal

  build-frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
          cache-dependency-path: tenxempires.client/package-lock.json
      
      - name: Install dependencies
        working-directory: tenxempires.client
        run: npm ci
      
      - name: Lint
        working-directory: tenxempires.client
        run: npm run lint
      
      - name: Run unit tests
        working-directory: tenxempires.client
        run: npm test
      
      - name: Build
        working-directory: tenxempires.client
        run: npm run build
```

### 2.2 Main branch pipeline (full validation with E2E)

**File:** `.github/workflows/main.yml`

**Purpose:** Full validation including E2E tests when code is merged to main. Ensures nothing broken gets deployed.

```yaml
name: Main Branch Validation

on:
  push:
    branches: [main]
  workflow_dispatch:

jobs:
  # Run all PR checks first
  build-backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      
      - name: Restore dependencies
        run: dotnet restore TenXEmpires.Server/TenXEmpires.Server.csproj
      
      - name: Build
        run: dotnet build TenXEmpires.Server/TenXEmpires.Server.csproj --configuration Release --no-restore
      
      - name: Run unit tests
        run: dotnet test --configuration Release --no-build --verbosity normal

  build-frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
          cache-dependency-path: tenxempires.client/package-lock.json
      
      - name: Install dependencies
        working-directory: tenxempires.client
        run: npm ci
      
      - name: Lint
        working-directory: tenxempires.client
        run: npm run lint
      
      - name: Run unit tests
        working-directory: tenxempires.client
        run: npm test
      
      - name: Build
        working-directory: tenxempires.client
        run: npm run build

  # E2E tests only on main branch
  e2e-tests:
    runs-on: ubuntu-latest
    needs: [build-backend, build-frontend]
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      
      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
          cache-dependency-path: tenxempires.client/package-lock.json
      
      - name: Install Playwright
        working-directory: tenxempires.client
        run: npx playwright install --with-deps chromium
      
      - name: Start services with Docker Compose
        run: docker-compose -f docker-compose.e2e.yml up -d
      
      - name: Wait for services to be healthy
        run: |
          timeout 120 bash -c 'until curl -f http://localhost:5019/api/health; do sleep 2; done'
          timeout 30 bash -c 'until curl -f http://localhost:5173/health; do sleep 2; done'
      
      - name: Run database migrations
        run: |
          dotnet run --project tools/DbMigrate -- \
            --connection "Host=localhost;Port=5433;Database=tenxempires_test;Username=tenxempires_test;Password=test_password;SslMode=Prefer;" \
            --scripts db/migrations \
            --ensure-database
      
      - name: Run E2E tests
        working-directory: tenxempires.client
        env:
          CI: true
          API_BASE_URL: http://localhost:5019
        run: npm run test:e2e
      
      - name: Upload Playwright report
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-report
          path: tenxempires.client/playwright-report/
          retention-days: 7
      
      - name: Stop services
        if: always()
        run: docker-compose -f docker-compose.e2e.yml down -v
```

### 2.3 Database migrations (no separate database app)

**Note:** Since we're using a DigitalOcean Managed Database (not an App Platform app), database migrations are handled as part of the backend deployment workflow (see 2.4). There is no separate database deployment workflow needed.

The database is a managed service that runs independently. Migrations run automatically when the backend is deployed (see `run-migrations` job in `deploy-backend.yml`).

### 2.4 Deploy backend pipeline

**File:** `.github/workflows/deploy-backend.yml`

```yaml
name: Deploy Backend

on:
  workflow_dispatch:
    inputs:
      branch:
        description: 'Branch to deploy from'
        required: true
        type: string
        default: 'main'
  workflow_call:
    inputs:
      branch:
        required: true
        type: string

permissions:
  contents: read
  packages: write

env:
  REGISTRY: ghcr.io/${{ github.repository_owner }}
  IMAGE_NAME: tenxempires/backend

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v5
        with:
          ref: ${{ inputs.branch || github.event.inputs.branch }}
      
      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3
      
      - name: Log in to GitHub Container Registry
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
      
      - name: Extract metadata
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}
          tags: |
            type=raw,value={{sha}}
            type=raw,value=latest,enable={{is_default_branch}}
      
      - name: Build and push
        uses: docker/build-push-action@v6
        with:
          context: .
          file: TenXEmpires.Server/Dockerfile
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          cache-from: type=gha
          cache-to: type=gha,mode=max

  run-migrations:
    runs-on: ubuntu-latest
    needs: build-and-push
    steps:
      - name: Checkout
        uses: actions/checkout@v5
        with:
          ref: ${{ inputs.branch || github.event.inputs.branch }}
      
      - name: Setup .NET 8
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: '8.0.x'
      
      - name: Restore
        run: dotnet restore tools/DbMigrate/DbMigrate.csproj
      
      - name: Build
        run: dotnet build --configuration Release tools/DbMigrate/DbMigrate.csproj
      
      - name: Run migrations
        env:
          TENX_DB_CONNECTION: ${{ secrets.TENX_DB_CONNECTION }}
        run: |
          if [ -z "${TENX_DB_CONNECTION}" ]; then
            echo "TENX_DB_CONNECTION secret is not set" >&2; exit 2; fi
          dotnet run --project tools/DbMigrate --configuration Release -- \
            --connection "$TENX_DB_CONNECTION" \
            --scripts db/migrations \
            --ensure-database

  deploy:
    runs-on: ubuntu-latest
    needs: [build-and-push, run-migrations]
    steps:
      - name: Deploy to DigitalOcean App Platform
        uses: digitalocean/app_action@v2
        with:
          app_name: tenxempires-backend
          token: ${{ secrets.DIGITALOCEAN_ACCESS_TOKEN }}
          # App Platform will pull the latest image from GHCR
```

This workflow always produces a tag equal to the exact commit SHA (plus `latest` on the default branch), so deployed backend images map directly to the commit on `main`.

### 2.5 Deploy frontend pipeline

**File:** `.github/workflows/deploy-frontend.yml`

```yaml
name: Deploy Frontend

on:
  workflow_dispatch:
    inputs:
      branch:
        description: 'Branch to deploy from'
        required: true
        type: string
        default: 'main'
      api_base_url:
        description: 'Backend API URL'
        required: true
        type: string
        default: 'https://api.yourdomain.com'
  workflow_call:
    inputs:
      branch:
        required: true
        type: string
      api_base_url:
        required: true
        type: string

permissions:
  contents: read
  packages: write

env:
  REGISTRY: ghcr.io/${{ github.repository_owner }}
  IMAGE_NAME: tenxempires/frontend

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v5
        with:
          ref: ${{ inputs.branch || github.event.inputs.branch }}
      
      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3
      
      - name: Log in to GitHub Container Registry
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
      
      - name: Extract metadata
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}
          tags: |
            type=raw,value={{sha}}
            type=raw,value=latest,enable={{is_default_branch}}
      
      - name: Build and push
        uses: docker/build-push-action@v6
        with:
          context: ./tenxempires.client
          file: ./tenxempires.client/Dockerfile
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          build-args: |
            VITE_API_BASE_URL=${{ inputs.api_base_url || github.event.inputs.api_base_url }}
          cache-from: type=gha
          cache-to: type=gha,mode=max

  deploy:
    runs-on: ubuntu-latest
    needs: build-and-push
    steps:
      - name: Deploy to DigitalOcean App Platform
        uses: digitalocean/app_action@v2
        with:
          app_name: tenxempires
          token: ${{ secrets.DIGITALOCEAN_ACCESS_TOKEN }}
```

### 2.6 Deploy full stack pipeline

**File:** `.github/workflows/deploy-fullstack.yml`

```yaml
name: Deploy Full Stack

on:
  workflow_dispatch:
    inputs:
      branch:
        description: 'Branch to deploy from'
        required: true
        type: string
        default: 'main'
      api_base_url:
        description: 'Backend API URL'
        required: true
        type: string
        default: 'https://api.yourdomain.com'

jobs:
  deploy-backend:
    uses: ./.github/workflows/deploy-backend.yml
    secrets: inherit
    with:
      branch: ${{ github.event.inputs.branch }}

  deploy-frontend:
    needs: deploy-backend
    uses: ./.github/workflows/deploy-frontend.yml
    secrets: inherit
    with:
      branch: ${{ github.event.inputs.branch }}
      api_base_url: ${{ github.event.inputs.api_base_url }}
```

---

## Phase 3: GitHub secrets configuration

### 3.1 Required secrets

Go to: `Settings > Secrets and variables > Actions`

Add these secrets:

1. `DIGITALOCEAN_ACCESS_TOKEN`
   - Generate in DigitalOcean: Account > API > Generate New Token
   - Scopes: Read and Write

2. `TENX_DB_CONNECTION`
   - Format: `Host=<db-hostname>;Port=5432;Database=tenxempires;Username=tenxempires;Password=<password>;SslMode=Prefer;`
   - Set after creating the database service

3. `EMAIL_SMTP_USERNAME` (if using email)
   - SMTP username

4. `EMAIL_SMTP_PASSWORD` (if using email)
   - SMTP password

> Note: Pushing to GHCR uses the built-in `GITHUB_TOKEN`, so no extra PAT/secret (like `GHCR_TOKEN`) is required as long as the repository and packages remain public. The `GITHUB_TOKEN` is automatically available in GitHub Actions workflows.

---

## Phase 4: DigitalOcean App Platform setup

### 4.1 Configure GitHub Container Registry (GHCR)

1. Images live under `ghcr.io/<github_owner>/tenxempires/*`, so no DigitalOcean registry is required.
   - Backend: `ghcr.io/quantumcores/tenxempires/backend`
   - Frontend: `ghcr.io/quantumcores/tenxempires/frontend`
2. Because the repository (and GHCR packages) are public, App Platform can pull the images anonymously—no additional credentials or PATs are needed.
3. For pushing images, the workflows use the built-in `GITHUB_TOKEN` (no `GHCR_TOKEN` secret needed for public packages).
4. If you ever switch the packages to private, create read/write PATs at that time and update both GitHub Actions (push) and App Platform (pull).
5. Images are tagged with the commit SHA (`{{sha}}`) plus `latest` tag on the default branch.

### 4.2 Architecture decision: Separate apps for domain routing

**Important:** DigitalOcean App Platform manages domains at the **app level**, not per component. To assign different domains to backend (`api.yourdomain.com`) and frontend (`yourdomain.com`), we need to deploy each service as a **separate App Platform app**.

**Architecture:**
- **Backend App Platform:** `tenxempires-backend`
  - Web Service Component: `api-tenxempires` → `api.yourdomain.com`
  - Database Component: `tenxempires-db` (PostgreSQL 16)
- **Frontend App Platform:** `tenxempires`
  - Web Service Component: `tenxempires-web` → `yourdomain.com`

**Cost impact:** Same total cost ($25/month).

### 4.3 Create managed PostgreSQL database

**Option 1: DigitalOcean Managed Database (Recommended)**

1. Go to: DigitalOcean > Databases > Create Database Cluster
2. Choose: PostgreSQL 16
3. Plan: Basic - $10/month (1 vCPU, 1 GiB RAM)
4. Datacenter: Same region as your apps
5. Database name: `tenxempires`
6. User: `tenxempires`
7. Password: Generate strong password (save it!)
8. Create database
9. **Note the connection string** from the database overview page (you'll need this for backend app)

**Option 2: Database as component in backend app (Alternative)**

If you prefer to keep everything in App Platform:
1. Create backend app first (see 4.4)
2. In backend app, add database as a component:
   - Click "Add Component" > "Database"
   - Choose: PostgreSQL 16
   - Name: `tenxempires-db` (or similar)
   - Plan: Basic - $10/month (1 vCPU, 1 GiB RAM)
3. **Connection String:** App Platform automatically provides a `DATABASE_URL` environment variable in PostgreSQL URL format (`postgres://user:pass@host:port/dbname`). You have two options:
   
   **Option A:** Use `DATABASE_URL` directly (if your app supports it)
   
   **Option B:** Convert to connection string format. App Platform also provides individual variables:
   - `DB_HOST` - internal hostname (e.g., `tenxempires-db`)
   - `DB_PORT` - port (usually `5432`)
   - `DB_NAME` - database name
   - `DB_USER` - username
   - `DB_PASSWORD` - password
   
   Set `ConnectionStrings__DefaultConnection` in backend environment variables:
   ```
   ConnectionStrings__DefaultConnection=Host=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD};SslMode=Prefer;
   ```
   
   Or manually construct it using the actual values from the database component's settings.

### 4.4 Create backend app

1. Go to: DigitalOcean > App Platform > Create App
2. Choose: "Container Image" (not GitHub)
3. Image: `ghcr.io/quantumcores/tenxempires/backend:latest`
4. Registry: GitHub Container Registry (ghcr.io)
5. Registry credentials: Not required (public image)
6. App name: `tenxempires-backend`
7. Component name: `api-tenxempires` (or let App Platform auto-generate, then rename)
8. Plan: Basic - $10/month (1 vCPU, 1 GiB RAM)
9. HTTP Port: `8080` (App Platform will automatically set `PORT=8080` environment variable)
10. Environment Variables:
   ```
   ASPNETCORE_ENVIRONMENT=Production
   ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
   ConnectionStrings__DefaultConnection=<use-connection-string-from-managed-db>
   Cors__AllowedOrigins__0=https://yourdomain.com
   Cors__AllowedOrigins__1=https://www.yourdomain.com
   Cors__AllowCredentials=true
   ```
   **Note:** `ASPNETCORE_HTTP_PORTS=8080` is optional since App Platform sets `PORT=8080` automatically and .NET 8 reads it. However, you can add it for explicitness if desired.
   
   **Database Connection:** Since you're using a database component (`tenxempires-db`), set the connection string using the component's environment variables:
   ```
   ConnectionStrings__DefaultConnection=Host=tenxempires-db;Port=5432;Database=<db-name>;Username=<user>;Password=<password>;SslMode=Prefer;
   ```
   (Check the database component's settings for actual values, or use the provided `DB_*` environment variables)
   (Add email config if needed)
11. Health Check:
    - Path: `/api/health`
    - Initial delay: 30s
    - Period: 10s
    - Timeout: 5s
    - Success threshold: 1
    - Failure threshold: 3
12. Auto-deploy: Disabled (manual via GitHub Actions)
13. **Domain:** Add domain `api.yourdomain.com` (Settings > Domains)
    - SSL: Auto (free)

### 4.5 Create frontend app

1. Go to: DigitalOcean > App Platform > Create App
2. Choose: "Container Image" (not GitHub)
3. Image: `ghcr.io/quantumcores/tenxempires/frontend:latest`
4. Registry: GitHub Container Registry (ghcr.io)
5. Registry credentials: Not required (public image)
6. App name: `tenxempires`
7. Component name: `tenxempires-web` (or let App Platform auto-generate, then rename)
8. Plan: Basic - $5/month (1 vCPU, 512 MiB RAM)
9. HTTP Port: `80`
10. Environment Variables:
   ```
   VITE_API_BASE_URL=https://api.yourdomain.com
   ```
   (Note: This is a build-time variable. If the image was built without it, rebuild with the build arg)
11. Health Check:
    - Path: `/health`
    - Initial delay: 10s
    - Period: 10s
    - Timeout: 5s
12. Auto-deploy: Disabled
13. **Domain:** Add domain `yourdomain.com` (Settings > Domains)
    - SSL: Auto (free)
    - **Add www alias:** Add `www.yourdomain.com` as additional domain

### 4.6 Configure DNS

1. **For backend app (`api.yourdomain.com`):**
   - In backend app, go to Settings > Domains
   - DigitalOcean will show DNS records (usually CNAME)
   - Add CNAME record in your DNS provider: `api.yourdomain.com` → shown CNAME target

2. **For frontend app (`yourdomain.com` and `www.yourdomain.com`):**
   - In frontend app, go to Settings > Domains
   - DigitalOcean will show DNS records
   - Add records in your DNS provider:
     - `yourdomain.com` → shown CNAME/A record target
     - `www.yourdomain.com` → shown CNAME/A record target

3. **Wait for propagation:**
   - DNS propagation: 5 minutes to 48 hours (usually < 1 hour)
   - SSL certificates: Auto-provisioned after DNS resolves (usually a few minutes)

### 4.7 Update CORS with actual domains

1. In backend service environment variables, update:
   ```
   Cors__AllowedOrigins__0=https://yourdomain.com
   Cors__AllowedOrigins__1=https://www.yourdomain.com
   ```
2. Redeploy backend after DNS/SSL is ready

---

## Phase 5: Testing and validation

### 5.1 Local Docker Compose testing

```bash
# Test E2E setup locally
docker-compose -f docker-compose.e2e.yml up -d
# Wait for services
curl http://localhost:5019/api/health
curl http://localhost:5173/health
# Run migrations
dotnet run --project tools/DbMigrate -- \
  --connection "Host=localhost;Port=5433;Database=tenxempires_test;Username=tenxempires_test;Password=test_password;SslMode=Prefer;" \
  --scripts db/migrations \
  --ensure-database
# Run E2E tests
cd tenxempires.client
npm run test:e2e
# Cleanup
docker-compose -f docker-compose.e2e.yml down -v
```

### 5.2 Test PR pipeline

1. Create a test PR
2. Verify:
   - Backend builds
   - Frontend builds
   - Unit tests pass
   - Linting passes
   - No E2E tests run (fast feedback)
   - No errors in workflow

### 5.3 Test main branch pipeline

1. Merge a test PR to main (or push directly to main)
2. Verify:
   - All PR checks run (backend build, frontend build, unit tests)
   - E2E tests run with Docker Compose
   - E2E tests pass
   - Playwright report is uploaded
   - No errors in workflow

### 5.4 Test deployment pipelines

1. Test backend deployment:
   - Run `deploy-backend.yml` manually
   - Select branch: `main`
   - Verify:
     - Image builds and pushes
     - Migrations run
     - Service deploys
     - Health check passes

2. Test frontend deployment:
   - Run `deploy-frontend.yml` manually
   - Set `api_base_url` correctly
   - Verify:
     - Image builds and pushes
     - Service deploys
     - Health check passes

3. Test full stack deployment:
   - Run `deploy-fullstack.yml`
   - Verify both services deploy

### 5.5 Post-deployment validation

1. Backend:
   ```bash
   curl https://api.yourdomain.com/api/health
   curl https://api.yourdomain.com/swagger  # If enabled in dev
   ```

2. Frontend:
   ```bash
   curl https://yourdomain.com/health
   # Open browser: https://yourdomain.com
   ```

3. Integration:
   - Open frontend in browser
   - Verify API calls work
   - Check browser console for CORS errors
   - Test authentication flow
   - Test main features

4. Database:
   - Verify migrations applied (check `schemaversions` table)
   - Test database connectivity from backend

---

## Phase 6: Monitoring and maintenance

### 6.1 Set up monitoring

1. App Platform metrics:
   - Monitor CPU, memory, request rates
   - Set alerts for high usage

2. Application logs:
   - View logs in App Platform dashboard
   - Serilog is configured

3. Health checks:
   - Monitor `/api/health` and `/health` endpoints

### 6.2 Scaling plan

If resources are insufficient:
- Backend: Scale to $25/month (2 GiB RAM)
- Database: Scale to $25/month (2 GiB RAM)
- Frontend: Usually fine at $5/month

### 6.3 Backup strategy (for demo)

- Manual backups: Use `pg_dump` via App Platform console
- Or add a scheduled backup job (optional)

---

## Checklist summary

### Code changes
- [ ] Create `tenxempires.client/Dockerfile`
- [ ] Create `tenxempires.client/nginx.conf`
- [ ] Update `TenXEmpires.Server/Dockerfile` (remove Node.js)
- [ ] Remove SPA proxy from `TenXEmpires.Server.csproj`
- [ ] Update `Program.cs` (remove static files or make conditional)
- [ ] Create `HealthController.cs`
- [ ] Update `appsettings.json` CORS
- [ ] Create `appsettings.Production.json`
- [ ] Update `vite.config.ts` for production API URL
- [ ] Create `docker-compose.e2e.yml`

### GitHub Actions
- [ ] Create `.github/workflows/pr.yml` (fast feedback - unit tests only)
- [ ] Create `.github/workflows/main.yml` (full validation with E2E tests)
- [ ] Create `.github/workflows/deploy-backend.yml` (includes database migrations)
- [ ] Create `.github/workflows/deploy-frontend.yml`
- [ ] Create `.github/workflows/deploy-fullstack.yml`
- [ ] Configure GitHub secrets

### DigitalOcean
- [ ] Create backend App Platform app: `tenxempires-backend`
  - [ ] Add web service component: `api-tenxempires` (pointing to GHCR image)
  - [ ] Add database component: `tenxempires-db` (PostgreSQL 16)
  - [ ] Configure domain `api.yourdomain.com` for backend app
  - [ ] Set environment variables (including database connection string)
  - [ ] Configure health checks
- [ ] Create frontend App Platform app: `tenxempires`
  - [ ] Add web service component: `tenxempires-web` (pointing to GHCR image)
  - [ ] Configure domains `yourdomain.com` and `www.yourdomain.com` for frontend app
  - [ ] Set environment variables
  - [ ] Configure health checks
- [ ] Update DNS records

### Testing
- [ ] Test Docker Compose locally
- [ ] Test PR pipeline (verify fast feedback, no E2E)
- [ ] Test main branch pipeline (verify E2E tests run)
- [ ] Test deployment pipelines
- [ ] Validate post-deployment
- [ ] Test end-to-end user flows

---

## Estimated timeline

- Code changes: 2-4 hours
- GitHub Actions setup: 2-3 hours
- DigitalOcean setup: 1-2 hours
- Testing and validation: 2-3 hours
- Total: 7-12 hours

This plan covers the steps needed to deploy to production. Follow each phase sequentially and test as you go.
