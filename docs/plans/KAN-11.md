# KAN-11 Scaffold backend solution with Clean Architecture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Empty-but-working .NET 10 Clean Architecture API: MediatR + FluentValidation pipeline, Problem Details errors, correlation id, `/health` with DB check. No business features.

**Architecture:** Four projects with inward references (Domain <- Application <- Infrastructure, Api). Validation runs in a MediatR pipeline behavior and throws `ValidationException`; one `IExceptionHandler` in Api turns exceptions into Problem Details. Correlation id comes from a middleware that also opens a log scope.

**Tech Stack:** .NET 10, ASP.NET Core controllers, MediatR 12.5.0, FluentValidation, EF Core + Npgsql, xUnit, FluentAssertions 7.2.0, Testcontainers.PostgreSql, Mvc.Testing.

**Spec:** Jira KAN-11 (+ subtasks KAN-46..49) and the 7 defaults accepted by the user on the ticket (comment 10002). Conventions: CLAUDE.md.

## Global Constraints

- Solution under `src/api` (NOT `/backend`). Projects: `StackTrack.Domain`, `.Application`, `.Infrastructure`, `.Api`, `.Application.Tests`, `.Api.Tests`, all `net10.0`, flat under `src/api/`.
- References: Application -> Domain; Infrastructure -> Application; Api -> Application + Infrastructure; Application.Tests -> Application; Api.Tests -> Api.
- Domain: zero package references.
- `Directory.Build.props`: `TreatWarningsAsErrors=true`, `Nullable=enable`, `ImplicitUsings=enable`.
- Packages (user-approved list only): MediatR **12.5.0**, FluentValidation + FluentValidation.DependencyInjectionExtensions, Microsoft.EntityFrameworkCore + Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore, xunit, FluentAssertions **7.2.0** (not 8+, license), Testcontainers.PostgreSql, Microsoft.AspNetCore.Mvc.Testing. Latest stable 10.x for Microsoft packages. No Serilog.
- Controllers thin: only `Send()` to MediatR. Every use case: Command/Query + Handler + validator.
- Tests: real Postgres via Testcontainers, no EF InMemory. **Docker daemon must be running** (it is stopped on this machine right now: start Docker Desktop before task 4).
- Correlation header: `X-Correlation-Id`. Commits: `KAN-11: message`. No migrations (DbContext has no entities).
- Don't edit files outside scope.
- Response shape decision: success = bare DTO (no envelope); errors = Problem Details; list endpoints = shared `PagedResult<T>` (`items, page, pageSize, totalCount`) in `Application`, added by the first ticket that returns a list. Record this in the Notion docs (task 6).

## Review Focus

- Malformed/empty JSON body to a command endpoint -> `400` Problem Details, not 500.
- Two validation failures on one field -> `errors["Field"]` has both messages (array).
- Unknown route -> `404` as `application/problem+json`, with correlation id.
- Inbound `X-Correlation-Id` that is junk (>64 chars or chars outside `[A-Za-z0-9_-]`) -> ignored, fresh id generated (no header/log injection).
- 500 in Production: body has generic `detail`, no stack trace and no exception message.
- DB unreachable -> `/health` is `503` with `database: Unhealthy`, never an unhandled 500.

## AC map

| AC | Task | Test / check |
|----|------|--------------|
| Build succeeds, warnings as errors | 1 | `dotnet build src/api` 0 warnings, 0 errors |
| Domain has no package refs; dependency rule | 1 | `ArchitectureTests` |
| `GET /health` 200 + DB status | 4 | `HealthTests` |
| Validation failure -> 400 + per-field `errors` | 2, 3 | `ValidationBehaviorTests`, `ErrorHandlingTests` |
| Unhandled exception -> 500, no stack trace outside Development | 3 | `ErrorHandlingTests` |
| Correlation id in every response and in logs | 3 | `CorrelationIdTests` |
| Docs/Notion/Jira kept in sync (KAN-10 convention) | 5, 6 | checklist |

Subtask map: KAN-46 = task 1, KAN-47 = task 2, KAN-48 = task 3, KAN-49 = task 4. All four ship in this one PR.

---

### Task 1: Solution, projects, references (KAN-46)

**Files:**
- Create: `src/api/Directory.Build.props`, `src/api/StackTrack.slnx`, six project folders via `dotnet new`, `src/api/StackTrack.Api.Tests/ArchitectureTests.cs`
- Remove: `src/api/.gitkeep`

**Interfaces:**
- Produces: all projects building; `StackTrack.Api.Tests` (used by tasks 3-4) and `StackTrack.Application.Tests` (task 2).

- [ ] **Step 1: Write failing `ArchitectureTests`** in Api.Tests (load assemblies with `Assembly.Load("StackTrack.X")`):
  - `Domain_has_no_package_and_no_project_references()`: Domain.csproj text has no `PackageReference`/`ProjectReference`.
  - `Application_references_only_Domain()`: referenced `StackTrack.*` assemblies == `["StackTrack.Domain"]`.
  - `Infrastructure_does_not_reference_Api()`.
