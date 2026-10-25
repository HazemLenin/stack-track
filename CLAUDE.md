# Stack Track

Trello-style task tracker. Monorepo: .NET 10 API + Angular 21 client. Jira project key: KAN.

## Commands
- API build: `dotnet build src/api`
- API tests: `dotnet test` (integration tests need Docker running, they use Testcontainers)
- Run API: `dotnet run --project src/api/StackTrack.Api`
- Client: `cd src/client && npm run start`
- Client tests: `cd src/client && npm test`
- Lint client: `cd src/client && npm run lint`
- New migration: `dotnet ef migrations add <Name> -p src/api/StackTrack.Infrastructure -s src/api/StackTrack.Api`

## Structure
- `src/api/StackTrack.Domain` entities, no dependencies
- `src/api/StackTrack.Application` MediatR commands/queries, validators, DTOs (one folder per feature)
- `src/api/StackTrack.Infrastructure` EF Core, PostgreSQL, auth
- `src/api/StackTrack.Api` thin controllers, only `Send()` to MediatR
- `src/client/src/app/features/<feature>` Angular feature folders
- `docs/plans/` implementation plans, one per ticket (`KAN-12.md`)

## Conventions
- CQRS: every use case is a Command or Query + Handler + FluentValidation validator
- Controllers hold zero business logic
- Auth: JWT access token + refresh token. Global roles only (Admin, Member), no per-board roles
- Angular: standalone components, signals for state, no NgModules
- Tests: xUnit + FluentAssertions for API, real Postgres via Testcontainers (no EF InMemory)
- Seed data lives in `StackTrack.Infrastructure/Seed`, keep it realistic

## Git & Jira
- Branch: `feature/KAN-<n>-short-slug` (`fix/KAN-<n>-short-slug` for bug fixes)
- Default branch: `main`
- Commits: `KAN-<n>: imperative message`
- One ticket = one branch = one PR
- Allowed Jira transitions: To Do -> In Progress -> In Review. Never move a ticket to Done
- If acceptance criteria are unclear, comment on the ticket and stop. Don't guess

## Don't
- Don't add NuGet/npm packages without asking
- Don't edit existing migrations, add a new one
- Don't touch files outside the ticket's scope "while you're there"
- Don't claim done until build + all tests pass