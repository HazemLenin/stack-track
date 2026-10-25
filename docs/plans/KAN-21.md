# KAN-21 Admin manages user accounts

**Goal:** An Admin lists users (20 per page, search, sort), deactivates, reactivates and changes roles, with self-action and last-admin protection, on an Admin users page.

**Spec:** Jira KAN-21 (subtasks KAN-74 list API, KAN-75 deactivate/reactivate/role APIs, KAN-76 users page), `CLAUDE.md`.

**Branch:** `feature/KAN-21-admin-users`, stacked on `feature/KAN-20-roles` (needs `AdminControllerBase` and `ICurrentUser`). PR base = KAN-20 branch.

## Gap analysis

| Rule / AC | Current state | Action |
|-----------|---------------|--------|
| Users list, paged/searchable/sortable | Nothing | `ListUsersQuery` + `GET admin/users` (Task 1) |
| Deactivated user cannot sign in | `SignInCommandHandler` throws `AccountDeactivatedException` (403) after password check; `RotateAsync` refuses inactive users | Test only (Task 2) |
| Deactivate keeps boards/cards/comments | Check FK delete behaviors | Test that rows survive (Task 2) |
| Cannot deactivate/demote self -> 400 | Nothing | Validator rule (Task 2) |
| At least one active Admin | Nothing | Atomic guarded UPDATE (Task 2) |
| Reactivate -> old password works | Password hash untouched | Test (Task 2) |

## Decisions (my calls, flag if wrong)

1. **Endpoints** (all under `AdminControllerBase`): `GET admin/users?search=&sort=name|created&dir=asc|desc&page=1`, `POST admin/users/{id}/deactivate`, `POST admin/users/{id}/reactivate`, `PUT admin/users/{id}/role` body `{ "role": "Admin|Member" }`. Page size fixed at 20; default sort `name asc`; search is case-insensitive substring on display name and email (`ILIKE`, `%` and `_` escaped).
2. **Response:** `{ items: [{id, displayName, email, role, isActive, createdAt}], page, pageSize, totalCount }`.
3. **Self action** -> `400` (validation error on `id`). **Last active admin** -> `409` with detail "There must be at least one active administrator". (Ticket says only "rejected"; 409 reuses `ConflictException`.)
4. **Race safety:** deactivate/demote runs as one conditional `ExecuteUpdate` that only applies when another active Admin exists (`NOT EXISTS`), so two admins cannot remove each other concurrently.
5. **Deactivation revokes the user's refresh tokens** so their session ends at the next renewal. Their current access token stays valid until it expires (`AccessTokenMinutes`). Ticket only requires "can no longer sign in"; instant revocation would need a per-request DB check, out of scope.
6. **Unknown user id** -> `404`. Role/deactivate on an already-in-that-state user is idempotent `204`.
7. UI role label stays "Member" (see KAN-20 decision 1). No new packages: `MatTable`, `MatPaginator`, `MatSort`, `MatDialog` ship with installed Angular Material.

## AC map

| AC | Task | Test |
|----|------|------|
| Browse: first 20 by name, search by name/email | 1, 5 | `List_returns_first_20_sorted_by_name`; `List_searches_name_and_email`; `List_sorts_by_created`; client `loads page 1 and searches` |
| Deactivate Omar -> status Deactivated, cannot sign in, data kept | 2, 5 | `Deactivate_blocks_sign_in_and_refresh`; `Deactivate_keeps_owned_boards_and_comments`; client `confirm dialog then deactivates` |
| Last admin protected | 2 | `Last_active_admin_cannot_be_deactivated_or_demoted`; `Concurrent_demotions_leave_one_admin` |
| Self actions: disabled in UI, API 400 | 2, 5 | `Self_deactivate_and_role_change_return_400`; client `disables actions on own row` |
| Reactivate -> old password works | 2 | `Reactivate_allows_sign_in_with_old_password` |
| Rule: Admin-only | 1 | `Member_gets_403_on_every_user_admin_route` |

## Files

- API: `Application/Admin/Users/{ListUsers,DeactivateUser,ReactivateUser,ChangeUserRole}/` (command/query + handler + validator + DTO each), `IUserAdminRepository` in Application, `Infrastructure/Auth/UserAdminRepository.cs`, `Api/Controllers/AdminUsersController.cs`
- Tests: `Api.Tests/Admin/UsersEndpointTests.cs`
- Client: `features/admin/users/{admin-users.service.ts, admin-users-page.ts/html/scss}` + specs, `admin.routes.ts`, replace placeholder `admin-page.ts`
- No migration (columns exist).

## Tasks

### Task 1: List users (KAN-74)
- [ ] Tests (see AC map, Testcontainers), run, expect FAIL
- [ ] `ListUsersQuery(Search, Sort, Dir, Page)` + validator (page >= 1, sort/dir whitelist, search max 100) + handler via `IUserAdminRepository`; controller `GET`
- [ ] Commit `KAN-21: list users with search, sort and paging`

### Task 2: Deactivate, reactivate, change role (KAN-75)
- [ ] Tests (self 400, last admin 409, race, sign-in blocked, tokens revoked, data kept, reactivate)
- [ ] Three commands + validators (self check via `ICurrentUser`) + guarded `ExecuteUpdate`; `ConflictException` mapped already
- [ ] Commit `KAN-21: deactivate, reactivate and change user roles`

### Task 3: Client service (KAN-76)
- [ ] Spec with `HttpTestingController` for the four calls and query params; impl `AdminUsersService`
- [ ] Commit `KAN-21: add admin users client service`

### Task 4: Users page (KAN-76)
- [ ] Specs: renders rows, page size 20, debounced search, sort headers, paginator, own row actions disabled, confirm dialog gates deactivate, role select (API errors are toasted by the existing error interceptor)
- [ ] Impl: standalone page with signals, `MatTable`/`MatPaginator`/`MatSort`/`MatDialog`; route `admin/users`
- [ ] Commit `KAN-21: add admin users page`

## Delegation
Tasks 1-2 -> `api-cqrs-implementer`; Tasks 3-4 -> `angular-feature-builder`. Client is coded against the endpoint contract above, so both run in parallel worktrees, then merge.