- [ ] **Step 2: Create solution and projects** with `dotnet new sln/classlib/webapi/xunit`, `dotnet sln add`, `dotnet add reference` per Global Constraints; write `Directory.Build.props`; add xunit/FluentAssertions 7.2.0 to both test projects and Mvc.Testing to Api.Tests. Api uses controllers (`AddControllers`), no sample endpoints.
- [ ] **Step 3: Run** `dotnet test src/api` -> PASS. To prove the test bites, add a `Domain -> Application` reference by hand once, see FAIL, remove it.
- [ ] **Step 4: Verify** `dotnet build src/api` -> 0 warnings, 0 errors.
- [ ] **Step 5: Commit** `KAN-11: scaffold Clean Architecture solution`

### Task 2: MediatR + validation pipeline (KAN-47)

**Files:**
- Create: `src/api/StackTrack.Application/DependencyInjection.cs`, `src/api/StackTrack.Application/Behaviors/ValidationBehavior.cs`, `src/api/StackTrack.Application.Tests/ValidationBehaviorTests.cs`

**Interfaces:**
- Produces: `public static IServiceCollection AddApplication(this IServiceCollection services)` registering MediatR handlers from the Application assembly, all `IValidator<>`s from it, and `ValidationBehavior<,>` as open-generic `IPipelineBehavior<,>`. `ValidationBehavior<TRequest,TResponse>` throws `FluentValidation.ValidationException` (all failures aggregated) before calling `next`.

- [ ] **Step 1: Write failing tests** (test-local `TestCommand(string Name) : IRequest<string>`, validator `NotEmpty` + `MinimumLength(3)` on Name, handler returning Name; services built with `AddMediatR` over the test assembly plus `ValidationBehavior<,>`):
  - `Invalid_request_throws_ValidationException_with_all_failures_and_handler_not_called()`: Name `""` -> `Errors` has both failures for `Name`; handler counter == 0.
  - `Valid_request_reaches_handler()`: `"abc"` -> `"abc"`.
  - `Request_without_validators_passes_through()`.
- [ ] **Step 2: Run** `dotnet test src/api/StackTrack.Application.Tests` -> FAIL.
- [ ] **Step 3: Implement** `ValidationBehavior` (inject `IEnumerable<IValidator<TRequest>>`, `ValidateAsync` all, throw if any failure) and `AddApplication`.
- [ ] **Step 4: Run tests** -> PASS.
- [ ] **Step 5: Commit** `KAN-11: add MediatR validation pipeline`

### Task 3: Problem Details, global errors, correlation id (KAN-48)

**Files:**
- Create: `src/api/StackTrack.Api/Middleware/CorrelationIdMiddleware.cs`, `src/api/StackTrack.Api/Errors/GlobalExceptionHandler.cs`, `src/api/StackTrack.Api.Tests/TestSupport/{ApiFactory,TestController,CapturingLoggerProvider}.cs`, `src/api/StackTrack.Api.Tests/{ErrorHandlingTests,CorrelationIdTests}.cs`
- Modify: `src/api/StackTrack.Api/Program.cs` (add `public partial class Program {}`)

**Interfaces:**
- Consumes: `AddApplication()` (task 2).
- Produces:
  - `CorrelationIdMiddleware`: reads `X-Correlation-Id`; accepts only `^[A-Za-z0-9_-]{1,64}$`, else `Guid.NewGuid().ToString("N")`; stores in `HttpContext.Items["CorrelationId"]`; sets the response header via `OnStarting`; runs the rest of the pipeline inside `logger.BeginScope(new Dictionary<string, object>{["CorrelationId"] = id})`. First in the pipeline.
  - `GlobalExceptionHandler : IExceptionHandler`: `ValidationException` -> 400 `ValidationProblemDetails` (`errors` field -> string[]); `BadHttpRequestException` -> 400; anything else -> 500, title "An unexpected error occurred", `detail` = exception text **only** if `IHostEnvironment.IsDevelopment()`. Every Problem Details carries extension `traceId` = correlation id. Unhandled exceptions are logged with `LogError` inside the scope.
  - Program: `AddProblemDetails()`, `AddExceptionHandler<GlobalExceptionHandler>()`, `UseExceptionHandler()`, `UseStatusCodePages()` so 404 etc. are Problem Details.
  - Test support: `ApiFactory : WebApplicationFactory<Program>` with configurable environment and extra `ILoggerProvider`; `TestController` (registered via `AddApplicationPart`) with `POST /test/validate` (sends `ValidateCommand(string Name)` through MediatR; validator `NotEmpty` + `MinimumLength(3)`) and `GET /test/boom` throwing `InvalidOperationException("secret detail")`.

