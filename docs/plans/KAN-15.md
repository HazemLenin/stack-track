# KAN-15 Implement EF Core persistence and initial migration

> **For agentic workers:** use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** Turn `docs/erd.md` into Domain entities, EF Core configurations (Npgsql, snake_case), an audit-timestamp interceptor, and the first migration.

**Architecture:** Entities in `StackTrack.Domain` (no dependencies). One `IEntityTypeConfiguration<T>` per entity in `StackTrack.Infrastructure/Persistence/Configurations`. A `SaveChangesInterceptor` stamps `CreatedAt`/`UpdatedAt`. Behaviour proven by integration tests against real Postgres (Testcontainers, no EF InMemory).

**Tech Stack:** .NET 10, EF Core 10.0.13, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 (already installed), xUnit, FluentAssertions, Testcontainers.PostgreSql (already installed). **No new packages.**

**Spec:** Jira KAN-15 (+ subtasks KAN-58 entities/configs, KAN-59 interceptor, KAN-60 migration), `docs/erd.md`, `CLAUDE.md`. KAN-14 (ERD) is Done and merged (PR #5). No links, no comments on KAN-15.

## Global Constraints

- Configurations in separate `IEntityTypeConfiguration<T>` classes, no data annotations
- snake_case table and column names
- `CreatedAt`/`UpdatedAt` set only by the interceptor, never by handlers
- All timestamps `timestamptz`, UTC
- Indexes: boards by member, cards by list ordered by position, cards by assignee
- Optimistic concurrency on `Card` and `List` (Postgres `xmin`)
- Delete behaviors, unique constraints, GUID PKs exactly as in `docs/erd.md`
- Don't edit existing migrations; don't add packages; tests use real Postgres

## Decisions (my calls, flagged for approval)

1. **snake_case without a package.** `EFCore.NamingConventions` is a new NuGet package, so CLAUDE.md says ask. Instead, `AppDbContext.OnModelCreating` loops over entity types after configurations are applied and rewrites table, column, key, index and FK names to snake_case with a small private helper (about 15 lines). Say so if you would rather add the package.
2. **`DateTimeOffset` for every timestamp** (`CreatedAt`, `UpdatedAt`, `JoinedAt`, `DueDate`, `ExpiresAt`). Npgsql maps it to `timestamptz` but throws on a non-zero offset when writing, so a value converter (`ToUniversalTime()` on write) is applied to all `DateTimeOffset` properties. That makes the Africa/Cairo criterion testable deterministically (offset `+02:00`/`+03:00`) without changing the machine timezone. ERD says "datetime"; this is the .NET type that satisfies it.
3. **Case-insensitive email at the DB level** = unique index on `lower(email)` (`HasIndex(...).HasDatabaseName(...)` with an expression via raw `migrationBuilder.Sql` in the migration, since EF has no expression-index API). The ERD's "stored lowercased" stays an application rule for later auth tickets. Same technique for `label (board_id, lower(name))`.
4. **Concurrency token:** `uint Version` on `Card` and `List`, mapped to column `xmin`, type `xid`, `ValueGeneratedOnAddOrUpdate()`, `IsConcurrencyToken()`. Not in the ERD diagram (system column, no real column).
5. **Clock:** interceptor takes `TimeProvider` (registered `TimeProvider.System`) so tests can fix time.
6. **Enum:** `UserRole { Admin, Member }` stored as string.
7. **String limits (not in ERD):** `email` 320, `display_name` 100, `name` 200 (board/list/label), `title` 300, `color` 7, `token_hash` 128, `description` and `comment.body` unbounded `text`. Cheap to change before first release.
8. **No auto-migrate at startup.** Migration is applied with `dotnet ef database update`; tests call `Database.MigrateAsync()`.
9. **Tests live in `StackTrack.Api.Tests/Persistence`** (Testcontainers already referenced there; Api references Infrastructure). One shared Postgres container per test class via a fixture.

## AC map

| AC | Task | Test |
|----|------|------|
| Initial migration committed, applies to empty DB | 4 | `Migration_applies_to_empty_database` |
| Applying twice is a no-op | 4 | `Migration_applied_twice_is_noop` (second `MigrateAsync` succeeds, `__EFMigrationsHistory` row count unchanged, no pending migrations) |
| Create sets CreatedAt == UpdatedAt (UTC); update changes only UpdatedAt | 2 | `Insert_sets_equal_utc_timestamps`, `Update_changes_only_updated_at` (fake `TimeProvider`) |
| Non-UTC timestamp stored and read back as same instant (Cairo) | 3 | `NonUtcOffset_roundtrips_same_instant` (`DueDate` with `+03:00`, compare `UtcDateTime`, plus raw SQL `SELECT pg_typeof` = `timestamp with time zone`) |
| Email unique regardless of case | 4 | `Email_unique_ignoring_case` (`A@x.com` then `a@X.com` -> `DbUpdateException`, Postgres 23505) |
| Concurrent update of same card -> `DbUpdateConcurrencyException` | 4 | `Concurrent_card_update_throws` (two contexts) |

Review Focus (extra tests, same files): `List` concurrency too (`Concurrent_list_update_throws`); board delete cascades and leaves users (`Board_delete_cascades_but_keeps_users`); user delete blocked by RESTRICT (`User_with_board_cannot_be_deleted`); label name unique per board ignoring case; duplicate `(card_id, user_id)` assignee rejected; `ReplacedByTokenId` set null on token delete.

## Files

- Create `StackTrack.Domain/Entities/`: `Entity.cs` (abstract: `Guid Id`, `DateTimeOffset CreatedAt`, `DateTimeOffset UpdatedAt`), `User.cs`, `UserRole.cs`, `Board.cs`, `BoardMember.cs`, `BoardList.cs` (class named `BoardList` to avoid clashing with `System.Collections.Generic.List<T>`; table `list`), `Card.cs`, `CardAssignee.cs`, `Label.cs`, `CardLabel.cs`, `Comment.cs`, `RefreshToken.cs`
- Create `StackTrack.Infrastructure/Persistence/Configurations/<Entity>Configuration.cs` x10
- Create `StackTrack.Infrastructure/Persistence/AuditInterceptor.cs`
- Modify `AppDbContext.cs` (DbSets, `ApplyConfigurationsFromAssembly`, snake_case, UTC converter), `DependencyInjection.cs` (interceptor, `TimeProvider`)
- Create migration via `dotnet ef migrations add InitialCreate -p src/api/StackTrack.Infrastructure -s src/api/StackTrack.Api`
- Create `StackTrack.Api.Tests/Persistence/PostgresFixture.cs`, `AuditTimestampTests.cs`, `TimestampTimezoneTests.cs`, `MigrationTests.cs`, `ConstraintTests.cs`, `ConcurrencyTests.cs`

## Tasks

### Task 1: Entities, configurations, DbContext wiring (KAN-58)
**Interfaces produces:** `AppDbContext` with `DbSet<User> Users`, `Boards`, `BoardMembers`, `BoardLists`, `Cards`, `CardAssignees`, `Labels`, `CardLabels`, `Comments`, `RefreshTokens`; `PostgresFixture` exposing `AppDbContext CreateContext()` (fresh context, same migrated DB, uses `TimeProvider` passed in or system).
- [ ] RED: write `PostgresFixture` (starts `PostgreSqlContainer`, builds options with `UseNpgsql` + the interceptor, `MigrateAsync` once) and `Model_matches_erd` in `ConstraintTests.cs`: asserts table names `user, board, board_member, list, card, card_assignee, label, card_label, comment, refresh_token`, every column snake_case (regex `^[a-z0-9_]+$`), all PKs `uuid`, every `*_at` column `timestamp with time zone`, delete behaviors per ERD table via `context.Model` (`FindEntityType`, `GetForeignKeys`). Run -> FAIL (does not compile).
- [ ] GREEN: add entities (navigation properties both ways only where a config needs them), configurations (keys, required, max lengths, FKs with ERD delete behavior, enum-to-string), snake_case helper, `DateTimeOffset` UTC converter, `Version`/`xmin` on `Card` and `BoardList`.
- [ ] Run `dotnet test src/api --filter Model_matches_erd` -> PASS. `dotnet build src/api` clean (warnings are errors).
- [ ] Commit `KAN-15: add domain entities and EF Core configurations`

### Task 2: Audit interceptor (KAN-59)
**Interfaces produces:** `public sealed class AuditInterceptor(TimeProvider clock) : SaveChangesInterceptor` overriding `SavingChanges` and `SavingChangesAsync`; for `Entity` entries: Added -> `CreatedAt = UpdatedAt = now`; Modified -> `UpdatedAt = now`, `CreatedAt` marked not modified.
- [ ] RED: `AuditTimestampTests`: `Insert_sets_equal_utc_timestamps` (fake `TimeProvider`, insert `User`, assert `CreatedAt == UpdatedAt == fixed time`, `Offset == TimeSpan.Zero`, same after reload); `Update_changes_only_updated_at` (advance clock, rename, assert `CreatedAt` unchanged, `UpdatedAt` = new time); `Update_ignores_handler_supplied_created_at` (set `CreatedAt` to garbage on a modified entity, still unchanged in DB). Run -> FAIL.
- [ ] GREEN: implement interceptor, register in `DependencyInjection` (`AddSingleton(TimeProvider.System)`, `AddScoped<AuditInterceptor>`, `options.AddInterceptors(...)`).
- [ ] Run tests -> PASS. Commit `KAN-15: stamp CreatedAt/UpdatedAt in a SaveChanges interceptor`

### Task 3: UTC timestamp handling
- [ ] RED: `TimestampTimezoneTests.NonUtcOffset_roundtrips_same_instant`: save a card with `DueDate = new DateTimeOffset(2026, 6, 1, 15, 0, 0, TimeSpan.FromHours(3))` (Cairo summer), reload in a new context, assert `DueDate.Value.UtcDateTime == 12:00 UTC` and `Offset == Zero`; assert `pg_typeof(due_date)` is `timestamp with time zone`. Expected FAIL only if the converter from Task 1 was skipped; if it already passes, keep the test (it pins the AC).
- [ ] GREEN: fix converter if needed. Commit `KAN-15: pin UTC round-trip for non-UTC offsets`

### Task 4: Indexes, unique constraints, migration (KAN-60)
- [ ] RED: `MigrationTests` (`Migration_applies_to_empty_database`: fresh DB in the container, `MigrateAsync`, all 10 tables exist; `Migration_applied_twice_is_noop`), `ConstraintTests` (email case, label name case per board, duplicate membership, duplicate assignee, duplicate card label, board delete cascade keeps users, user delete restricted, token SET NULL), `ConcurrencyTests` (card and list: two contexts load same row, both change it, second `SaveChangesAsync` throws `DbUpdateConcurrencyException`), index test asserting existence of: `board_member (user_id)`, `card (list_id, position)`, `card_assignee (user_id)`, `list (board_id, position)`. Run -> FAIL (no migration yet; fixture uses `MigrateAsync`).
- [ ] GREEN: configure indexes and unique indexes in configurations; generate `InitialCreate`; edit the generated (new, unapplied) migration to add `CREATE UNIQUE INDEX ... ON "user" (lower(email))` and `... ON label (board_id, lower(name))` through `migrationBuilder.Sql`, with matching `Down`; keep the model snapshot consistent (expression indexes are not in the model, so verify `dotnet ef migrations has-pending-model-changes` reports none).
- [ ] Run `dotnet test src/api` -> all PASS (Docker running). Commit `KAN-15: add InitialCreate migration with indexes and concurrency tokens`

### Task 5: Verify, review, Notion, PR
- [ ] `dotnet build src/api`, `dotnet test src/api`, `cd src/client && npm test && npm run lint`, paste real output. Also `dotnet ef database update` against `docker compose up -d db` twice (second is a no-op).
- [ ] Code review against this plan and CLAUDE.md; fix findings.
- [ ] Notion "Feature log" entry (ticket, what, where, how to try) and layout/commands update (persistence folders, migration command); do it before the PR.
- [ ] Push `feature/KAN-15-ef-core-persistence`, PR `KAN-15: Implement EF Core persistence and initial migration` (what changed, how each AC is met, how to test), Jira comment with link, transition to In Review (never Done).

## Out of scope
Seed data, auth, handlers/controllers, repositories, startup auto-migration, client changes, editing `docs/erd.md` (any ERD correction found along the way gets raised, not silently changed).
