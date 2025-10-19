# DbMigrate (DbUp runner)

A minimal DbUp-based runner that applies SQL migrations in `db/migrations` to PostgreSQL.

## Prerequisites
- .NET 8 SDK
- Connection string via one of:
  - `--connection "Host=...;Port=5432;Database=...;Username=...;Password=...;SslMode=Require;"`
  - `TENX_DB_CONNECTION` or `POSTGRES_CONNECTION_STRING` env var
  - `DATABASE_URL` (e.g., `postgres://user:pass@host:5432/db?sslmode=require`)

## Scripts location
- Default: `db/migrations` relative to the current working directory.
- Pass `--scripts <path>` to override.

## Usage
From repo root (recommended):

```bash
# Preview which scripts would run
dotnet run --project tools/DbMigrate -- --connection "Host=localhost;Port=5432;Database=tenx;Username=postgres;Password=postgres;SslMode=Prefer;" --preview --scripts db/migrations

# Apply migrations
dotnet run --project tools/DbMigrate -- --connection "Host=localhost;Port=5432;Database=tenx;Username=postgres;Password=postgres;SslMode=Prefer;" --scripts db/migrations
```

Flags:
- `--preview`           Print applied/pending scripts and exit.
- `--scripts <path>`    Path to `.sql` files (default `db/migrations`).
- `--ensure-database`   Create database if it doesn't exist (DbUp.EnsureDatabase).
- `--timeout <seconds>` Note only informational; for long statements prefer `SET statement_timeout` in SQL.

## Ordering
Scripts are executed in filename order. Use UTC timestamp prefixes like `YYYYMMDDHHmmss_description.sql`.

## Journal
Applied scripts are recorded in `app.schemaversions`.

## Notes
- RLS (Row-Level Security) policies are defined in migrations. The application sets `SET LOCAL app.user_id = '<uuid>'` per request.
- Public/shared tables (maps, map_tiles, unit_definitions, analytics_events, settings) have RLS disabled per design.
- Run migrations in CI or on application startup before handling traffic.

## RLS testing with psql

Quick manual checks from your shell (connect as a login that is a member of `app_user`, e.g., `tenx_app`):

```bash
# Example: single-transaction visibility check
psql "$TENX_DB_CONNECTION" -v ON_ERROR_STOP=1 <<'SQL'
begin;
  set local app.user_id = '00000000-0000-0000-0000-0000000000a1';
  select now(); -- your queries here, only rows for this user are visible
rollback;
SQL

# Full smoke test (creates two games with different user_ids and exercises RLS)
psql "$TENX_DB_CONNECTION" -v ON_ERROR_STOP=1 -f db/testing/rls-smoke.sql
```

Expected outcomes (from the smoke test):
- Each user sees only their own `app.games` row.
- UPDATE on another user’s game affects 0 rows due to `USING` predicate.
- INSERT into `app.saves` for another user’s game fails due to `WITH CHECK` predicate.

## Secrets and roles

### TENX_DB_CONNECTION
- Set a PostgreSQL connection string in repository or environment secrets as `TENX_DB_CONNECTION`.
- Example: `Host=db.example.com;Port=5432;Database=tenx;Username=tenx_migrator;Password=***;SslMode=Require;`

### Recommended database roles
This project defines application roles inside migrations (as NOLOGIN roles):
- `app_user` (subject to RLS)
- `app_admin` (BYPASSRLS)
- `app_migrator` (DDL/seeding)

Provision login roles and grant membership:
```sql
-- Run as a superuser or an admin with CREATEROLE
create role tenx_migrator login password 'strong-password';
create role tenx_app login password 'strong-password';
create role tenx_admin login password 'strong-password';

grant app_migrator to tenx_migrator;
grant app_user     to tenx_app;
grant app_admin    to tenx_admin; -- reserved for maintenance procs
```

Initial migration requires the ability to create schemas, tables, and (optionally) roles.
Options:
- Use `tenx_migrator` with `CREATEDB`/`CREATEROLE` for the very first migration, or
- Pre-create `app_user`, `app_admin`, `app_migrator` and run with a role that can create schemas/tables but not roles.

For CI, set `TENX_DB_CONNECTION` to the `tenx_migrator` login.