- [ ] **Step 1: Write failing tests**
  - `ErrorHandlingTests`:
    - `Validation_failure_returns_400_problem_details_with_per_field_errors()`: POST `{"name":""}` -> 400, `application/problem+json`, `errors["Name"]` has 2 messages.
    - `Malformed_json_returns_400_problem_details()`.
    - `Unknown_route_returns_404_problem_details()`.
    - `Unhandled_exception_in_Production_returns_500_without_stack_or_message()`: body lacks `secret detail`, `StackTrace`, `   at `.
    - `Unhandled_exception_in_Development_includes_detail()`.
  - `CorrelationIdTests`:
    - `Response_has_generated_correlation_id_and_body_traceId_matches()`.
    - `Valid_inbound_id_is_echoed()` (`abc-123`).
    - `Junk_inbound_id_is_replaced()` (65 chars; value with spaces/CRLF) -> header matches allowed pattern and differs.
    - `Correlation_id_appears_in_server_log_scope_for_the_request()`: capturing provider; the logged 500 entry's scope `CorrelationId` == response header.
- [ ] **Step 2: Run** `dotnet test src/api/StackTrack.Api.Tests` -> FAIL.
- [ ] **Step 3: Implement** middleware, handler, Program wiring per Interfaces (`UseMiddleware<CorrelationIdMiddleware>()` before `UseExceptionHandler()`; write via `IProblemDetailsService` for consistent format).
- [ ] **Step 4: Run tests** -> PASS; `dotnet build src/api` 0 warnings.
- [ ] **Step 5: Commit** `KAN-11: add problem details error handling and correlation id`

### Task 4: Infrastructure + `/health` (KAN-49)

**Files:**
- Create: `src/api/StackTrack.Infrastructure/DependencyInjection.cs`, `src/api/StackTrack.Infrastructure/Persistence/AppDbContext.cs`, `src/api/StackTrack.Api.Tests/PostgresFixture.cs`, `src/api/StackTrack.Api.Tests/HealthTests.cs`
- Modify: `src/api/StackTrack.Api/Program.cs`, `src/api/StackTrack.Api.Tests/TestSupport/ApiFactory.cs` (connection string override)

**Interfaces:**
- Produces:
  - `public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)` (no entities).
  - `public static IServiceCollection AddInfrastructure(this IServiceCollection s, IConfiguration c)`: `AddDbContext<AppDbContext>(o => o.UseNpgsql(c.GetConnectionString("Default")))` and `AddHealthChecks().AddDbContextCheck<AppDbContext>("database")`.
  - Api: `app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = ... })` writing `{"status":"Healthy|Unhealthy","checks":{"database":"Healthy|Unhealthy"}}`; default mapping Unhealthy -> 503.
  - `PostgresFixture : IAsyncLifetime` exposing `ConnectionString` (Testcontainers `PostgreSqlBuilder`), used via `IClassFixture`.

- [ ] **Step 1: Write failing `HealthTests`**:
  - `Health_returns_200_and_database_healthy_when_db_reachable()`: `status == "Healthy"`, `checks.database == "Healthy"`, response has `X-Correlation-Id`.
  - `Health_returns_503_and_database_unhealthy_when_db_unreachable()`: connection string `Host=localhost;Port=1;Timeout=2;Username=x;Password=x;Database=x` -> 503, both statuses `Unhealthy`.
- [ ] **Step 2: Run** (Docker running) -> FAIL.
- [ ] **Step 3: Implement** registration and `/health` mapping; Program calls `AddApplication()` and `AddInfrastructure(builder.Configuration)`.
- [ ] **Step 4: Run all tests** `dotnet test src/api` -> PASS (tasks 1-4).
- [ ] **Step 5: Commit** `KAN-11: add Infrastructure and /health database check`

### Task 5: Local dev setup and docs

**Files:**
- Create: `docker-compose.yml` (root; service `db`: postgres, port 5432, named volume, user/password/db `stacktrack`), `src/api/StackTrack.Api/appsettings.Development.json` (`ConnectionStrings:Default`)
- Modify: `README.md` ("How to run" with real commands)

- [ ] **Step 1: Verify manually**: `docker compose up -d db`; `dotnet run --project src/api/StackTrack.Api`; `curl -i <url>/health` -> 200 + `X-Correlation-Id`; `docker compose stop db` -> 503.
- [ ] **Step 2: Update README** to match.
- [ ] **Step 3: Run** `bash scripts/check-repo.sh` -> `OK`.
- [ ] **Step 4: Commit** `KAN-11: add docker compose for local Postgres and update README`

### Task 6: Verify, document, PR

- [ ] `dotnet build src/api` (0 warnings) and `dotnet test src/api`: paste real output. Client tests/lint: no client yet, say so.
- [ ] Code review against this plan + CLAUDE.md; fix findings.
- [ ] Update Notion "Stack Track — Developer Docs": layout, commands, architecture (pipeline, error format, correlation id, `/health`, response shape decision), Feature log entry for KAN-11.
- [ ] Update Jira KAN-11 description to final behaviour (`src/api`, test projects, `X-Correlation-Id`, `503`) with a "Changes from the original ticket" table, as for KAN-10.
- [ ] Push `feature/KAN-11-backend-scaffold`, open PR `KAN-11: Scaffold backend solution with Clean Architecture` with the PR template; comment PR link + summary on KAN-11; transition to In Review (never Done).
