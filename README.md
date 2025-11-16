## TenXEmpires

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=white)](https://react.dev/)
[![Vite](https://img.shields.io/badge/Vite-7-646CFF?logo=vite&logoColor=white)](https://vitejs.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.9-3178C6?logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![Docker](https://img.shields.io/badge/Docker-ready-2496ED?logo=docker&logoColor=white)](https://www.docker.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green)](LICENSE)

### Table of Contents
- [Project description](#project-description)
- [Tech stack](#tech-stack)
  - [React client](#react-client)
  - [Web API](#web-api)
- [Project structure](#project-structure)
- [Getting started locally](#getting-started-locally)
  - [Prerequisites](#prerequisites)
  - [Install dependencies](#install-dependencies)
  - [Run in development](#run-in-development)
  - [Build for production](#build-for-production)
  - [Run with Docker (optional)](#run-with-docker-optional)
- [Available scripts](#available-scripts)
  - [Client (npm)](#client-npm)
  - [Server (dotnet)](#server-dotnet)
- [Project scope](#project-scope)
- [Project status](#project-status)
- [License](#license)

## Project description
TenXEmpires is a two-part solution:
- React single-page application in `tenxempires.client` built with Vite and TypeScript.
- ASP.NET Core 8 Web API in `TenXEmpires.Server` that:
  - Serves the production SPA build as static files.
  - Proxies to the Vite dev server in development via ASP.NET Core SPA Proxy.
  - Exposes API endpoints with interactive docs via Swagger in Development.

The solution is structured so the Web API hosts the SPA in production. In development, you typically run both the Vite dev server and the .NET API; the API uses a proxy to forward UI requests to the Vite dev server.

## Tech stack

### React client
- React 19 (`react`, `react-dom`)
- Vite 7 with `@vitejs/plugin-react`
- TypeScript 5.9
- ESLint 9 with React Hooks and React Refresh plugins

Key files:
- `tenxempires.client/package.json` (scripts and dependencies)
- `tenxempires.client/vite.config.ts` (Vite configuration)
- `tenxempires.client/src` (application code)

### Web API
- .NET 8 (`<TargetFramework>net8.0</TargetFramework>`)
- ASP.NET Core Minimal Hosting
- Swagger/OpenAPI via `Swashbuckle.AspNetCore` (Development only)
- SPA integration via `Microsoft.AspNetCore.SpaProxy`
- Docker-enabled (`Dockerfile` provided; Visual Studio profile: "Container (Dockerfile)")

Key files:
- `TenXEmpires.Server/TenXEmpires.Server.csproj` (packages, SPA proxy config)
- `TenXEmpires.Server/Program.cs` (middleware, controllers, Swagger)
- `TenXEmpires.Server/Controllers` (e.g., sample `WeatherForecastController.cs`)
- `TenXEmpires.Server/Properties/launchSettings.json` (dev URLs and profiles)

## Project structure

- `tenxempires.client` — React app (Vite, TypeScript)
- `TenXEmpires.Server` — ASP.NET Core Web API host (serves SPA in prod)
- `TenXEmpires.Server.Domain` — domain entities and interfaces (no infra deps)
- `TenXEmpires.Server.Infrastructure` — EF Core DbContext, mappings, repositories
- `db/migrations` — SQL-first DbUp migrations (DDL, RLS, grants)
- `db/testing` — RLS helpers and dev-only scripts
- `tools/DbMigrate` — DbUp migration runner CLI
- `.ai` — planning docs (prd, db-plan, api-plan, tech-stack)
- `.cursor/rules` — repository coding/review rules
- `.diary` — internal dev notes
- `.github` — GitHub Actions workflows

Design notes

- Layering: `Domain <- Infrastructure <- Server` (one-way references)
- Add entities in `TenXEmpires.Server.Domain/Entities`, map them in `TenXEmpires.Server.Infrastructure/Data/TenXDbContext.cs`, expose via controllers in `TenXEmpires.Server`.
- Add schema changes as SQL files under `db/migrations` and apply via DbUp (see `tools/DbMigrate`).

More details: see `docs/architecture/project-structure.md`.

## Getting started locally

### Prerequisites
- .NET SDK 8.0+
- Node.js (recommend LTS ≥ 20) and npm
- Docker (optional)
- HTTPS developer certificates trusted (for local HTTPS)
  - Windows/macOS: `dotnet dev-certs https --trust`

### Install dependencies
```bash
# From repo root
cd tenxempires.client
npm install
```

### Run in development
Option A: Run client and server separately
```bash
# Terminal 1 - client
cd tenxempires.client
npm run dev

# Terminal 2 - server
cd ../TenXEmpires.Server
dotnet run
```
- API development URLs (from `launchSettings.json`):
  - HTTP: `http://localhost:5019`
  - HTTPS: `https://localhost:7212`
- Swagger UI (Development): visit `/swagger` on the API URL.
- SPA proxy (from `TenXEmpires.Server.csproj`):
  - `SpaProxyLaunchCommand`: `npm run dev`
  - `SpaProxyServerUrl`: `https://localhost:55414`

Option B: Run via IDE profile (Visual Studio)
- Use the "https" or "Container (Dockerfile)" profiles. Some profiles may launch the SPA via the proxy automatically; if not, start the client as shown in Option A.

### Build for production
```bash
# Build client
cd tenxempires.client
npm run build

# Run server (serves built SPA and API)
cd ../TenXEmpires.Server
dotnet run
```

### Run with Docker (optional)
A `Dockerfile` is available under `TenXEmpires.Server/`.

Build the image from the repo root:
```bash
docker build -t tenxempires.server -f TenXEmpires.Server/Dockerfile .
```

Run the container mapping the ports specified in the Docker profile (HTTP 8080, HTTPS 8081):
```bash
docker run -p 8080:8080 -p 8081:8081 tenxempires.server
```
Then open `http://localhost:8080/swagger`.

## Available scripts

### Client (npm)
From `tenxempires.client`:
```bash
npm run dev       # Start Vite dev server
npm run build     # TypeScript project build + Vite production build
npm run preview   # Preview production build locally
npm run lint      # Lint sources
```

### Server (dotnet)
From `TenXEmpires.Server`:
```bash
dotnet build              # Build the Web API
dotnet run                # Run the Web API (Swagger in Development)
dotnet watch run          # Hot-reload during development
dotnet publish -c Release # Publish for deployment
```

## Project scope
- Bootstrap template integrating:
  - React 19 + Vite 7 + TypeScript 5.9 frontend
  - ASP.NET Core 8 backend with controllers and Swagger
  - SPA Proxy for streamlined dev experience
- Example endpoint(s) provided by the default template (e.g., `GET /weatherforecast`)
- Production serves the compiled SPA from the Web API

If a formal PRD exists, link it here when available.

## Project status
- Status: Early-stage/bootstrap
- Active development; APIs and UI are expected to evolve
- CI/CD: GitHub Actions workflows (`.github/workflows/main.yml`, `pr.yml`, `deploy-*.yml`) run lint/test suites and coordinate deployments
- Documentation: PRD, UI plans, API/test plans, and other living docs live under `.ai/` and `docs/` and are kept current alongside the codebase

## License
This project is licensed under the [MIT License](LICENSE).

