# Tech stack

## Frontend
React 19: embeded in .Net mvc + Vite: fast dev server, HMR, simple build

TypeScript 5: supports static code typing and better IDE support

Tailwind 4 (PostCSS): quick to ship functional UI; purge keeps bundle tiny.

## Backend
.NET 8: great perf, minimal APIs available, good testing story.
Serilog as a logging library, greate integrations and configurable sinks.

Server-authoritative: exactly what I want for deterministic rules.

PostgreSQL: solid, cheap, reliable; EF Core used for data access; migrations run via DbUp.

DbUp (NuGet): SQL-first migrations runner that applies versioned DDL (schemas, tables, constraints, RLS policies, roles/grants) to PostgreSQL. Scripts are idempotent where possible and executed in deterministic order.

## Testing

### Frontend Unit & Component Testing
- **Vitest**: Fast, modern test runner with native ESM and TypeScript support
- **React Testing Library (RTL)**: Component testing utilities focused on testing behavior rather than implementation

### Backend Unit & Integration Testing
- **xUnit**: Standard .NET testing framework for unit and integration tests
- **FluentAssertions**: Readable assertion library for C# tests
- **Bogus**: Fake data generation library for realistic test data
- **Testcontainers**: Docker containers for integration tests (PostgreSQL)
- **Respawn**: Database cleanup utility for test isolation

### End-to-End Testing
- **Playwright**: Cross-browser E2E testing with visual regression capabilities

### Performance Testing
- **k6**: Load testing tool for API performance validation

### Security Testing
- **OWASP ZAP**: Dynamic application security testing (DAST)
- **npm audit / dotnet list package --vulnerable**: Dependency vulnerability scanning

## CI/CD and hosting
GithubActions: for creating CI/CD pipelines
DigitalOcean: for hosting application docker images

Migrations: DbUp runs on application startup or as a CI step prior to deployment to ensure the target database is on the expected schema version.

## Related Docs

- Project Structure (overview): README.md#project-structure
- Project Structure (detailed): docs/architecture/project-structure.md
