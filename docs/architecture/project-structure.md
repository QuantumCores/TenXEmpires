# Project Structure

This document describes the repository layout, responsibilities of each area, and how to add new code while preserving clean layering.

## Top-level layout

- `tenxempires.client` — React app (Vite, TypeScript)
- `TenXEmpires.Server` — ASP.NET Core 8 Web API host
- `TenXEmpires.Server.Domain` — domain entities and interfaces
- `TenXEmpires.Server.Infrastructure` — EF Core DbContext, mappings, repositories
- `db/migrations` — SQL-first DbUp migrations (DDL, RLS, roles/grants)
- `db/testing` — RLS smoke tests and dev scripts
- `tools/DbMigrate` — DbUp runner CLI for applying migrations
- `.ai` — planning documents (prd, db-plan, api-plan, tech-stack)
- `.cursor/rules` — repository rules for review and coding
- `.diary` — engineering journal notes
- `.github` — CI/CD workflows

## Layering and dependencies

- Domain (pure): data structures and interfaces, no dependency on EF Core or ASP.NET.
- Infrastructure: depends on Domain; implements persistence (EF Core), repositories, DbContext.
- Server (Web API): depends on Domain and Infrastructure; hosts controllers, middleware, DI, and serves SPA in production.

Dependency flow: `Domain <- Infrastructure <- Server`.

## Where to add things

- New entity
  - Define in `TenXEmpires.Server.Domain/Entities` (POCO).
  - Add mapping in `TenXEmpires.Server.Infrastructure/Data/TenXDbContext.cs`.
  - Add repository interface in Domain and implementation in Infrastructure if needed.

- Database change
  - Add a new SQL file under `db/migrations` (idempotent where possible).
  - Run via `tools/DbMigrate` in CI or application startup per environment policy.

- New endpoint
  - Add controller or minimal API in `TenXEmpires.Server`.
  - Inject repositories/services via DI; keep business rules server-authoritative.

- Game rules/business logic
  - Prefer service classes in Domain (interfaces) and Server (implementations) to keep controllers thin.

## Conventions

- Keep migrations SQL-first to ensure deterministic, reviewable DDL.
- Use RLS policies described in `db-plan.md`; set `SET LOCAL app.user_id` per request.
- Keep API payloads and contracts documented in `.ai/api-plan.md`.

